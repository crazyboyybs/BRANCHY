namespace Branchy.Models;

/// <summary>Identifies the semantic role of a line in a unified Git diff.</summary>
public enum GitDiffLineKind
{
    /// <summary>Unchanged content or an informational line.</summary>
    Context,

    /// <summary>Content added by the change.</summary>
    Addition,

    /// <summary>Content removed by the change.</summary>
    Deletion,

    /// <summary>A unified-diff hunk location.</summary>
    HunkHeader,
}

/// <summary>Represents one displayable line from a unified Git diff.</summary>
public sealed record GitDiffLine(string Text, GitDiffLineKind Kind)
{
    /// <summary>Converts raw Git output into displayable semantic lines.</summary>
    public static IReadOnlyList<GitDiffLine> Parse(string diff)
    {
        if (string.IsNullOrEmpty(diff))
        {
            return [];
        }

        string normalized = diff.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        string[] sourceLines = normalized.Split('\n');
        var lines = new List<GitDiffLine>(sourceLines.Length);

        for (int index = 0; index < sourceLines.Length; index++)
        {
            string line = sourceLines[index];
            if (index == sourceLines.Length - 1 && line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith("diff --git ", StringComparison.Ordinal))
            {
                continue;
            }

            if (line.StartsWith("@@", StringComparison.Ordinal))
            {
                lines.Add(new GitDiffLine(line, GitDiffLineKind.HunkHeader));
                continue;
            }

            if (IsHeaderMetadata(line))
            {
                continue;
            }

            GitDiffLineKind kind = line.StartsWith('+')
                ? GitDiffLineKind.Addition
                : line.StartsWith('-')
                    ? GitDiffLineKind.Deletion
                    : GitDiffLineKind.Context;
            lines.Add(new GitDiffLine(line, kind));
        }

        return lines;
    }

    private static bool IsHeaderMetadata(string line) =>
        line.StartsWith("index ", StringComparison.Ordinal) ||
        line.StartsWith("--- ", StringComparison.Ordinal) ||
        line.StartsWith("+++ ", StringComparison.Ordinal) ||
        line.StartsWith("new file mode ", StringComparison.Ordinal) ||
        line.StartsWith("deleted file mode ", StringComparison.Ordinal) ||
        line.StartsWith("old mode ", StringComparison.Ordinal) ||
        line.StartsWith("new mode ", StringComparison.Ordinal);
}
