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

public class UpdateTestCaseCommand : ICommand
{
    public Guid TestSuiteId { get; set; }
    public Guid TestCaseId { get; set; }
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
    public List<VariableInput> Variables { get; set; } = new ();

    public TestCaseModel Result { get; set; }
}

public class UpdateTestCaseCommandHandler : ICommandHandler<UpdateTestCaseCommand>
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

    public UpdateTestCaseCommandHandler(
        IRepository<TestSuite, Guid> suiteRepository,
        IRepository<TestCase, Guid> testCaseRepository,
        IRepository<TestCaseRequest, Guid> requestRepository,
        IRepository<TestCaseExpectation, Guid> expectationRepository,
        IRepository<TestCaseVariable, Guid> variableRepository,
        IRepository<TestCaseChangeLog, Guid> changeLogRepository)
    {
        _suiteRepository = suiteRepository;
        _testCaseRepository = testCaseRepository;
        _requestRepository = requestRepository;
        _expectationRepository = expectationRepository;
        _variableRepository = variableRepository;
        _changeLogRepository = changeLogRepository;
    }

    public async Task HandleAsync(UpdateTestCaseCommand command, CancellationToken cancellationToken = default)
    {
        // 1) Validate inputs
        if (command.TestSuiteId == Guid.Empty)
        {
            throw new ValidationException("TestSuiteId là bắt buộc.");
        }

        if (command.TestCaseId == Guid.Empty)
        {
            throw new ValidationException("TestCaseId là bắt buộc.");
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
            throw new ValidationException("Không thể cập nhật test case cho test suite đã archived.");
        }

        // 3) Load test case
        var testCase = await _testCaseRepository.FirstOrDefaultAsync(
            _testCaseRepository.GetQueryableSet()
                .Where(x => x.Id == command.TestCaseId && x.TestSuiteId == command.TestSuiteId));

        if (testCase == null)
        {
            throw new NotFoundException($"Không tìm thấy test case với mã '{command.TestCaseId}'.");
        }

        var now = DateTimeOffset.UtcNow;

        // 4) Update TestCase fields
        testCase.EndpointId = command.EndpointId;
        testCase.Name = command.Name.Trim();
        testCase.Description = command.Description?.Trim();
        testCase.TestType = command.TestType;
        testCase.Priority = command.Priority;
        testCase.IsEnabled = command.IsEnabled;
        testCase.Tags = command.Tags != null ? JsonSerializer.Serialize(command.Tags, JsonOpts) : null;
        testCase.LastModifiedById = command.CurrentUserId;
        testCase.Version += 1;
        testCase.UpdatedDateTime = now;

        await _testCaseRepository.UpdateAsync(testCase, cancellationToken);

        // 5) Update Request
        var existingRequest = await _requestRepository.FirstOrDefaultAsync(
            _requestRepository.GetQueryableSet()
                .Where(x => x.TestCaseId == command.TestCaseId));

        if (existingRequest != null)
        {
            existingRequest.HttpMethod = command.RequestHttpMethod ?? HttpMethod.GET;
            existingRequest.Url = command.RequestUrl;
            existingRequest.Headers = command.RequestHeaders;
            existingRequest.PathParams = command.RequestPathParams;
            existingRequest.QueryParams = command.RequestQueryParams;
            existingRequest.BodyType = command.RequestBodyType;
            existingRequest.Body = command.RequestBody;
            existingRequest.Timeout = command.RequestTimeout;
            existingRequest.UpdatedDateTime = now;
            await _requestRepository.UpdateAsync(existingRequest, cancellationToken);
        }
        else
        {
            existingRequest = new TestCaseRequest
            {
                Id = Guid.NewGuid(),
                TestCaseId = command.TestCaseId,
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
            await _requestRepository.AddAsync(existingRequest, cancellationToken);
        }

        // 6) Update Expectation
        var existingExpectation = await _expectationRepository.FirstOrDefaultAsync(
            _expectationRepository.GetQueryableSet()
                .Where(x => x.TestCaseId == command.TestCaseId));

        if (existingExpectation != null)
        {
            existingExpectation.ExpectedStatus = command.ExpectedStatus;
            existingExpectation.ResponseSchema = command.ResponseSchema;
            existingExpectation.HeaderChecks = command.HeaderChecks;
            existingExpectation.BodyContains = command.BodyContains;
            existingExpectation.BodyNotContains = command.BodyNotContains;
            existingExpectation.JsonPathChecks = command.JsonPathChecks;
            existingExpectation.MaxResponseTime = command.MaxResponseTime;
            existingExpectation.UpdatedDateTime = now;
            await _expectationRepository.UpdateAsync(existingExpectation, cancellationToken);
        }
        else
        {
            existingExpectation = new TestCaseExpectation
            {
                Id = Guid.NewGuid(),
                TestCaseId = command.TestCaseId,
                ExpectedStatus = command.ExpectedStatus,
                ResponseSchema = command.ResponseSchema,
                HeaderChecks = command.HeaderChecks,
                BodyContains = command.BodyContains,
                BodyNotContains = command.BodyNotContains,
                JsonPathChecks = command.JsonPathChecks,
                MaxResponseTime = command.MaxResponseTime,
                CreatedDateTime = now,
            };
            await _expectationRepository.AddAsync(existingExpectation, cancellationToken);
        }

        // 7) Replace Variables collection
        var existingVariables = await _variableRepository.ToListAsync(
            _variableRepository.GetQueryableSet()
                .Where(x => x.TestCaseId == command.TestCaseId));

        foreach (var ev in existingVariables)
        {
            _variableRepository.Delete(ev);
        }

        var newVariables = new List<TestCaseVariable>();
        if (command.Variables != null)
        {
            foreach (var v in command.Variables)
            {
                var variable = new TestCaseVariable
                {
                    Id = Guid.NewGuid(),
                    TestCaseId = command.TestCaseId,
                    VariableName = v.VariableName,
                    ExtractFrom = v.ExtractFrom,
                    JsonPath = v.JsonPath,
                    HeaderName = v.HeaderName,
                    Regex = v.Regex,
                    DefaultValue = v.DefaultValue,
                    CreatedDateTime = now,
                };
                newVariables.Add(variable);
                await _variableRepository.AddAsync(variable, cancellationToken);
            }
        }

        // 8) Create ChangeLog
        await _changeLogRepository.AddAsync(new TestCaseChangeLog
        {
            Id = Guid.NewGuid(),
            TestCaseId = command.TestCaseId,
            ChangedById = command.CurrentUserId,
            ChangeType = TestCaseChangeType.RequestChanged,
            FieldName = null,
            OldValue = null,
            NewValue = JsonSerializer.Serialize(new
            {
                testCase.Name,
                testCase.TestType,
                testCase.Priority,
                testCase.EndpointId,
                VariableCount = newVariables.Count,
            }, JsonOpts),
            ChangeReason = "Cập nhật test case thủ công.",
            VersionAfterChange = testCase.Version,
            CreatedDateTime = now,
        }, cancellationToken);

        // 9) Update suite version
        suite.Version += 1;
        suite.LastModifiedById = command.CurrentUserId;
        suite.UpdatedDateTime = now;
        suite.RowVersion = Guid.NewGuid().ToByteArray();
        await _suiteRepository.UpdateAsync(suite, cancellationToken);

        await _testCaseRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

        // 10) Build result
        testCase.Request = existingRequest;
        testCase.Expectation = existingExpectation;
        testCase.Variables = newVariables;
        command.Result = TestCaseModel.FromEntity(testCase);
    }
}
