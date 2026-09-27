using PurePrep.Domain;

namespace PurePrep.Application;

/// <summary>A freshly parsed recipe plus where its photo can come from (page image, or a generation ticket).</summary>
public sealed record ParsedImport(ParsedRecipe Recipe, Uri? ImageUrl, string? ImageTicket);
