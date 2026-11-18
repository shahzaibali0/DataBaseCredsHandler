using System.Text;
using DbConnectHub.Core.Abstractions;
using DbConnectHub.Core.Models;
using DbConnectHub.Integrations;
using DbConnectHub.Presentation;
using DbConnectHub.Storage;

namespace DbConnectHub.App.Services;

/// <summary>
/// Answers pgAdmin's PasswordExecCommand: <c>DbConnectHub.exe --pgadmin-password &lt;login key&gt;</c>. It reads that
/// login's password from Credential Manager, writes it to standard output (which pgAdmin reads through a pipe) and
/// exits. The password is never written to a file or a log, and the error messages never contain it.
/// </summary>
public static class PgAdminPasswordCommand
{
    private const int Found = 0;
    private const int NotFound = 1;
    private const int CannotSend = 2;

    public static bool IsRequested(string[] args) => args.Length == 2 && args[0] == PgAdminServers.PasswordArgument;

    public static int Run(string[] args)
    {
        try
        {
            return Answer(args[1]);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // Last resort, so a failure while the password is in memory never becomes a crash report or dump.
            // The fixed text keeps any detail (and so any value) out of what pgAdmin logs.
            return Fail(CannotSend, "DB Connect Hub couldn't hand over the password. Type it in pgAdmin instead.");
        }
    }

    private static int Answer(string key)
    {
        if (!DatabaseLogin.TryParseKey(key, out var login)) return Fail(NotFound, "That isn't a DB Connect Hub login key.");

        var password = TryFindPassword(login);
        if (password is null)
        {
            return Fail(NotFound,
                $"DB Connect Hub has no single saved password for {login.Username}@{login.Host}:{login.Port}. Type it in pgAdmin.");
        }
        if (!PgAdminServers.CanSendPassword(password))
            return Fail(CannotSend, "This password has characters pgAdmin can't receive from a command. Type it in pgAdmin instead.");

        // Printable ASCII only (checked above), so it reads the same whichever code page pgAdmin's Python uses.
        var bytes = Encoding.ASCII.GetBytes(password);
        try
        {
            using var output = Console.OpenStandardOutput();
            output.Write(bytes);
            return Found;
        }
        catch (IOException)
        {
            return Fail(CannotSend, "pgAdmin stopped waiting for the password. Try connecting again.");
        }
        finally
        {
            Array.Clear(bytes);
        }
    }

    private static string? TryFindPassword(DatabaseLogin login)
    {
        try
        {
            var stores = new DataStores(
                new JsonConnectionRepository(), CredentialStoreFactory.Create(), new JsonSettingsStore(), new JsonInventoryRepository());
            return SavedPasswordLookup.FindPassword(stores, login);
        }
        catch (Exception exception) when (exception is DataFileUnreadableException or IOException or UnauthorizedAccessException)
        {
            // Answered as "not found": pgAdmin then reports the failed sign-in and the user can type the password.
            return null;
        }
    }

    private static int Fail(int exitCode, string message)
    {
        try
        {
            using var errors = Console.OpenStandardError();
            errors.Write(Encoding.UTF8.GetBytes(message + Environment.NewLine));
        }
        catch (IOException)
        {
            // pgAdmin already closed the pipe; the exit code still tells it the sign-in failed.
        }
        return exitCode;
    }
}
