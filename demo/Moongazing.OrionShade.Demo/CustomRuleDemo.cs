namespace Moongazing.OrionShade.Demo;

using Microsoft.Extensions.DependencyInjection;

using Moongazing.OrionShade.Redaction;

/// <summary>
/// Extending the redactor: a custom pattern rule (phone numbers) and a custom sensitive key
/// (national_id), layered on top of the built-in defaults through the real builder.
/// </summary>
internal sealed class CustomRuleDemo
{
    public void Run()
    {
        DemoConsole.Section("4. A custom rule and a custom sensitive key");

        using var provider = new ServiceCollection()
            .AddOrionShade(shade => shade
                .UseDefaults()                                              // built-in email/card/jwt + default keys
                .AddRule("phone", @"\+?\d[\d ]{7,}\d", Masks.KeepLast(2))   // custom pattern, partial mask
                .AddSensitiveKeys("national_id", "iban")                    // custom sensitive keys
                .UseKeyMask(Masks.Full("***")))                            // custom key mask
            .BuildServiceProvider();

        var redactor = provider.GetRequiredService<IRedactor>();

        DemoConsole.Step("Custom 'phone' rule fires on free text, keeping the last two digits");
        const string text = "call me on +1 415 555 0199 or mail jane@acme.com";
        DemoConsole.BeforeAfter(text, redactor.Redact(text));

        DemoConsole.Step("Custom sensitive key 'national_id' masked wholesale with the custom key mask");
        DemoConsole.KeyValue("national_id", "AB-1234567", redactor.RedactValue("national_id", "AB-1234567"));

        DemoConsole.Step("Built-in defaults still apply alongside the custom additions");
        DemoConsole.KeyValue("password", "hunter2", redactor.RedactValue("password", "hunter2"));
    }
}
