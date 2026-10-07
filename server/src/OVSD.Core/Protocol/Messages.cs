using System.Text.Json.Serialization;
using OVSD.Core.Actions;
using OVSD.Core.Model;

namespace OVSD.Core.Protocol;

public static class ProtocolInfo
{
    public const int Version = 1;
}

public enum ClientRole { Deck, Editor }

// ------------------------------------------------------------------ client → server

/// <summary>Messages sent from a deck/editor client to the server over the WebSocket.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(HelloMessage), "hello")]
[JsonDerivedType(typeof(InputMessage), "input")]
[JsonDerivedType(typeof(NavigateMessage), "navigate")]
[JsonDerivedType(typeof(SelectProfileMessage), "selectProfile")]
public abstract record ClientMessage;

public sealed record HelloMessage(
    int ProtocolVersion,
    string? Token,
    ClientRole Role,
    DeviceInfo Device,
    Viewport Viewport) : ClientMessage;

public sealed record DeviceInfo(string Name, string UserAgent);

public sealed record Viewport(int Width, int Height, double PixelRatio);

/// <summary>A gesture on a tile. Value is the slider position for "change".</summary>
public sealed record InputMessage(string ControlId, Gesture Gesture, double? Value = null) : ClientMessage;

public enum NavigateTarget { Back, Home, Page }

public sealed record NavigateMessage(NavigateTarget Target, string? PageId = null) : ClientMessage;

public sealed record SelectProfileMessage(string ProfileId) : ClientMessage;

// ------------------------------------------------------------------ server → client

/// <summary>Messages sent from the server to clients.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(WelcomeMessage), "welcome")]
[JsonDerivedType(typeof(ErrorMessage), "error")]
[JsonDerivedType(typeof(LayoutMessage), "layout")]
[JsonDerivedType(typeof(TilesMessage), "tiles")]
[JsonDerivedType(typeof(NotifyMessage), "notify")]
[JsonDerivedType(typeof(ConfigChangedMessage), "configChanged")]
[JsonDerivedType(typeof(DeckSettingsMessage), "deckSettings")]
public abstract record ServerMessage;

public sealed record DeckSettingsMessage(DeckSettings Settings) : ServerMessage;

public sealed record WelcomeMessage(
    int ProtocolVersion,
    string ServerName,
    string ServerVersion,
    string? DeviceId,
    string? DeviceName,
    bool IsLocal,
    DeckSettings Settings) : ServerMessage;

/// <summary>Error codes: unpaired, protocol_mismatch, bad_message.</summary>
public sealed record ErrorMessage(string Code, string Message) : ServerMessage;

public sealed record ProfileSummary(string Id, string Name);

public sealed record LayoutMessage(
    string ProfileId,
    string ProfileName,
    int Revision,
    string PageId,
    string PageName,
    int Rows,
    int Cols,
    Theme Theme,
    bool CanGoBack,
    List<ProfileSummary> Profiles) : ServerMessage;

/// <summary>Tiles that changed since the last message. With Reset, the client drops every tile not included.</summary>
public sealed record TilesMessage(List<TileState> Tiles, List<string> Removed, bool Reset) : ServerMessage;

public sealed record NotifyMessage(NotifyLevel Level, string Message) : ServerMessage;

public enum ConfigKind { Profiles, Devices, Settings }

/// <summary>Tells editors something changed on disk so they can refresh.</summary>
public sealed record ConfigChangedMessage(ConfigKind Kind, string? Id, int? Revision) : ServerMessage;

/// <summary>Fully rendered tile: templates resolved, state appearance merged.</summary>
public sealed record TileState
{
    public required string Id { get; init; }
    public ControlKind Kind { get; init; }
    public required int Row { get; init; }
    public required int Col { get; init; }
    public int RowSpan { get; init; } = 1;
    public int ColSpan { get; init; } = 1;
    public string? Background { get; init; }
    public string? Icon { get; init; }
    public string? IconColor { get; init; }
    public string? Image { get; init; }
    public ImageFit? ImageFit { get; init; }
    public string? Text { get; init; }
    public string? TextColor { get; init; }
    public int? FontSize { get; init; }
    public TextPosition? TextPosition { get; init; }
    public string? State { get; init; }
    /// <summary>Client must wait for a possible long press before sending tap.</summary>
    public bool HasLongPress { get; init; }
    /// <summary>Client must wait for a possible second tap before sending tap.</summary>
    public bool HasDoubleTap { get; init; }
    public SliderTile? Slider { get; init; }
    public WidgetTile? Widget { get; init; }
}

public sealed record SliderTile(double Value, double Min, double Max, double Step, Orientation Orientation, string? Color);

public sealed record WidgetTile(WidgetType Type, double? Value, double Min, double Max, List<double>? Series, string? Color);
