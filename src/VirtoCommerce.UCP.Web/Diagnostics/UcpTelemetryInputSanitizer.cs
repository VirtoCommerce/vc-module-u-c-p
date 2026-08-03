using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace VirtoCommerce.UCP.Web.Diagnostics;

internal sealed partial class UcpTelemetryInputSanitizer
{
    private const int FingerprintByteCount = 8;
    public const int MaxIdentifierLength = 128;
    public const int MaxTextLength = 256;
    public const int MaxNameLength = 64;
    public const int MaxSummaryItems = 10;

    private readonly HashSet<string> _truncatedFields = new(StringComparer.Ordinal);

    public bool InputTruncated { get; private set; }
    public string TruncatedFields => string.Join(',', _truncatedFields.OrderBy(x => x, StringComparer.Ordinal));

    public string SerializeBounded(JsonObject source, int maxLength, string field)
    {
        var originalJson = source.ToJsonString();
        if (originalJson.Length <= maxLength)
        {
            return originalJson;
        }

        var clone = (JsonObject)source.DeepClone();
        var json = originalJson;
        while (json.Length > maxLength && RemoveLastArrayItem(clone))
        {
            MarkTruncated(field);
            json = clone.ToJsonString();
        }
        if (json.Length <= maxLength)
        {
            return json;
        }

        MarkTruncated(field);
        return CreateTruncatedPayload(originalJson);
    }

    public string Sanitize(string value, int maxLength, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        var result = new StringBuilder(Math.Min(trimmed.Length, maxLength));
        foreach (var character in trimmed)
        {
            if (result.Length >= maxLength)
            {
                MarkTruncated(field);
                break;
            }
            result.Append(char.IsControl(character) ? ' ' : character);
        }
        return result.ToString();
    }

    public void MarkTruncated(string field)
    {
        InputTruncated = true;
        _truncatedFields.Add(field);
    }

    public static string SanitizeText(string value, int maxLength = MaxTextLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var result = new StringBuilder(Math.Min(value.Length, maxLength));
        foreach (var character in value.Trim())
        {
            if (result.Length >= maxLength)
            {
                break;
            }
            result.Append(char.IsControl(character) ? ' ' : character);
        }
        return result.ToString();
    }

    public static string RedactPotentialPii(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var result = EmailPattern().Replace(value, "[redacted-email]");
        return PhonePattern().Replace(result, "[redacted-phone]");
    }

    public static string ComputeFingerprint(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash.AsSpan(0, FingerprintByteCount)).ToLowerInvariant();
    }

    public static string JoinNames(IEnumerable<string> names, int maxItems = MaxSummaryItems)
    {
        return string.Join(',', names
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .Take(maxItems)
            .Select(name => name.Length > MaxNameLength ? name[..MaxNameLength] : name));
    }

    internal static string CreateTruncatedPayload(string originalJson)
    {
        return new JsonObject
        {
            ["truncated"] = true,
            ["fingerprint"] = ComputeFingerprint(originalJson),
            ["original_length"] = originalJson.Length,
        }.ToJsonString();
    }

    private static bool RemoveLastArrayItem(JsonNode node)
    {
        var arrays = new List<JsonArray>();
        CollectArrays(node, arrays);
        var target = arrays.Where(x => x.Count > 0).OrderByDescending(x => x.Count).FirstOrDefault();
        if (target == null)
        {
            return false;
        }
        target.RemoveAt(target.Count - 1);
        return true;
    }

    private static void CollectArrays(JsonNode node, ICollection<JsonArray> arrays)
    {
        switch (node)
        {
            case JsonArray array:
                arrays.Add(array);
                foreach (var item in array.Where(x => x != null))
                {
                    CollectArrays(item, arrays);
                }
                break;
            case JsonObject obj:
                foreach (var item in obj.Select(x => x.Value).Where(x => x != null))
                {
                    CollectArrays(item, arrays);
                }
                break;
        }
    }

    [GeneratedRegex(
        @"\b[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex EmailPattern();

    [GeneratedRegex(
        @"(?<!\w)(?:\+?\d[\d\s().\-]{5,}\d)(?!\w)",
        RegexOptions.CultureInvariant)]
    private static partial Regex PhonePattern();
}
