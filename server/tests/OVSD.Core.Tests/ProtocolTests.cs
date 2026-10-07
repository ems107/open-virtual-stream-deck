using System.Text.Json;
using OVSD.Core.Model;
using OVSD.Core.Protocol;

namespace OVSD.Core.Tests;

public class ProtocolTests
{
    private static ClientMessage? Parse(string json) => JsonSerializer.Deserialize<ClientMessage>(json, ProtocolJson.Options);

    [Fact]
    public void InputWithoutValueParses()
    {
        var input = Assert.IsType<InputMessage>(Parse("""{"type":"input","controlId":"c1","gesture":"longPress"}"""));
        Assert.Equal(Gesture.LongPress, input.Gesture);
        Assert.Null(input.Value);
    }

    [Fact]
    public void NavigateWithoutPageParses() =>
        Assert.Equal(NavigateTarget.Back, Assert.IsType<NavigateMessage>(Parse("""{"type":"navigate","target":"back"}""")).Target);

    [Fact]
    public void HelloRequiresDevice() =>
        Assert.Throws<JsonException>(() => Parse("""{"type":"hello","protocolVersion":1,"token":null,"role":"deck"}"""));

    [Fact]
    public void ServerMessagesUseCamelCaseAndOmitNulls()
    {
        var json = JsonSerializer.Serialize<ServerMessage>(
            new TilesMessage([new TileState { Id = "a", Row = 0, Col = 1, Kind = ControlKind.Slider }], [], false),
            ProtocolJson.Options);
        Assert.StartsWith("""{"type":"tiles","tiles":[{"id":"a","kind":"slider","row":0,"col":1""", json);
        Assert.DoesNotContain("null", json);
    }
}
