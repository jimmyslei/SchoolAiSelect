using System.Text.Json;
using System.Text.Json.Nodes;

namespace SchoolRecommender.Api.Services;

internal static class FunctionCallArgumentNormalization
{
    public static Dictionary<string, object?>? TryNormalizeArguments(
        string? functionName,
        IDictionary<string, object?>? arguments,
        object? rawRepresentation)
    {
        if (arguments is not null)
        {
            if (arguments.TryGetValue("arguments", out var nestedArguments)
                && TryNormalizeArgumentValue(functionName, nestedArguments, arguments) is { } normalizedNested)
            {
                return normalizedNested;
            }

            if (arguments.Count == 1)
            {
                var onlyArgument = arguments.First().Value;
                if (TryNormalizeArgumentValue(functionName, onlyArgument, arguments) is { } normalizedSingle)
                {
                    return normalizedSingle;
                }
            }
        }

        if (rawRepresentation is null)
        {
            return null;
        }

        var rawJson = ExtractRawArgumentsJson(rawRepresentation);
        return TryNormalizeRawJson(functionName, rawJson, arguments);
    }

    public static string? TryNormalizeArgumentsJsonNode(string? functionName, JsonNode? argumentsNode)
    {
        if (argumentsNode is null)
        {
            return null;
        }

        if (argumentsNode is JsonArray jsonArray)
        {
            return ArrayToNamedArgsJson(jsonArray, functionName);
        }

        if (argumentsNode is JsonValue jsonValue)
        {
            if (jsonValue.TryGetValue<string>(out var raw))
            {
                return TryNormalizeArgumentsString(functionName, raw);
            }

            return TryNormalizeArgumentsString(functionName, jsonValue.ToJsonString());
        }

        return null;
    }

    public static string? TryNormalizeArgumentsString(string? functionName, string? raw)
    {
        var normalized = TryNormalizeRawJson(functionName, raw, existingArguments: null);
        return normalized is null ? null : JsonSerializer.Serialize(normalized);
    }

    private static Dictionary<string, object?>? TryNormalizeArgumentValue(
        string? functionName,
        object? value,
        IDictionary<string, object?>? existingArguments)
    {
        return value switch
        {
            JsonElement { ValueKind: JsonValueKind.Array } arrayElement => ArrayToNamedArgs(functionName, arrayElement),
            JsonElement { ValueKind: JsonValueKind.String } stringElement => TryNormalizeRawJson(functionName, stringElement.GetString(), existingArguments),
            string stringValue => TryNormalizeRawJson(functionName, stringValue, existingArguments),
            _ => null,
        };
    }

    private static Dictionary<string, object?>? TryNormalizeRawJson(
        string? functionName,
        string? raw,
        IDictionary<string, object?>? existingArguments)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var trimmed = raw.Trim();
        if (trimmed.StartsWith('{'))
        {
            return null;
        }

        if (trimmed.StartsWith('['))
        {
            using var doc = JsonDocument.Parse(trimmed);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                return ArrayToNamedArgs(functionName, doc.RootElement);
            }

            return null;
        }

        if (existingArguments is not null
            && existingArguments.Count > 0
            && !existingArguments.ContainsKey("arguments"))
        {
            return null;
        }

        var primaryParam = GetPrimaryParameterName(functionName);
        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            [primaryParam] = trimmed,
        };
    }

    private static Dictionary<string, object?>? ArrayToNamedArgs(string? functionName, JsonElement arrayElement)
    {
        var items = arrayElement.EnumerateArray().ToList();
        if (items.Count == 0)
        {
            return null;
        }

        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var primaryParam = GetPrimaryParameterName(functionName);
        result[primaryParam] = items[0].Clone();

        var secondaryParams = GetSecondaryParameterNames(functionName);
        for (var i = 1; i < items.Count && i - 1 < secondaryParams.Length; i++)
        {
            result[secondaryParams[i - 1]] = items[i].Clone();
        }

        return result;
    }

    private static string? ArrayToNamedArgsJson(JsonArray array, string? functionName)
    {
        var items = array.ToList();
        if (items.Count == 0)
        {
            return null;
        }

        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var primaryParam = GetPrimaryParameterName(functionName);
        result[primaryParam] = items[0]?.Deserialize<object?>();

        var secondaryParams = GetSecondaryParameterNames(functionName);
        for (var i = 1; i < items.Count && i - 1 < secondaryParams.Length; i++)
        {
            result[secondaryParams[i - 1]] = items[i]?.Deserialize<object?>();
        }

        return JsonSerializer.Serialize(result);
    }

    private static string? ExtractRawArgumentsJson(object rawRepresentation)
    {
        var type = rawRepresentation.GetType();

        var funcArgsProperty = type.GetProperty("FunctionArguments")
            ?? type.GetProperty("Arguments");
        if (funcArgsProperty is not null)
        {
            var value = funcArgsProperty.GetValue(rawRepresentation);
            return value?.ToString();
        }

        var functionProperty = type.GetProperty("Function");
        if (functionProperty is not null)
        {
            var function = functionProperty.GetValue(rawRepresentation);
            if (function is not null)
            {
                var argsProperty = function.GetType().GetProperty("Arguments");
                if (argsProperty is not null)
                {
                    return argsProperty.GetValue(function)?.ToString();
                }
            }
        }

        return null;
    }

    private static string GetPrimaryParameterName(string? functionName) =>
        functionName switch
        {
            var n when n is not null && n.Contains("search_school_info", StringComparison.OrdinalIgnoreCase) => "school_name",
            var n when n is not null && n.Contains("wechat", StringComparison.OrdinalIgnoreCase) => "query",
            var n when n is not null && n.Contains("xiaohongshu", StringComparison.OrdinalIgnoreCase) => "keyword",
            var n when n is not null && n.Contains("feed", StringComparison.OrdinalIgnoreCase) => "feed_id",
            var n when n is not null && n.Contains("user", StringComparison.OrdinalIgnoreCase) => "user_id",
            _ => "keyword",
        };

    private static string[] GetSecondaryParameterNames(string? functionName) =>
        functionName switch
        {
            var n when n is not null && n.Contains("search_school_info", StringComparison.OrdinalIgnoreCase) => ["aspect", "region"],
            var n when n is not null && n.Contains("wechat", StringComparison.OrdinalIgnoreCase) => ["num", "region"],
            var n when n is not null && n.Contains("xiaohongshu", StringComparison.OrdinalIgnoreCase) => ["limit", "region"],
            var n when n is not null && n.Contains("feed", StringComparison.OrdinalIgnoreCase) => ["xsec_token"],
            var n when n is not null && n.Contains("user", StringComparison.OrdinalIgnoreCase) => ["xsec_token"],
            _ => [],
        };
}