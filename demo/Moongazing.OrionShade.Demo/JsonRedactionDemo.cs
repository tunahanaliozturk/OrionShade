namespace Moongazing.OrionShade.Demo;

/// <summary>
/// Structured JSON redaction. Takes a JSON document that has leaked secrets both by sensitive key
/// name and by value shape, and scrubs it with <see cref="IRedactor.RedactJson"/>. The structure is
/// preserved: nested objects and arrays are recursed, each string value is redacted in the context
/// of the property that owns it, and non-string values are left untouched. Invalid JSON falls back
/// to free-text redaction.
/// </summary>
internal sealed class JsonRedactionDemo(IRedactor redactor)
{
    public void Run()
    {
        DemoConsole.Section("6. Redact a JSON document, structure preserved");

        const string json =
            """
            {
              "user": "jane.doe@acme.com",
              "password": "hunter2",
              "attempts": 3,
              "active": true,
              "contacts": ["+1 415 555 0100", "team@acme.com"],
              "payment": { "iban": "DE89 3704 0044 0532 0130 00", "note": "primary card" }
            }
            """;

        DemoConsole.Step("A single RedactJson() call recurses the whole document");
        DemoConsole.BeforeAfter(json, redactor.RedactJson(json));
        Console.WriteLine();
        Console.WriteLine("  Note: the 'password' and 'iban' keys are masked whole by key name; the email and");
        Console.WriteLine("  phone are caught by pattern; numbers and booleans are left as they were.");

        DemoConsole.Step("Invalid JSON falls back to free-text redaction");
        const string notJson = "user jane.doe@acme.com called from +1 415 555 0100";
        DemoConsole.BeforeAfter(notJson, redactor.RedactJson(notJson));
    }
}
