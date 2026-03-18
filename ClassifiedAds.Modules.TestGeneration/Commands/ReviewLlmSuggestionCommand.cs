using ClassifiedAds.Application;
using ClassifiedAds.Contracts.Subscription.Enums;
using ClassifiedAds.Contracts.Subscription.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using ClassifiedAds.Modules.TestGeneration.Models.Requests;
using ClassifiedAds.Modules.TestGeneration.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Commands;

public class ReviewLlmSuggestionCommand : ICommand
{
    public Guid TestSuiteId { get; set; }
    public Guid SuggestionId { get; set; }
    public Guid CurrentUserId { get; set; }
    public string ReviewAction { get; set; }
    public string RowVersion { get; set; }
    public string ReviewNotes { get; set; }
    public EditableLlmSuggestionInput ModifiedContent { get; set; }
    public LlmSuggestionModel Result { get; set; }
}

public class ReviewLlmSuggestionCommandHandler : ICommandHandler<ReviewLlmSuggestionCommand>
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly IRepository<TestSuite, Guid> _suiteRepository;
    private readonly IRepository<LlmSuggestion, Guid> _suggestionRepository;
    private readonly IRepository<TestCase, Guid> _testCaseRepository;
    private readonly IRepository<TestCaseRequest, Guid> _requestRepository;
    private readonly IRepository<TestCaseExpectation, Guid> _expectationRepository;
    private readonly IRepository<TestCaseVariable, Guid> _variableRepository;
    private readonly IRepository<TestCaseChangeLog, Guid> _changeLogRepository;
    private readonly IRepository<TestSuiteVersion, Guid> _versionRepository;
    private readonly ILlmSuggestionMaterializer _materializer;
    private readonly IApiTestOrderGateService _gateService;
    private readonly ISubscriptionLimitGatewayService _subscriptionLimitService;
    private readonly ILogger<ReviewLlmSuggestionCommandHandler> _logger;

    public ReviewLlmSuggestionCommandHandler(
        IRepository<TestSuite, Guid> suiteRepository,
        IRepository<LlmSuggestion, Guid> suggestionRepository,
        IRepository<TestCase, Guid> testCaseRepository,
        IRepository<TestCaseRequest, Guid> requestRepository,
        IRepository<TestCaseExpectation, Guid> expectationRepository,
        IRepository<TestCaseVariable, Guid> variableRepository,
        IRepository<TestCaseChangeLog, Guid> changeLogRepository,
        IRepository<TestSuiteVersion, Guid> versionRepository,
        ILlmSuggestionMaterializer materializer,
        IApiTestOrderGateService gateService,
        ISubscriptionLimitGatewayService subscriptionLimitService,
        ILogger<ReviewLlmSuggestionCommandHandler> logger)
    {
        _suiteRepository = suiteRepository;
        _suggestionRepository = suggestionRepository;
        _testCaseRepository = testCaseRepository;
        _requestRepository = requestRepository;
        _expectationRepository = expectationRepository;
        _variableRepository = variableRepository;
        _changeLogRepository = changeLogRepository;
        _versionRepository = versionRepository;
        _materializer = materializer;
        _gateService = gateService;
        _subscriptionLimitService = subscriptionLimitService;
        _logger = logger;
    }

    public async Task HandleAsync(
        ReviewLlmSuggestionCommand command,
        CancellationToken cancellationToken = default)
    {
        // 1) Validate inputs
        ValidationException.Requires(command.SuggestionId != Guid.Empty, "SuggestionId là bắt buộc.");
        ValidationException.Requires(command.TestSuiteId != Guid.Empty, "TestSuiteId là bắt buộc.");
        ValidationException.Requires(
            !string.IsNullOrWhiteSpace(command.RowVersion),
            "RowVersion là bắt buộc cho concurrency control.");

        var validActions = new[] { "Approve", "Reject", "Modify" };
        ValidationException.Requires(
            validActions.Contains(command.ReviewAction, StringComparer.OrdinalIgnoreCase),
            "ReviewAction phải là 'Approve', 'Reject', hoặc 'Modify'.");

        if (string.Equals(command.ReviewAction, "Reject", StringComparison.OrdinalIgnoreCase))
        {
            ValidationException.Requires(
                !string.IsNullOrWhiteSpace(command.ReviewNotes),
                "ReviewNotes là bắt buộc khi reject suggestion.");
        }

        if (string.Equals(command.ReviewAction, "Modify", StringComparison.OrdinalIgnoreCase))
        {
            ValidationException.Requires(
                command.ModifiedContent != null,
                "ModifiedContent là bắt buộc khi Modify suggestion.");
            ValidationException.Requires(
                !string.IsNullOrWhiteSpace(command.ModifiedContent?.Name),
                "ModifiedContent.Name là bắt buộc.");
        }

        // 2) Load suite + ownership check
        var suite = await _suiteRepository.FirstOrDefaultAsync(
            _suiteRepository.GetQueryableSet().Where(x => x.Id == command.TestSuiteId));

        if (suite == null)
            throw new NotFoundException($"Không tìm thấy test suite với mã '{command.TestSuiteId}'.");

        ValidationException.Requires(
            suite.CreatedById == command.CurrentUserId,
            "Bạn không phải chủ sở hữu của test suite này.");

        // 3) Load suggestion + validate status
        var suggestion = await _suggestionRepository.FirstOrDefaultAsync(
            _suggestionRepository.GetQueryableSet()
                .Where(x => x.Id == command.SuggestionId && x.TestSuiteId == command.TestSuiteId));

        if (suggestion == null)
            throw new NotFoundException($"Không tìm thấy suggestion với mã '{command.SuggestionId}'.");

        ValidationException.Requires(
            suggestion.ReviewStatus == ReviewStatus.Pending,
            $"Không thể review suggestion ở trạng thái '{suggestion.ReviewStatus}'. Chỉ có thể review suggestion đang Pending.");

        // 4) Apply RowVersion for concurrency
        var parsedRowVersion = Convert.FromBase64String(command.RowVersion);
        _suggestionRepository.SetRowVersion(suggestion, parsedRowVersion);

        var now = DateTimeOffset.UtcNow;

        // 5) Dispatch based on action
        if (string.Equals(command.ReviewAction, "Reject", StringComparison.OrdinalIgnoreCase))
        {
            await HandleRejectAsync(command, suggestion, now, cancellationToken);
        }
        else
        {
            bool isModify = string.Equals(command.ReviewAction, "Modify", StringComparison.OrdinalIgnoreCase);
            await HandleApproveAsync(command, suite, suggestion, now, isModify, cancellationToken);
        }

        command.Result = LlmSuggestionModel.FromEntity(suggestion);
    }

    private async Task HandleRejectAsync(
        ReviewLlmSuggestionCommand command,
        LlmSuggestion suggestion,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        suggestion.ReviewStatus = ReviewStatus.Rejected;
        suggestion.ReviewedById = command.CurrentUserId;
        suggestion.ReviewedAt = now;
        suggestion.ReviewNotes = command.ReviewNotes;
        suggestion.UpdatedDateTime = now;
        suggestion.RowVersion = Guid.NewGuid().ToByteArray();

        try
        {
            await _suggestionRepository.UpdateAsync(suggestion, cancellationToken);
            await _suggestionRepository.UnitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (_suggestionRepository.IsDbUpdateConcurrencyException(ex))
        {
            throw new ConflictException("CONCURRENCY_CONFLICT",
                "Suggestion đã được thay đổi bởi thao tác khác. Vui lòng tải lại và thử lại.");
        }

        _logger.LogInformation(
            "LLM suggestion rejected. SuggestionId={SuggestionId}, TestSuiteId={TestSuiteId}, ActorUserId={UserId}",
            suggestion.Id, suggestion.TestSuiteId, command.CurrentUserId);
    }

    private async Task HandleApproveAsync(
        ReviewLlmSuggestionCommand command,
        TestSuite suite,
        LlmSuggestion suggestion,
        DateTimeOffset now,
        bool isModify,
        CancellationToken cancellationToken)
    {
        // Check subscription limit for test cases
        var tcLimitCheck = await _subscriptionLimitService.CheckLimitAsync(
            command.CurrentUserId, LimitType.MaxTestCasesPerSuite, 1, cancellationToken);

        if (!tcLimitCheck.IsAllowed)
        {
            throw new ValidationException(
                $"Đã vượt quá giới hạn test case cho gói subscription. {tcLimitCheck.DenialReason}");
        }

        // Get approved order for materializer
        var approvedOrder = await _gateService.RequireApprovedOrderAsync(command.TestSuiteId, cancellationToken);
        var orderItemMap = approvedOrder.ToDictionary(e => e.EndpointId);
        ApiOrderItemModel orderItem = null;
        if (suggestion.EndpointId.HasValue)
        {
            orderItemMap.TryGetValue(suggestion.EndpointId.Value, out orderItem);
        }

        // Determine next order index
        var existingCaseCount = await _testCaseRepository.ToListAsync(
            _testCaseRepository.GetQueryableSet()
                .Where(x => x.TestSuiteId == command.TestSuiteId));
        int nextOrderIndex = existingCaseCount.Count;

        // Materialize test case
        TestCase testCase;
        if (isModify)
        {
            suggestion.ModifiedContent = JsonSerializer.Serialize(command.ModifiedContent, JsonOpts);
            testCase = _materializer.MaterializeFromModifiedContent(suggestion, command.ModifiedContent, orderItem, nextOrderIndex);
        }
        else
        {
            testCase = _materializer.MaterializeFromSuggestion(suggestion, orderItem, nextOrderIndex);
        }

        // Update suggestion review fields
        suggestion.ReviewStatus = isModify ? ReviewStatus.ModifiedAndApproved : ReviewStatus.Approved;
        suggestion.ReviewedById = command.CurrentUserId;
        suggestion.ReviewedAt = now;
        suggestion.ReviewNotes = command.ReviewNotes;
        suggestion.AppliedTestCaseId = testCase.Id;
        suggestion.UpdatedDateTime = now;
        suggestion.RowVersion = Guid.NewGuid().ToByteArray();

        // Execute in transaction
        try
        {
            await _testCaseRepository.UnitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                // Persist test case graph
                testCase.CreatedDateTime = now;
                await _testCaseRepository.AddAsync(testCase, ct);
                await _requestRepository.AddAsync(testCase.Request, ct);
                await _expectationRepository.AddAsync(testCase.Expectation, ct);

                foreach (var variable in testCase.Variables)
                {
                    await _variableRepository.AddAsync(variable, ct);
                }

                // Create change log
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
                        SuggestionId = suggestion.Id,
                    }, JsonOpts),
                    ChangeReason = isModify
                        ? $"Modified and approved from LLM suggestion preview (SuggestionId={suggestion.Id})"
                        : $"Approved from LLM suggestion preview (SuggestionId={suggestion.Id})",
                    VersionAfterChange = 1,
                    CreatedDateTime = now,
                }, ct);

                // Create suite version
                await _versionRepository.AddAsync(new TestSuiteVersion
                {
                    Id = Guid.NewGuid(),
                    TestSuiteId = command.TestSuiteId,
                    VersionNumber = suite.Version + 1,
                    ChangedById = command.CurrentUserId,
                    ChangeType = VersionChangeType.TestCasesModified,
                    ChangeDescription =
                        $"{(isModify ? "Modified and approved" : "Approved")} LLM suggestion '{testCase.Name}' " +
                        $"as test case (SuggestionId={suggestion.Id}).",
                    TestCaseOrderSnapshot = JsonSerializer.Serialize(
                        new[] { new { testCase.Id, testCase.EndpointId, testCase.Name, testCase.OrderIndex, testCase.TestType } },
                        JsonOpts),
                    ApprovalStatusSnapshot = suite.ApprovalStatus,
                    CreatedDateTime = now,
                }, ct);

                // Update suite version
                suite.Version += 1;
                suite.LastModifiedById = command.CurrentUserId;
                suite.UpdatedDateTime = now;
                suite.RowVersion = Guid.NewGuid().ToByteArray();
                await _suiteRepository.UpdateAsync(suite, ct);

                // Update suggestion
                await _suggestionRepository.UpdateAsync(suggestion, ct);

                await _testCaseRepository.UnitOfWork.SaveChangesAsync(ct);
            }, cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (_suggestionRepository.IsDbUpdateConcurrencyException(ex))
        {
            throw new ConflictException("CONCURRENCY_CONFLICT",
                "Suggestion đã được thay đổi bởi thao tác khác. Vui lòng tải lại và thử lại.");
        }

        // Increment subscription usage (post-commit)
        await _subscriptionLimitService.IncrementUsageAsync(
            new Contracts.Subscription.DTOs.IncrementUsageRequest
            {
                UserId = command.CurrentUserId,
                LimitType = LimitType.MaxTestCasesPerSuite,
                IncrementValue = 1,
            },
            cancellationToken);

        _logger.LogInformation(
            "LLM suggestion {Action}. SuggestionId={SuggestionId}, TestCaseId={TestCaseId}, " +
            "TestSuiteId={TestSuiteId}, ActorUserId={UserId}",
            isModify ? "modified and approved" : "approved",
            suggestion.Id, testCase.Id, command.TestSuiteId, command.CurrentUserId);
    }
}
