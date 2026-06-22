namespace Moongazing.OrionShade.Demo;

/// <summary>
/// Pattern redaction on free text. Takes a realistic application log line that has leaked an
/// email, a credit-card number, and a JWT all at once, and scrubs it with the built-in rules.
/// </summary>
internal sealed class LogLineDemo(IRedactor redactor)
{
    public void Run()
    {
        DemoConsole.Section("1. Redact a realistic log line (email + credit card + JWT)");

        const string logLine =
            "2026-06-18T09:14:02Z INFO  checkout: user jane.doe@acme.com paid with card " +
            "4242 4242 4242 4242 token=eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjMifQ.s5d8Qb7rXk2yqZ";

        var scrubbed = redactor.Redact(logLine);

        DemoConsole.Step("A single Redact() call sweeps every built-in pattern over the line");
        DemoConsole.BeforeAfter(logLine, scrubbed);
        Console.WriteLine();
        Console.WriteLine("  Note: the email and JWT are masked whole; the card keeps its last four digits.");
    }
}
