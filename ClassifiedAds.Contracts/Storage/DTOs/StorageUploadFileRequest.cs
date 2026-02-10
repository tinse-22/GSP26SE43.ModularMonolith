using ClassifiedAds.Contracts.Storage.Enums;
using System;
using System.IO;

namespace ClassifiedAds.Contracts.Storage.DTOs;

public class StorageUploadFileRequest
{
    public string FileName { get; set; }

    public string ContentType { get; set; }

    public long FileSize { get; set; }

    public FileCategory FileCategory { get; set; }

    public Guid? OwnerId { get; set; }

    public Stream Content { get; set; }
}
