using ClassifiedAds.Contracts.TestGeneration.DTOs;
using ClassifiedAds.Modules.TestExecution.Models;
using System.Collections.Generic;

namespace ClassifiedAds.Modules.TestExecution.Services;

public interface IVariableExtractor
{
    IReadOnlyDictionary<string, string> Extract(
        HttpTestResponse response,
        IReadOnlyList<ExecutionVariableRuleDto> variables);
}
