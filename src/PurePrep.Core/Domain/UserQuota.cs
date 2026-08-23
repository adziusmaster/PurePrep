namespace PurePrep.Domain;

/// <summary>
/// Monetization state. Recipe storage is unlimited and free for everyone; the only paid product is
/// consumable AI "Smart Credits" spent when importing a recipe by link via the AI Smart Parser.
/// Every user starts with <see cref="FreeCredits"/>. When credits run out, link import is disabled
/// but manual recipe entry always remains available.
/// </summary>
public sealed class UserQuota
{
    /// <summary>Free AI Smart Credits granted to every new user.</summary>
    public const int FreeCredits = 10;

    public int Credits { get; private set; } = FreeCredits;

    /// <summary>Link import (AI Smart Parser) is available only while the user has credits.</summary>
    public bool CanImportByLink => Credits > 0;

    /// <summary>Recipe storage and Focus Mode are free for everyone.</summary>
    public bool CanUseFocusMode => true;

    /// <summary>Spends one credit for an AI import. Returns false when none remain.</summary>
    public bool TrySpendCredit()
    {
        if (Credits <= 0)
            return false;
        Credits--;
        return true;
    }

    /// <summary>Returns a credit (e.g. when an import fails after the credit was spent).</summary>
    public void RefundCredit() => Credits++;

    /// <summary>Adds credits from a purchased Smart Credit pack.</summary>
    public void AddCredits(int amount)
    {
        if (amount > 0)
            Credits += amount;
    }
}