using OVSD.Core.Platform;

namespace OVSD.Core.Actions.BuiltIn;

public sealed class KeyboardActions(IKeyboard keyboard) : IActionProvider
{
    private static readonly ParamDescriptor KeysParam =
        new() { Name = "keys", Label = "Keys", Type = ParamType.Hotkey, Required = true, Placeholder = "Ctrl+Shift+M" };

    public IEnumerable<IActionHandler> GetActions() =>
    [
        new DelegateAction(new ActionDescriptor
        {
            Id = "keyboard.hotkey", Category = "keyboard", Name = "Hotkey", Icon = "mdi:keyboard",
            Description = "Presses a key combination. Several combinations separated by spaces are sent one after another.",
            Params = [KeysParam],
        }, async (_, args, ct) =>
        {
            var chords = Keys.ParseSequence(args.Required("keys"));
            for (var i = 0; i < chords.Count; i++)
            {
                if (i > 0) await Task.Delay(30, ct);
                await keyboard.SendChordAsync(chords[i], ct);
            }
        }),

        new DelegateAction(new ActionDescriptor
        {
            Id = "keyboard.type", Category = "keyboard", Name = "Type text", Icon = "mdi:form-textbox",
            Params =
            [
                new ParamDescriptor { Name = "text", Label = "Text", Type = ParamType.MultilineText, Required = true },
                new ParamDescriptor { Name = "delay", Label = "Delay between characters (ms)", Type = ParamType.Number, Default = "0" },
            ],
        }, (_, args, ct) => keyboard.TypeTextAsync(args.Required("text"), args.Int("delay", 0), ct)),

        DelegateAction.Sync(new ActionDescriptor
        {
            Id = "keyboard.keyDown", Category = "keyboard", Name = "Hold keys down", Icon = "mdi:arrow-collapse-down",
            Description = "Use on \"press\" together with \"Release keys\" on \"release\" for push-to-talk.",
            Params = [KeysParam],
        }, (_, args) => keyboard.KeyDown(Keys.ParseChord(args.Required("keys")))),

        DelegateAction.Sync(new ActionDescriptor
        {
            Id = "keyboard.keyUp", Category = "keyboard", Name = "Release keys", Icon = "mdi:arrow-collapse-up",
            Params = [KeysParam],
        }, (_, args) => keyboard.KeyUp(Keys.ParseChord(args.Required("keys")))),
    ];
}
