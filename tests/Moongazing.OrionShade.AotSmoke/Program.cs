// NativeAOT smoke test. Publishing this with PublishAot=true must produce zero trim/AOT warnings,
// and running it must exit 0 - OrionShade's AOT exit criterion. Runtime checks, not a framework:
// the point is to prove the redaction path (DI + source-gen regex + frozen key set + JSON) survives
// trimming in a real native binary.
using Microsoft.Extensions.DependencyInjection;
using Moongazing.OrionShade;

var services = new ServiceCollection();
services.AddOrionShade(); // built-in rules (email, card, JWT, IBAN, phone, connection secret) + default keys

using var provider = services.BuildServiceProvider();
var redactor = provider.GetRequiredService<IRedactor>();

// Pattern redaction (source-generated regex under AOT).
const string email = "user@example.com";
var redactedEmail = redactor.Redact($"mail me at {email} please");
Check(!redactedEmail.Contains(email, StringComparison.Ordinal), "email was not redacted");

// A credit-card number should be masked (Luhn keep-last-4).
var redactedCard = redactor.Redact("card 4111 1111 1111 1111 on file");
Check(!redactedCard.Contains("4111 1111 1111 1111", StringComparison.Ordinal), "card was not redacted");

// Sensitive-key redaction (frozen key set lookup).
var redactedValue = redactor.RedactValue("password", "hunter2");
Check(!redactedValue.Contains("hunter2", StringComparison.Ordinal), "sensitive value was not redacted");

// A non-sensitive key passes through unchanged.
Check(redactor.RedactValue("city", "Istanbul") == "Istanbul", "non-sensitive value was altered");

// JSON redaction path.
var redactedJson = redactor.RedactJson("{\"token\":\"abc123\",\"city\":\"Istanbul\"}");
Check(!redactedJson.Contains("abc123", StringComparison.Ordinal), "json secret was not redacted");
Check(redactedJson.Contains("Istanbul", StringComparison.Ordinal), "json non-secret was lost");

Console.WriteLine("OrionShade AOT smoke test passed.");
return 0;

static void Check(bool condition, string message)
{
    if (!condition)
    {
        Console.Error.WriteLine($"AOT smoke test failed: {message}");
        Environment.Exit(1);
    }
}
