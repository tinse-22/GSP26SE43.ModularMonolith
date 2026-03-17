using ClassifiedAds.Contracts.Storage.Enums;

namespace ClassifiedAds.Modules.TestReporting.Models;

public class RenderedReportFile
{
    public byte[] Content { get; set; }

    public string FileName { get; set; }

    public string ContentType { get; set; }

    public FileCategory FileCategory { get; set; }
}
