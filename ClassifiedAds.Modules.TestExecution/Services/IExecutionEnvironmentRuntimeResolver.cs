using ClassifiedAds.Modules.TestExecution.Entities;
using ClassifiedAds.Modules.TestExecution.Models;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestExecution.Services;

public interface IExecutionEnvironmentRuntimeResolver
{
    Task<ResolvedExecutionEnvironment> ResolveAsync(ExecutionEnvironment environment, CancellationToken ct = default);
}
