using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using DbConnectHub.Core.Abstractions;

namespace DbConnectHub.Storage;

/// <summary>
/// Stores each password in Windows Credential Manager under
/// "DbConnectHub:{connectionId}" as a CRED_TYPE_GENERIC credential scoped to the
/// current user (CRED_PERSIST_LOCAL_MACHINE would be shared; we use CRED_PERSIST_ENTERPRISE
/// so it roams with the user profile but stays per-user).
/// The secret is DPAPI-protected by Windows per user account, so no other user can read it.
/// A value larger than one credential can hold is split across numbered credentials (see <see cref="CredentialChunks"/>).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsCredentialStore : ICredentialStore
{
    private readonly string _prefix;

    /// <param name="prefix">The start of every target name; the app's own prefix unless reading another (older) name.</param>
    public WindowsCredentialStore(string prefix = Paths.CredentialPrefix) => _prefix = prefix;

    public void Save(string connectionId, string username, string password)
    {
        var target = Target(connectionId);
        var bytes = Encoding.Unicode.GetBytes(password);
        try
        {
            var chunks = CredentialChunks.Split(bytes);
            for (var index = 0; index < chunks.Count; index++)
                WriteBlob(CredentialChunks.TargetFor(target, index), username, chunks[index]);

            DeleteChunksFrom(target, chunks.Count); // a shorter value than last time leaves no old pieces behind
        }
        finally
        {
            Array.Clear(bytes);
        }
    }

    public string? Read(string connectionId)
    {
        var target = Target(connectionId);
        var first = ReadBlob(target);
        if (first is null) return null;

        var chunks = new List<byte[]> { first };
        for (var index = 1; index < CredentialChunks.MaxChunks; index++)
        {
            var next = ReadBlob(CredentialChunks.TargetFor(target, index));
            if (next is null) break;
            chunks.Add(next);
        }

        byte[]? joined = null;
        try
        {
            joined = CredentialChunks.Join(chunks);
            return Encoding.Unicode.GetString(joined);
        }
        finally
        {
            if (joined is not null) Array.Clear(joined);
            foreach (var chunk in chunks) Array.Clear(chunk);
        }
    }

    public void Delete(string connectionId) => DeleteChunksFrom(Target(connectionId), 0);

    private static void WriteBlob(string target, string username, byte[] blob)
    {
        var handle = GCHandle.Alloc(blob, GCHandleType.Pinned);
        try
        {
            var cred = new CREDENTIAL
            {
                Type = CRED_TYPE_GENERIC,
                TargetName = target,
                CredentialBlob = handle.AddrOfPinnedObject(),
                CredentialBlobSize = (uint)blob.Length,
                Persist = CRED_PERSIST_ENTERPRISE,
                UserName = string.IsNullOrEmpty(username) ? "postgres" : username,
                Comment = "Saved by DbConnectHub"
            };

            if (!CredWrite(ref cred, 0))
                throw new InvalidOperationException(
                    $"Windows Credential Manager refused to save \"{username}\" (error {Marshal.GetLastWin32Error()}).");
        }
        finally
        {
            handle.Free();
            Array.Clear(blob);
        }
    }

    private static byte[]? ReadBlob(string target)
    {
        if (!CredRead(target, CRED_TYPE_GENERIC, 0, out var handle)) return null;
        try
        {
            var cred = Marshal.PtrToStructure<CREDENTIAL>(handle);
            if (cred.CredentialBlobSize == 0 || cred.CredentialBlob == IntPtr.Zero) return [];
            var bytes = new byte[cred.CredentialBlobSize];
            Marshal.Copy(cred.CredentialBlob, bytes, 0, bytes.Length);
            return bytes;
        }
        finally { CredFree(handle); }
    }

    private static void DeleteChunksFrom(string target, int firstIndex)
    {
        foreach (var chunkTarget in CredentialChunks.TargetsFrom(target, firstIndex))
            CredDelete(chunkTarget, CRED_TYPE_GENERIC, 0);
    }

    private string Target(string connectionId) => $"{_prefix}:{connectionId}";

    // --- P/Invoke ---
    private const uint CRED_TYPE_GENERIC = 1;
    private const uint CRED_PERSIST_ENTERPRISE = 3;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CREDENTIAL
    {
        public uint Flags;
        public uint Type;
        public string TargetName;
        public string? Comment;
        public long LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string UserName;
    }

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "CredWriteW")]
    private static extern bool CredWrite(ref CREDENTIAL credential, uint flags);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "CredReadW")]
    private static extern bool CredRead(string target, uint type, uint flags, out IntPtr credential);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "CredDeleteW")]
    private static extern bool CredDelete(string target, uint type, uint flags);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern void CredFree(IntPtr buffer);
}
