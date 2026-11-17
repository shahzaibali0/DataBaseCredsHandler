namespace PgCredsHandler.Storage;

/// <summary>
/// Windows Credential Manager holds at most 2,560 bytes per credential (CRED_MAX_CREDENTIAL_BLOB_SIZE), and a
/// database password can be bigger. Such a password is split across numbered credentials and joined again when
/// read, so it still lives only in Credential Manager.
/// </summary>
public static class CredentialChunks
{
    /// <summary>CRED_MAX_CREDENTIAL_BLOB_SIZE: 5 × 512 bytes. Even, so a UTF-16 character is never cut in half.</summary>
    public const int MaxBlobBytes = 2560;

    /// <summary>A generous ceiling (about 160 KB) so a corrupt chain can't loop for ever.</summary>
    public const int MaxChunks = 64;

    /// <summary>The value's bytes in pieces of at most <see cref="MaxBlobBytes"/>; one empty piece for an empty value.</summary>
    public static IReadOnlyList<byte[]> Split(byte[] value)
    {
        if (value.Length > MaxBlobBytes * MaxChunks)
            throw new InvalidOperationException($"The value is too large to store ({value.Length:N0} bytes).");
        if (value.Length == 0) return [value];

        return value.Chunk(MaxBlobBytes).ToList();
    }

    public static byte[] Join(IEnumerable<byte[]> chunks) => chunks.SelectMany(chunk => chunk).ToArray();

    /// <summary>
    /// The credential name for a piece: the first keeps the plain name (so values saved before chunking still read),
    /// later pieces add #1, #2…
    /// </summary>
    public static string TargetFor(string baseTarget, int chunkIndex) =>
        chunkIndex == 0 ? baseTarget : $"{baseTarget}#{chunkIndex}";

    /// <summary>Every possible credential target from an index onward, including targets after a missing suffix.</summary>
    public static IEnumerable<string> TargetsFrom(string baseTarget, int firstIndex) =>
        Enumerable.Range(firstIndex, MaxChunks - firstIndex).Select(index => TargetFor(baseTarget, index));
}
