namespace PurePrep.Server.Services;

/// <summary>
/// Google Play Developer API settings used to verify purchases server-side.
/// The service-account JSON is mounted into the container as a file; only its path is configured,
/// so the key itself never appears in an environment variable, compose file or log.
/// </summary>
public sealed class PlayOptions
{
    public const string SectionName = "Play";

    /// <summary>The app's Play package name. Must match the AAB uploaded to the Play Console.</summary>
    public string PackageName { get; set; } = "com.adziusmaster.pureprep";

    /// <summary>Path to the mounted service-account JSON key file.</summary>
    public string? ServiceAccountJsonPath { get; set; }

    /// <summary>True when real Google validation can actually be performed.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ServiceAccountJsonPath) && File.Exists(ServiceAccountJsonPath);
}
