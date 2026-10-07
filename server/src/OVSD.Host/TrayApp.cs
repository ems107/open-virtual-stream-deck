using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace OVSD.Host;

/// <summary>System tray icon. Runs on its own STA thread with a WinForms message loop.</summary>
public sealed class TrayApp(int port, string dataDir, Action onExit)
{
    public void Run()
    {
        Application.EnableVisualStyles();
        using var icon = new NotifyIcon
        {
            Icon = LoadIcon(),
            Text = "Open Virtual Stream Deck",
            Visible = true,
            ContextMenuStrip = new ContextMenuStrip(),
        };

        var editorUrl = $"http://localhost:{port}/editor";
        var menu = icon.ContextMenuStrip.Items;
        menu.Add("Abrir editor", null, (_, _) => Open(editorUrl));
        menu.Add("Conectar dispositivo (QR)", null, (_, _) => Open($"http://localhost:{port}/pair"));
        menu.Add(new ToolStripSeparator());

        var autostart = new ToolStripMenuItem("Iniciar con Windows") { Checked = Autostart.IsEnabled, CheckOnClick = true };
        autostart.CheckedChanged += (_, _) => Autostart.Set(autostart.Checked);
        menu.Add(autostart);
        menu.Add("Abrir carpeta de datos", null, (_, _) => Open(dataDir));
        menu.Add(new ToolStripSeparator());
        menu.Add("Salir", null, (_, _) =>
        {
            icon.Visible = false;
            Application.ExitThread();
        });
        icon.ContextMenuStrip.Opening += (_, _) => autostart.Checked = Autostart.IsEnabled;
        icon.DoubleClick += (_, _) => Open(editorUrl);

        Application.Run();
        onExit();
    }

    private static Icon LoadIcon()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "ovsd.ico");
        if (File.Exists(path)) return new Icon(path, SystemInformation.SmallIconSize);
        // Published single-file build: the icon is embedded in the executable.
        return Environment.ProcessPath is { } exe ? Icon.ExtractAssociatedIcon(exe) ?? SystemIcons.Application : SystemIcons.Application;
    }

    private static void Open(string target) =>
        Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
}
