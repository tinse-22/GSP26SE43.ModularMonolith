using ClassifiedAds.Application;
using ClassifiedAds.Contracts.Subscription.Enums;
using ClassifiedAds.Contracts.Subscription.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using ClassifiedAds.Modules.TestGeneration.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Commands;

public class GenerateBoundaryNegativeTestCasesCommand : ICommand
{
    public Guid TestSuiteId { get; set; }

    public Guid CurrentUserId { get; set; }

    public Guid SpecificationId { get; set; }

    public bool ForceRegenerate { get; set; }

    public bool IncludePathMutations { get; set; } = true;

    public bool IncludeBodyMutations { get; set; } = true;

    public bool IncludeLlmSuggestions { get; set; } = true;

    public GenerateBoundaryNegativeResultModel Result { get; set; }
}

public class GenerateBoundaryNegativeTestCasesCommandHandler : ICommandHandler<GenerateBoundaryNegativeTestCasesCommand>
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly IRepository<TestSuite, Guid> _suiteRepository;
    private readonly IRepository<TestCase, Guid> _testCaseRepository;
    private readonly IRepository<TestCaseDependency, Guid> _dependencyRepository;
    private readonly IRepository<TestCaseRequest, Guid> _requestRepository;
    private readonly IRepository<TestCaseExpectation, Guid> _expectationRepository;
    private readonly IRepository<TestCaseVariable, Guid> _variableRepository;
    private readonly IRepository<TestCaseChangeLog, Guid> _changeLogRepository;
    private readonly IRepository<TestSuiteVersion, Guid> _versionRepository;
    private readonly IApiTestOrderGateService _gateService;
    private readonly IBoundaryNegativeTestCaseGenerator _generator;
    private readonly ISubscriptionLimitGatewayService _subscriptionLimitService;
    private readonly ILogger<GenerateBoundaryNegativeTestCasesCommandHandler> _logger;

    public GenerateBoundaryNegativeTestCasesCommandHandler(
        IRepository<TestSuite, Guid> suiteRepository,
        IRepository<TestCase, Guid> testCaseRepository,
        IRepository<TestCaseDependency, Guid> dependencyRepository,
        IRepository<TestCaseRequest, Guid> requestRepository,
        IRepository<TestCaseExpectation, Guid> expectationRepository,
        IRepository<TestCaseVariable, Guid> variableRepository,
        IRepository<TestCaseChangeLog, Guid> changeLogRepository,
        IRepository<TestSuiteVersion, Guid> versionRepository,
        IApiTestOrderGateService gateService,
        IBoundaryNegativeTestCaseGenerator generator,
        ISubscriptionLimitGatewayService subscriptionLimitService,
        ILogger<GenerateBoundaryNegativeTestCasesCommandHandler> logger)
    {
        _suiteRepository = suiteRepository;
        _testCaseRepository = testCaseRepository;
        _dependencyRepository = dependencyRepository;
        _requestRepository = requestRepository;
        _expectationRepository = expectationRepository;
        _variableRepository = variableRepository;
        _changeLogRepository = changeLogRepository;
        _versionRepository = versionRepository;
        _gateService = gateService;
        _generator = generator;
        _subscriptionLimitService = subscriptionLimitService;
        _logger = logger;
    }

    public async Task HandleAsync(GenerateBoundaryNegativeTestCasesCommand command, CancellationToken cancellationToken = default)
    {
        // 1) Validate inputs
        if (command.TestSuiteId == Guid.Empty)
            throw new ValidationException("TestSuiteId là bắt buộc.");
        if (command.SpecificationId == Guid.Empty)
            throw new ValidationException("SpecificationId là bắt buộc.");
        if (!command.IncludePathMutations && !command.IncludeBodyMutations && !command.IncludeLlmSuggestions)
            throw new ValidationException("Ít nhất một nguồn tạo test case phải được bật (PathMutations, BodyMutations, hoặc LlmSuggestions).");

        // 2) Load test suite with ownership check
        var suite = await _suiteRepository.FirstOrDefaultAsync(
            _suiteRepository.GetQueryableSet()
                .Where(x => x.Id == command.TestSuiteId));

        if (suite == null)
            throw new NotFoundException($"Không tìm thấy test suite với mã '{command.TestSuiteId}'.");

        if (suite.CreatedById != command.CurrentUserId)
            throw new ValidationException("Bạn không có quyền thao tác test suite này.");

        if (suite.Status == TestSuiteStatus.Archived)
            throw new ValidationException("Không thể generate test cases cho test suite đã archived.");

        // 3) Gate check: require approved API order
        var approvedOrder = await _gateService.RequireApprovedOrderAsync(command.TestSuiteId, cancellationToken);

        // 4) Check if boundary/negative test cases already exist
        var existingCases = await _testCaseRepository.ToListAsync(
            _testCaseRepository.GetQueryableSet()
                .Where(x => x.TestSuiteId == command.TestSuiteId
                    && (x.TestType == TestType.Boundary || x.TestType == TestType.Negative)));

        if (existingCases.Count > 0 && !command.ForceRegenerate)
        {
            throw new ValidationException(
                $"Test suite đã có {existingCases.Count} boundary/negative test case(s). " +
                "Sử dụng ForceRegenerate=true để tạo lại.");
        }

        // 5) Check subscription limits
        var limitCheck = await _subscriptionLimitService.CheckLimitAsync(
            command.CurrentUserId,
            LimitType.MaxTestCasesPerSuite,
            approvedOrder.Count,
            cancellationToken);

        if (!limitCheck.IsAllowed)
        {
            throw new ValidationException(
                $"Đã vượt quá giới hạn test case cho gói subscription. {limitCheck.DenialReason}");
        }

        // Check LLM call limit if LLM suggestions enabled
        if (command.IncludeLlmSuggestions)
        {
            var llmLimitCheck = await _subscriptionLimitService.CheckLimitAsync(
                command.CurrentUserId,
                LimitType.MaxLlmCallsPerMonth,
                1,
                cancellationToken);

            if (!llmLimitCheck.IsAllowed)
            {
                throw new ValidationException(
                    $"Đã vượt quá giới hạn LLM calls cho gói subscription. {llmLimitCheck.DenialReason}");
            }
        }

        // 6) If regenerating, delete existing boundary/negative test cases
        if (existingCases.Count > 0 && command.ForceRegenerate)
        {
            _logger.LogInformation(
                "Force regenerating boundary/negative test cases. Deleting {Count} existing test cases. TestSuiteId={TestSuiteId}",
                existingCases.Count, command.TestSuiteId);

            foreach (var existing in existingCases)
            {
                _testCaseRepository.Delete(existing);
            }

            await _testCaseRepository.UnitOfWork.SaveChangesAsync(cancellationToken);
        }

        // 7) Generate test cases via orchestrator pipeline
        var options = new BoundaryNegativeOptions
        {
            IncludePathMutations = command.IncludePathMutations,
            IncludeBodyMutations = command.IncludeBodyMutations,
            IncludeLlmSuggestions = command.IncludeLlmSuggestions,
            UserId = command.CurrentUserId,
        };

        var generationResult = await _generator.GenerateAsync(
            suite, approvedOrder, command.SpecificationId, options, cancellationToken);

        if (generationResult.TestCases.Count == 0)
        {
            command.Result = new GenerateBoundaryNegativeResultModel
            {
                TestSuiteId = command.TestSuiteId,
                TotalGenerated = 0,
                PathMutationCount = 0,
                BodyMutationCount = 0,
                LlmSuggestionCount = 0,
                EndpointsCovered = 0,
                LlmModel = generationResult.LlmModel,
                LlmTokensUsed = generationResult.LlmTokensUsed,
                GeneratedAt = DateTimeOffset.UtcNow,
                TestCases = new List<GeneratedTestCaseSummary>(),
            };
            return;
        }

        // 8) Persist everything in a transaction
        var now = DateTimeOffset.UtcNow;

        await _testCaseRepository.UnitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            foreach (var testCase in generationResult.TestCases)
            {
                testCase.CreatedDateTime = now;

                await _testCaseRepository.AddAsync(testCase, ct);
                await _requestRepository.AddAsync(testCase.Request, ct);
                await _expectationRepository.AddAsync(testCase.Expectation, ct);

                foreach (var variable in testCase.Variables)
                {
                    await _variableRepository.AddAsync(variable, ct);
                }

                foreach (var dependency in testCase.Dependencies)
                {
                    dependency.CreatedDateTime = now;
                    await _dependencyRepository.AddAsync(dependency, ct);
                }

                // Create change log entry
                await _changeLogRepository.AddAsync(new TestCaseChangeLog
                {
                    Id = Guid.NewGuid(),
                    TestCaseId = testCase.Id,
                    ChangedById = command.CurrentUserId,
                    ChangeType = TestCaseChangeType.Created,
                    FieldName = null,
                    OldValue = null,
                    NewValue = JsonSerializer.Serialize(new
                    {
                        testCase.Name,
                        testCase.TestType,
                        testCase.EndpointId,
                        testCase.OrderIndex,
                        VariableCount = testCase.Variables.Count,
                    }, JsonOpts),
                    ChangeReason = $"Auto-generated {testCase.TestType} test case via boundary/negative generator " +
                        $"({generationResult.LlmModel ?? "rule-based"})",
                    VersionAfterChange = 1,
                    CreatedDateTime = now,
                }, ct);
            }

            // Create suite version snapshot
            await _versionRepository.AddAsync(new TestSuiteVersion
            {
                Id = Guid.NewGuid(),
                TestSuiteId = command.TestSuiteId,
                VersionNumber = suite.Version + 1,
                ChangedById = command.CurrentUserId,
                ChangeType = VersionChangeType.TestCasesModified,
                ChangeDescription =
                    $"Generated {generationResult.TestCases.Count} boundary/negative test case(s). " +
                    $"Path mutations: {generationResult.PathMutationCount}, " +
                    $"Body mutations: {generationResult.BodyMutationCount}, " +
                    $"LLM suggestions: {generationResult.LlmSuggestionCount}. " +
                    $"Endpoints covered: {generationResult.EndpointsCovered}.",
                TestCaseOrderSnapshot = JsonSerializer.Serialize(
                    generationResult.TestCases
                        .OrderBy(tc => tc.OrderIndex)
                        .Select(tc => new { tc.Id, tc.EndpointId, tc.Name, tc.OrderIndex, tc.TestType })
                        .ToList(),
                    JsonOpts),
                ApprovalStatusSnapshot = suite.ApprovalStatus,
                CreatedDateTime = now,
            }, ct);

            // Update suite version and status
            suite.Version += 1;
            suite.Status = TestSuiteStatus.Ready;
            suite.LastModifiedById = command.CurrentUserId;
            suite.UpdatedDateTime = now;
            suite.RowVersion = Guid.NewGuid().ToByteArray();
            await _suiteRepository.UpdateAsync(suite, ct);

            await _testCaseRepository.UnitOfWork.SaveChangesAsync(ct);
        }, cancellationToken: cancellationToken);

        // 9) Increment subscription usage
        await _subscriptionLimitService.IncrementUsageAsync(
            new Contracts.Subscription.DTOs.IncrementUsageRequest
            {
                UserId = command.CurrentUserId,
                LimitType = LimitType.MaxTestCasesPerSuite,
                IncrementValue = generationResult.TestCases.Count,
            },
            cancellationToken);

        if (command.IncludeLlmSuggestions && generationResult.LlmSuggestionCount > 0)
        {
            await _subscriptionLimitService.IncrementUsageAsync(
                new Contracts.Subscription.DTOs.IncrementUsageRequest
                {
                    UserId = command.CurrentUserId,
                    LimitType = LimitType.MaxLlmCallsPerMonth,
                    IncrementValue = 1,
                },
                cancellationToken);
        }

        // 10) Build result model
        command.Result = new GenerateBoundaryNegativeResultModel
        {
            TestSuiteId = command.TestSuiteId,
            TotalGenerated = generationResult.TestCases.Count,
            PathMutationCount = generationResult.PathMutationCount,
            BodyMutationCount = generationResult.BodyMutationCount,
            LlmSuggestionCount = generationResult.LlmSuggestionCount,
            EndpointsCovered = generationResult.EndpointsCovered,
            LlmModel = generationResult.LlmModel,
            LlmTokensUsed = generationResult.LlmTokensUsed,
            GeneratedAt = now,
            TestCases = generationResult.TestCases.Select(tc => new GeneratedTestCaseSummary
            {
                TestCaseId = tc.Id,
                EndpointId = tc.EndpointId,
                Name = tc.Name,
                HttpMethod = tc.Request?.HttpMethod.ToString(),
                Path = tc.Request?.Url,
                OrderIndex = tc.OrderIndex,
                VariableCount = tc.Variables?.Count ?? 0,
            }).ToList(),
        };

        _logger.LogInformation(
            "Boundary/negative test case generation persisted. TestSuiteId={TestSuiteId}, TotalGenerated={Total}, " +
            "PathMutations={PathMutations}, BodyMutations={BodyMutations}, LlmSuggestions={LlmSuggestions}, " +
            "EndpointsCovered={Covered}, ActorUserId={UserId}",
            command.TestSuiteId, generationResult.TestCases.Count,
            generationResult.PathMutationCount, generationResult.BodyMutationCount, generationResult.LlmSuggestionCount,
            generationResult.EndpointsCovered, command.CurrentUserId);
    }
}
