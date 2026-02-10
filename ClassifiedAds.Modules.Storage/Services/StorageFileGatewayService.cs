using ClassifiedAds.Application;
using ClassifiedAds.Contracts.Storage.DTOs;
using ClassifiedAds.Contracts.Storage.Services;
using ClassifiedAds.Infrastructure.Storages;
using ClassifiedAds.Modules.Storage.Entities;
using ClassifiedAds.Modules.Storage.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.Storage.Services;

public class StorageFileGatewayService : IStorageFileGatewayService
{
    private readonly Dispatcher _dispatcher;
    private readonly IFileStorageManager _fileStorageManager;

    public StorageFileGatewayService(Dispatcher dispatcher, IFileStorageManager fileStorageManager)
    {
        _dispatcher = dispatcher;
        _fileStorageManager = fileStorageManager;
    }

    public async Task<StorageUploadedFileDTO> UploadAsync(StorageUploadFileRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (request.Content == null)
        {
            throw new ArgumentException("File content is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.FileName))
        {
            throw new ArgumentException("File name is required.", nameof(request));
        }

        var fileEntry = new FileEntry
        {
            Name = request.FileName,
            Size = request.FileSize,
            UploadedTime = DateTimeOffset.UtcNow,
            FileName = request.FileName,
            ContentType = string.IsNullOrWhiteSpace(request.ContentType) ? "application/octet-stream" : request.ContentType,
            OwnerId = request.OwnerId,
            FileCategory = (Entities.FileCategory)request.FileCategory,
            Encrypted = false,
        };

        await _dispatcher.DispatchAsync(new AddOrUpdateEntityCommand<FileEntry>(fileEntry), cancellationToken);

        fileEntry.FileLocation = DateTime.UtcNow.ToString("yyyy/MM/dd/") + fileEntry.Id;
        await _dispatcher.DispatchAsync(new AddOrUpdateEntityCommand<FileEntry>(fileEntry), cancellationToken);

        if (request.Content.CanSeek)
        {
            request.Content.Position = 0;
        }

        await _fileStorageManager.CreateAsync(fileEntry.ToModel(), request.Content, cancellationToken);

        await _dispatcher.DispatchAsync(new AddOrUpdateEntityCommand<FileEntry>(fileEntry), cancellationToken);

        return new StorageUploadedFileDTO
        {
            Id = fileEntry.Id,
        };
    }
}
