using System.Linq;
using Robust.Client.Console;
using Robust.Shared.Log;

namespace Content.Client._LuaM.Console;

public sealed partial class ConsoleErrorBufferSystem : EntitySystem
{
    [Dependency] private IClientConsoleHost _consoleHost = default!;

    private const int Capacity = 50;

    private static readonly string[] ErrorPrefixes =
    {
        $"[{LogMessage.LogNameError}]",
        $"[{LogMessage.LogNameFatal}]",
    };

    private readonly Queue<string> _entries = new();

    public override void Initialize()
    {
        base.Initialize();
        _consoleHost.AddFormatted += OnAddFormatted;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _consoleHost.AddFormatted -= OnAddFormatted;
    }

    private void OnAddFormatted(object? sender, AddFormattedMessageArgs args)
    {
        var text = args.Message.ToString();
        if (!ErrorPrefixes.Any(prefix => text.StartsWith(prefix, StringComparison.Ordinal)))
            return;

        lock (_entries)
        {
            _entries.Enqueue(text);
            while (_entries.Count > Capacity)
                _entries.Dequeue();
        }
    }

    public List<string> GetLast(int count)
    {
        lock (_entries)
        {
            return _entries.Skip(Math.Max(0, _entries.Count - count)).ToList();
        }
    }
}
