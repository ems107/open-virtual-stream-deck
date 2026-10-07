using System.Collections.Concurrent;
using OVSD.Core.Expressions;

namespace OVSD.Core.Variables;

/// <summary>
/// Global named values (media.title, obs.scene, sys.cpu, user.counter...).
/// Providers write here; tiles, conditions and templates read from here.
/// Names are case-insensitive.
/// </summary>
public sealed class VariableStore
{
    /// <summary>Variables with this prefix are persisted across restarts.</summary>
    public const string PersistentPrefix = "user.";

    private readonly ConcurrentDictionary<string, object?> _values = new(StringComparer.OrdinalIgnoreCase);
    private long _version;

    /// <summary>Raised after a value actually changes. Handlers must be fast and thread-safe.</summary>
    public event Action<string>? Changed;

    /// <summary>Monotonic counter bumped on every change.</summary>
    public long Version => Interlocked.Read(ref _version);

    public object? Get(string name) => _values.GetValueOrDefault(name);

    public bool Contains(string name) => _values.ContainsKey(name);

    public void Set(string name, object? value)
    {
        value = Values.Normalize(value);
        var changed = false;
        _values.AddOrUpdate(name,
            _ => { changed = true; return value; },
            (_, old) => { changed = !Equals(old, value); return value; });
        if (changed) Notify(name);
    }

    public void Remove(string name)
    {
        if (_values.TryRemove(name, out _)) Notify(name);
    }

    public void RemovePrefix(string prefix)
    {
        foreach (var key in _values.Keys.Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            Remove(key);
    }

    public IReadOnlyDictionary<string, object?> Snapshot() =>
        new SortedDictionary<string, object?>(_values, StringComparer.OrdinalIgnoreCase);

    /// <summary>Resolver for expressions: local values first, then the global store.</summary>
    public Func<string, object?> Resolver(IReadOnlyDictionary<string, object?>? locals = null) =>
        locals is null or { Count: 0 }
            ? Get
            : name => locals.TryGetValue(name, out var local) ? local : Get(name);

    private void Notify(string name)
    {
        Interlocked.Increment(ref _version);
        Changed?.Invoke(name);
    }
}
