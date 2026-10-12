using System.Diagnostics;
using System.Text;
using Branchy.Models;
using Branchy.Services;

var localizer = new TestLocalizer();
var files = new WorkingTreeFileService(localizer);
string repository = Path.Combine(Path.GetTempPath(), "branchy-validation-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(repository);
await Git("init", "-b", "main");
await Git("config", "user.name", "BRANCHY Validation");
await Git("config", "user.email", "validation@example.invalid");
string path = Path.Combine(repository, "example.txt");
await File.WriteAllTextAsync(path, "primeira frase\r\nsegunda frase\r\n", new UTF8Encoding(true));
await Git("add", "example.txt");
await Git("commit", "-m", "Initial validation");
await File.AppendAllTextAsync(path, "preparada\r\n", new UTF8Encoding(false));
await Git("add", "example.txt");
string indexBefore = await Git("show", ":example.txt");
WorkingTreeDocument document = await files.ReadAsync(repository, "example.txt");
await files.SaveAsync(repository, "example.txt", document, document.Text.Replace("primeira frase", "frase editada"));
byte[] saved = await File.ReadAllBytesAsync(path);
Assert(saved.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }), "UTF-8 BOM preserved");
Assert(Encoding.UTF8.GetString(saved).Contains("frase editada\r\nsegunda frase\r\n"), "Text and CRLF preserved");
Assert(await Git("show", ":example.txt") == indexBefore, "Staged content unchanged");
Assert((await Git("diff", "--", "example.txt")).Contains("frase editada"), "Working-tree diff refreshed");

document = await files.ReadAsync(repository, "example.txt");
await File.AppendAllTextAsync(path, "external edit\r\n");
await Reject(() => files.SaveAsync(repository, "example.txt", document, "overwrite"), "Error_QuickEditChanged");
Assert((await File.ReadAllTextAsync(path)).Contains("external edit"), "Concurrent edit preserved");
await Reject(() => files.ReadAsync(repository, "../outside.txt"), "Error_QuickEditPath");
await Reject(() => files.ReadAsync(repository, ".git/config"), "Error_QuickEditPath");
await File.WriteAllBytesAsync(Path.Combine(repository, "binary.dat"), [0, 1, 2, 3]);
await Reject(() => files.ReadAsync(repository, "binary.dat"), "Error_QuickEditUnsupported");
await File.WriteAllBytesAsync(Path.Combine(repository, "legacy.dat"), [0xFF, 0x80, 0xA0]);
await Reject(() => files.ReadAsync(repository, "legacy.dat"), "Error_QuickEditUnsupported");
await File.WriteAllBytesAsync(Path.Combine(repository, "large.txt"), new byte[4 * 1024 * 1024 + 1]);
await Reject(() => files.ReadAsync(repository, "large.txt"), "Error_QuickEditUnsupported");
await File.WriteAllTextAsync(Path.Combine(repository, "utf16.txt"), "ação\r\n", new UnicodeEncoding(false, true, true));
WorkingTreeDocument utf16 = await files.ReadAsync(repository, "utf16.txt");
await files.SaveAsync(repository, "utf16.txt", utf16, "ação rápida\r\n");
Assert((await File.ReadAllBytesAsync(Path.Combine(repository, "utf16.txt"))).AsSpan().StartsWith(new byte[] { 0xFF, 0xFE }), "UTF-16 BOM preserved");
Assert(!Directory.EnumerateFiles(repository, ".branchy-edit-*.tmp").Any(), "No temporary edit files left");

var git = new GitService(localizer);
GitRepositorySnapshot snapshot = await git.GetSnapshotAsync(repository);
GitFileChange change = snapshot.Changes.Single(file => file.Path == "example.txt");
IReadOnlyList<GitDiffLine> lines = GitDiffLine.Parse(await git.GetDiffAsync(repository, change, false));
Assert(lines.Any(line => line.Kind == GitDiffLineKind.Addition && line.Text.Contains("frase editada")), "Diff additions classified");
Assert(lines.Any(line => line.Kind == GitDiffLineKind.Deletion && line.Text.Contains("primeira frase")), "Diff removals classified");
Assert(lines.All(line => !line.Text.StartsWith("diff --git")), "Command metadata hidden");
Console.WriteLine($"Validation repository: {repository}");

if (args.Length > 0)
{
    string target = Path.GetFullPath(args[0]);
    await Measure("GetSnapshotAsync", () => git.GetSnapshotAsync(target));
    await Measure("GetBranchesAsync", () => git.GetBranchesAsync(target));
    await Measure("GetHistoryAsync", () => git.GetHistoryAsync(target));
}
string patch = "diff --git a/file b/file\n--- a/file\n+++ b/file\n@@ -1,10000 +1,10000 @@\n" +
    string.Concat(Enumerable.Range(0, 10000).Select(index => $"-before {index}\n+after {index}\n"));
await Measure("Parse 20,001 patch lines", () => Task.FromResult(GitDiffLine.Parse(patch)));
Console.WriteLine("All validation checks passed.");

async Task<string> Git(params string[] arguments)
{
    var start = new ProcessStartInfo("git") { WorkingDirectory = repository, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
    foreach (string argument in arguments) start.ArgumentList.Add(argument);
    using var process = Process.Start(start)!;
    Task<string> output = process.StandardOutput.ReadToEndAsync();
    Task<string> error = process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();
    if (process.ExitCode != 0) throw new InvalidOperationException(await error);
    return await output;
}

static void Assert(bool condition, string label)
{
    if (!condition) throw new InvalidOperationException(label);
    Console.WriteLine("PASS: " + label);
}

static async Task Reject(Func<Task> action, string key)
{
    try { await action(); }
    catch (InvalidOperationException exception) when (exception.Message == key) { Console.WriteLine("PASS: " + key); return; }
    throw new InvalidOperationException("Expected rejection: " + key);
}

static async Task Measure<T>(string label, Func<Task<T>> action)
{
    await action();
    var elapsed = new List<double>();
    for (int index = 0; index < 5; index++)
    {
        var watch = Stopwatch.StartNew();
        await action();
        elapsed.Add(watch.Elapsed.TotalMilliseconds);
    }
    elapsed.Sort();
    Console.WriteLine($"MEASURE: {label}: median {elapsed[2]:F2} ms (5 samples)");
}

sealed class TestLocalizer : ILocalizationService
{
    public string GetString(string key) => key;
    public string Format(string key, params object[] arguments) => key + " " + string.Join(", ", arguments);
}
