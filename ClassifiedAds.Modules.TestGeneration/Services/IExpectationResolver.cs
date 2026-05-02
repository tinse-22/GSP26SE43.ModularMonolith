using ClassifiedAds.Modules.TestGeneration.Models;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Services;

public interface IExpectationResolver
{
    ResolvedExpectation Resolve(GeneratedScenarioContext context);

    N8nTestCaseExpectation ResolveToN8nExpectation(GeneratedScenarioContext context);
}
