using System.Text.RegularExpressions;
using OVSD.Core.Model;
using OVSD.Core.Platform;

namespace OVSD.Core.Runtime;

public static class ProfileMatcher
{
    public static bool Matches(Profile profile, ForegroundApp app) => profile.MatchRules.Any(rule => Matches(rule, app));

    public static bool Matches(MatchRule rule, ForegroundApp app)
    {
        if (string.IsNullOrWhiteSpace(rule.Process) && string.IsNullOrWhiteSpace(rule.TitleContains)) return false;
        if (!string.IsNullOrWhiteSpace(rule.Process) && !Wildcard(rule.Process.Trim(), StripExe(app.Process))) return false;
        if (!string.IsNullOrWhiteSpace(rule.TitleContains) && !app.Title.Contains(rule.TitleContains.Trim(), StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }

    private static string StripExe(string process) =>
        process.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? process[..^4] : process;

    private static bool Wildcard(string pattern, string value) =>
        Regex.IsMatch(value, "^" + Regex.Escape(StripExe(pattern)).Replace(@"\*", ".*") + "$", RegexOptions.IgnoreCase);
}
