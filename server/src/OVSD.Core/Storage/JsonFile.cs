using System.Text.Json;
using OVSD.Core.Protocol;

namespace OVSD.Core.Storage;

public static class JsonFile
{
    private static readonly JsonSerializerOptions Indented = new(ProtocolJson.Options) { WriteIndented = true };

    public static T? Read<T>(string path) where T : class =>
        File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path), ProtocolJson.Options) : null;

    /// <summary>Writes to a temp file and renames it over the target, so a crash never leaves a half-written file.</summary>
    public static void WriteAtomic<T>(string path, T value)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(value, Indented));
        File.Move(tmp, path, overwrite: true);
    }
}
