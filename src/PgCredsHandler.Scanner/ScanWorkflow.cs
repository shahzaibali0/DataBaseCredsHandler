using PgCredsHandler.Core.Abstractions;
using PgCredsHandler.Core.Models;
using PgCredsHandler.Core.Services;

namespace PgCredsHandler.Scanner;

/// <summary>What one scan changed, for the summary line shown after it.</summary>
public sealed record ScanSummary(int Found, int Added, int PasswordsChanged, int MarkedStale, int Total, IReadOnlyList<string> ScannedRoots);

/// <summary>
/// The "Scan my PC" operation: pick the roots, find credentials, merge them into the saved list,
/// store passwords in the credential store and save the list. Everything stays on this PC.
/// </summary>
public sealed class ScanWorkflow
{
    private readonly DataStores _stores;
    private readonly Func<IReadOnlyList<string>> _listDefaultRoots;
    private readonly TimeProvider _clock;

    /// <param name="stores">Where connections, passwords and settings are kept.</param>
    /// <param name="listDefaultRoots">Roots used when the user hasn't chosen folders (all fixed drives).</param>
    /// <param name="clock">Time source for first/last-seen stamps; the system clock when omitted.</param>
    public ScanWorkflow(DataStores stores, Func<IReadOnlyList<string>> listDefaultRoots, TimeProvider? clock = null)
    {
        _stores = stores;
        _listDefaultRoots = listDefaultRoots;
        _clock = clock ?? TimeProvider.System;
    }

    /// <summary>The folders a scan will walk with the current settings.</summary>
    public IReadOnlyList<string> ListScanRoots() => ChooseRoots(_stores.Settings.Load());

    /// <summary>
    /// Scans, merges and saves. Cancelling, or a failure to store a password, saves nothing,
    /// so the saved list is never half-updated.
    /// </summary>
    public ScanSummary ScanAndSave(IProgress<ScanProgress>? progress, CancellationToken cancellationToken)
    {
        var settings = _stores.Settings.Load();
        var roots = ChooseRoots(settings);
        var existing = _stores.Connections.GetAll();

        var found = new ProjectScanner(settings).Scan(roots, progress, cancellationToken);
        var merge = ConnectionMerger.Merge(
            existing, found, roots, settings.HiddenFingerprints, _stores.Credentials.Read, _clock.GetLocalNow());

        cancellationToken.ThrowIfCancellationRequested();
        SavePasswordsThenList(merge);

        return new ScanSummary(
            ConnectionMerger.Deduplicate(found).Count(), merge.Added, merge.PasswordsChanged,
            merge.MarkedStale, merge.Connections.Count, roots);
    }

    private IReadOnlyList<string> ChooseRoots(AppSettings settings) =>
        settings.ScanRoots.Count > 0 ? settings.ScanRoots : _listDefaultRoots();

    // Passwords first: if Credential Manager refuses one, the exception stops us before the list
    // claims HasPassword for an entry whose password was never stored.
    private void SavePasswordsThenList(MergeResult merge)
    {
        foreach (var (connectionId, username, password) in merge.PasswordWrites)
            _stores.Credentials.Save(connectionId, username, password);

        _stores.Connections.SaveAll(merge.Connections);
    }
}
