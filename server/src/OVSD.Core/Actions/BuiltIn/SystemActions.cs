using OVSD.Core.Platform;

namespace OVSD.Core.Actions.BuiltIn;

public sealed class SystemActions(IProcessLauncher launcher) : IActionProvider
{
    public IEnumerable<IActionHandler> GetActions() =>
    [
        DelegateAction.Sync(new ActionDescriptor
        {
            Id = "system.launch", Category = "system", Name = "Open app or file", Icon = "mdi:application",
            Params =
            [
                new ParamDescriptor { Name = "path", Label = "Path", Required = true, Placeholder = @"C:\Program Files\App\app.exe" },
                new ParamDescriptor { Name = "args", Label = "Arguments" },
                new ParamDescriptor { Name = "workingDir", Label = "Working directory" },
            ],
        }, (_, args) => launcher.Launch(args.Required("path"), args.String("args"), args.String("workingDir"))),

        DelegateAction.Sync(new ActionDescriptor
        {
            Id = "system.openUrl", Category = "system", Name = "Open URL", Icon = "mdi:web",
            Params = [new ParamDescriptor { Name = "url", Label = "URL", Required = true, Placeholder = "https://" }],
        }, (_, args) => launcher.OpenUrl(args.Required("url"))),

        new DelegateAction(new ActionDescriptor
        {
            Id = "system.command", Category = "system", Name = "Run command", Icon = "mdi:console",
            Description = "Runs a command; its output can be stored in a variable to show it on a tile.",
            Params =
            [
                new ParamDescriptor
                {
                    Name = "shell", Label = "Shell", Type = ParamType.Select, Default = "powershell",
                    Options = [new("powershell", "PowerShell"), new("cmd", "cmd"), new("none", "None (run executable)")],
                },
                new ParamDescriptor { Name = "command", Label = "Command", Type = ParamType.MultilineText, Required = true },
                new ParamDescriptor { Name = "resultVariable", Label = "Store output in variable", Type = ParamType.Variable, Placeholder = "user.output" },
                new ParamDescriptor { Name = "timeout", Label = "Timeout (s)", Type = ParamType.Number, Default = "30" },
                new ParamDescriptor { Name = "hidden", Label = "Hide window", Type = ParamType.Bool, Default = "true" },
            ],
        }, async (ctx, args, ct) =>
        {
            var result = await launcher.RunAsync(
                args.Enum("shell", CommandShell.PowerShell),
                args.Required("command"),
                TimeSpan.FromSeconds(Math.Clamp(args.Int("timeout", 30), 1, 3600)),
                args.Bool("hidden", true),
                ct);
            if (args.String("resultVariable") is { Length: > 0 } variable)
            {
                ctx.SetVariable(variable, result.Output);
                ctx.SetVariable(variable + ".exitCode", result.ExitCode);
            }
            if (result.ExitCode != 0)
                throw new ActionException($"Command exited with code {result.ExitCode}: {Truncate(result.Error.Length > 0 ? result.Error : result.Output)}");
        }),
    ];

    private static string Truncate(string s) => s.Length > 200 ? s[..200] + "…" : s;
}
