namespace Moongazing.OrionShade.Redaction;

using System.Text.RegularExpressions;

/// <summary>
/// A named rule that finds sensitive substrings with a regular expression and replaces each match
/// using a mask.
/// </summary>
public sealed class RedactionRule
{
    /// <summary>Create a rule from a compiled regular expression.</summary>
    /// <param name="name">A short identifier used as the telemetry tag (for example <c>email</c>).</param>
    /// <param name="pattern">The pattern that matches the sensitive text.</param>
    /// <param name="mask">How to mask each match.</param>
    public RedactionRule(string name, Regex pattern, Func<string, string> mask)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(mask);
        Name = name;
        Pattern = pattern;
        Mask = mask;
    }

    /// <summary>The rule name.</summary>
    public string Name { get; }

    /// <summary>The pattern that matches sensitive text.</summary>
    public Regex Pattern { get; }

    /// <summary>The mask applied to each match.</summary>
    public Func<string, string> Mask { get; }
}
