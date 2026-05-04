using CapFinLoan.Document.Application.Interfaces;

namespace CapFinLoan.Document.Infrastructure.Storage;

/// <summary>
/// Stores uploaded files on the local filesystem under wwwroot/uploads.
/// </summary>
public class LocalFileStorageService : IFileStorageService
{
    private readonly string _basePath;

    public LocalFileStorageService(string basePath)
    {
        _basePath = basePath;
        Directory.CreateDirectory(_basePath);
    }

    public async Task<string> SaveFileAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(fileName);
        var storedFileName = $"{Guid.NewGuid()}{extension}";
        var filePath = Path.Combine(_basePath, storedFileName);

        await using var outputStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        await fileStream.CopyToAsync(outputStream, cancellationToken);

        return storedFileName;
    }

    public Task<Stream?> GetFileStreamAsync(string storedFileName, CancellationToken cancellationToken = default)
    {
        var filePath = Path.Combine(_basePath, storedFileName);

        if (!File.Exists(filePath))
            return Task.FromResult<Stream?>(null);

        Stream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        return Task.FromResult<Stream?>(stream);
    }
}
