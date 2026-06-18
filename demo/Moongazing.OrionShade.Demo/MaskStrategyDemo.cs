namespace Moongazing.OrionShade.Demo;

using Microsoft.Extensions.DependencyInjection;

using Moongazing.OrionShade.Redaction;

/// <summary>
/// Mask strategies. A mask is a <c>Func&lt;string, string&gt;</c>; the library ships
/// <see cref="Masks.Full()"/> (replace whole), <see cref="Masks.Full(string)"/> (fixed token), and
/// <see cref="Masks.KeepLast(int, char)"/> (keep the trailing characters). This demo invokes the
/// strategies directly, then shows <c>KeepLast</c> wired into a real redactor as the key mask.
/// </summary>
internal sealed class MaskStrategyDemo
{
    public void Run()
    {
        DemoConsole.Section("3. Mask strategies: Masks.Full vs Masks.KeepLast");

        const string secret = "4111111111111234";

        var full = Masks.Full();             // => "[REDACTED]"
        var fullToken = Masks.Full("***");   // => "***"
        var keepLast4 = Masks.KeepLast(4);   // keep the last 4, mask the rest with '*'
        var keepLast2Hash = Masks.KeepLast(2, '#');

        DemoConsole.Step("Calling each strategy on the same secret value");
        Console.WriteLine($"  value              : {secret}");
        Console.WriteLine($"  Masks.Full()       : {full(secret)}");
        Console.WriteLine($"  Masks.Full(\"***\")  : {fullToken(secret)}");
        Console.WriteLine($"  Masks.KeepLast(4)  : {keepLast4(secret)}");
        Console.WriteLine($"  Masks.KeepLast(2,#): {keepLast2Hash(secret)}");

        DemoConsole.Step("KeepLast never leaves a short secret in the clear");
        Console.WriteLine($"  Masks.KeepLast(4)(\"1234\") : {keepLast4("1234")}   (value <= visible -> fully masked)");

        DemoConsole.Step("The same strategy wired into a redactor as the sensitive-key mask");
        using var provider = new ServiceCollection()
            .AddOrionShade(shade => shade
                .UseDefaults()
                .UseKeyMask(Masks.KeepLast(4)))
            .BuildServiceProvider();

        var redactor = provider.GetRequiredService<IRedactor>();
        DemoConsole.KeyValue("card_number", secret, redactor.RedactValue("card_number", secret));
        Console.WriteLine("  (UseKeyMask swaps the default [REDACTED] for KeepLast(4) on sensitive-key values.)");
    }
}
