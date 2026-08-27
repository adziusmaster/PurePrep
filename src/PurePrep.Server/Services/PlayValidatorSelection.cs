namespace PurePrep.Server.Services;

public enum PlayValidatorChoice
{
    /// <summary>Verify the token with Google's Developer API.</summary>
    GooglePlay,

    /// <summary>Accept any non-empty token. Local development only — never reachable in Production.</summary>
    Development,
}

/// <summary>
/// Chooses the purchase validator for the running environment.
///
/// This exists because the original composition registered <see cref="DevPlayValidator"/>
/// unconditionally, despite comments asserting otherwise — so production accepted forged purchase
/// tokens as proof of payment. A misconfigured production deployment must fail loudly at startup
/// rather than come up quietly in a state where credits can be minted for free.
/// </summary>
public static class PlayValidatorSelection
{
    public static PlayValidatorChoice Select(bool isProduction, PlayOptions options)
    {
        if (options.IsConfigured)
            return PlayValidatorChoice.GooglePlay;

        if (isProduction)
            throw new InvalidOperationException(
                "Google Play purchase validation is not configured. Set Play__ServiceAccountJsonPath to a " +
                "readable service-account JSON key with Android Publisher access. Refusing to start: without " +
                "it the server would accept any purchase token as valid and grant credits for free.");

        return PlayValidatorChoice.Development;
    }
}
