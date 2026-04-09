using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Commands;

public class AddTestCaseCommand : ICommand
{
    public Guid TestSuiteId { get; set; }
    public Guid CurrentUserId { get; set; }
    public Guid? EndpointId { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public TestType TestType { get; set; }
    public TestPriority Priority { get; set; }
    public bool IsEnabled { get; set; } = true;
    public List<string> Tags { get; set; }

    // Request
    public HttpMethod? RequestHttpMethod { get; set; }
    public string RequestUrl { get; set; }
    public string RequestHeaders { get; set; }
    public string RequestPathParams { get; set; }
    public string RequestQueryParams { get; set; }
    public BodyType RequestBodyType { get; set; }
    public string RequestBody { get; set; }
    public int RequestTimeout { get; set; } = 30000;

    // Expectation
    public string ExpectedStatus { get; set; }
    public string ResponseSchema { get; set; }
    public string HeaderChecks { get; set; }
    public string BodyContains { get; set; }
    public string BodyNotContains { get; set; }
    public string JsonPathChecks { get; set; }
    public int? MaxResponseTime { get; set; }

    // Variables
    public List<VariableInput> Variables { get; set; } = new List<VariableInput>();

    public TestCaseModel Result { get; set; }
}

public class VariableInput
{
    public string VariableName { get; set; }
    public ExtractFrom ExtractFrom { get; set; }
    public string JsonPath { get; set; }
    public string HeaderName { get; set; }
    public string Regex { get; set; }
    public string DefaultValue { get; set; }
}

public class AddTestCaseCommandHandler : ICommandHandler<AddTestCaseCommand>
{
    private static readonly JsonSerializerOptions JsonOpts = new ()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly IRepository<TestSuite, Guid> _suiteRepository;
    private readonly IRepository<TestCase, Guid> _testCaseRepository;
    private readonly IRepository<TestCaseRequest, Guid> _requestRepository;
    private readonly IRepository<TestCaseExpectation, Guid> _expectationRepository;
    private readonly IRepository<TestCaseVariable, Guid> _variableRepository;
    private readonly IRepository<TestCaseChangeLog, Guid> _changeLogRepository;
    private readonly IRepository<TestSuiteVersion, Guid> _versionRepository;

    public AddTestCaseCommandHandler(
        IRepository<TestSuite, Guid> suiteRepository,
        IRepository<TestCase, Guid> testCaseRepository,
        IRepository<TestCaseRequest, Guid> requestRepository,
        IRepository<TestCaseExpectation, Guid> expectationRepository,
        IRepository<TestCaseVariable, Guid> variableRepository,
        IRepository<TestCaseChangeLog, Guid> changeLogRepository,
        IRepository<TestSuiteVersion, Guid> versionRepository)
    {
        _suiteRepository = suiteRepository;
        _testCaseRepository = testCaseRepository;
        _requestRepository = requestRepository;
        _expectationRepository = expectationRepository;
        _variableRepository = variableRepository;
        _changeLogRepository = changeLogRepository;
        _versionRepository = versionRepository;
    }

