using ClassifiedAds.Modules.TestExecution.Models;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestExecution.Services;

public interface IHttpTestExecutor
{
    Task<HttpTestResponse> ExecuteAsync(ResolvedTestCaseRequest request, CancellationToken ct = default);
}
