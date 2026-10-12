namespace Branchy.Models;

/// <summary>Describes one configured Git remote and its transfer URLs.</summary>
public sealed record GitRemote(string Name, string FetchUrl, string? PushUrl)
{
    /// <summary>Gets whether this remote has an explicit push URL.</summary>
    public bool HasSeparatePushUrl => !string.IsNullOrWhiteSpace(PushUrl);

    /// <summary>Gets the URL used when pushing to this remote.</summary>
    public string EffectivePushUrl => PushUrl ?? FetchUrl;
}
