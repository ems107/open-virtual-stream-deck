using System.Diagnostics;
using System.Security.Cryptography;
using System.ServiceProcess;
using OVSD.Platform.Windows.Sensors;

namespace OVSD.Host.SystemSetup;

/// <summary>
/// The CPU sensor service: a copy of OVSD.exe running as "--sensor-service" under LocalSystem. It only
/// reads the CPU sensors (through the PawnIO driver) and publishes them on a read-only named pipe, so the
/// app itself never needs administrator rights.
///
/// The copy lives in Program Files, which only administrators can modify: a service must never run an
/// executable from a folder the user (or malware running as the user) can overwrite.
/// </summary>
public static class SensorService
{
    public static string InstallDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "OVSD Sensors");
    private static string ServiceExe => Path.Combine(InstallDir, "OVSD.exe");

    // PawnIO is a separate, signed driver (freeware, by namazso). It is downloaded from its official
    // release and checked against this hash before running its installer.
    private const string PawnIoUrl = "https://github.com/namazso/PawnIO.Setup/releases/download/2.2.0/PawnIO_setup.exe";
    private const string PawnIoSha256 = "1F519A22E47187F70A1379A48CA604981C4FCF694F4E65B734AAA74A9FBA3032";

    public static string State()
    {
        try
        {
            using var service = new ServiceController(SensorPipe.ServiceName);
            return service.Status == ServiceControllerStatus.Running ? "running" : "stopped";
        }
        catch (InvalidOperationException)
        {
            return "notInstalled";
        }
    }

    /// <summary>Installs PawnIO if missing, copies OVSD and (re)registers the service. Needs administrator.</summary>
    public static async Task InstallAsync(Action<string> log)
    {
        if (!CpuSensors.DriverInstalled) await InstallPawnIoAsync(log);

        StopAndDelete(log);
        Directory.CreateDirectory(InstallDir);
        File.Copy(Environment.ProcessPath!, ServiceExe, overwrite: true);
        log($"Copied OVSD to {ServiceExe}");

        Sc("create", SensorPipe.ServiceName, "binPath=", $"\"{ServiceExe}\" --sensor-service", "start=", "auto", "DisplayName=", "OVSD Sensors");
        Sc("description", SensorPipe.ServiceName, "Reads the CPU temperature for Open Virtual Stream Deck. Read-only; publishes the values to the OVSD app.");
        Sc("failure", SensorPipe.ServiceName, "reset=", "86400", "actions=", "restart/5000/restart/30000//");
        using var service = new ServiceController(SensorPipe.ServiceName);
        service.Start();
        service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(20));
        log("Sensor service running");
    }

    /// <summary>Stops and removes the service and its copy of OVSD. PawnIO stays (other apps may use it).</summary>
    public static void Uninstall(Action<string> log)
    {
        StopAndDelete(log);
        if (Directory.Exists(InstallDir))
        {
            // The service process may need a moment to release the executable.
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    Directory.Delete(InstallDir, recursive: true);
                    break;
                }
                catch (Exception) when (attempt < 10)
                {
                    Thread.Sleep(500);
                }
            }
        }
        log("Sensor service removed");
    }

    private static void StopAndDelete(Action<string> log)
    {
        if (State() == "notInstalled") return;
        using (var service = new ServiceController(SensorPipe.ServiceName))
        {
            if (service.Status != ServiceControllerStatus.Stopped)
            {
                service.Stop();
                service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(20));
            }
        }
        Sc("delete", SensorPipe.ServiceName);
        log("Previous sensor service removed");
    }

    private static async Task InstallPawnIoAsync(Action<string> log)
    {
        log("Downloading PawnIO driver");
        var setup = Path.Combine(Path.GetTempPath(), $"PawnIO_setup-{Guid.NewGuid():N}.exe");
        try
        {
            using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) })
                await File.WriteAllBytesAsync(setup, await http.GetByteArrayAsync(PawnIoUrl));
            var hash = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(setup)));
            if (!hash.Equals(PawnIoSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The downloaded PawnIO installer doesn't match the expected file; not running it.");

            log("Installing PawnIO driver");
            using var process = Process.Start(new ProcessStartInfo(setup, "-install -silent") { UseShellExecute = false, CreateNoWindow = true })!;
            await process.WaitForExitAsync();
            // 3010 = installed, restart required: the driver still loads without one.
            if (process.ExitCode is not (0 or 3010)) throw new InvalidOperationException($"The PawnIO installer failed (code {process.ExitCode}).");
            log("PawnIO installed");
        }
        finally
        {
            File.Delete(setup);
        }
    }

    private static void Sc(params string[] args)
    {
        var info = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "sc.exe")) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        using var process = Process.Start(info)!;
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0) throw new InvalidOperationException($"sc {args[0]} failed: {output.Trim()}");
    }
}
