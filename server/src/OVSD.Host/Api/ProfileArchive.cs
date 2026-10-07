using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using OVSD.Core.Model;
using OVSD.Core.Protocol;
using OVSD.Core.Storage;

namespace OVSD.Host.Api;

/// <summary>Zip with profile.json plus every /media/ image it references, for backups and sharing.</summary>
public static partial class ProfileArchive
{
    private const string ProfileEntry = "profile.json";
    private const long MaxEntryBytes = 20 * 1024 * 1024;

    public static byte[] Export(Profile profile, MediaStore media)
    {
        var json = JsonSerializer.Serialize(profile, new JsonSerializerOptions(ProtocolJson.Options) { WriteIndented = true });
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var writer = new StreamWriter(zip.CreateEntry(ProfileEntry).Open()))
                writer.Write(json);

            foreach (var file in MediaReference().Matches(json).Select(m => m.Groups[1].Value).Distinct())
            {
                if (media.Find(file) is not { } found) continue;
                zip.CreateEntryFromFile(found.Path, "media/" + file);
            }
        }
        return buffer.ToArray();
    }

    /// <summary>Stores the images and returns the profile with fresh ids, ready to save.</summary>
    public static Profile Import(Stream zipStream, MediaStore media)
    {
        using var zip = new ZipArchive(zipStream, ZipArchiveMode.Read);
        var entry = zip.GetEntry(ProfileEntry) ?? throw new InvalidDataException("profile.json not found in archive");
        Profile profile;
        using (var stream = entry.Open())
            profile = JsonSerializer.Deserialize<Profile>(stream, ProtocolJson.Options) ?? throw new InvalidDataException("Empty profile");

        foreach (var image in zip.Entries.Where(e => e.FullName.StartsWith("media/", StringComparison.Ordinal)))
        {
            var extension = Path.GetExtension(image.Name);
            if (!MediaStore.IsAllowedExtension(extension) || image.Length > MaxEntryBytes) continue;
            using var stream = image.Open();
            using var copy = new MemoryStream();
            stream.CopyTo(copy);
            media.Save(copy.ToArray(), extension);
        }
        return ProfileNormalizer.WithNewIds(profile);
    }

    [GeneratedRegex(@"/media/([a-f0-9]{32}\.[a-z]{3,4})")]
    private static partial Regex MediaReference();
}
