namespace Moongazing.OrionShade.Tests;

using System.Text.Json;

using Moongazing.OrionShade;
using Moongazing.OrionShade.Diagnostics;
using Moongazing.OrionShade.Redaction;

using Xunit;

/// <summary>
/// Covers <see cref="Redactor.RedactJson"/>: structure-preserving redaction of JSON documents,
/// masking sensitive property values (including inside nested objects and arrays) while leaving
/// siblings, non-string leaves, and non-matching text untouched, plus graceful handling of input
/// that is not valid JSON.
/// </summary>
[Collection(nameof(MeterSerial))]
public sealed class RedactJsonTests
{
    private static Redactor Build(ShadeDiagnostics diagnostics) =>
        new(BuiltInRules.All, SensitiveKeyset.Default, Masks.Full(), diagnostics);

    [Fact]
    public void A_sensitive_property_is_masked_while_siblings_remain()
    {
        using var diag = new ShadeDiagnostics();
        var redactor = Build(diag);

        const string json = """{"user":"alice","password":"hunter2"}""";
        var result = redactor.RedactJson(json);

        using var parsed = JsonDocument.Parse(result);
        var root = parsed.RootElement;
        Assert.Equal("alice", root.GetProperty("user").GetString());
        Assert.Equal(Masks.DefaultToken, root.GetProperty("password").GetString());
    }

    [Fact]
    public void A_nested_sensitive_property_is_masked_while_its_siblings_remain()
    {
        using var diag = new ShadeDiagnostics();
        var redactor = Build(diag);

        const string json = """
            {"name":"alice","credentials":{"username":"alice","apikey":"abc123"}}
            """;
        var result = redactor.RedactJson(json);

        using var parsed = JsonDocument.Parse(result);
        var credentials = parsed.RootElement.GetProperty("credentials");
        Assert.Equal("alice", parsed.RootElement.GetProperty("name").GetString());
        Assert.Equal("alice", credentials.GetProperty("username").GetString());
        Assert.Equal(Masks.DefaultToken, credentials.GetProperty("apikey").GetString());
    }

    [Fact]
    public void String_array_elements_are_redacted_by_the_pattern_sweep()
    {
        using var diag = new ShadeDiagnostics();
        var redactor = Build(diag);

        const string json = """{"contacts":["a@b.com","plain text","c@d.org"]}""";
        var result = redactor.RedactJson(json);

        Assert.DoesNotContain("a@b.com", result, StringComparison.Ordinal);
        Assert.DoesNotContain("c@d.org", result, StringComparison.Ordinal);
        Assert.Contains("plain text", result, StringComparison.Ordinal);
    }

    [Fact]
    public void A_sensitive_key_masks_every_string_element_of_its_array()
    {
        using var diag = new ShadeDiagnostics();
        var redactor = Build(diag);

        const string json = """{"token":["one","two","three"]}""";
        var result = redactor.RedactJson(json);

        using var parsed = JsonDocument.Parse(result);
        var tokens = parsed.RootElement.GetProperty("token");
        Assert.Equal(3, tokens.GetArrayLength());
        foreach (var element in tokens.EnumerateArray())
        {
            Assert.Equal(Masks.DefaultToken, element.GetString());
        }
    }

    [Fact]
    public void Arrays_of_objects_recurse_into_each_object()
    {
        using var diag = new ShadeDiagnostics();
        var redactor = Build(diag);

        const string json = """
            {"users":[{"name":"a","secret":"s1"},{"name":"b","secret":"s2"}]}
            """;
        var result = redactor.RedactJson(json);

        using var parsed = JsonDocument.Parse(result);
        var users = parsed.RootElement.GetProperty("users");
        foreach (var user in users.EnumerateArray())
        {
            Assert.Equal(Masks.DefaultToken, user.GetProperty("secret").GetString());
            var name = user.GetProperty("name").GetString();
            Assert.True(name is "a" or "b");
        }
    }

    [Fact]
    public void Non_string_leaves_are_preserved()
    {
        using var diag = new ShadeDiagnostics();
        var redactor = Build(diag);

        const string json = """{"count":42,"active":true,"ratio":1.5,"missing":null}""";
        var result = redactor.RedactJson(json);

        using var parsed = JsonDocument.Parse(result);
        var root = parsed.RootElement;
        Assert.Equal(42, root.GetProperty("count").GetInt32());
        Assert.True(root.GetProperty("active").GetBoolean());
        Assert.Equal(1.5, root.GetProperty("ratio").GetDouble());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("missing").ValueKind);
    }

    [Fact]
    public void A_pattern_match_inside_a_non_sensitive_property_is_masked()
    {
        using var diag = new ShadeDiagnostics();
        var redactor = Build(diag);

        const string json = """{"note":"reach me at a@b.com please"}""";
        var result = redactor.RedactJson(json);

        using var parsed = JsonDocument.Parse(result);
        var note = parsed.RootElement.GetProperty("note").GetString();
        Assert.NotNull(note);
        Assert.DoesNotContain("a@b.com", note, StringComparison.Ordinal);
        Assert.Contains(Masks.DefaultToken, note, StringComparison.Ordinal);
    }

    [Fact]
    public void A_clean_document_is_returned_with_its_values_intact()
    {
        using var diag = new ShadeDiagnostics();
        var redactor = Build(diag);

        const string json = """{"status":"shipped","items":["book","pen"]}""";
        var result = redactor.RedactJson(json);

        using var parsed = JsonDocument.Parse(result);
        Assert.Equal("shipped", parsed.RootElement.GetProperty("status").GetString());
        var items = parsed.RootElement.GetProperty("items");
        Assert.Equal("book", items[0].GetString());
        Assert.Equal("pen", items[1].GetString());
    }

    [Fact]
    public void Invalid_json_falls_back_to_free_text_redaction()
    {
        using var diag = new ShadeDiagnostics();
        var redactor = Build(diag);

        // Not a JSON document: the email is still masked via the free-text path.
        const string notJson = "this is not json but here is a@b.com";
        var result = redactor.RedactJson(notJson);

        Assert.DoesNotContain("a@b.com", result, StringComparison.Ordinal);
        Assert.Contains(Masks.DefaultToken, result, StringComparison.Ordinal);
    }

    [Fact]
    public void An_empty_input_is_returned_unchanged()
    {
        using var diag = new ShadeDiagnostics();
        var redactor = Build(diag);

        Assert.Equal(string.Empty, redactor.RedactJson(string.Empty));
    }

    [Fact]
    public void RedactJson_throws_when_the_input_is_null()
    {
        using var diag = new ShadeDiagnostics();
        var redactor = Build(diag);

        Assert.Throws<ArgumentNullException>(() => redactor.RedactJson(null!));
    }

    [Fact]
    public void Deeply_nested_structures_are_redacted_throughout()
    {
        using var diag = new ShadeDiagnostics();
        var redactor = Build(diag);

        const string json = """
            {"a":{"b":{"c":{"password":"deep","note":"clean"}}}}
            """;
        var result = redactor.RedactJson(json);

        using var parsed = JsonDocument.Parse(result);
        var c = parsed.RootElement
            .GetProperty("a").GetProperty("b").GetProperty("c");
        Assert.Equal(Masks.DefaultToken, c.GetProperty("password").GetString());
        Assert.Equal("clean", c.GetProperty("note").GetString());
    }
}
