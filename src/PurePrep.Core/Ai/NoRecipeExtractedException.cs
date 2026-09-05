namespace PurePrep.Ai;

/// <summary>
/// Raised when the page loaded fine but no recipe could be extracted from it (empty ingredients and
/// steps). Distinct from a fetch failure so the endpoint can refund the credit and tell the user the
/// page simply had no recipe, rather than blaming the site or our service.
/// </summary>
public sealed class NoRecipeExtractedException(string message) : Exception(message);
