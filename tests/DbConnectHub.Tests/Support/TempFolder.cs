namespace DbConnectHub.Tests.Support;

/// <summary>A fresh folder under %TEMP%\dbconnecthub-tests, deleted when the test ends.</summary>
public sealed class TempFolder : IDisposable
{
    public TempFolder()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dbconnecthub-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string Combine(params string[] parts) => System.IO.Path.Combine([Path, .. parts]);

    /// <summary>Writes a file at a path relative to this folder, creating its folders.</summary>
    public string WriteFile(string relativePath, string content)
    {
        var fullPath = Combine(relativePath.Split('/'));
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
        return fullPath;
    }

    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); }
        catch (IOException) { /* a file still open by a failed test; %TEMP% is cleaned eventually */ }
    }
}
