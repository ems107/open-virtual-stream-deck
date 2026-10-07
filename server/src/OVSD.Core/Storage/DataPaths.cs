namespace OVSD.Core.Storage;

/// <summary>Layout of the data directory (by default %APPDATA%\OVSD).</summary>
public sealed class DataPaths
{
    public DataPaths(string root)
    {
        Root = Path.GetFullPath(root);
        foreach (var dir in new[] { Root, Profiles, Media, Backups, Logs }) Directory.CreateDirectory(dir);
    }

    public static string DefaultRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OVSD");

    public string Root { get; }
    public string Profiles => Path.Combine(Root, "profiles");
    public string Media => Path.Combine(Root, "media");
    public string Backups => Path.Combine(Root, "backups");
    public string Logs => Path.Combine(Root, "logs");
    public string ConfigFile => Path.Combine(Root, "config.json");
    public string VariablesFile => Path.Combine(Root, "variables.json");
}
