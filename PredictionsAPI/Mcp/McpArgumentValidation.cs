using System.Text.Json;
using System.Text.RegularExpressions;

namespace PredictionsAPI.Mcp;

internal static class McpArgumentValidation
{
    public static void Validate(JsonElement schema, IDictionary<string, JsonElement>? arguments) =>
        ValidateValue(schema, JsonSerializer.SerializeToElement(arguments ?? new Dictionary<string, JsonElement>()), schema, "arguments");

    private static void ValidateValue(JsonElement schema, JsonElement value, JsonElement root, string name)
    {
        if (schema.TryGetProperty("$ref", out var reference))
        {
            schema = root;
            foreach (var segment in reference.GetString()!.Split('/').Skip(1))
                schema = schema.GetProperty(segment.Replace("~1", "/").Replace("~0", "~"));
        }
        if (schema.TryGetProperty("anyOf", out var alternatives))
        {
            foreach (var alternative in alternatives.EnumerateArray())
                try { ValidateValue(alternative, value, root, name); return; } catch (ToolInputException) { }
            throw new ToolInputException($"{name} does not match the tool schema.");
        }
        if (schema.TryGetProperty("type", out var type))
        {
            var types = type.ValueKind == JsonValueKind.Array ? type.EnumerateArray().Select(t => t.GetString()) : [type.GetString()];
            if (!types.Any(t => Matches(t, value))) throw new ToolInputException($"{name} has an invalid type; use the tool schema.");
        }
        if (value.ValueKind == JsonValueKind.Null) return;
        if (value.ValueKind == JsonValueKind.Object)
        {
            if (schema.TryGetProperty("required", out var required))
                foreach (var key in required.EnumerateArray())
                    if (!value.TryGetProperty(key.GetString()!, out _)) throw new ToolInputException($"Missing required argument: {key.GetString()}.");
            if (!schema.TryGetProperty("properties", out var properties))
            {
                if (value.EnumerateObject().Any()) throw new ToolInputException("Unknown arguments.");
                return;
            }
            foreach (var property in value.EnumerateObject())
            {
                if (!properties.TryGetProperty(property.Name, out var propertySchema))
                    throw new ToolInputException("Unknown argument. Use only the arguments in the tool schema.");
                ValidateValue(propertySchema, property.Value, root, property.Name);
            }
        }
        else if (value.ValueKind == JsonValueKind.Number)
        {
            if (!value.TryGetInt32(out var number)) throw new ToolInputException($"{name} must be a 32-bit integer.");
            if (name == "limit" && number is < 1 or > 100) throw new ToolInputException("limit must be between 1 and 100.");
            if ((name.EndsWith("Id", StringComparison.Ordinal) || name == "season") && number <= 0)
                throw new ToolInputException($"{name} must be positive.");
            if (name is "offset" or "homeGoals" or "awayGoals" && number < 0)
                throw new ToolInputException($"{name} must be nonnegative.");
        }
        else if (value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString()!;
            if (name == "userId" && (string.IsNullOrWhiteSpace(text) || text.Length > 128))
                throw new ToolInputException("userId must be an account ID of 1 to 128 characters.");
            if (schema.TryGetProperty("format", out var format) && format.GetString() == "date-time" &&
                (!value.TryGetDateTimeOffset(out _) || !Regex.IsMatch(text, @"(Z|[+-]\d{2}:\d{2})$", RegexOptions.IgnoreCase)))
                throw new ToolInputException($"{name} must be an ISO-8601 date/time with a UTC offset.");
        }
    }

    private static bool Matches(string? type, JsonElement value) => type switch
    {
        "object" => value.ValueKind == JsonValueKind.Object,
        "string" => value.ValueKind == JsonValueKind.String,
        "integer" => value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out _),
        "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
        "null" => value.ValueKind == JsonValueKind.Null,
        "array" => value.ValueKind == JsonValueKind.Array,
        _ => false
    };
}
