namespace Moongazing.OrionShade.Demo;

/// <summary>
/// Small console helpers shared by the feature demos: section headers and an aligned
/// before/after print so each demo reads the same way.
/// </summary>
internal static class DemoConsole
{
    public static void Section(string title)
    {
        Console.WriteLine();
        Console.WriteLine(new string('=', 78));
        Console.WriteLine(title);
        Console.WriteLine(new string('=', 78));
    }

    public static void Step(string title)
    {
        Console.WriteLine();
        Console.WriteLine("-- " + title);
    }

    public static void BeforeAfter(string original, string redacted)
    {
        Console.WriteLine("  original : " + original);
        Console.WriteLine("  redacted : " + redacted);
    }

    public static void KeyValue(string key, string original, string redacted)
    {
        Console.WriteLine($"  key '{key}'");
        Console.WriteLine("    original : " + original);
        Console.WriteLine("    redacted : " + redacted);
    }
}
