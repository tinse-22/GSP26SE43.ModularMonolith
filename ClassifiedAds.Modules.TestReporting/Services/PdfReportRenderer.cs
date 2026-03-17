using ClassifiedAds.Contracts.Storage.Enums;
using ClassifiedAds.Modules.TestReporting.Entities;
using ClassifiedAds.Modules.TestReporting.Models;
using DinkToPdf;
using DinkToPdf.Contracts;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestReporting.Services;

public class PdfReportRenderer : IReportRenderer
{
    private readonly HtmlReportRenderer _htmlReportRenderer;
    private readonly IConverter _converter;

    public PdfReportRenderer(HtmlReportRenderer htmlReportRenderer, IConverter converter)
    {
        _htmlReportRenderer = htmlReportRenderer;
        _converter = converter;
    }

    public ReportFormat Format => ReportFormat.PDF;

    public async Task<RenderedReportFile> RenderAsync(TestRunReportDocumentModel document, CancellationToken ct = default)
    {
        var htmlFile = await _htmlReportRenderer.RenderAsync(document, ct);
        var htmlContent = Encoding.UTF8.GetString(htmlFile.Content);

        var pdfDocument = new HtmlToPdfDocument
        {
            GlobalSettings = new GlobalSettings
            {
                ColorMode = ColorMode.Color,
                Orientation = Orientation.Portrait,
                PaperSize = PaperKind.A4,
                DocumentTitle = document.FileBaseName,
            },
            Objects =
            {
                new ObjectSettings
                {
                    HtmlContent = htmlContent,
                    WebSettings = new WebSettings
                    {
                        DefaultEncoding = "utf-8",
                    },
                },
            },
        };

        return new RenderedReportFile
        {
            Content = _converter.Convert(pdfDocument),
            FileName = $"{document.FileBaseName}.pdf",
            ContentType = "application/pdf",
            FileCategory = FileCategory.Report,
        };
    }
}
