using ClassifiedAds.Contracts.Storage.DTOs;

namespace ClassifiedAds.Modules.TestReporting.Models;

public class TestReportFileModel
{
    public byte[] Content { get; set; }

    public string FileName { get; set; }

    public string ContentType { get; set; }

    public static TestReportFileModel FromStorageDownload(StorageDownloadResult file)
    {
        if (file == null)
        {
            return null;
        }

        return new TestReportFileModel
        {
            Content = file.Content,
            FileName = file.FileName,
            ContentType = file.ContentType,
        };
    }
}
