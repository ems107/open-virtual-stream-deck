using System.Text.Json.Serialization;

namespace OVSD.Core.Protocol;

/// <summary>Messages sent from a deck/editor client to the server over the WebSocket.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(HelloMessage), "hello")]
public abstract record ClientMessage;

public sealed record HelloMessage(
    int ProtocolVersion,
    string? Token,
    DeviceInfo Device,
    Viewport Viewport) : ClientMessage;

public sealed record DeviceInfo(string Name, string UserAgent);

public sealed record Viewport(int Width, int Height, double PixelRatio);

/// <summary>Messages sent from the server to clients.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(WelcomeMessage), "welcome")]
[JsonDerivedType(typeof(ErrorMessage), "error")]
public abstract record ServerMessage;

public sealed record WelcomeMessage(int ProtocolVersion, string ServerName, string ServerVersion) : ServerMessage;

public sealed record ErrorMessage(string Code, string Message) : ServerMessage;

public static class ProtocolInfo
{
    public const int Version = 1;
}
