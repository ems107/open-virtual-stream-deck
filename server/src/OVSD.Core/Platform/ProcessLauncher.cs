using System.Diagnostics;
using System.Text;

namespace OVSD.Core.Platform;

/// <summary>Process launching is portable; only the shells differ per OS.</summary>
public sealed class ProcessLauncher : IProcessLauncher
{
    public void Launch(string path, string? arguments, string? workingDirectory)
    {
        var psi = new ProcessStartInfo(Environment.ExpandEnvironmentVariables(path))
        {
            UseShellExecute = true,
            Arguments = arguments ?? "",
        };
        if (!string.IsNullOrWhiteSpace(workingDirectory)) psi.WorkingDirectory = Environment.ExpandEnvironmentVariables(workingDirectory);
        Process.Start(psi)?.Dispose();
    }

    public void OpenUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) throw new ArgumentException($"Invalid URL '{url}'");
        Process.Start(new ProcessStartInfo(uri.ToString()) { UseShellExecute = true })?.Dispose();
    }

    public async Task<CommandResult> RunAsync(CommandShell shell, string command, TimeSpan timeout, bool hidden, CancellationToken ct)
    {
        var psi = BuildStartInfo(shell, command);
        psi.UseShellExecute = false;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        psi.CreateNoWindow = hidden;
        psi.StandardOutputEncoding = Encoding.UTF8;
        psi.StandardErrorEncoding = Encoding.UTF8;

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Could not start process");
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);

        var stdout = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
        var stderr = process.StandardError.ReadToEndAsync(timeoutCts.Token);
        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
            return new CommandResult(process.ExitCode, (await stdout).TrimEnd(), (await stderr).TrimEnd());
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            if (ct.IsCancellationRequested) throw;
            throw new TimeoutException($"Command timed out after {timeout.TotalSeconds:0.#}s");
        }
    }

    private static ProcessStartInfo BuildStartInfo(CommandShell shell, string command)
    {
        if (OperatingSystem.IsWindows())
        {
            return shell switch
            {
                CommandShell.PowerShell => new ProcessStartInfo("powershell.exe")
                {
                    ArgumentList = { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-Command", command },
                },
                CommandShell.Cmd => new ProcessStartInfo("cmd.exe") { ArgumentList = { "/c", command } },
                _ => SplitExecutable(command),
            };
        }
        return shell == CommandShell.None ? SplitExecutable(command) : new ProcessStartInfo("/bin/sh") { ArgumentList = { "-c", command } };
    }

    private static ProcessStartInfo SplitExecutable(string command)
    {
        command = command.Trim();
        if (command.StartsWith('"'))
        {
            var end = command.IndexOf('"', 1);
            if (end > 0) return new ProcessStartInfo(command[1..end], command[(end + 1)..].Trim());
        }
        var space = command.IndexOf(' ');
        return space < 0 ? new ProcessStartInfo(command) : new ProcessStartInfo(command[..space], command[(space + 1)..]);
    }
}
