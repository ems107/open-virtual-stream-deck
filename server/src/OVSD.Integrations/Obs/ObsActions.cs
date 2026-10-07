using System.Text.Json;
using OVSD.Core.Actions;

namespace OVSD.Integrations.Obs;

public sealed class ObsActions(ObsClient obs) : IActionProvider
{
    private static ParamDescriptor ModeParam(params (string Value, string Label)[] extra) => new()
    {
        Name = "mode", Label = "Mode", Type = ParamType.Select, Default = "toggle",
        Options = [new("toggle", "Toggle"), new("on", "Start / on"), new("off", "Stop / off"), .. extra.Select(e => new OptionItem(e.Value, e.Label))],
    };

    private static readonly ParamDescriptor SceneParam =
        new() { Name = "scene", Label = "Scene", Type = ParamType.Select, OptionsSource = "obs.scenes", AllowCustom = true, Required = true };

    private static readonly ParamDescriptor InputParam =
        new() { Name = "input", Label = "Audio input", Type = ParamType.Select, OptionsSource = "obs.inputs", AllowCustom = true, Required = true };

    private Task Output(ActionArgs args, string toggle, string start, string stop, CancellationToken ct) =>
        obs.RequestAsync(args.Mode() switch { null => toggle, true => start, false => stop }, null, ct);

