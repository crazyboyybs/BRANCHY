namespace Branchy.Services;

/// <summary>Indicates that a valid folder is not contained in a Git repository.</summary>
public sealed class GitRepositoryNotFoundException(string message) : InvalidOperationException(message);
