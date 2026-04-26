using ClassifiedAds.Contracts.ApiDocumentation.Services;
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
    private readonly IProjectOwnershipGatewayService _projectOwnershipGatewayService;

    public TestExecutionReadGatewayService(
        IRepository<TestSuite, Guid> suiteRepository,
        IRepository<TestCase, Guid> testCaseRepository,
        IRepository<TestCaseRequest, Guid> requestRepository,
        IRepository<TestCaseExpectation, Guid> expectationRepository,
        IRepository<TestCaseVariable, Guid> variableRepository,
        IRepository<TestCaseDependency, Guid> dependencyRepository,
        IApiTestOrderGateService orderGateService,
        IProjectOwnershipGatewayService projectOwnershipGatewayService)
    {
        _suiteRepository = suiteRepository;
        _testCaseRepository = testCaseRepository;
        _requestRepository = requestRepository;
        _expectationRepository = expectationRepository;
        _variableRepository = variableRepository;
        _dependencyRepository = dependencyRepository;
        _orderGateService = orderGateService;
        _projectOwnershipGatewayService = projectOwnershipGatewayService;
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

        var projectName = await _projectOwnershipGatewayService.GetProjectNameAsync(suite.ProjectId, ct);

        return new TestSuiteAccessContextDto
        {
            TestSuiteId = suite.Id,
            ProjectId = suite.ProjectId,
            ProjectName = projectName,
            ApiSpecId = suite.ApiSpecId,
            GenerationType = suite.GenerationType.ToString(),
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
        // 1. Load suite access context
        var suiteContext = await GetSuiteAccessContextAsync(testSuiteId, ct);

        // Manual suites behave like Postman-style execution: no approved order required.
        var useApprovedOrder = !string.Equals(
            suiteContext.GenerationType,
            GenerationType.Manual.ToString(),
            StringComparison.OrdinalIgnoreCase);

        IReadOnlyList<ApiOrderItemModel> approvedOrder = Array.Empty<ApiOrderItemModel>();
        if (useApprovedOrder)
        {
            approvedOrder = await _orderGateService.RequireApprovedOrderAsync(testSuiteId, ct);
        }

        // 2. Batch load all enabled test cases for suite
        var enabledTestCases = await _testCaseRepository.ToListAsync(
            _testCaseRepository.GetQueryableSet()
                .Where(x => x.TestSuiteId == testSuiteId && x.IsEnabled));

        var enabledTestCaseMap = enabledTestCases.ToDictionary(x => x.Id);
        var enabledTestCaseIds = enabledTestCases.Select(x => x.Id).ToHashSet();

        // 3. Determine final test case set
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

        // 4. Auto-expand selection with transitive dependencies (avoids cross-batch dependency errors).
        // Load all suite-wide dependencies once, then BFS-expand the selection set.
        if (selectedTestCaseIds != null && selectedTestCaseIds.Count > 0)
        {
            var allSuiteDependencies = await _dependencyRepository.ToListAsync(
                _dependencyRepository.GetQueryableSet()
                    .Where(x => enabledTestCaseIds.Contains(x.TestCaseId)));

            var depLookup = allSuiteDependencies
                .GroupBy(x => x.TestCaseId)
                .ToDictionary(g => g.Key, g => g.Select(d => d.DependsOnTestCaseId).ToList());

            var queue = new Queue<Guid>(finalTestCaseIdSet);
            while (queue.Count > 0)
            {
                var tcId = queue.Dequeue();
                if (!depLookup.TryGetValue(tcId, out var deps)) continue;
                foreach (var depId in deps)
                {
                    if (!finalTestCaseIdSet.Contains(depId) && enabledTestCaseMap.ContainsKey(depId))
                    {
                        finalTestCaseIdSet.Add(depId);
                        queue.Enqueue(depId);
                    }
                }
            }
        }

        // 5. Batch load all related data by the (potentially expanded) finalTestCaseIdSet
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

        // 6. Build in-memory dictionaries for mapping
        var requestMap = requests.ToDictionary(x => x.TestCaseId);
        var expectationMap = expectations.ToDictionary(x => x.TestCaseId);
        var variableMap = variables.GroupBy(x => x.TestCaseId).ToDictionary(g => g.Key, g => g.ToList());
        var dependencyMap = dependencies.GroupBy(x => x.TestCaseId).ToDictionary(g => g.Key, g => g.Select(d => d.DependsOnTestCaseId).ToList());

        // 7. Build endpoint order lookup only when approved order is available
        var endpointOrderMap = useApprovedOrder
            ? approvedOrder.ToDictionary(x => x.EndpointId, x => x.OrderIndex)
            : new Dictionary<Guid, int>();
        var orderedEndpointIds = useApprovedOrder
            ? approvedOrder.OrderBy(x => x.OrderIndex).Select(x => x.EndpointId).ToList()
            : new List<Guid>();

        // 8. Build baseline order from endpoint/custom order, then enforce dependency topology.
        var baselineOrderedCases = finalTestCaseIdSet
            .Select(id => enabledTestCaseMap[id])
            .OrderBy(tc => tc.EndpointId.HasValue && endpointOrderMap.ContainsKey(tc.EndpointId.Value)
                ? endpointOrderMap[tc.EndpointId.Value]
                : int.MaxValue)
            .ThenBy(tc => tc.CustomOrderIndex ?? tc.OrderIndex)
            .ThenBy(tc => tc.Name)
            .ThenBy(tc => tc.Id)
            .ToList();

        var topologicallyOrderedCases = OrderCasesByDependencyTopology(baselineOrderedCases, dependencies);

        // 9. Map to DTOs with deterministic ordering
        var orderedTestCases = topologicallyOrderedCases
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

    private static List<TestCase> OrderCasesByDependencyTopology(
        IReadOnlyList<TestCase> baselineOrderedCases,
        IReadOnlyCollection<TestCaseDependency> dependencies)
    {
        if (baselineOrderedCases == null || baselineOrderedCases.Count == 0)
        {
            return new List<TestCase>();
        }

        var caseById = baselineOrderedCases.ToDictionary(x => x.Id);
        var baselineIndexById = baselineOrderedCases
            .Select((testCase, index) => new { testCase.Id, Index = index })
            .ToDictionary(x => x.Id, x => x.Index);

        var inDegree = baselineOrderedCases.ToDictionary(x => x.Id, _ => 0);
        var adjacency = baselineOrderedCases.ToDictionary(x => x.Id, _ => new List<Guid>());
        var seenEdges = new HashSet<(Guid DependentId, Guid DependencyId)>();

        foreach (var dependency in dependencies ?? Array.Empty<TestCaseDependency>())
        {
            if (!caseById.ContainsKey(dependency.TestCaseId)
                || !caseById.ContainsKey(dependency.DependsOnTestCaseId)
                || dependency.TestCaseId == dependency.DependsOnTestCaseId)
            {
                continue;
            }

            var edge = (DependentId: dependency.TestCaseId, DependencyId: dependency.DependsOnTestCaseId);
            if (!seenEdges.Add(edge))
            {
                continue;
            }

            adjacency[dependency.DependsOnTestCaseId].Add(dependency.TestCaseId);
            inDegree[dependency.TestCaseId] += 1;
        }

        var ready = new SortedSet<(int BaselineIndex, Guid TestCaseId)>(
            inDegree
                .Where(x => x.Value == 0)
                .Select(x => (baselineIndexById[x.Key], x.Key)));

        var orderedIds = new List<Guid>(baselineOrderedCases.Count);

        while (orderedIds.Count < baselineOrderedCases.Count)
        {
            if (ready.Count == 0)
            {
                // Cycle detected — break it by picking the unprocessed node
                // with the lowest baseline index (preserves original ordering
                // for cycle participants instead of blocking the entire run).
                var cycleBreaker = inDegree
                    .Where(x => x.Value > 0)
                    .OrderBy(x => baselineIndexById[x.Key])
                    .First();

                inDegree[cycleBreaker.Key] = 0;
                ready.Add((baselineIndexById[cycleBreaker.Key], cycleBreaker.Key));
            }

            var next = ready.Min;
            ready.Remove(next);

            orderedIds.Add(next.TestCaseId);

            foreach (var dependentId in adjacency[next.TestCaseId])
            {
                inDegree[dependentId] -= 1;

                if (inDegree[dependentId] == 0)
                {
                    ready.Add((baselineIndexById[dependentId], dependentId));
                }
            }
        }

        return orderedIds.Select(id => caseById[id]).ToList();
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
                Regex = v.Regex,
                DefaultValue = v.DefaultValue,
            }).ToList().AsReadOnly() ?? (IReadOnlyList<ExecutionVariableRuleDto>)Array.Empty<ExecutionVariableRuleDto>(),
        };
    }
}
