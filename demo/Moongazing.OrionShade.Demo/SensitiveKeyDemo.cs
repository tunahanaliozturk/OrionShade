namespace Moongazing.OrionShade.Demo;

/// <summary>
/// Key-name redaction. <see cref="IRedactor.RedactValue"/> masks a value wholesale when its key is
/// in the sensitive keyset (case-insensitive), regardless of what the value looks like. A
/// non-sensitive key falls through to the pattern sweep instead.
/// </summary>
internal sealed class SensitiveKeyDemo(IRedactor redactor)
{
    public void Run()
    {
        DemoConsole.Section("2. Redact a value by sensitive key name");

        DemoConsole.Step("Sensitive keys: the whole value is masked, whatever it contains");
        Demo("password", "hunter2");
        Demo("Authorization", "Bearer abc.def.ghi");   // matched case-insensitively
        Demo("ssn", "123-45-6789");

        DemoConsole.Step("Non-sensitive key: value passes through the pattern rules instead");
        Demo("username", "jane");                        // nothing matches, returned unchanged
        Demo("note", "ping jane.doe@acme.com");          // not a sensitive key, but the email pattern still fires
    }

    private void Demo(string key, string value) =>
        DemoConsole.KeyValue(key, value, redactor.RedactValue(key, value));
}
