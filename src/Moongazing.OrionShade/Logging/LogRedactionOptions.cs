namespace Moongazing.OrionShade.Logging;

/// <summary>
/// Declares how OrionShade redacts log messages in the <c>Microsoft.Extensions.Logging</c> pipeline:
/// a default redactor applied to every category, and optional per-category-prefix redactors so a
/// verbose debug logger and an audited logger can run different rule and key sets from the same
/// registration.
/// </summary>
/// <remarks>
/// A category is matched to a redactor by the longest registered prefix that the category name
/// starts with (ordinal, case-sensitive). When no prefix matches, the <see cref="DefaultRedactor"/>
/// is used. When no default is set and no prefix matches, the message is passed through unchanged.
/// </remarks>
public sealed class LogRedactionOptions
{
    private readonly List<CategoryRedactor> categoryRedactors = [];

    /// <summary>
    /// The redactor applied to every log category that no more specific prefix claims. When null,
    /// unmatched categories are left unredacted.
    /// </summary>
    public IRedactor? DefaultRedactor { get; set; }

    /// <summary>
    /// The per-category-prefix redactors, in registration order. Resolution picks the entry whose
    /// prefix is the longest match for a given category name.
    /// </summary>
    public IReadOnlyList<CategoryRedactor> CategoryRedactors => categoryRedactors;

    /// <summary>
    /// Apply <paramref name="redactor"/> to every log category whose name starts with
    /// <paramref name="categoryPrefix"/>. A more specific (longer) prefix wins over a shorter one.
    /// </summary>
    /// <param name="categoryPrefix">The category-name prefix, for example a namespace or type name.</param>
    /// <param name="redactor">The redactor to apply to matching categories.</param>
    /// <returns>The same options instance, for chaining.</returns>
    public LogRedactionOptions RedactCategory(string categoryPrefix, IRedactor redactor)
    {
        ArgumentException.ThrowIfNullOrEmpty(categoryPrefix);
        ArgumentNullException.ThrowIfNull(redactor);
        categoryRedactors.Add(new CategoryRedactor(categoryPrefix, redactor));
        return this;
    }

    /// <summary>
    /// Resolve the redactor for a log <paramref name="categoryName"/>: the longest registered prefix
    /// that the category starts with, falling back to <see cref="DefaultRedactor"/>. Returns null when
    /// nothing matches and no default is set, meaning the category is not redacted.
    /// </summary>
    /// <param name="categoryName">The log category name.</param>
    public IRedactor? ResolveFor(string categoryName)
    {
        ArgumentNullException.ThrowIfNull(categoryName);

        IRedactor? match = null;
        var matchedPrefixLength = -1;
        foreach (var entry in categoryRedactors)
        {
            if (entry.Prefix.Length > matchedPrefixLength &&
                categoryName.StartsWith(entry.Prefix, StringComparison.Ordinal))
            {
                match = entry.Redactor;
                matchedPrefixLength = entry.Prefix.Length;
            }
        }

        return match ?? DefaultRedactor;
    }
}

/// <summary>A category-name prefix bound to the redactor that should run for it.</summary>
/// <param name="Prefix">The category-name prefix matched ordinally from the start of the name.</param>
/// <param name="Redactor">The redactor applied to matching categories.</param>
public sealed record CategoryRedactor(string Prefix, IRedactor Redactor);
