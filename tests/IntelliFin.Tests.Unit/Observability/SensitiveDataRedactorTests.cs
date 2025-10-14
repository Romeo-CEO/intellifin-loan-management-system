using System.Collections.Generic;
using System.Text.RegularExpressions;
using IntelliFin.Shared.Observability;

namespace IntelliFin.Tests.Unit.Observability;

public class SensitiveDataRedactorTests
{
    private static SensitiveDataRedactor CreateRedactor(params string[] sensitiveKeys)
    {
        var patterns = new List<Regex>
        {
            new("\\b\\d{6}/\\d{2}/\\d{1}\\b", RegexOptions.Compiled),
            new("\\+260\\d{9}\\b", RegexOptions.Compiled)
        };

        return new SensitiveDataRedactor(patterns, new HashSet<string>(sensitiveKeys, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void Redact_ReplacesNrcAndPhoneNumbers()
    {
        var redactor = CreateRedactor();
        const string message = "Client NRC 123456/78/9 and phone +260987654321 should be hidden.";

        var sanitized = redactor.Redact(message);

        sanitized.Should().NotContain("123456/78/9");
        sanitized.Should().NotContain("+260987654321");
        sanitized.Should().Contain("***");
    }

    [Fact]
    public void RedactValue_MasksConfiguredSensitiveKeys()
    {
        var redactor = CreateRedactor("nationalRegistration", "phoneNumber");

        redactor.RedactValue("nationalRegistration", "123456/78/9").Should().Be("***");
        redactor.RedactValue("phoneNumber", "+260987654321").Should().Be("***");
        redactor.RedactValue("accountId", "ABC-123").Should().Be("ABC-123");
    }

    [Fact]
    public void RedactValue_RunsRegexOnStringValuesWhenKeyIsUnknown()
    {
        var redactor = CreateRedactor();

        var sanitized = redactor.RedactValue("notes", "Contact +260111222333 for NRC 654321/12/3");

        sanitized.Should().Be("Contact *** for NRC ***");
    }
}
