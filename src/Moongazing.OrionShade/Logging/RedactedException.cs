namespace Moongazing.OrionShade.Logging;

/// <summary>
/// Wraps an exception so the text a log sink renders from it is redacted. A sink typically writes an
/// exception by appending its <see cref="Exception.ToString()"/> (and sometimes its
/// <see cref="Exception.Message"/>) after the formatted message; both are scrubbed here through an
/// <see cref="IRedactor"/>. The original type name and stack frames survive in the rendered output
/// because redaction only masks substrings that match a rule, which type names and frame text do not,
/// so the structure a reader relies on to diagnose a failure is preserved while any secret or PII in
/// the message is masked.
/// </summary>
/// <remarks>
/// The original stack trace is forwarded verbatim through <see cref="StackTrace"/> so a sink that
/// reads it directly still sees the real frames, and the inner exception is wrapped recursively so a
/// PII-bearing cause is scrubbed too.
/// </remarks>
internal sealed class RedactedException : Exception
{
    private readonly Exception original;
    private readonly IRedactor redactor;

    /// <summary>Wrap <paramref name="original"/> so its rendered text is redacted.</summary>
    /// <param name="original">The exception whose rendered text must be scrubbed.</param>
    /// <param name="redactor">The redactor applied to the rendered text.</param>
    public RedactedException(Exception original, IRedactor redactor)
        : base(RedactMessage(original, redactor), WrapCause(original, redactor))
    {
        // original and redactor are validated in the helpers invoked above, which run before this body.
        this.original = original;
        this.redactor = redactor;

        HResult = original.HResult;
        Source = original.Source;
        HelpLink = original.HelpLink;
    }

    /// <summary>The original exception's stack trace, forwarded unchanged.</summary>
    public override string? StackTrace => original.StackTrace;

    /// <summary>
    /// The original exception rendered to text and then redacted. This is the form most text sinks
    /// write, so scrubbing it here keeps secrets and PII out of the log while leaving the type name and
    /// stack frames intact.
    /// </summary>
    public override string ToString() => redactor.Redact(original.ToString());

    private static string RedactMessage(Exception original, IRedactor redactor)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(redactor);
        return redactor.Redact(original.Message);
    }

    private static RedactedException? WrapCause(Exception original, IRedactor redactor) =>
        original.InnerException is { } cause ? new RedactedException(cause, redactor) : null;
}