    public async Task HandleAsync(AddTestCaseCommand command, CancellationToken cancellationToken = default)
    {
        // 1) Validate inputs
        if (command.TestSuiteId == Guid.Empty)
        {
            throw new ValidationException("TestSuiteId là bắt buộc.");
        }

        if (string.IsNullOrWhiteSpace(command.Name))
        {
            throw new ValidationException("Tên test case là bắt buộc.");
        }

        if (command.Name.Trim().Length > 200)
        {
            throw new ValidationException("Tên test case không được vượt quá 200 ký tự.");
        }

        // 2) Load and verify suite
        var suite = await _suiteRepository.FirstOrDefaultAsync(
            _suiteRepository.GetQueryableSet()
                .Where(x => x.Id == command.TestSuiteId));

        if (suite == null)
        {
            throw new NotFoundException($"Không tìm thấy test suite với mã '{command.TestSuiteId}'.");
        }

        if (suite.CreatedById != command.CurrentUserId)
        {
            throw new ValidationException("Bạn không có quyền thao tác test suite này.");
        }

        if (suite.Status == TestSuiteStatus.Archived)
        {
            throw new ValidationException("Không thể thêm test case cho test suite đã archived.");
        }

        // 3) Calculate next OrderIndex
        var existingCases = await _testCaseRepository.ToListAsync(
            _testCaseRepository.GetQueryableSet()
                .Where(x => x.TestSuiteId == command.TestSuiteId));

        var maxOrderIndex = existingCases.Count > 0
            ? existingCases.Max(x => x.OrderIndex)
            : -1;

        var now = DateTimeOffset.UtcNow;
        var testCaseId = Guid.NewGuid();

        // 4) Create TestCase entity
        var testCase = new TestCase
        {
            Id = testCaseId,
            TestSuiteId = command.TestSuiteId,
            EndpointId = command.EndpointId,
            Name = command.Name.Trim(),
            Description = command.Description?.Trim(),
            TestType = command.TestType,
            Priority = command.Priority,
            IsEnabled = command.IsEnabled,
            OrderIndex = maxOrderIndex + 1,
            Tags = command.Tags != null ? JsonSerializer.Serialize(command.Tags, JsonOpts) : null,
            LastModifiedById = command.CurrentUserId,
            Version = 1,
            CreatedDateTime = now,
        };

        // 5) Create Request entity
        var requestId = Guid.NewGuid();
        var request = new TestCaseRequest
        {
            Id = requestId,
            TestCaseId = testCaseId,
            HttpMethod = command.RequestHttpMethod ?? HttpMethod.GET,
            Url = command.RequestUrl,
            Headers = command.RequestHeaders,
            PathParams = command.RequestPathParams,
            QueryParams = command.RequestQueryParams,
            BodyType = command.RequestBodyType,
            Body = command.RequestBody,
            Timeout = command.RequestTimeout,
            CreatedDateTime = now,
        };

        // 6) Create Expectation entity
        var expectationId = Guid.NewGuid();
        var expectation = new TestCaseExpectation
        {
            Id = expectationId,
            TestCaseId = testCaseId,
            ExpectedStatus = command.ExpectedStatus,
            ResponseSchema = command.ResponseSchema,
            HeaderChecks = command.HeaderChecks,
            BodyContains = command.BodyContains,
            BodyNotContains = command.BodyNotContains,
            JsonPathChecks = command.JsonPathChecks,
            MaxResponseTime = command.MaxResponseTime,
            CreatedDateTime = now,
        };

        // 7) Create Variable entities
        var variables = new List<TestCaseVariable>();
        if (command.Variables != null)
        {
            foreach (var v in command.Variables)
            {
                variables.Add(new TestCaseVariable
                {
                    Id = Guid.NewGuid(),
                    TestCaseId = testCaseId,
                    VariableName = v.VariableName,
                    ExtractFrom = v.ExtractFrom,
                    JsonPath = v.JsonPath,
                    HeaderName = v.HeaderName,
                    Regex = v.Regex,
                    DefaultValue = v.DefaultValue,
                    CreatedDateTime = now,
                });
            }
        }

        // 8) Persist
        await _testCaseRepository.AddAsync(testCase, cancellationToken);
        await _requestRepository.AddAsync(request, cancellationToken);
        await _expectationRepository.AddAsync(expectation, cancellationToken);

        foreach (var variable in variables)
        {
            await _variableRepository.AddAsync(variable, cancellationToken);
        }

        // 9) Create ChangeLog
        await _changeLogRepository.AddAsync(new TestCaseChangeLog
        {
            Id = Guid.NewGuid(),
            TestCaseId = testCaseId,
            ChangedById = command.CurrentUserId,
            ChangeType = TestCaseChangeType.Created,
            FieldName = null,
            OldValue = null,
            NewValue = JsonSerializer.Serialize(new
            {
                testCase.Name,
                testCase.TestType,
                testCase.Priority,
                testCase.EndpointId,
                testCase.OrderIndex,
                VariableCount = variables.Count,
            }, JsonOpts),
            ChangeReason = "Tạo test case thủ công.",
            VersionAfterChange = 1,
            CreatedDateTime = now,
        }, cancellationToken);

        // 10) Update suite version
        suite.Version += 1;
        suite.LastModifiedById = command.CurrentUserId;
        suite.UpdatedDateTime = now;
        suite.RowVersion = Guid.NewGuid().ToByteArray();
        await _suiteRepository.UpdateAsync(suite, cancellationToken);

        await _versionRepository.AddAsync(new TestSuiteVersion
        {
            Id = Guid.NewGuid(),
            TestSuiteId = command.TestSuiteId,
            VersionNumber = suite.Version,
            ChangedById = command.CurrentUserId,
            ChangeType = VersionChangeType.TestCasesModified,
            ChangeDescription = $"Thêm test case '{testCase.Name}'.",
            CreatedDateTime = now,
        }, cancellationToken);

        await _testCaseRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

        // 11) Build result
        testCase.Request = request;
        testCase.Expectation = expectation;
        testCase.Variables = variables;
        command.Result = TestCaseModel.FromEntity(testCase);
    }
}
