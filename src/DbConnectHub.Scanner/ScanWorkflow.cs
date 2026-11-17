using DbConnectHub.Core.Abstractions;
using DbConnectHub.Core.Models;
using DbConnectHub.Core.Services;

namespace DbConnectHub.Scanner;

/// <summary>What one scan changed, for the summary line shown after it.</summary>
public sealed record ScanSummary(int Found, int Added, int PasswordsChanged, int MarkedStale, int Total, IReadOnlyList<string> ScannedRoots)
{
    public int AppsFound { get; init; }
}

/// <summary>
/// The "Scan my PC" operation: pick the roots, find databases and apps, merge them into what is saved,
/// store database passwords in the credential store, then save the lists. Everything stays on this PC.
/// </summary>
public sealed class ScanWorkflow
{
    private readonly DataStores _stores;
    private readonly Func<IReadOnlyList<string>> _listDefaultRoots;
    private readonly TimeProvider _clock;

    /// <param name="stores">Where connections, apps, passwords and settings are kept.</param>
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
    /// so the saved lists are never half-updated.
    /// </summary>
    public ScanSummary ScanAndSave(IProgress<ScanProgress>? progress, CancellationToken cancellationToken)
    {
        var settings = _stores.Settings.Load();
        var roots = ChooseRoots(settings);
        var existingConnections = _stores.Connections.GetAll();
        var existingInventory = _stores.Inventory.Load();
        var now = _clock.GetLocalNow();

        var findings = new ProjectScanner(settings).Scan(roots, progress, cancellationToken);
        var connections = ConnectionMerger.Merge(
            existingConnections, findings.Connections, roots, settings.HiddenFingerprints, _stores.Credentials.Read, now);
        var inventory = InventoryMerger.Merge(new InventoryMergeInput(existingInventory, findings.Apps, roots));

        cancellationToken.ThrowIfCancellationRequested();
        SavePasswordsThenLists(connections, inventory);

        return new ScanSummary(
            ConnectionMerger.Deduplicate(findings.Connections).Count(), connections.Added, connections.PasswordsChanged,
            connections.MarkedStale, connections.Connections.Count, roots)
        {
            AppsFound = inventory.Apps.Count(app => roots.Any(root => Core.Util.ConnectionKey.IsUnder(app.AppPath, root))),
        };
    }

    private IReadOnlyList<string> ChooseRoots(AppSettings settings) =>
        settings.ScanRoots.Count > 0 ? settings.ScanRoots : _listDefaultRoots();

    // Passwords first: if Credential Manager refuses one, the exception stops us before metadata claims it was stored.
    private void SavePasswordsThenLists(MergeResult connections, InventorySnapshot inventory)
    {
        foreach (var (connectionId, username, password) in connections.PasswordWrites)
            _stores.Credentials.Save(connectionId, username, password);

        _stores.Connections.SaveAll(connections.Connections);
        _stores.Inventory.Save(inventory);
    }
}
