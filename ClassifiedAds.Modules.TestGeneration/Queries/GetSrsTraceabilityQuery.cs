using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Queries;

public class GetSrsTraceabilityQuery : IQuery<TraceabilityMatrix>
{
    public Guid ProjectId { get; set; }

    public Guid TestSuiteId { get; set; }

    public Guid CurrentUserId { get; set; }
}

public class GetSrsTraceabilityQueryHandler : IQueryHandler<GetSrsTraceabilityQuery, TraceabilityMatrix>
{
    private readonly IRepository<TestSuite, Guid> _suiteRepository;
    private readonly IRepository<SrsRequirement, Guid> _requirementRepository;
    private readonly IRepository<TestCaseRequirementLink, Guid> _linkRepository;
    private readonly IRepository<TestCase, Guid> _testCaseRepository;

    public GetSrsTraceabilityQueryHandler(
        IRepository<TestSuite, Guid> suiteRepository,
        IRepository<SrsRequirement, Guid> requirementRepository,
        IRepository<TestCaseRequirementLink, Guid> linkRepository,
        IRepository<TestCase, Guid> testCaseRepository)
    {
        _suiteRepository = suiteRepository;
        _requirementRepository = requirementRepository;
        _linkRepository = linkRepository;
        _testCaseRepository = testCaseRepository;
    }

    public async Task<TraceabilityMatrix> HandleAsync(GetSrsTraceabilityQuery query, CancellationToken cancellationToken = default)
    {
        var suite = await _suiteRepository.FirstOrDefaultAsync(
            _suiteRepository.GetQueryableSet()
                .Where(x => x.Id == query.TestSuiteId && x.ProjectId == query.ProjectId));

        if (suite == null)
        {
            throw new NotFoundException($"TestSuite {query.TestSuiteId} khong tim thay.");
        }

        if (suite.SrsDocumentId == null)
        {
            return new TraceabilityMatrix
            {
                TestSuiteId = query.TestSuiteId,
                SrsDocumentId = null,
                Requirements = new List<TraceabilityRequirementRow>(),
            };
        }

        var requirements = await _requirementRepository.ToListAsync(
            _requirementRepository.GetQueryableSet()
                .Where(x => x.SrsDocumentId == suite.SrsDocumentId.Value)
                .OrderBy(x => x.DisplayOrder));

        if (!requirements.Any())
        {
            return new TraceabilityMatrix
            {
                TestSuiteId = query.TestSuiteId,
                SrsDocumentId = suite.SrsDocumentId,
                Requirements = new List<TraceabilityRequirementRow>(),
            };
        }

        var requirementIds = requirements.Select(r => r.Id).ToList();

        // Get all test cases in this suite
        var testCasesInSuite = await _testCaseRepository.ToListAsync(
            _testCaseRepository.GetQueryableSet()
                .Where(x => x.TestSuiteId == query.TestSuiteId));

        var testCaseIds = testCasesInSuite.Select(tc => tc.Id).ToList();

        // Get links for requirements in this suite
        var links = await _linkRepository.ToListAsync(
            _linkRepository.GetQueryableSet()
                .Where(x => requirementIds.Contains(x.SrsRequirementId)
                    && testCaseIds.Contains(x.TestCaseId)));

        var testCaseDict = testCasesInSuite.ToDictionary(tc => tc.Id);
        var linksByReq = links.GroupBy(l => l.SrsRequirementId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var rows = requirements.Select(req =>
        {
            var reqLinks = linksByReq.GetValueOrDefault(req.Id, new List<TestCaseRequirementLink>());
            var testCaseRefs = reqLinks
                .Where(l => testCaseDict.ContainsKey(l.TestCaseId))
                .Select(l => new TraceabilityTestCaseRef
                {
                    TestCaseId = l.TestCaseId,
                    TestCaseName = testCaseDict[l.TestCaseId].Name,
                    TraceabilityScore = l.TraceabilityScore,
                    MappingRationale = l.MappingRationale,
                })
                .ToList();

            return new TraceabilityRequirementRow
            {
                RequirementId = req.Id,
                RequirementCode = req.RequirementCode,
                Title = req.Title,
                RequirementType = req.RequirementType,
                ConfidenceScore = req.ConfidenceScore,
                IsReviewed = req.IsReviewed,
                IsCovered = testCaseRefs.Any(),
                TestCases = testCaseRefs,
            };
        }).ToList();

        var covered = rows.Count(r => r.IsCovered);
        var total = rows.Count;

        return new TraceabilityMatrix
        {
            TestSuiteId = query.TestSuiteId,
            SrsDocumentId = suite.SrsDocumentId,
            Requirements = rows,
            TotalRequirements = total,
            CoveredRequirements = covered,
            UncoveredRequirements = total - covered,
            CoveragePercent = total > 0 ? Math.Round((double)covered / total * 100, 1) : 0,
        };
    }
}
