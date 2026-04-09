using ClassifiedAds.Contracts.TestGeneration.DTOs;
using ClassifiedAds.Contracts.TestGeneration.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Services;

public class TestExecutionReadGatewayService : ITestExecutionReadGatewayService
{
    private readonly IRepository<TestSuite, Guid> _suiteRepository;
    private readonly IRepository<TestCase, Guid> _testCaseRepository;
    private readonly IRepository<TestCaseRequest, Guid> _requestRepository;
    private readonly IRepository<TestCaseExpectation, Guid> _expectationRepository;
    private readonly IRepository<TestCaseVariable, Guid> _variableRepository;
    private readonly IRepository<TestCaseDependency, Guid> _dependencyRepository;
    private readonly IApiTestOrderGateService _orderGateService;

    public TestExecutionReadGatewayService(
        IRepository<TestSuite, Guid> suiteRepository,
        IRepository<TestCase, Guid> testCaseRepository,
        IRepository<TestCaseRequest, Guid> requestRepository,
        IRepository<TestCaseExpectation, Guid> expectationRepository,
        IRepository<TestCaseVariable, Guid> variableRepository,
        IRepository<TestCaseDependency, Guid> dependencyRepository,
        IApiTestOrderGateService orderGateService)
    {
        _suiteRepository = suiteRepository;
        _testCaseRepository = testCaseRepository;
        _requestRepository = requestRepository;
        _expectationRepository = expectationRepository;
        _variableRepository = variableRepository;
        _dependencyRepository = dependencyRepository;
        _orderGateService = orderGateService;
    }

    public async Task<TestSuiteAccessContextDto> GetSuiteAccessContextAsync(
        Guid testSuiteId,
        CancellationToken ct = default)
    {
        var suite = await _suiteRepository.FirstOrDefaultAsync(
            _suiteRepository.GetQueryableSet().Where(x => x.Id == testSuiteId));

        if (suite == null)
        {
            throw new NotFoundException($"Không tìm thấy test suite với mã '{testSuiteId}'.");
        }

        return new TestSuiteAccessContextDto
        {
            TestSuiteId = suite.Id,
            ProjectId = suite.ProjectId,
            ApiSpecId = suite.ApiSpecId,
            CreatedById = suite.CreatedById,
            Status = suite.Status.ToString(),
            Name = suite.Name,
        };
    }

    public async Task<TestSuiteExecutionContextDto> GetExecutionContextAsync(
        Guid testSuiteId,
        IReadOnlyCollection<Guid> selectedTestCaseIds,
        CancellationToken ct = default)
    {
        // 1. Enforce FE-05A gate and get approved endpoint order
        var approvedOrder = await _orderGateService.RequireApprovedOrderAsync(testSuiteId, ct);

        // 2. Load suite access context
        var suiteContext = await GetSuiteAccessContextAsync(testSuiteId, ct);

        // 3. Batch load all enabled test cases for suite
        var enabledTestCases = await _testCaseRepository.ToListAsync(
            _testCaseRepository.GetQueryableSet()
                .Where(x => x.TestSuiteId == testSuiteId && x.IsEnabled));

        var enabledTestCaseMap = enabledTestCases.ToDictionary(x => x.Id);
        var enabledTestCaseIds = enabledTestCases.Select(x => x.Id).ToHashSet();

        // 4. Determine final test case set
        IReadOnlyCollection<Guid> finalTestCaseIds;
        if (selectedTestCaseIds != null && selectedTestCaseIds.Count > 0)
        {
            ValidateSelectedTestCases(selectedTestCaseIds, enabledTestCaseMap);
            finalTestCaseIds = selectedTestCaseIds;
        }
        else
        {
            finalTestCaseIds = enabledTestCaseIds.ToList();
        }

        var finalTestCaseIdSet = finalTestCaseIds.ToHashSet();

        // 5. Batch load all related data by testCaseIds
        var requests = await _requestRepository.ToListAsync(
            _requestRepository.GetQueryableSet()
                .Where(x => finalTestCaseIdSet.Contains(x.TestCaseId)));

        var expectations = await _expectationRepository.ToListAsync(
            _expectationRepository.GetQueryableSet()
                .Where(x => finalTestCaseIdSet.Contains(x.TestCaseId)));

        var variables = await _variableRepository.ToListAsync(
            _variableRepository.GetQueryableSet()
                .Where(x => finalTestCaseIdSet.Contains(x.TestCaseId)));

        var dependencies = await _dependencyRepository.ToListAsync(
            _dependencyRepository.GetQueryableSet()
                .Where(x => finalTestCaseIdSet.Contains(x.TestCaseId)));

        // 6. Validate dependency closure for selected cases
        if (selectedTestCaseIds != null && selectedTestCaseIds.Count > 0)
        {
            ValidateDependencyClosure(dependencies, finalTestCaseIdSet, enabledTestCaseMap);
        }

        // 7. Build in-memory dictionaries for mapping
        var requestMap = requests.ToDictionary(x => x.TestCaseId);
        var expectationMap = expectations.ToDictionary(x => x.TestCaseId);
        var variableMap = variables.GroupBy(x => x.TestCaseId).ToDictionary(g => g.Key, g => g.ToList());
        var dependencyMap = dependencies.GroupBy(x => x.TestCaseId).ToDictionary(g => g.Key, g => g.Select(d => d.DependsOnTestCaseId).ToList());

        // 8. Build endpoint order lookup from approved order
        var endpointOrderMap = approvedOrder
            .ToDictionary(x => x.EndpointId, x => x.OrderIndex);
        var orderedEndpointIds = approvedOrder
            .OrderBy(x => x.OrderIndex)
            .Select(x => x.EndpointId)
            .ToList();

        // 9. Map to DTOs with deterministic ordering
        var orderedTestCases = finalTestCaseIds
            .Select(id => enabledTestCaseMap[id])
            .OrderBy(tc => tc.EndpointId.HasValue && endpointOrderMap.ContainsKey(tc.EndpointId.Value)
                ? endpointOrderMap[tc.EndpointId.Value]
                : int.MaxValue)
            .ThenBy(tc => tc.CustomOrderIndex ?? tc.OrderIndex)
            .ThenBy(tc => tc.Name)
            .ThenBy(tc => tc.Id)
            .Select((tc, index) => MapToExecutionTestCaseDto(tc, index, requestMap, expectationMap, variableMap, dependencyMap))
            .ToList();

        return new TestSuiteExecutionContextDto
        {
            Suite = suiteContext,
            OrderedTestCases = orderedTestCases,
            OrderedEndpointIds = orderedEndpointIds,
        };
    }

