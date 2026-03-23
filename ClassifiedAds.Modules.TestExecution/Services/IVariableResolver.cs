using ClassifiedAds.Contracts.TestGeneration.DTOs;
using ClassifiedAds.Modules.TestExecution.Models;
using System.Collections.Generic;

namespace ClassifiedAds.Modules.TestExecution.Services;

public interface IVariableResolver
{
    ResolvedTestCaseRequest Resolve(
        ExecutionTestCaseDto testCase,
        IReadOnlyDictionary<string, string> variables,
        ResolvedExecutionEnvironment environment);
}
