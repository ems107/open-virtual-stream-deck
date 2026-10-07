using System.Threading.Channels;
using OVSD.Core.Actions;
using OVSD.Core.Protocol;

namespace OVSD.Core.Runtime;

/// <summary>
/// One connected client. The socket handler drains <see cref="Outbox"/>; the runtime owns all other state
/// and only touches it while holding its lock.
/// </summary>
public sealed class DeckSession : IDeckSessionControl
{
    private readonly DeckRuntime _runtime;
    private readonly Channel<ServerMessage> _outbox =
        Channel.CreateUnbounded<ServerMessage>(new UnboundedChannelOptions { SingleReader = true });

    internal DeckSession(DeckRuntime runtime, string? deviceId, string name, bool isLocal, ClientRole role)
    {
        _runtime = runtime;
        DeviceId = deviceId;
        Name = name;
        IsLocal = isLocal;
        Role = role;
    }

    public string SessionId { get; } = Ids.New();
    public string? DeviceId { get; }
    public string Name { get; }
    public bool IsLocal { get; }
    public ClientRole Role { get; }

    public ChannelReader<ServerMessage> Outbox => _outbox.Reader;

    // ---- runtime-owned state
    internal string? ProfileId;
    internal string? ManualProfileId;
    internal List<string> PageStack = [];
    internal string? LastLayoutKey;
    internal readonly Dictionary<string, string> SentTiles = new();

    internal string? CurrentPageId => PageStack.Count > 0 ? PageStack[^1] : null;

    internal void Send(ServerMessage message) => _outbox.Writer.TryWrite(message);

    internal void Close() => _outbox.Writer.TryComplete();

    // ---- IDeckSessionControl (used by actions)
    public void OpenPage(string pageId) => _runtime.OpenPage(this, pageId);
    public void Back() => _runtime.Back(this);
    public void Home() => _runtime.Home(this);
    public void SwitchProfile(string profileId) => _runtime.SelectProfile(this, profileId);
    public void Notify(string message, NotifyLevel level = NotifyLevel.Info) => Send(new NotifyMessage(level, message));
}
