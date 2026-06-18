namespace Moongazing.OrionShade.Demo;

using Moongazing.OrionShade.Diagnostics;
using Moongazing.OrionShade.Redaction;

/// <summary>
/// Each built-in rule exercised on its own. <see cref="BuiltInRules.Email"/>,
/// <see cref="BuiltInRules.CreditCard"/>, and <see cref="BuiltInRules.Jwt"/> are real
/// <see cref="RedactionRule"/> instances; here each is dropped into a single-rule
/// <see cref="Redactor"/> so its effect is isolated from the others.
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
            BuiltInRules.CreditCard,
            "charged 4111 1111 1111 1234 today",
            "masks card-like digit runs, keeping the last four");

        ShowRule(
            BuiltInRules.Jwt,
            "bearer eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjMifQ.s5d8Qb7rXk2yqZ",
            "masks JSON Web Tokens whole");
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
