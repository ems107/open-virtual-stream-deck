using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;

namespace OVSD.Host.SystemSetup;

/// <summary>
/// The few things that need administrator rights, run as a short-lived elevated copy of OVSD.exe:
///   OVSD.exe --setup [--result file] firewall | remove-firewall | private-network=&lt;ifIndex&gt; | sensors | remove-sensors
/// The installer and the Settings page start it with one UAC prompt; the app itself keeps running as the user.
/// </summary>
public static class ElevatedSetup
{
    public sealed record Result(bool Ok, bool Cancelled, string? Error, List<string> Log);

    /// <summary>Entry point for "--setup" (already elevated).</summary>
    public static async Task<int> RunAsync(string[] args)
    {
        var log = new List<string>();
        string? error = null;
        var resultFile = ValueAfter(args, "--result");
        var program = Environment.ProcessPath!;
        try
        {
            foreach (var task in args.SkipWhile(a => a != "--setup").Skip(1).Where(a => !a.StartsWith("--") && a != resultFile))
            {
                switch (task.Split('=', 2))
                {
                    case ["firewall"]:
                        FirewallRules.Allow(program);
                        // Tells the uninstaller there are rules to remove.
                        await File.WriteAllTextAsync(FirewallMarker(program), "");
                        log.Add($"Firewall rules added for {program}");
                        break;
                    case ["remove-firewall"]:
                        FirewallRules.Remove(program);
                        File.Delete(FirewallMarker(program));
                        log.Add("Firewall rules removed");
                        break;
                    case ["private-network", var index]:
                        NetworkProfiles.MakePrivate(int.Parse(index));
                        log.Add($"Network on interface {index} set to private");
                        break;
                    case ["sensors"]:
                        await SensorService.InstallAsync(log.Add);
                        break;
                    case ["remove-sensors"]:
                        SensorService.Uninstall(log.Add);
                        break;
                    default:
                        throw new ArgumentException($"Unknown setup task: {task}");
                }
            }
        }
        catch (Exception ex)
        {
            error = ex.Message;
        }

        if (resultFile is not null)
            await File.WriteAllTextAsync(resultFile, JsonSerializer.Serialize(new Result(error is null, false, error, log)));
        return error is null ? 0 : 1;
    }

    /// <summary>Runs "--setup tasks" elevated (one UAC prompt) and waits for it.</summary>
    public static async Task<Result> RunElevatedAsync(params string[] tasks)
    {
        var resultFile = Path.Combine(Path.GetTempPath(), $"ovsd-setup-{Guid.NewGuid():N}.json");
        var info = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = true, Verb = "runas" };
        info.ArgumentList.Add("--setup");
        info.ArgumentList.Add("--result");
        info.ArgumentList.Add(resultFile);
        foreach (var task in tasks) info.ArgumentList.Add(task);
        try
        {
            using var process = Process.Start(info)!;
            await process.WaitForExitAsync();
            return File.Exists(resultFile)
                ? JsonSerializer.Deserialize<Result>(await File.ReadAllTextAsync(resultFile))!
                : new Result(false, false, $"Setup exited with code {process.ExitCode}", []);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return new Result(false, true, null, []); // the user said no to the UAC prompt
        }
        finally
        {
            File.Delete(resultFile);
        }
    }

    private static string FirewallMarker(string program) => Path.Combine(Path.GetDirectoryName(program)!, "firewall.configured");

    private static string? ValueAfter(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
