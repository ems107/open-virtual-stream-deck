using LibreHardwareMonitor.Hardware;

namespace OVSD.Platform.Windows.Sensors;

/// <summary>One reading of the CPU sensors. Values are null when the hardware doesn't expose them.</summary>
public sealed record CpuReading(string? Name, double? Temperature, double? Power, double? Clock);

/// <summary>
/// Reads CPU temperature, package power and clock through LibreHardwareMonitor. The sensors live behind
/// the PawnIO kernel driver, which only an elevated process can use: this runs inside the sensor service.
/// </summary>
public sealed class CpuSensors : IDisposable
{
    private readonly Computer _computer = new() { IsCpuEnabled = true };
    private readonly IHardware? _cpu;

    public CpuSensors()
    {
        _computer.Open();
        _cpu = _computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);
    }

    /// <summary>Whether the PawnIO driver is installed (without it there is no temperature).</summary>
    public static bool DriverInstalled => LibreHardwareMonitor.PawnIo.PawnIo.IsInstalled;

    public CpuReading Read()
    {
        if (_cpu is null) return new CpuReading(null, null, null, null);
        // Without the driver LibreHardwareMonitor still lists the sensors, all reading 0.
        if (!DriverInstalled) return new CpuReading(_cpu.Name, null, null, null);
        _cpu.Update();
        var temperature =
            Find(SensorType.Temperature, "CPU Package") ??   // Intel
            Find(SensorType.Temperature, "Core (Tctl/Tdie)") ?? // AMD Ryzen
            Find(SensorType.Temperature, "Core (Tdie)") ??
            Find(SensorType.Temperature, null);
        var power = Find(SensorType.Power, "Package") ?? Find(SensorType.Power, "CPU Package");
        var clock = Find(SensorType.Clock, "Cores (Average)") ?? AverageCoreClock();
        return new CpuReading(_cpu.Name, temperature, power, clock);
    }

    private double? Find(SensorType type, string? name)
    {
        var sensor = _cpu!.Sensors.FirstOrDefault(s => s.SensorType == type && (name is null || s.Name == name) && s.Value is not null);
        return sensor?.Value is { } value ? Math.Round(value, 1) : null;
    }

    private double? AverageCoreClock()
    {
        var clocks = _cpu!.Sensors
            .Where(s => s.SensorType == SensorType.Clock && s.Name.StartsWith("Core #") && !s.Name.Contains('(') && s.Value is not null)
            .Select(s => (double)s.Value!.Value)
            .ToList();
        return clocks.Count > 0 ? Math.Round(clocks.Average()) : null;
    }

    public void Dispose() => _computer.Close();
}