    public async Task<IReadOnlyList<Guid>> GetTestCaseIdsBySuiteAsync(
        Guid testSuiteId,
        CancellationToken ct = default)
    {
        return await _testCaseRepository.ToListAsync(
            _testCaseRepository.GetQueryableSet()
                .Where(x => x.TestSuiteId == testSuiteId && x.IsEnabled)
                .Select(x => x.Id));
    }

    private static void ValidateSelectedTestCases(
        IReadOnlyCollection<Guid> selectedIds,
        Dictionary<Guid, TestCase> enabledMap)
    {
        foreach (var id in selectedIds)
        {
            if (!enabledMap.TryGetValue(id, out var tc))
            {
                throw new ValidationException($"Test case '{id}' không tồn tại trong suite hoặc đã bị vô hiệu hóa.");
            }

            if (!tc.IsEnabled)
            {
                throw new ValidationException($"Test case '{tc.Name}' đã bị vô hiệu hóa, không thể chọn để chạy.");
            }
        }
    }

    private static void ValidateDependencyClosure(
        List<TestCaseDependency> dependencies,
        HashSet<Guid> selectedIdSet,
        Dictionary<Guid, TestCase> enabledMap)
    {
        var missingDeps = new List<string>();

        foreach (var dep in dependencies)
        {
            if (!selectedIdSet.Contains(dep.DependsOnTestCaseId))
            {
                var dependentName = enabledMap.TryGetValue(dep.TestCaseId, out var tc) ? tc.Name : dep.TestCaseId.ToString();
                var dependsOnName = enabledMap.TryGetValue(dep.DependsOnTestCaseId, out var depTc) ? depTc.Name : dep.DependsOnTestCaseId.ToString();
                missingDeps.Add($"'{dependentName}' phụ thuộc '{dependsOnName}'");
            }
        }

        if (missingDeps.Count > 0)
        {
            throw new ValidationException(
                $"Danh sách test case được chọn thiếu các dependency cần thiết: {string.Join("; ", missingDeps)}.");
        }
    }

    private static ExecutionTestCaseDto MapToExecutionTestCaseDto(
        TestCase tc,
        int orderIndex,
        Dictionary<Guid, TestCaseRequest> requestMap,
        Dictionary<Guid, TestCaseExpectation> expectationMap,
        Dictionary<Guid, List<TestCaseVariable>> variableMap,
        Dictionary<Guid, List<Guid>> dependencyMap)
    {
        requestMap.TryGetValue(tc.Id, out var request);
        expectationMap.TryGetValue(tc.Id, out var expectation);
        variableMap.TryGetValue(tc.Id, out var variables);
        dependencyMap.TryGetValue(tc.Id, out var depIds);

        return new ExecutionTestCaseDto
        {
            TestCaseId = tc.Id,
            EndpointId = tc.EndpointId,
            Name = tc.Name,
            Description = tc.Description,
            TestType = tc.TestType.ToString(),
            OrderIndex = orderIndex,
            DependencyIds = depIds?.AsReadOnly() ?? (IReadOnlyList<Guid>)Array.Empty<Guid>(),
            Request = request != null
                ? new ExecutionTestCaseRequestDto
                {
                    HttpMethod = request.HttpMethod.ToString(),
                    Url = request.Url,
                    Headers = request.Headers,
                    PathParams = request.PathParams,
                    QueryParams = request.QueryParams,
                    BodyType = request.BodyType.ToString(),
                    Body = request.Body,
                    Timeout = request.Timeout,
                }
                : null,
            Expectation = expectation != null
                ? new ExecutionTestCaseExpectationDto
                {
                    ExpectedStatus = expectation.ExpectedStatus,
                    ResponseSchema = expectation.ResponseSchema,
                    HeaderChecks = expectation.HeaderChecks,
                    BodyContains = expectation.BodyContains,
                    BodyNotContains = expectation.BodyNotContains,
                    JsonPathChecks = expectation.JsonPathChecks,
                    MaxResponseTime = expectation.MaxResponseTime,
                }
                : null,
            Variables = variables?.Select(v => new ExecutionVariableRuleDto
            {
                VariableName = v.VariableName,
                ExtractFrom = v.ExtractFrom.ToString(),
                JsonPath = v.JsonPath,
                HeaderName = v.HeaderName,
                DefaultValue = v.DefaultValue,
            }).ToList().AsReadOnly() ?? (IReadOnlyList<ExecutionVariableRuleDto>)Array.Empty<ExecutionVariableRuleDto>(),
        };
    }
}
