using ClassifiedAds.Modules.TestReporting.Entities;
using ClassifiedAds.Modules.TestReporting.Models;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestReporting.Services;

public interface IReportRenderer
{
    ReportFormat Format { get; }

    Task<RenderedReportFile> RenderAsync(TestRunReportDocumentModel document, CancellationToken ct = default);
}
