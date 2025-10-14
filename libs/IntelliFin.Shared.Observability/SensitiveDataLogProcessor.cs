using System;
using System.Collections.Generic;
using OpenTelemetry.Logs;

namespace IntelliFin.Shared.Observability;

public sealed class SensitiveDataLogProcessor : BaseProcessor<LogRecord>
{
    private readonly SensitiveDataRedactor _redactor;

    public SensitiveDataLogProcessor(SensitiveDataRedactor redactor)
    {
        _redactor = redactor ?? throw new ArgumentNullException(nameof(redactor));
    }

    public override void OnEnd(LogRecord data)
    {
        if (data == null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(data.FormattedMessage))
        {
            data.FormattedMessage = _redactor.Redact(data.FormattedMessage!);
        }

        if (data.Body is string body && !string.IsNullOrEmpty(body))
        {
            data.Body = _redactor.Redact(body);
        }

        if (data.StateValues is IList<KeyValuePair<string, object?>> stateList && stateList.Count > 0)
        {
            for (var index = 0; index < stateList.Count; index++)
            {
                var entry = stateList[index];
                var sanitizedValue = _redactor.RedactValue(entry.Key, entry.Value);
                if (!Equals(entry.Value, sanitizedValue))
                {
                    stateList[index] = new KeyValuePair<string, object?>(entry.Key, sanitizedValue);
                }
            }
        }

        if (data.Attributes is IList<KeyValuePair<string, object?>> attributeList && attributeList.Count > 0)
        {
            for (var index = 0; index < attributeList.Count; index++)
            {
                var attribute = attributeList[index];
                var sanitized = _redactor.RedactValue(attribute.Key, attribute.Value);
                if (!Equals(attribute.Value, sanitized))
                {
                    attributeList[index] = new KeyValuePair<string, object?>(attribute.Key, sanitized);
                }
            }
        }
    }
}
