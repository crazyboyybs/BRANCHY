using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Branchy.Models;

namespace Branchy.Services;

/// <summary>Persists planning boards atomically in the BRANCHY local data directory.</summary>
public sealed class PlanningBoardService : IPlanningBoardService, IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly string _boardsDirectory;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    /// <summary>Initializes the local planning board store.</summary>
    public PlanningBoardService()
    {
        _boardsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BRANCHY",
            "PlanningBoards");
    }

    /// <inheritdoc />
    public async Task<PlanningBoard> LoadAsync(
        string repositoryPath,
        CancellationToken cancellationToken = default)
    {
        string boardPath = GetBoardPath(repositoryPath);
        if (!File.Exists(boardPath))
        {
            return new PlanningBoard();
        }

        await using FileStream stream = File.OpenRead(boardPath);
        PlanningBoard? board = await JsonSerializer.DeserializeAsync<PlanningBoard>(
            stream,
            SerializerOptions,
            cancellationToken);
        return board ?? new PlanningBoard();
    }

    /// <inheritdoc />
    public async Task SaveAsync(
        string repositoryPath,
        PlanningBoard board,
        CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(_boardsDirectory);
            string boardPath = GetBoardPath(repositoryPath);
            string temporaryPath = boardPath + ".tmp";

            await using (FileStream stream = File.Create(temporaryPath))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    board,
                    SerializerOptions,
                    cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, boardPath, overwrite: true);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose() => _writeLock.Dispose();

    private string GetBoardPath(string repositoryPath)
    {
        string normalizedPath = Path.GetFullPath(repositoryPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .ToUpperInvariant();
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedPath));
        return Path.Combine(_boardsDirectory, $"{Convert.ToHexString(hash)}.json");
    }
}
