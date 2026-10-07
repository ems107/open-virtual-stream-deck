namespace OVSD.Core.Actions.BuiltIn;

public sealed class DeckActions : IActionProvider
{
    private static IDeckSessionControl Session(ActionContext ctx) =>
        ctx.Session ?? throw new ActionException("This action needs a deck (it was not triggered from a device)");

    public IEnumerable<IActionHandler> GetActions() =>
    [
        DelegateAction.Sync(new ActionDescriptor
        {
            Id = "deck.openPage", Category = "deck", Name = "Open page / folder", Icon = "mdi:folder-open",
            Params = [new ParamDescriptor { Name = "page", Label = "Page", Type = ParamType.Page, Required = true }],
        }, (ctx, args) => Session(ctx).OpenPage(args.Required("page"))),

        DelegateAction.Sync(new ActionDescriptor
        {
            Id = "deck.back", Category = "deck", Name = "Go back", Icon = "mdi:arrow-left",
        }, (ctx, _) => Session(ctx).Back()),

        DelegateAction.Sync(new ActionDescriptor
        {
            Id = "deck.home", Category = "deck", Name = "Go to home page", Icon = "mdi:home",
        }, (ctx, _) => Session(ctx).Home()),

        DelegateAction.Sync(new ActionDescriptor
        {
            Id = "deck.switchProfile", Category = "deck", Name = "Switch profile", Icon = "mdi:swap-horizontal",
            Description = "Shows another profile on this device until the automatic rules choose a different one.",
            Params = [new ParamDescriptor { Name = "profile", Label = "Profile", Type = ParamType.Profile, Required = true }],
        }, (ctx, args) => Session(ctx).SwitchProfile(args.Required("profile"))),

        DelegateAction.Sync(new ActionDescriptor
        {
            Id = "deck.notify", Category = "deck", Name = "Show message", Icon = "mdi:message-text",
            Params =
            [
                new ParamDescriptor { Name = "message", Label = "Message", Required = true },
                new ParamDescriptor
                {
                    Name = "level", Label = "Type", Type = ParamType.Select, Default = "info",
                    Options = [new("info", "Info"), new("success", "Success"), new("warning", "Warning"), new("error", "Error")],
                },
            ],
        }, (ctx, args) => Session(ctx).Notify(args.Required("message"), args.Enum("level", NotifyLevel.Info))),

        DelegateAction.Sync(new ActionDescriptor
        {
            Id = "deck.toggle", Category = "deck", Name = "Set toggle state", Icon = "mdi:toggle-switch",
            Description = "Changes the on/off state of a toggle button (this one if no control id is given).",
            Params =
            [
                new ParamDescriptor
                {
                    Name = "mode", Label = "Mode", Type = ParamType.Select, Default = "toggle",
                    Options = [new("toggle", "Toggle"), new("on", "On"), new("off", "Off")],
                },
                new ParamDescriptor { Name = "control", Label = "Control id (optional)" },
            ],
        }, (ctx, args) =>
        {
            var id = args.String("control") ?? ctx.ControlId ?? throw new ActionException("No control to toggle");
            var name = ToggleVariable(id);
            ctx.Variables.Set(name, args.Mode() ?? !Expressions.Values.IsTruthy(ctx.Variables.Get(name)));
        }),
    ];

    public static string ToggleVariable(string controlId) => $"toggle.{controlId}";
}
