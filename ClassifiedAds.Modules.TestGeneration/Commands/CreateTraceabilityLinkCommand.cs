using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Commands;

public class CreateTraceabilityLinkCommand : ICommand
{
    public Guid ProjectId { get; set; }

    public Guid TestSuiteId { get; set; }

    public Guid TestCaseId { get; set; }

    public Guid SrsRequirementId { get; set; }

    public Guid CurrentUserId { get; set; }

    public TraceabilityLinkModel Result { get; set; }
}

public class TraceabilityLinkModel
{
    public Guid Id { get; set; }

    public Guid TestCaseId { get; set; }

    public string TestCaseName { get; set; }

    public Guid SrsRequirementId { get; set; }

    public string RequirementCode { get; set; }

    public float? TraceabilityScore { get; set; }

    public string MappingRationale { get; set; }
}

public class CreateTraceabilityLinkCommandHandler : ICommandHandler<CreateTraceabilityLinkCommand>
{
    private readonly IRepository<TestSuite, Guid> _suiteRepository;
    private readonly IRepository<TestCase, Guid> _testCaseRepository;
    private readonly IRepository<SrsRequirement, Guid> _requirementRepository;
    private readonly IRepository<SrsDocument, Guid> _srsDocumentRepository;
    private readonly IRepository<TestCaseRequirementLink, Guid> _linkRepository;

    public CreateTraceabilityLinkCommandHandler(
        IRepository<TestSuite, Guid> suiteRepository,
        IRepository<TestCase, Guid> testCaseRepository,
        IRepository<SrsRequirement, Guid> requirementRepository,
        IRepository<SrsDocument, Guid> srsDocumentRepository,
        IRepository<TestCaseRequirementLink, Guid> linkRepository)
    {
        _suiteRepository = suiteRepository;
        _testCaseRepository = testCaseRepository;
        _requirementRepository = requirementRepository;
        _srsDocumentRepository = srsDocumentRepository;
        _linkRepository = linkRepository;
    }

    public async Task HandleAsync(CreateTraceabilityLinkCommand command, CancellationToken cancellationToken = default)
    {
        var suite = await _suiteRepository.FirstOrDefaultAsync(
            _suiteRepository.GetQueryableSet()
                .Where(x => x.Id == command.TestSuiteId && x.ProjectId == command.ProjectId));

        if (suite == null)
        {
            throw new NotFoundException($"TestSuite {command.TestSuiteId} khong tim thay.");
        }

        // Validate test case belongs to this suite
        var testCase = await _testCaseRepository.FirstOrDefaultAsync(
            _testCaseRepository.GetQueryableSet()
                .Where(x => x.Id == command.TestCaseId && x.TestSuiteId == command.TestSuiteId));

        if (testCase == null)
        {
            throw new NotFoundException($"TestCase {command.TestCaseId} khong thuoc suite nay.");
        }

        // Validate requirement belongs to the SRS document linked to this suite
        Guid? srsDocumentId = suite.SrsDocumentId;
        if (srsDocumentId == null)
        {
            var linkedDoc = await _srsDocumentRepository.FirstOrDefaultAsync(
                _srsDocumentRepository.GetQueryableSet()
                    .Where(x => x.TestSuiteId == command.TestSuiteId && !x.IsDeleted));
            srsDocumentId = linkedDoc?.Id;
        }

        if (srsDocumentId == null)
        {
            throw new ValidationException("Suite nay khong co SRS document duoc lien ket. Hay link SRS document truoc.");
        }

        var requirement = await _requirementRepository.FirstOrDefaultAsync(
            _requirementRepository.GetQueryableSet()
                .Where(x => x.Id == command.SrsRequirementId && x.SrsDocumentId == srsDocumentId.Value));

        if (requirement == null)
        {
            throw new NotFoundException($"SrsRequirement {command.SrsRequirementId} khong thuoc SRS document cua suite nay.");
        }

        // Check for duplicate link
        var exists = await _linkRepository.GetQueryableSet()
            .AnyAsync(x => x.TestCaseId == command.TestCaseId && x.SrsRequirementId == command.SrsRequirementId, cancellationToken);

        if (exists)
        {
            throw new ValidationException("Lien ket giua test case nay va requirement nay da ton tai.");
        }

        var link = new TestCaseRequirementLink
        {
            TestCaseId = command.TestCaseId,
            SrsRequirementId = command.SrsRequirementId,
            TraceabilityScore = null, // manually created — no LLM score
            MappingRationale = "Manual link created by user.",
        };

        await _linkRepository.AddAsync(link, cancellationToken);
        await _linkRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

        command.Result = new TraceabilityLinkModel
        {
            Id = link.Id,
            TestCaseId = link.TestCaseId,
            TestCaseName = testCase.Name,
            SrsRequirementId = link.SrsRequirementId,
            RequirementCode = requirement.RequirementCode,
            TraceabilityScore = link.TraceabilityScore,
            MappingRationale = link.MappingRationale,
        };
    }
}
