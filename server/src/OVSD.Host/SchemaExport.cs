using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using OVSD.Core.Actions;
using OVSD.Core.Integrations;
using OVSD.Core.Model;
using OVSD.Core.Protocol;
using OVSD.Core.Storage;
using OVSD.Host.Api;
using OVSD.Host.Security;

namespace OVSD.Host;

/// <summary>Writes JSON schemas of the protocol and API types, consumed by web/scripts/gen-types.mjs.</summary>
public static class SchemaExport
{
    private static readonly Type[] RootTypes =
    [
        typeof(ClientMessage), typeof(ServerMessage), typeof(Profile), typeof(AppSettings), typeof(ActionDescriptor),
        typeof(ProfileListItem), typeof(DeviceView), typeof(PairInfo), typeof(ServerInfo), typeof(PairingResult),
        typeof(IntegrationStatus), typeof(BackupInfo), typeof(OptionItem),
    ];

    public static void Run(string outputDir)
    {
        Directory.CreateDirectory(outputDir);
        var exporterOptions = new JsonSchemaExporterOptions
        {
            TreatNullObliviousAsNonNullable = true,
            TransformSchemaNode = (_, node) => RequireNonNullable(node),
        };
        foreach (var type in RootTypes)
        {
            var schema = ProtocolJson.Options.GetJsonSchemaAsNode(type, exporterOptions);
            if (schema is JsonObject obj) obj["title"] = type.Name;
            var path = Path.Combine(outputDir, $"{type.Name}.schema.json");
            File.WriteAllText(path, schema.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
        Console.WriteLine($"Exported {RootTypes.Length} schemas to {outputDir}");
    }

    /// <summary>
    /// The server omits only null values, so every property that cannot be null (including the polymorphic
    /// "type" discriminator and properties with defaults) is always present: mark it required for TypeScript.
    /// </summary>
    private static JsonNode RequireNonNullable(JsonNode node)
    {
        if (node is not JsonObject obj || obj["properties"] is not JsonObject props) return node;

        var required = obj["required"] as JsonArray ?? [];
        var present = required.Select(r => r?.GetValue<string>()).ToHashSet();
        foreach (var (name, schema) in props)
        {
            if (present.Contains(name) || CanBeNull(schema)) continue;
            if (name == "type") required.Insert(0, name);
            else required.Add(name);
        }
        obj["required"] = required;
        return node;
    }

    private static bool CanBeNull(JsonNode? schema) => schema switch
    {
        JsonObject o when o["type"] is JsonArray types => types.Any(t => t?.GetValue<string>() == "null"),
        JsonObject o when o["type"] is JsonValue type => type.GetValue<string>() == "null",
        JsonObject o when o["enum"] is JsonArray values => values.Any(v => v is null),
        JsonObject o when o["anyOf"] is JsonArray any => any.Any(CanBeNull),
        _ => false,
    };
}
