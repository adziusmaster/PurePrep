namespace PurePrep.Application;

/// <summary>Outcome of trying to redeem a promo/tester code.</summary>
public enum PromoRedeemOutcome
{
    Success,
    InvalidCode,
    Revoked,
    Expired,
    AlreadyRedeemed,
    NetworkError,
}

/// <summary>Result of a promo-code redemption, including the resulting balance on success.</summary>
public sealed record PromoRedeemResult(PromoRedeemOutcome Outcome, int CreditsGranted, int Balance)
{
    public bool Success => Outcome == PromoRedeemOutcome.Success;
}
