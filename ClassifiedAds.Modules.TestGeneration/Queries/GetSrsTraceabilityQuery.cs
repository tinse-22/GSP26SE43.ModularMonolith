using ClassifiedAds.Application;
using ClassifiedAds.Contracts.TestExecution.Services;
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

    /// <summary>
    /// Optional: specify a particular test run to use for execution evidence.
    /// When null, the latest finished run for the suite is used.
    /// </summary>
    public Guid? TestRunId { get; set; }
}

public class GetSrsTraceabilityQueryHandler : IQueryHandler<GetSrsTraceabilityQuery, TraceabilityMatrix>
{
    private readonly IRepository<TestSuite, Guid> _suiteRepository;
    private readonly IRepository<SrsDocument, Guid> _srsDocumentRepository;
    private readonly IRepository<SrsRequirement, Guid> _requirementRepository;
    private readonly IRepository<TestCaseRequirementLink, Guid> _linkRepository;
    private readonly IRepository<TestCase, Guid> _testCaseRepository;
    private readonly ITestCaseExecutionEvidenceReadGatewayService _evidenceGateway;

    public GetSrsTraceabilityQueryHandler(
        IRepository<TestSuite, Guid> suiteRepository,
        IRepository<SrsDocument, Guid> srsDocumentRepository,
        IRepository<SrsRequirement, Guid> requirementRepository,
        IRepository<TestCaseRequirementLink, Guid> linkRepository,
        IRepository<TestCase, Guid> testCaseRepository,
        ITestCaseExecutionEvidenceReadGatewayService evidenceGateway)
    {
        _suiteRepository = suiteRepository;
        _srsDocumentRepository = srsDocumentRepository;
        _requirementRepository = requirementRepository;
        _linkRepository = linkRepository;
        _testCaseRepository = testCaseRepository;
        _evidenceGateway = evidenceGateway;
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

        // Resolve SrsDocumentId: prefer the FK on the suite, fall back to reverse lookup via SrsDocument.TestSuiteId.
        var srsDocumentId = suite.SrsDocumentId;
        if (srsDocumentId == null)
        {
            var linkedDoc = await _srsDocumentRepository.FirstOrDefaultAsync(
                _srsDocumentRepository.GetQueryableSet()
                    .Where(x => x.TestSuiteId == query.TestSuiteId && !x.IsDeleted));
            srsDocumentId = linkedDoc?.Id;
        }

        if (srsDocumentId == null)
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
                .Where(x => x.SrsDocumentId == srsDocumentId.Value)
                .OrderBy(x => x.DisplayOrder));

        if (!requirements.Any())
        {
            return new TraceabilityMatrix
            {
                TestSuiteId = query.TestSuiteId,
                SrsDocumentId = srsDocumentId,
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

        // Fetch execution evidence via contract gateway (never touches TestExecution DbContext directly).
        var evidence = await _evidenceGateway.GetLatestEvidenceByTestSuiteAsync(
            query.TestSuiteId, query.TestRunId, cancellationToken);

        var evidenceByTestCase = evidence
            .GroupBy(e => e.TestCaseId)
            .ToDictionary(g => g.Key, g => g.First()); // latest evidence per test case

        var evidenceRunId = evidence.FirstOrDefault()?.TestRunId;

        var rows = requirements.Select(req =>
        {
            var reqLinks = linksByReq.GetValueOrDefault(req.Id, new List<TestCaseRequirementLink>());
            var testCaseRefs = reqLinks
                .Where(l => testCaseDict.ContainsKey(l.TestCaseId))
                .Select(l =>
                {
                    evidenceByTestCase.TryGetValue(l.TestCaseId, out var ev);
                    return new TraceabilityTestCaseRef
                    {
                        TestCaseId = l.TestCaseId,
                        TestCaseName = testCaseDict[l.TestCaseId].Name,
                        TraceabilityScore = l.TraceabilityScore,
                        MappingRationale = l.MappingRationale,
                        LastRunStatus = ev?.Status,
                        LastRunId = ev?.TestRunId,
                        LastRunAt = ev?.CompletedAt,
                        HttpStatusCode = ev?.HttpStatusCode,
                        FailureCodes = ev?.FailureCodes?.ToList(),
                        FailureSummary = ev?.FailureSummary,
                        HasAdaptiveWarning = ev?.HasAdaptiveWarning ?? false,
                        WarningCodes = ev?.WarningCodes?.ToList(),
                    };
                })
                .ToList();

            var validationStatus = ComputeValidationStatus(testCaseRefs);
            var (passed, failed, skipped, unverified) = CountByStatus(testCaseRefs);

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
                ValidationStatus = validationStatus,
                ValidationSummary = BuildValidationSummary(validationStatus, passed, failed, skipped, unverified),
                PassedTestCaseCount = passed,
                FailedTestCaseCount = failed,
                SkippedTestCaseCount = skipped,
                UnverifiedTestCaseCount = unverified,
            };
        }).ToList();

        var covered = rows.Count(r => r.IsCovered);
        var total = rows.Count;

        var validatedCount = rows.Count(r => r.ValidationStatus == RequirementValidationStatus.Validated);
        var violatedCount = rows.Count(r => r.ValidationStatus == RequirementValidationStatus.Violated);
        var partialCount = rows.Count(r => r.ValidationStatus == RequirementValidationStatus.Partial);
        var unverifiedCount = rows.Count(r => r.ValidationStatus == RequirementValidationStatus.Unverified);
        var skippedOnlyCount = rows.Count(r => r.ValidationStatus == RequirementValidationStatus.SkippedOnly);
        var inconclusiveCount = rows.Count(r => r.ValidationStatus == RequirementValidationStatus.Inconclusive);

        var validationPercent = evidenceRunId.HasValue && covered > 0
            ? Math.Round((double)validatedCount / covered * 100, 1)
            : -1;

        return new TraceabilityMatrix
        {
            TestSuiteId = query.TestSuiteId,
            SrsDocumentId = srsDocumentId,
            Requirements = rows,
            TotalRequirements = total,
            CoveredRequirements = covered,
            UncoveredRequirements = total - covered,
            CoveragePercent = total > 0 ? Math.Round((double)covered / total * 100, 1) : 0,
            EvidenceRunId = evidenceRunId,
            ValidatedRequirements = validatedCount,
            ViolatedRequirements = violatedCount,
            PartialRequirements = partialCount,
            UnverifiedRequirements = unverifiedCount,
            SkippedOnlyRequirements = skippedOnlyCount,
            InconclusiveRequirements = inconclusiveCount,
            ValidationPercent = validationPercent,
        };
    }

