using ClassifiedAds.Modules.TestExecution.Models.Requests;
using FluentValidation;
using System;
using System.Linq;

namespace ClassifiedAds.Modules.TestExecution.Models.Validators;

public class StartTestRunRequestValidator : AbstractValidator<StartTestRunRequest>
{
    private const int MaxSelectedTestCaseCount = 1000;

    public StartTestRunRequestValidator()
    {
        RuleFor(x => x.EnvironmentId)
            .Must(id => !id.HasValue || id.Value != Guid.Empty)
            .WithMessage("EnvironmentId không hợp lệ.");

        RuleFor(x => x.SelectedTestCaseIds)
            .Must(ids => ids == null || ids.All(id => id != Guid.Empty))
            .WithMessage("Danh sách test case chứa ID không hợp lệ.");

        RuleFor(x => x.SelectedTestCaseIds)
            .Must(ids => ids == null || ids.Count <= MaxSelectedTestCaseCount)
            .WithMessage($"Không thể chạy quá {MaxSelectedTestCaseCount} test cases cùng lúc.");
    }
}
