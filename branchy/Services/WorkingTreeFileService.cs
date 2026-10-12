using System.Security.Cryptography;
using System.Text;

namespace Branchy.Services;

/// <summary>Holds the exact file version and encoding used by a quick-edit session.</summary>
public sealed class WorkingTreeDocument
{
    internal WorkingTreeDocument(string path, byte[] bytes, Encoding encoding, int preambleLength, string text)
    {
        Path = path;
        OriginalHash = SHA256.HashData(bytes);
        Encoding = encoding;
        Preamble = bytes[..preambleLength];
        Text = text;
        Newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : text.Contains('\r') ? "\r" : "\n";
    }

    /// <summary>Gets the validated file path.</summary>
    public string Path { get; }
    /// <summary>Gets the text at the start of the session.</summary>
    public string Text { get; }
    internal byte[] OriginalHash { get; }
    internal Encoding Encoding { get; }
    internal byte[] Preamble { get; }
    internal string Newline { get; }
}

/// <summary>Edits bounded text files without changing the index, encoding, or external modifications.</summary>
public sealed class WorkingTreeFileService(ILocalizationService localizer)
{
    private const int MaximumBytes = 4 * 1024 * 1024;

    /// <summary>Reads a UTF-8 or BOM-marked UTF-16 working-tree file.</summary>
    public async Task<WorkingTreeDocument> ReadAsync(string repositoryPath, string relativePath, CancellationToken cancellationToken = default)
    {
        string path = ValidatePath(repositoryPath, relativePath);
        if (new FileInfo(path).Length > MaximumBytes) throw Error("Error_QuickEditUnsupported");
        byte[] bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        if (bytes.Length > MaximumBytes) throw Error("Error_QuickEditUnsupported");
        Encoding encoding = new UTF8Encoding(false, true);
        int preambleLength = 0;
        if (bytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF })) preambleLength = 3;
        else if (bytes.AsSpan().StartsWith(new byte[] { 0xFF, 0xFE }))
        {
            encoding = new UnicodeEncoding(false, false, true); preambleLength = 2;
        }
        else if (bytes.AsSpan().StartsWith(new byte[] { 0xFE, 0xFF }))
        {
            encoding = new UnicodeEncoding(true, false, true); preambleLength = 2;
        }
        string text;
        try { text = encoding.GetString(bytes, preambleLength, bytes.Length - preambleLength); }
        catch (DecoderFallbackException) { throw Error("Error_QuickEditUnsupported"); }
        if (text.Contains('\0')) throw Error("Error_QuickEditUnsupported");
        return new WorkingTreeDocument(path, bytes, encoding, preambleLength, text);
    }

    /// <summary>Saves atomically only if the original file has not changed since it was read.</summary>
    public async Task SaveAsync(string repositoryPath, string relativePath, WorkingTreeDocument document, string text, CancellationToken cancellationToken = default)
    {
        string path = ValidatePath(repositoryPath, relativePath);
        if (!string.Equals(path, document.Path, StringComparison.OrdinalIgnoreCase)) throw Error("Error_QuickEditPath");
        string normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        if (document.Newline != "\n") normalized = normalized.Replace("\n", document.Newline, StringComparison.Ordinal);
        byte[] content;
        try { content = document.Encoding.GetBytes(normalized); }
        catch (EncoderFallbackException) { throw Error("Error_QuickEditUnsupported"); }
        if (content.Length + document.Preamble.Length > MaximumBytes || normalized.Contains('\0')) throw Error("Error_QuickEditUnsupported");
        string temporaryPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path)!, $".branchy-edit-{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var temporary = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
            {
                await temporary.WriteAsync(document.Preamble, cancellationToken).ConfigureAwait(false);
                await temporary.WriteAsync(content, cancellationToken).ConfigureAwait(false);
                await temporary.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            ValidatePath(repositoryPath, relativePath);
            // Deny concurrent writes during verification and replacement; permit the atomic rename.
            await using var original = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 4096, FileOptions.Asynchronous);
            byte[] currentHash = await SHA256.HashDataAsync(original, cancellationToken).ConfigureAwait(false);
            if (!CryptographicOperations.FixedTimeEquals(currentHash, document.OriginalHash)) throw Error("Error_QuickEditChanged");
            cancellationToken.ThrowIfCancellationRequested();
            File.Replace(temporaryPath, path, null);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private string ValidatePath(string repositoryPath, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || System.IO.Path.IsPathRooted(relativePath) || relativePath.Contains(':')) throw Error("Error_QuickEditPath");
        string root = System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(repositoryPath));
        string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(root, relativePath));
        if (!path.StartsWith(root + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw Error("Error_QuickEditPath");
        string[] parts = relativePath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Any(part => part.TrimEnd(' ', '.').Equals(".git", StringComparison.OrdinalIgnoreCase))) throw Error("Error_QuickEditPath");
        for (string? current = path; current is not null; current = System.IO.Path.GetDirectoryName(current))
        {
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw Error("Error_QuickEditPath");
            if (current.Equals(root, StringComparison.OrdinalIgnoreCase)) break;
        }
        return path;
    }

    private InvalidOperationException Error(string key) => new(localizer.GetString(key));
}
