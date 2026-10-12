using System;

namespace Branchy.Services;

internal static class CommitMessageNormalizer
{
    internal static string Normalize(string message) =>
        message.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
}
