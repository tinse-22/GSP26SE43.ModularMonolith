using ClassifiedAds.Contracts.Storage.Enums;
using ClassifiedAds.Modules.TestReporting.Entities;
using ClassifiedAds.Modules.TestReporting.Models;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestReporting.Services;

public class JsonReportRenderer : IReportRenderer
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter(),
        },
    };

    public ReportFormat Format => ReportFormat.JSON;

    public Task<RenderedReportFile> RenderAsync(TestRunReportDocumentModel document, CancellationToken ct = default)
    {
        var payload = JsonSerializer.Serialize(document, JsonOptions);

        return Task.FromResult(new RenderedReportFile
        {
            Content = Encoding.UTF8.GetBytes(payload),
            FileName = $"{document.FileBaseName}.json",
            ContentType = "application/json; charset=utf-8",
            FileCategory = FileCategory.Export,
        });
    }
}
