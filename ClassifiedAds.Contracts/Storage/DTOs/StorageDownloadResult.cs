namespace ClassifiedAds.Contracts.Storage.DTOs;

public class StorageDownloadResult
{
    public byte[] Content { get; set; }

    public string FileName { get; set; }

    public string ContentType { get; set; }
}
