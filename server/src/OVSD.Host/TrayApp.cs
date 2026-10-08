using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace OVSD.Host;

/// <summary>System tray icon. Runs on its own STA thread with a WinForms message loop.</summary>
public sealed class TrayApp(int port, string dataDir, bool firstRun, bool lanReady, CancellationToken stopping, Action onExit)
{
    private static readonly bool Spanish = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "es";

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
        var pairUrl = $"http://localhost:{port}/pair";
        var menu = icon.ContextMenuStrip.Items;
        menu.Add(new ToolStripMenuItem($"OVSD {AppInfo.Version} · {NetworkInfo.GetPrimaryUrl(port)}") { Enabled = false });
        menu.Add(new ToolStripSeparator());
        var openEditor = new ToolStripMenuItem(L("Abrir editor", "Open editor"), null, (_, _) => Open(editorUrl));
        openEditor.Font = new Font(openEditor.Font, FontStyle.Bold);
        menu.Add(openEditor);
        menu.Add(L("Conectar dispositivo (QR)", "Connect a device (QR)"), null, (_, _) => Open(pairUrl));
        menu.Add(new ToolStripSeparator());

        var autostart = new ToolStripMenuItem(L("Iniciar con Windows", "Start with Windows")) { Checked = Autostart.IsEnabled, CheckOnClick = true };
        autostart.CheckedChanged += (_, _) => Autostart.Set(autostart.Checked);
        menu.Add(autostart);
        menu.Add(L("Abrir carpeta de datos", "Open data folder"), null, (_, _) => Open(dataDir));
        menu.Add(new ToolStripSeparator());
        menu.Add(L("Salir", "Exit"), null, (_, _) =>
        {
            icon.Visible = false;
            Application.ExitThread();
        });
        icon.ContextMenuStrip.Opening += (_, _) => autostart.Checked = Autostart.IsEnabled;
        icon.DoubleClick += (_, _) => Open(editorUrl);
        icon.BalloonTipClicked += (_, _) => Open(editorUrl);

        if (firstRun)
        {
            // Nothing else tells a new user where the app went: point at the tray icon and open the editor.
            icon.ShowBalloonTip(
                10_000,
                L("OVSD está en marcha", "OVSD is running"),
                lanReady
                    ? L("Lo encontrarás en este icono de la bandeja. Doble clic: editor. Clic derecho: conectar el móvil y más opciones.",
                        "You'll find it in this tray icon. Double-click: editor. Right-click: connect your phone and more.")
                    : L("Lo encontrarás en este icono de la bandeja. Para conectar el móvil, permite el acceso desde tu red en el editor (Ajustes → Este PC).",
                        "You'll find it in this tray icon. To connect your phone, allow access from your network in the editor (Settings → This PC)."),
                ToolTipIcon.Info);
            Open(editorUrl);
        }

        // The server can stop on its own (restart after enabling LAN access): take the icon down with it
        // instead of leaving a dead icon in the tray.
        using var marshal = new Control();
        marshal.CreateControl();
        using var stopRegistration = stopping.Register(() => marshal.BeginInvoke(() =>
        {
            icon.Visible = false;
            Application.ExitThread();
        }));

        Application.Run();
        onExit();
    }

    /// <summary>Error shown when the server cannot start (e.g. the port is taken), since a tray app has no console.</summary>
    public static void ShowStartupError(int port, Exception error)
    {
        var portInUse = error is IOException { InnerException: System.Net.Sockets.SocketException { SocketErrorCode: System.Net.Sockets.SocketError.AddressAlreadyInUse } }
            || error.InnerException is System.Net.Sockets.SocketException { SocketErrorCode: System.Net.Sockets.SocketError.AddressAlreadyInUse };
        var message = portInUse
            ? L($"El puerto {port} ya está en uso por otro programa.\n\nCierra ese programa o cambia \"Port\" en appsettings.json (junto a OVSD.exe).",
                $"Port {port} is already used by another program.\n\nClose it or change \"Port\" in appsettings.json (next to OVSD.exe).")
            : L($"OVSD no ha podido arrancar:\n\n{error.Message}", $"OVSD could not start:\n\n{error.Message}");
        MessageBox.Show(message, "Open Virtual Stream Deck", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private static string L(string es, string en) => Spanish ? es : en;

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
