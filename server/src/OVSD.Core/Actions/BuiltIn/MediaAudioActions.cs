using OVSD.Core.Platform;

namespace OVSD.Core.Actions.BuiltIn;

public sealed class MediaActions(IMediaController media) : IActionProvider
{
    public IEnumerable<IActionHandler> GetActions() =>
    [
        new DelegateAction(new ActionDescriptor { Id = "media.playPause", Category = "media", Name = "Play / pause", Icon = "mdi:play-pause" },
            (_, _, _) => media.PlayPauseAsync()),
        new DelegateAction(new ActionDescriptor { Id = "media.next", Category = "media", Name = "Next track", Icon = "mdi:skip-next" },
            (_, _, _) => media.NextAsync()),
        new DelegateAction(new ActionDescriptor { Id = "media.previous", Category = "media", Name = "Previous track", Icon = "mdi:skip-previous" },
            (_, _, _) => media.PreviousAsync()),
        new DelegateAction(new ActionDescriptor { Id = "media.stop", Category = "media", Name = "Stop", Icon = "mdi:stop" },
            (_, _, _) => media.StopAsync()),
    ];
}

public sealed class AudioActions(IAudioController audio) : IActionProvider
{
    private static readonly ParamDescriptor TargetParam = new()
    {
        Name = "target", Label = "Target", Type = ParamType.Select, Default = "master",
        Options = [new("master", "Speakers (master)"), new("mic", "Microphone"), new("app", "Application")],
    };

    private static readonly ParamDescriptor AppParam = new()
    {
        Name = "app", Label = "Application", Type = ParamType.Select, OptionsSource = "audio.apps", AllowCustom = true,
        ShowIf = "target=app", Placeholder = "spotify",
    };

    public IEnumerable<IActionHandler> GetActions() =>
    [
        DelegateAction.Sync(new ActionDescriptor
        {
            Id = "audio.setVolume", Category = "audio", Name = "Set volume", Icon = "mdi:volume-high",
            Description = "Absolute value 0-100, or relative with a sign: +5 / -5. On a slider use {{value}}.",
            Params =
            [
                TargetParam, AppParam,
                new ParamDescriptor { Name = "value", Label = "Volume", Required = true, Default = "{{value}}" },
            ],
        }, (_, args) =>
        {
            var target = args.Enum("target", AudioTarget.Master);
            var app = args.String("app");
            var raw = args.Raw("value")!.Trim();
            var value = args.Double("value") ?? 0;
            if (raw.StartsWith('+') || raw.StartsWith('-'))
                value += audio.GetVolume(target, app) ?? 0;
            audio.SetVolume(target, app, Math.Clamp(value, 0, 100));
        }),

        DelegateAction.Sync(new ActionDescriptor
        {
            Id = "audio.mute", Category = "audio", Name = "Mute", Icon = "mdi:volume-off",
            Params =
            [
                TargetParam, AppParam,
                new ParamDescriptor
                {
                    Name = "mode", Label = "Mode", Type = ParamType.Select, Default = "toggle",
                    Options = [new("toggle", "Toggle"), new("on", "Mute"), new("off", "Unmute")],
                },
            ],
        }, (_, args) => audio.SetMute(args.Enum("target", AudioTarget.Master), args.String("app"), args.Mode())),

        DelegateAction.Sync(new ActionDescriptor
        {
            Id = "audio.setDevice", Category = "audio", Name = "Set default audio device", Icon = "mdi:speaker-multiple",
            Params =
            [
                new ParamDescriptor
                {
                    Name = "device", Label = "Device", Type = ParamType.Select, OptionsSource = "audio.devices", Required = true,
                },
            ],
        }, (_, args) => audio.SetDefaultDevice(args.Required("device"))),
    ];

    public static IEnumerable<IOptionsProvider> OptionProviders(IAudioController audio) =>
    [
        DelegateOptions.Sync("audio.apps", () => audio.GetSessionApps().Select(a => new OptionItem(a, a))),
        DelegateOptions.Sync("audio.devices", () =>
            audio.GetDevices(AudioFlow.Output).Select(d => new OptionItem(d.Id, "🔊 " + d.Name))
                .Concat(audio.GetDevices(AudioFlow.Input).Select(d => new OptionItem(d.Id, "🎤 " + d.Name)))),
    ];
}
