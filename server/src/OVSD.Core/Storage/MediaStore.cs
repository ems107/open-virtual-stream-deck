using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace OVSD.Core.Storage;

/// <summary>
/// Content-addressed image store: files are named by the hash of their content, so identical uploads
/// (or the same album art seen twice) are stored once and URLs can be cached forever.
/// </summary>
public sealed partial class MediaStore(DataPaths paths)
{
    public const string UrlPrefix = "/media/";

    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
        [".svg"] = "image/svg+xml",
        [".bmp"] = "image/bmp",
        [".ico"] = "image/x-icon",
    };

    public static bool IsAllowedExtension(string extension) => ContentTypes.ContainsKey(extension);

    public static string ExtensionFor(string? contentType) =>
        ContentTypes.FirstOrDefault(kv => kv.Value.Equals(contentType, StringComparison.OrdinalIgnoreCase)).Key ?? ".png";

    /// <summary>Stores the bytes and returns the public URL (/media/&lt;hash&gt;.&lt;ext&gt;).</summary>
    public string Save(ReadOnlySpan<byte> content, string extension)
    {
        extension = extension.ToLowerInvariant();
        if (!IsAllowedExtension(extension)) throw new ArgumentException($"Unsupported image type {extension}");
        var name = Convert.ToHexStringLower(SHA256.HashData(content))[..32] + extension;
        var path = Path.Combine(paths.Media, name);
        if (!File.Exists(path))
        {
            var tmp = path + ".tmp";
            File.WriteAllBytes(tmp, content);
            File.Move(tmp, path, overwrite: true);
        }
        return UrlPrefix + name;
    }

    /// <summary>Resolves a stored file name to (path, content type), or null if invalid or missing.</summary>
    public (string Path, string ContentType)? Find(string fileName)
    {
        if (!FileNamePattern().IsMatch(fileName)) return null;
        var path = Path.Combine(paths.Media, fileName);
        return File.Exists(path) ? (path, ContentTypes[Path.GetExtension(fileName)]) : null;
    }

    /// <summary>File names of media referenced by a URL like "/media/x.png", if it is one of ours.</summary>
    public static string? FileNameFromUrl(string? url) =>
        url is not null && url.StartsWith(UrlPrefix, StringComparison.Ordinal) ? url[UrlPrefix.Length..] : null;

    [GeneratedRegex(@"^[a-f0-9]{32}\.[a-z]{3,4}$")]
    private static partial Regex FileNamePattern();
}
