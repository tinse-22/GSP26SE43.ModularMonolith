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

public class GenerateHappyPathTestCasesCommand : ICommand
{
    public Guid TestSuiteId { get; set; }
    public Guid CurrentUserId { get; set; }
    public Guid SpecificationId { get; set; }
    public bool ForceRegenerate { get; set; }

    public GenerateHappyPathResultModel Result { get; set; }
}

public class GenerateHappyPathTestCasesCommandHandler : ICommandHandler<GenerateHappyPathTestCasesCommand>
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
    private readonly IHappyPathTestCaseGenerator _generator;
    private readonly ISubscriptionLimitGatewayService _subscriptionLimitService;
    private readonly ILogger<GenerateHappyPathTestCasesCommandHandler> _logger;

    public GenerateHappyPathTestCasesCommandHandler(
        IRepository<TestSuite, Guid> suiteRepository,
        IRepository<TestCase, Guid> testCaseRepository,
        IRepository<TestCaseDependency, Guid> dependencyRepository,
        IRepository<TestCaseRequest, Guid> requestRepository,
        IRepository<TestCaseExpectation, Guid> expectationRepository,
        IRepository<TestCaseVariable, Guid> variableRepository,
        IRepository<TestCaseChangeLog, Guid> changeLogRepository,
        IRepository<TestSuiteVersion, Guid> versionRepository,
        IApiTestOrderGateService gateService,
        IHappyPathTestCaseGenerator generator,
        ISubscriptionLimitGatewayService subscriptionLimitService,
        ILogger<GenerateHappyPathTestCasesCommandHandler> logger)
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

    public async Task HandleAsync(GenerateHappyPathTestCasesCommand command, CancellationToken cancellationToken = default)
    {
        // 1) Validate inputs
        if (command.TestSuiteId == Guid.Empty)
            throw new ValidationException("TestSuiteId là bắt buộc.");
        if (command.SpecificationId == Guid.Empty)
            throw new ValidationException("SpecificationId là bắt buộc.");

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

        // 4) Check if happy-path test cases already exist
        var existingHappyPath = await _testCaseRepository.ToListAsync(
            _testCaseRepository.GetQueryableSet()
                .Where(x => x.TestSuiteId == command.TestSuiteId
                    && x.TestType == TestType.HappyPath));

        if (existingHappyPath.Count > 0 && !command.ForceRegenerate)
        {
            throw new ValidationException(
                $"Test suite đã có {existingHappyPath.Count} happy-path test case(s). " +
                "Sử dụng ForceRegenerate=true để tạo lại.");
        }

        // 5) Check subscription limits (MaxTestCasesPerSuite)
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

        // 6) If regenerating, delete existing happy-path test cases
        if (existingHappyPath.Count > 0 && command.ForceRegenerate)
        {
            _logger.LogInformation(
                "Force regenerating happy-path test cases. Deleting {Count} existing test cases. TestSuiteId={TestSuiteId}",
                existingHappyPath.Count, command.TestSuiteId);

            foreach (var existing in existingHappyPath)
            {
                _testCaseRepository.Delete(existing);
            }

            await _testCaseRepository.UnitOfWork.SaveChangesAsync(cancellationToken);
        }

        // 7) Generate test cases via n8n/LLM pipeline
        var generationResult = await _generator.GenerateAsync(
            suite, approvedOrder, command.SpecificationId, cancellationToken);

        if (generationResult.TestCases.Count == 0)
        {
            command.Result = new GenerateHappyPathResultModel
            {
                TestSuiteId = command.TestSuiteId,
                TotalGenerated = 0,
                EndpointsCovered = 0,
                LlmModel = generationResult.LlmModel,
                TokensUsed = generationResult.TokensUsed,
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

                // Persist dependency chain links
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
                    ChangeReason = $"Auto-generated happy-path test case via LLM ({generationResult.LlmModel ?? "unknown"})",
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
                ChangeDescription = $"Generated {generationResult.TestCases.Count} happy-path test case(s) via LLM ({generationResult.LlmModel ?? "auto"}). " +
                    $"Endpoints covered: {generationResult.EndpointsCovered}.",
                TestCaseOrderSnapshot = JsonSerializer.Serialize(
                    generationResult.TestCases
                        .OrderBy(tc => tc.OrderIndex)
                        .Select(tc => new { tc.Id, tc.EndpointId, tc.Name, tc.OrderIndex })
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

        // 10) Build result model
        command.Result = new GenerateHappyPathResultModel
        {
            TestSuiteId = command.TestSuiteId,
            TotalGenerated = generationResult.TestCases.Count,
            EndpointsCovered = generationResult.EndpointsCovered,
            LlmModel = generationResult.LlmModel,
            TokensUsed = generationResult.TokensUsed,
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
            "Happy-path test case generation persisted. TestSuiteId={TestSuiteId}, TotalGenerated={Total}, EndpointsCovered={Covered}, ActorUserId={UserId}",
            command.TestSuiteId, generationResult.TestCases.Count, generationResult.EndpointsCovered, command.CurrentUserId);
    }
}