    public IEnumerable<IActionHandler> GetActions() =>
    [
        new DelegateAction(new ActionDescriptor
        {
            Id = "obs.setScene", Category = "obs", Name = "Switch scene", Icon = "mdi:filmstrip-box",
            Params = [SceneParam],
        }, (_, args, ct) => obs.RequestAsync("SetCurrentProgramScene", new { sceneName = args.Required("scene") }, ct)),

        new DelegateAction(new ActionDescriptor
        {
            Id = "obs.stream", Category = "obs", Name = "Streaming", Icon = "mdi:broadcast", Params = [ModeParam()],
        }, (_, args, ct) => Output(args, "ToggleStream", "StartStream", "StopStream", ct)),

        new DelegateAction(new ActionDescriptor
        {
            Id = "obs.record", Category = "obs", Name = "Recording", Icon = "mdi:record-rec",
            Params = [ModeParam(("pause", "Pause / resume"))],
        }, (_, args, ct) => args.String("mode") == "pause"
            ? obs.RequestAsync("ToggleRecordPause", null, ct)
            : Output(args, "ToggleRecord", "StartRecord", "StopRecord", ct)),

        new DelegateAction(new ActionDescriptor
        {
            Id = "obs.replay", Category = "obs", Name = "Replay buffer", Icon = "mdi:replay",
            Params = [ModeParam(("save", "Save replay"))],
        }, (_, args, ct) => args.String("mode") == "save"
            ? obs.RequestAsync("SaveReplayBuffer", null, ct)
            : Output(args, "ToggleReplayBuffer", "StartReplayBuffer", "StopReplayBuffer", ct)),

        new DelegateAction(new ActionDescriptor
        {
            Id = "obs.virtualcam", Category = "obs", Name = "Virtual camera", Icon = "mdi:webcam", Params = [ModeParam()],
        }, (_, args, ct) => Output(args, "ToggleVirtualCam", "StartVirtualCam", "StopVirtualCam", ct)),

        new DelegateAction(new ActionDescriptor
        {
            Id = "obs.mute", Category = "obs", Name = "Mute audio input", Icon = "mdi:microphone-off",
            Params = [InputParam, ModeParam()],
        }, (_, args, ct) => args.Mode() is { } mute
            ? obs.RequestAsync("SetInputMute", new { inputName = args.Required("input"), inputMuted = mute }, ct)
            : obs.RequestAsync("ToggleInputMute", new { inputName = args.Required("input") }, ct)),

        new DelegateAction(new ActionDescriptor
        {
            Id = "obs.inputVolume", Category = "obs", Name = "Audio input volume", Icon = "mdi:volume-medium",
            Description = "0-100 (on a slider use {{value}}).",
            Params = [InputParam, new ParamDescriptor { Name = "value", Label = "Volume", Default = "{{value}}", Required = true }],
        }, (_, args, ct) => obs.RequestAsync("SetInputVolume",
            new { inputName = args.Required("input"), inputVolumeMul = Math.Clamp((args.Double("value") ?? 0) / 100.0, 0, 1) }, ct)),

        new DelegateAction(new ActionDescriptor
        {
            Id = "obs.sourceVisibility", Category = "obs", Name = "Show / hide source", Icon = "mdi:eye",
            Params =
            [
                SceneParam,
                new ParamDescriptor { Name = "source", Label = "Source", Type = ParamType.Select, OptionsSource = "obs.sources", AllowCustom = true, Required = true },
                ModeParam(),
            ],
        }, async (_, args, ct) =>
        {
            var scene = args.Required("scene");
            var id = (await obs.RequestAsync("GetSceneItemId", new { sceneName = scene, sourceName = args.Required("source") }, ct))
                .GetProperty("sceneItemId").GetInt32();
            var enabled = args.Mode() ?? !(await obs.RequestAsync("GetSceneItemEnabled", new { sceneName = scene, sceneItemId = id }, ct))
                .GetProperty("sceneItemEnabled").GetBoolean();
            await obs.RequestAsync("SetSceneItemEnabled", new { sceneName = scene, sceneItemId = id, sceneItemEnabled = enabled }, ct);
        }),

        new DelegateAction(new ActionDescriptor
        {
            Id = "obs.studioMode", Category = "obs", Name = "Studio mode", Icon = "mdi:view-split-vertical", Params = [ModeParam()],
        }, async (ctx, args, ct) =>
        {
            var enabled = args.Mode() ?? !Core.Expressions.Values.IsTruthy(ctx.Variables.Get("obs.studio"));
            await obs.RequestAsync("SetStudioModeEnabled", new { studioModeEnabled = enabled }, ct);
        }),

        new DelegateAction(new ActionDescriptor
        {
            Id = "obs.transition", Category = "obs", Name = "Studio transition (preview → program)", Icon = "mdi:transition",
        }, (_, _, ct) => obs.RequestAsync("TriggerStudioModeTransition", null, ct)),

        new DelegateAction(new ActionDescriptor
        {
            Id = "obs.request", Category = "obs", Name = "Custom request", Icon = "mdi:code-json",
            Description = "Any obs-websocket v5 request, e.g. SetCurrentSceneTransition with {\"transitionName\":\"Fade\"}.",
            Params =
            [
                new ParamDescriptor { Name = "requestType", Label = "Request type", Required = true },
                new ParamDescriptor { Name = "data", Label = "Request data (JSON)", Type = ParamType.Json },
                new ParamDescriptor { Name = "resultVariable", Label = "Store response in variable", Type = ParamType.Variable },
            ],
        }, async (ctx, args, ct) =>
        {
            JsonElement? data = args.String("data") is { Length: > 0 } json
                ? JsonDocument.Parse(json).RootElement.Clone()
                : null;
            var response = await obs.RequestAsync(args.Required("requestType"), data, ct);
            if (args.String("resultVariable") is { Length: > 0 } variable) ctx.SetVariable(variable, response.GetRawText());
        }),
    ];

    public static IEnumerable<IOptionsProvider> OptionProviders(ObsClient obs) =>
    [
        new DelegateOptions("obs.scenes", async ct => (await obs.SceneNamesAsync(ct)).Select(s => new OptionItem(s, s)).ToList()),
        new DelegateOptions("obs.inputs", async ct => (await obs.InputNamesAsync(ct)).Select(s => new OptionItem(s, s)).ToList()),
        DelegateOptions.Sync("obs.sources", () => obs.SourceNames.Select(s => new OptionItem(s, s))),
    ];
}
