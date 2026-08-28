using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;
using PurePrep.Application;

namespace PurePrep.Platforms.Android;

/// <summary>
/// Delivers the cook-timer alert through an Android alarm plus a notification, so it still fires
/// when PurePrep's process has been reclaimed — the normal outcome for a long bake.
///
/// Exactness degrades rather than being required: exact alarms are used where the platform allows
/// them, and an inexact Doze-tolerant alarm is used otherwise. The app deliberately does not
/// declare USE_EXACT_ALARM, which is reserved for alarm-clock apps and invites Play review.
/// </summary>
public sealed class CookTimerNotifier : ICookTimerNotifier
{
    internal const string ChannelId = "pureprep_cook_timers";
    internal const int NotificationId = 4201;
    private const int RequestCode = 4201;

    public bool IsSupported => true;

    private static Context Context => global::Android.App.Application.Context;

    public async Task<bool> EnsurePermissionAsync(CancellationToken cancellationToken = default)
    {
        // Notification permission became a runtime grant in Android 13.
        if (!OperatingSystem.IsAndroidVersionAtLeast(33))
            return true;

        var status = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
        if (status != PermissionStatus.Granted)
            status = await Permissions.RequestAsync<Permissions.PostNotifications>();

        return status == PermissionStatus.Granted;
    }

    public Task ScheduleAsync(string label, DateTimeOffset endsAt, CancellationToken cancellationToken = default)
    {
        CreateChannel();

        var manager = (AlarmManager?)Context.GetSystemService(Context.AlarmService);
        if (manager is null)
            return Task.CompletedTask;

        var triggerAt = endsAt.ToUnixTimeMilliseconds();
        var pending = BuildPendingIntent(label);

        // SetExactAndAllowWhileIdle needs the exact-alarm capability from Android 12 onward. Where
        // it is unavailable we still schedule, just inexactly: a cook timer that fires a little
        // late is far better than one that does not fire at all.
        if (!OperatingSystem.IsAndroidVersionAtLeast(31) || manager.CanScheduleExactAlarms())
            manager.SetExactAndAllowWhileIdle(AlarmType.RtcWakeup, triggerAt, pending);
        else
            manager.SetAndAllowWhileIdle(AlarmType.RtcWakeup, triggerAt, pending);

        return Task.CompletedTask;
    }

    public Task CancelAsync(CancellationToken cancellationToken = default)
    {
        var manager = (AlarmManager?)Context.GetSystemService(Context.AlarmService);
        manager?.Cancel(BuildPendingIntent(label: string.Empty));

        NotificationManagerCompat.From(Context).Cancel(NotificationId);
        return Task.CompletedTask;
    }

    private static PendingIntent BuildPendingIntent(string label)
    {
        var intent = new Intent(Context, typeof(CookTimerAlarmReceiver));
        intent.PutExtra(CookTimerAlarmReceiver.LabelExtra, label);

        // Mutable pending intents are rejected from Android 12; Immutable is required here.
        var flags = PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable;
        return PendingIntent.GetBroadcast(Context, RequestCode, intent, flags)!;
    }

    internal static void CreateChannel()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
            return;

        var manager = (NotificationManager?)Context.GetSystemService(Context.NotificationService);
        if (manager is null || manager.GetNotificationChannel(ChannelId) is not null)
            return;

        // High importance so the alert surfaces while the user is in another app — which is exactly
        // where they will be when a timer they set ten minutes ago goes off.
        var channel = new NotificationChannel(ChannelId, "Cook timers", NotificationImportance.High)
        {
            Description = "Alerts when a cooking timer finishes.",
        };
        channel.EnableVibration(true);
        manager.CreateNotificationChannel(channel);
    }
}

/// <summary>Posts the notification when the scheduled alarm fires.</summary>
[BroadcastReceiver(Enabled = true, Exported = false)]
public sealed class CookTimerAlarmReceiver : BroadcastReceiver
{
    internal const string LabelExtra = "pureprep.timer.label";

    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null)
            return;

        CookTimerNotifier.CreateChannel();

        var label = intent?.GetStringExtra(LabelExtra);
        var body = string.IsNullOrWhiteSpace(label)
            ? "Your cooking timer has finished."
            : $"{label} is up.";

        // Tapping the notification returns to PurePrep rather than dumping the user on the launcher.
        var launch = context.PackageManager?.GetLaunchIntentForPackage(context.PackageName!);
        PendingIntent? contentIntent = launch is null
            ? null
            : PendingIntent.GetActivity(context, 0, launch,
                PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

        var builder = new NotificationCompat.Builder(context, CookTimerNotifier.ChannelId)
            .SetContentTitle("Timer finished")
            .SetContentText(body)
            .SetSmallIcon(global::Android.Resource.Drawable.IcDialogInfo)
            .SetPriority((int)NotificationPriority.High)
            .SetCategory(NotificationCompat.CategoryAlarm)
            .SetAutoCancel(true);

        if (contentIntent is not null)
            builder.SetContentIntent(contentIntent);

        try
        {
            NotificationManagerCompat.From(context)
                .Notify(CookTimerNotifier.NotificationId, builder.Build());
        }
        catch (Java.Lang.SecurityException)
        {
            // Notification permission was refused. The in-app countdown still works, so this is
            // a degraded experience rather than a failure worth crashing for.
        }
    }
}
