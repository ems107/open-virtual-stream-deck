using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using OVSD.Core.Protocol;

namespace OVSD.Host;

/// <summary>Writes JSON schemas of the protocol types, consumed by web/scripts/gen-types.mjs.</summary>
public static class SchemaExport
{
    private static readonly Type[] RootTypes = [typeof(ClientMessage), typeof(ServerMessage)];

    public static void Run(string outputDir)
    {
        Directory.CreateDirectory(outputDir);
        var exporterOptions = new JsonSchemaExporterOptions
        {
            TreatNullObliviousAsNonNullable = true,
            TransformSchemaNode = (_, node) => RequireDiscriminator(node),
        };
        foreach (var type in RootTypes)
        {
            var schema = ProtocolJson.Options.GetJsonSchemaAsNode(type, exporterOptions);
            if (schema is JsonObject obj) obj["title"] = type.Name;
            var path = Path.Combine(outputDir, $"{type.Name}.schema.json");
            File.WriteAllText(path, schema.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"Wrote {path}");
        }
    }

    /// <summary>The polymorphic "type" discriminator is always written, so mark it required for TS unions.</summary>
    private static JsonNode RequireDiscriminator(JsonNode node)
    {
        if (node is JsonObject obj
            && obj["properties"] is JsonObject props
            && props["type"] is JsonObject typeSchema
            && typeSchema.ContainsKey("const"))
        {
            var required = obj["required"] as JsonArray ?? [];
            if (!required.Any(r => r?.GetValue<string>() == "type")) required.Insert(0, "type");
            obj["required"] = required;
        }
        return node;
    }
}
