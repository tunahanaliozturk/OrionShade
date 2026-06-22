namespace Moongazing.OrionShade.Demo;

using Moongazing.OrionShade.Diagnostics;
using Moongazing.OrionShade.Redaction;

/// <summary>
/// Each built-in rule exercised on its own. <see cref="BuiltInRules.Email"/>,
/// <see cref="BuiltInRules.Iban"/>, <see cref="BuiltInRules.Phone"/>,
/// <see cref="BuiltInRules.CreditCard"/>, <see cref="BuiltInRules.Jwt"/>, and
/// <see cref="BuiltInRules.ConnectionStringSecret"/> are real <see cref="RedactionRule"/> instances;
/// here each is dropped into a single-rule <see cref="Redactor"/> so its effect is isolated from the
/// others.
/// </summary>
internal sealed class BuiltInRulesDemo
{
    public void Run()
    {
        DemoConsole.Section("5. The built-in rules, individually");

        ShowRule(
            BuiltInRules.Email,
            "user jane.doe@acme.com signed in",
            "masks email addresses whole");

        ShowRule(
            BuiltInRules.Iban,
            "transfer to DE89 3704 0044 0532 0130 00 cleared",
            "masks IBAN bank account numbers whole");

        ShowRule(
            BuiltInRules.Phone,
            "call back on +1 415 555 0100 anytime",
            "masks international phone numbers, keeping the last two digits");

        ShowRule(
            BuiltInRules.CreditCard,
            "charged 4242 4242 4242 4242 today",
            "masks Luhn-valid card runs, keeping the last four");

        ShowRule(
            BuiltInRules.Jwt,
            "bearer eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjMifQ.s5d8Qb7rXk2yqZ",
            "masks JSON Web Tokens whole");

        ShowRule(
            BuiltInRules.ConnectionStringSecret,
            "Server=db;Database=app;Password=P@ssw0rd!;Pooling=true",
            "masks the secret value of a connection-string pair, keeping the key visible");
    }

    private static void ShowRule(RedactionRule rule, string sample, string description)
    {
        // A redactor configured with exactly one rule and no sensitive keys, built from the real
        // public Redactor constructor so the rule runs in isolation.
        using var diagnostics = new ShadeDiagnostics();
        var redactor = new Redactor(
            [rule],
            new SensitiveKeyset([]),
            Masks.Full(),
            diagnostics);

        DemoConsole.Step($"rule '{rule.Name}' - {description}");
        DemoConsole.BeforeAfter(sample, redactor.Redact(sample));
    }
}
