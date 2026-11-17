namespace PgCredsHandler.App.Services;

/// <summary>
/// Keeps one copy of the app per Windows user. Starting it again (from the Start menu or start.cmd) doesn't open a
/// second app: it signals the running one to show its window, then exits.
/// </summary>
public sealed class SingleInstance : IDisposable
{
    private const string MutexName = @"Local\PgCredsHandler.SingleInstance";
    private const string ShowEventName = @"Local\PgCredsHandler.ShowWindow";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _showEvent;
    private readonly CancellationTokenSource _stopListening = new();

    public SingleInstance()
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        IsFirstInstance = createdNew;
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
    }

    public bool IsFirstInstance { get; }

    /// <summary>Called by a second launch: asks the running app to show its window.</summary>
    public void SignalRunningInstance() => _showEvent.Set();

    /// <summary>Runs <paramref name="onShowRequested"/> (on a background thread) whenever another launch asks to show.</summary>
    public void ListenForShowRequests(Action onShowRequested)
    {
        var thread = new Thread(() =>
        {
            var handles = new[] { _showEvent, _stopListening.Token.WaitHandle };
            while (WaitHandle.WaitAny(handles) == 0) onShowRequested();
        })
        { IsBackground = true, Name = "PgCredsHandler show requests" };
        thread.Start();
    }

    public void Dispose()
    {
        _stopListening.Cancel();
        if (IsFirstInstance) _mutex.ReleaseMutex();
        _mutex.Dispose();
        _showEvent.Dispose();
    }
}
