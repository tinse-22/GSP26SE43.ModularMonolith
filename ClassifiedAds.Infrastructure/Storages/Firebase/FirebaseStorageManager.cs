using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Infrastructure.Storages.Firebase;

/// <summary>
/// Firebase Storage (Google Cloud Storage) implementation of IFileStorageManager.
/// Firebase Storage uses GCS under the hood, so we use the Google.Cloud.Storage.V1 SDK.
/// </summary>
public class FirebaseStorageManager : IFileStorageManager
{
    private readonly FirebaseOptions _options;
    private readonly Lazy<StorageClient> _storageClient;

    public FirebaseStorageManager(FirebaseOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));

        if (string.IsNullOrWhiteSpace(options.Bucket))
        {
            throw new ArgumentException("Firebase Storage bucket name is required.", nameof(options));
        }

        _storageClient = new Lazy<StorageClient>(() => CreateStorageClient());
    }

    private StorageClient CreateStorageClient()
    {
        if (!string.IsNullOrWhiteSpace(_options.CredentialFilePath))
        {
            var credential = GoogleCredential.FromFile(_options.CredentialFilePath);
            return StorageClient.Create(credential);
        }

        // Falls back to GOOGLE_APPLICATION_CREDENTIALS environment variable
        return StorageClient.Create();
    }

    public async Task CreateAsync(IFileEntry fileEntry, Stream stream, CancellationToken cancellationToken = default)
    {
        var objectName = GetObjectName(fileEntry);
        var contentType = GetContentType(fileEntry.FileName);

        await _storageClient.Value.UploadObjectAsync(
            _options.Bucket,
            objectName,
            contentType,
            stream,
            cancellationToken: cancellationToken);
    }

    public async Task<byte[]> ReadAsync(IFileEntry fileEntry, CancellationToken cancellationToken = default)
    {
        var objectName = GetObjectName(fileEntry);

        using var memoryStream = new MemoryStream();
        await _storageClient.Value.DownloadObjectAsync(
            _options.Bucket,
            objectName,
            memoryStream,
            cancellationToken: cancellationToken);

        return memoryStream.ToArray();
    }

    public async Task DeleteAsync(IFileEntry fileEntry, CancellationToken cancellationToken = default)
    {
        var objectName = GetObjectName(fileEntry);

        try
        {
            await _storageClient.Value.DeleteObjectAsync(
                _options.Bucket,
                objectName,
                cancellationToken: cancellationToken);
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // File already deleted or doesn't exist — safe to ignore
        }
    }

    public Task ArchiveAsync(IFileEntry fileEntry, CancellationToken cancellationToken = default)
    {
        // Firebase Storage doesn't have a built-in archive tier like Azure/AWS
        // Could be implemented by changing storage class to COLDLINE/ARCHIVE
        return Task.CompletedTask;
    }

    public Task UnArchiveAsync(IFileEntry fileEntry, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    private string GetObjectName(IFileEntry fileEntry)
    {
        var location = fileEntry.FileLocation ?? $"{fileEntry.Id}/{fileEntry.FileName}";

        return string.IsNullOrWhiteSpace(_options.Path)
            ? location
            : $"{_options.Path.TrimEnd('/')}/{location}";
    }

    private static string GetContentType(string fileName)
    {
        var extension = System.IO.Path.GetExtension(fileName)?.ToLowerInvariant();
        return extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".pdf" => "application/pdf",
            _ => "application/octet-stream",
        };
    }
}