    // ── Deterministic RequirementValidationStatus formula ───────────────────

    private static RequirementValidationStatus ComputeValidationStatus(
        IReadOnlyList<TraceabilityTestCaseRef> testCaseRefs)
    {
        if (testCaseRefs == null || testCaseRefs.Count == 0)
        {
            return RequirementValidationStatus.Uncovered;
        }

        var hasAnyResult = testCaseRefs.Any(r => r.LastRunStatus != null);
        if (!hasAnyResult)
        {
            return RequirementValidationStatus.Unverified;
        }

        var passedCount = testCaseRefs.Count(r =>
            string.Equals(r.LastRunStatus, "Passed", StringComparison.OrdinalIgnoreCase));
        var failedCount = testCaseRefs.Count(r =>
            string.Equals(r.LastRunStatus, "Failed", StringComparison.OrdinalIgnoreCase));
        var skippedCount = testCaseRefs.Count(r =>
            string.Equals(r.LastRunStatus, "Skipped", StringComparison.OrdinalIgnoreCase));
        var unverifiedCount = testCaseRefs.Count(r => r.LastRunStatus == null);

        // Any failure → VIOLATED
        if (failedCount > 0)
        {
            return RequirementValidationStatus.Violated;
        }

        // All skipped, no passes
        if (passedCount == 0 && skippedCount > 0 && unverifiedCount == 0)
        {
            return RequirementValidationStatus.SkippedOnly;
        }

        // All passed (none skipped or unverified)
        if (passedCount > 0 && skippedCount == 0 && unverifiedCount == 0)
        {
            return RequirementValidationStatus.Validated;
        }

        // Mix of passed and skipped/unverified
        if (passedCount > 0 && (skippedCount > 0 || unverifiedCount > 0))
        {
            return RequirementValidationStatus.Partial;
        }

        return RequirementValidationStatus.Inconclusive;
    }

    private static (int passed, int failed, int skipped, int unverified) CountByStatus(
        IReadOnlyList<TraceabilityTestCaseRef> testCaseRefs)
    {
        int passed = 0, failed = 0, skipped = 0, unverified = 0;
        foreach (var r in testCaseRefs ?? Array.Empty<TraceabilityTestCaseRef>())
        {
            if (r.LastRunStatus == null) { unverified++; continue; }
            if (string.Equals(r.LastRunStatus, "Passed", StringComparison.OrdinalIgnoreCase)) { passed++; continue; }
            if (string.Equals(r.LastRunStatus, "Failed", StringComparison.OrdinalIgnoreCase)) { failed++; continue; }
            if (string.Equals(r.LastRunStatus, "Skipped", StringComparison.OrdinalIgnoreCase)) { skipped++; continue; }
            unverified++;
        }

        return (passed, failed, skipped, unverified);
    }

    private static string BuildValidationSummary(
        RequirementValidationStatus status,
        int passed,
        int failed,
        int skipped,
        int unverified)
    {
        return status switch
        {
            RequirementValidationStatus.Uncovered => "No test cases linked.",
            RequirementValidationStatus.Unverified => "Linked but not yet executed.",
            RequirementValidationStatus.Validated => $"All {passed} test case(s) passed.",
            RequirementValidationStatus.Violated => $"{failed} test case(s) failed.",
            RequirementValidationStatus.Partial => $"{passed} passed, {skipped + unverified} skipped/unverified.",
            RequirementValidationStatus.SkippedOnly => $"All {skipped} test case(s) skipped.",
            RequirementValidationStatus.Inconclusive => "Insufficient execution data.",
            _ => string.Empty,
        };
    }
}

