namespace SmartInvest.Application.Interfaces;

public record LookupMatchCategory(string CategoryKey, List<string> UnresolvedNames, List<string> ExistingNames);

public interface ILookupMatchSuggestionService
{
    /// <summary>
    /// For each category, suggests which existing name an unresolved name most likely refers to
    /// (e.g. a typo or a "مركز"/"مدينة" prefix variant of an already-known name). Returns
    /// categoryKey -> (unresolvedName -> suggested existing name, or absent/null when unsure).
    /// Never throws - a failed or unparsable AI call just yields no suggestions.
    /// </summary>
    Task<Dictionary<string, Dictionary<string, string?>>> SuggestMatchesAsync(
        List<LookupMatchCategory> categories, CancellationToken cancellationToken = default);
}
