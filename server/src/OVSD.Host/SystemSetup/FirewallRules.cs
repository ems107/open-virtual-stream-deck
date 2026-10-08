namespace OVSD.Host.SystemSetup;

/// <summary>
/// Windows Firewall rules for OVSD, through the HNetCfg.FwPolicy2 COM API. Reading works as a normal
/// user; adding and removing rules needs administrator rights (see <see cref="ElevatedSetup"/>).
///
/// Windows asks "allow this app?" the first time a program without rules listens on the network. OVSD
/// avoids that prompt: it only listens on the LAN once its own rules exist, and creates them in a single
/// elevated step (installer or Settings), the same rules the prompt would create: allowed on private and
/// domain networks, blocked on public ones.
/// </summary>
public static class FirewallRules
{
    public const string Group = "Open Virtual Stream Deck";

    private const int ProfileDomain = 1, ProfilePrivate = 2, ProfilePublic = 4;
    private const int DirectionIn = 1;
    private const int ActionBlock = 0, ActionAllow = 1;
    private const int ProtocolAny = 256;

    /// <summary>
    /// Whether devices on a private network can reach <paramref name="program"/>: the firewall is off for
    /// private networks, or an enabled inbound rule allows the program and none blocks it.
    /// </summary>
    public static bool IsAllowed(string program)
    {
        try
        {
            dynamic policy = CreatePolicy();
            if (!(bool)policy.FirewallEnabled[ProfilePrivate]) return true;
            var allowed = false;
            foreach (var rule in ProgramRules(policy, program))
            {
                if (((int)rule.Profiles & ProfilePrivate) == 0) continue;
                if ((int)rule.Action == ActionBlock) return false;
                allowed = true;
            }
            return allowed;
        }
        catch
        {
            // No firewall service or API: nothing will prompt either.
            return true;
        }
    }

    /// <summary>Replaces the rules for <paramref name="program"/> with OVSD's own (needs administrator).</summary>
    public static void Allow(string program)
    {
        dynamic policy = CreatePolicy();
        Remove(policy, program, includePromptRules: true);
        policy.Rules.Add(NewRule(program, "Open Virtual Stream Deck", ActionAllow, ProfilePrivate | ProfileDomain,
            "Lets your phones and tablets on private networks reach OVSD."));
        policy.Rules.Add(NewRule(program, "Open Virtual Stream Deck (public networks)", ActionBlock, ProfilePublic,
            "OVSD is not reachable on public networks (cafés, airports…)."));
        RemoveStaleRules(policy);
    }

    /// <summary>Removes OVSD's rules for <paramref name="program"/> (needs administrator).</summary>
    public static void Remove(string program) => Remove(CreatePolicy(), program, includePromptRules: false);

    private static void Remove(dynamic policy, string program, bool includePromptRules)
    {
        foreach (var rule in ProgramRules(policy, program))
        {
            // The rules Windows' own prompt created (named after the executable) are superseded by ours.
            if ((string?)rule.Grouping == Group || (includePromptRules && IsPromptRule(rule)))
                Delete(policy, rule);
        }
    }

    /// <summary>Rules left behind by OVSD executables that no longer exist (moved or uninstalled builds).</summary>
    private static void RemoveStaleRules(dynamic policy)
    {
        var stale = new List<dynamic>();
        foreach (var rule in policy.Rules)
        {
            if ((string?)rule.ApplicationName is not { } app || !app.EndsWith("\\ovsd.exe", StringComparison.OrdinalIgnoreCase)) continue;
            if (((string?)rule.Grouping == Group || IsPromptRule(rule)) && !File.Exists(app)) stale.Add(rule);
        }
        foreach (var rule in stale) Delete(policy, rule);
    }

    /// <summary>Rules.Remove takes a name, and names aren't unique ("ovsd.exe" for every copy): rename first.</summary>
    private static void Delete(dynamic policy, dynamic rule)
    {
        var unique = "OVSD-delete-" + Guid.NewGuid().ToString("N");
        rule.Name = unique;
        policy.Rules.Remove(unique);
    }

    private static bool IsPromptRule(dynamic rule) =>
        string.Equals((string?)rule.Name, "ovsd.exe", StringComparison.OrdinalIgnoreCase);

    private static List<dynamic> ProgramRules(dynamic policy, string program)
    {
        var path = Path.GetFullPath(program);
        var rules = new List<dynamic>();
        foreach (var rule in policy.Rules)
        {
            if ((int)rule.Direction == DirectionIn && (bool)rule.Enabled
                && string.Equals((string?)rule.ApplicationName, path, StringComparison.OrdinalIgnoreCase))
                rules.Add(rule);
        }
        return rules;
    }

    private static dynamic NewRule(string program, string name, int action, int profiles, string description)
    {
        dynamic rule = Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FWRule", throwOnError: true)!)!;
        rule.Name = name;
        rule.Description = description;
        rule.ApplicationName = Path.GetFullPath(program);
        rule.Protocol = ProtocolAny;
        rule.Direction = DirectionIn;
        rule.Action = action;
        rule.Profiles = profiles;
        rule.Grouping = Group;
        rule.Enabled = true;
        return rule;
    }

    private static dynamic CreatePolicy() =>
        Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwPolicy2", throwOnError: true)!)!;
}
