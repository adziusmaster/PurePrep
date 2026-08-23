namespace PurePrep.Domain;

public sealed class UserQuota
{
    public const int FreeRecipeLimit = 10;
    public int SavedRecipeCount { get; private set; } = 3;
    public bool IsPremium { get; private set; }
    public int RemainingFreeRecipes => IsPremium ? int.MaxValue : Math.Max(0, FreeRecipeLimit - SavedRecipeCount);
    public bool CanSaveRecipe => IsPremium || SavedRecipeCount < FreeRecipeLimit;
    public bool CanUseFocusMode => IsPremium;

    public void RecordRecipeSaved()
    {
        if (!CanSaveRecipe)
            throw new InvalidOperationException("The free recipe quota has been reached.");
        SavedRecipeCount++;
    }

    public void SetPremiumStatus(bool isPremium) => IsPremium = isPremium;
}