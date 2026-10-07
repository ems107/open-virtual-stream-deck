using System.Net.NetworkInformation;
using LibreHardwareMonitor.Hardware;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OVSD.Core.Model;
using OVSD.Core.Storage;
using OVSD.Core.Variables;

namespace OVSD.Platform.Windows;

/// <summary>
/// Publishes sys.* variables every second: CPU, RAM and network from Win32, plus GPU/CPU sensors from
/// LibreHardwareMonitor when enabled in settings.
/// </summary>
public sealed class SystemMetrics(VariableStore variables, ConfigRepository config, ILogger<SystemMetrics> logger) : BackgroundService
{
    private ulong _lastIdle, _lastTotal;
    private long _lastRx, _lastTx;
    private DateTime _lastNetSample;
    private Computer? _computer;
    private MetricsSettings? _sensorSettings;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Sensors initialization can take seconds; never block startup on it.
        await Task.Yield();
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            do
            {
                try
                {
                    SampleCpu();
                    SampleMemory();
                    SampleNetwork();
                    SampleSensors();
                    variables.Set("sys.uptime", Math.Round(Environment.TickCount64 / 1000.0));
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Metrics sample failed");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) { }
        finally
        {
            _computer?.Close();
        }
    }

    private void SampleCpu()
    {
        if (!Native.GetSystemTimes(out var idle, out var kernel, out var user)) return;
        var total = kernel.Value + user.Value; // kernel time includes idle time
        var dTotal = total - _lastTotal;
        var dIdle = idle.Value - _lastIdle;
        if (_lastTotal != 0 && dTotal > 0)
            variables.Set("sys.cpu", Math.Round(100.0 * (dTotal - dIdle) / dTotal, 1));
        _lastTotal = total;
        _lastIdle = idle.Value;
    }

    private void SampleMemory()
    {
        var status = new Native.MEMORYSTATUSEX { dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Native.MEMORYSTATUSEX>() };
        if (!Native.GlobalMemoryStatusEx(ref status)) return;
        const double gb = 1024.0 * 1024 * 1024;
        variables.Set("sys.ram", (double)status.dwMemoryLoad);
        variables.Set("sys.ram.used", Math.Round((status.ullTotalPhys - status.ullAvailPhys) / gb, 1));
        variables.Set("sys.ram.total", Math.Round(status.ullTotalPhys / gb, 1));
    }

    private void SampleNetwork()
    {
        long rx = 0, tx = 0;
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
            var stats = nic.GetIPStatistics();
            rx += stats.BytesReceived;
            tx += stats.BytesSent;
        }
        var now = DateTime.UtcNow;
        var seconds = (now - _lastNetSample).TotalSeconds;
        if (_lastNetSample != default && seconds > 0)
        {
            var down = Math.Max(0, (rx - _lastRx) / seconds / 1024);
            var up = Math.Max(0, (tx - _lastTx) / seconds / 1024);
            variables.Set("sys.net.down", Math.Round(down, 1));
            variables.Set("sys.net.up", Math.Round(up, 1));
            variables.Set("sys.net.down.text", Rate(down));
            variables.Set("sys.net.up.text", Rate(up));
        }
        _lastRx = rx;
        _lastTx = tx;
        _lastNetSample = now;
    }

    private static string Rate(double kbPerSecond) =>
        kbPerSecond >= 1024 ? $"{kbPerSecond / 1024:0.0} MB/s" : $"{kbPerSecond:0} KB/s";

    private void SampleSensors()
    {
        var settings = config.Settings.Metrics;
        if (settings != _sensorSettings)
        {
            _computer?.Close();
            _computer = null;
            _sensorSettings = settings;
            if (settings.GpuSensors || settings.CpuSensors)
            {
                try
                {
                    _computer = new Computer { IsGpuEnabled = settings.GpuSensors, IsCpuEnabled = settings.CpuSensors };
                    _computer.Open();
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Hardware sensors unavailable");
                    _computer = null;
                }
            }
        }
        if (_computer is null) return;

        var gpus = _computer.Hardware
            .Where(h => h.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel)
            .OrderBy(h => h.HardwareType == HardwareType.GpuIntel) // prefer the discrete GPU
            .ToList();
        if (gpus.FirstOrDefault() is { } gpu)
        {
            gpu.Update();
            variables.Set("sys.gpu", Sensor(gpu, SensorType.Load, "GPU Core"));
            variables.Set("sys.gpu.temp", Sensor(gpu, SensorType.Temperature, "GPU Core"));
            variables.Set("sys.gpu.mem", Sensor(gpu, SensorType.Load, "GPU Memory"));
            variables.Set("sys.gpu.name", gpu.Name);
        }

        if (_computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu) is { } cpu)
        {
            cpu.Update();
            variables.Set("sys.cpu.temp",
                Sensor(cpu, SensorType.Temperature, "CPU Package") ??
                Sensor(cpu, SensorType.Temperature, "Core (Tctl/Tdie)") ??
                Sensor(cpu, SensorType.Temperature, null));
        }
    }

    private static double? Sensor(IHardware hardware, SensorType type, string? name)
    {
        var sensor = hardware.Sensors.FirstOrDefault(s => s.SensorType == type && (name is null || s.Name == name));
        return sensor?.Value is { } value ? Math.Round(value, 1) : null;
    }
}
