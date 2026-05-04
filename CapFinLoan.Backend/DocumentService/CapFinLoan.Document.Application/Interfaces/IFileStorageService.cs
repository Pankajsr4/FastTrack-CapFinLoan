namespace CapFinLoan.Document.Application.Interfaces;

public interface IFileStorageService
{
    Task<string> SaveFileAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default);
    Task<Stream?> GetFileStreamAsync(string storedFileName, CancellationToken cancellationToken = default);
}
