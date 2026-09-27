using Android.Views;
using AndroidX.Core.View;

namespace PurePrep;

/// <summary>
/// Chooses how the window reacts to the keyboard. Most pages keep Android's default pan behaviour
/// (the window shifts so the focused field stays visible — resizing instead re-lays out Home's
/// recipe list under a focused search box and drops focus). The window resizes instead while
/// a bottom sheet is open (it is docked to the bottom, where panning would hide its buttons behind
/// the keyboard) or while a form page with a sticky bottom button has asked for it (the recipe editor,
/// Paste text — panning slides the page under the status bar and hides Save). While resizing,
/// <c>MainActivity</c>'s insets listener pads the content by the keyboard height.
/// </summary>
internal static class SheetKeyboard
{
    private static readonly HashSet<object> PageRequests = new();
    private static bool _sheetOpen;

    /// <summary>True while the window resizes for the keyboard (a sheet is open or a page asked for it).</summary>
    public static bool ResizeActive { get; private set; }

    /// <summary>Called by <c>BottomSheet</c> whenever the set of open sheets changes.</summary>
    public static void SetSheetOpen(bool open)
    {
        _sheetOpen = open;
        Apply();
    }

    /// <summary>A page asks for resize while it is visible (call from OnAppearing). Idempotent per page.</summary>
    public static void RequestPageResize(object page)
    {
        PageRequests.Add(page);
        Apply();
    }

    /// <summary>Withdraws a page's request (call from OnDisappearing). Idempotent per page.</summary>
    public static void ReleasePageResize(object page)
    {
        PageRequests.Remove(page);
        Apply();
    }

    private static void Apply()
    {
        var resize = _sheetOpen || PageRequests.Count > 0;
        if (ResizeActive == resize)
            return;
        var window = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity?.Window;
        if (window is null)
            return;

        ResizeActive = resize;
        window.SetSoftInputMode(resize ? SoftInput.AdjustResize : SoftInput.AdjustPan);
        if (window.DecorView.FindViewById(Android.Resource.Id.Content) is { } content)
            ViewCompat.RequestApplyInsets(content);
    }
}
