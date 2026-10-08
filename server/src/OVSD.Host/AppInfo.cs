namespace OVSD.Host;

public static class AppInfo
{
    /// <summary>Product version (the &lt;Version&gt; in OVSD.Host.csproj), e.g. "1.0.0".</summary>
    public static readonly string Version = typeof(AppInfo).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
}
