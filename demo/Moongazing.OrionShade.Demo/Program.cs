namespace Moongazing.OrionShade.Demo;

using Microsoft.Extensions.DependencyInjection;

using Moongazing.OrionShade;

/// <summary>
/// Runnable tour of OrionShade. Builds a real redactor through the library's DI entry point
/// (<c>AddOrionShade</c>) with the built-in defaults, then walks through pattern redaction,
/// key-name redaction, mask strategies, custom rules/keys, and each built-in rule on its own.
/// Prints original then redacted at every step so the effect is visible.
/// </summary>
internal static class Program
{
    private static void Main()
    {
        Console.WriteLine("OrionShade demo - sensitive-data redaction for .NET");
        Console.WriteLine("Every section prints the original input and then the redacted output.");

        // The default redactor: built-in email/card/JWT patterns plus the common sensitive keys.
        using var provider = new ServiceCollection()
            .AddOrionShade()
            .BuildServiceProvider();

        var redactor = provider.GetRequiredService<IRedactor>();

        new LogLineDemo(redactor).Run();
        new SensitiveKeyDemo(redactor).Run();
        new MaskStrategyDemo().Run();
        new CustomRuleDemo().Run();
        new BuiltInRulesDemo().Run();

        Console.WriteLine();
        Console.WriteLine(new string('=', 78));
        Console.WriteLine("Demo complete. No sensitive values were printed in the clear above.");
    }
}
