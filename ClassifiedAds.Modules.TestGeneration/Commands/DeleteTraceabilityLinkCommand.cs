using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Commands;

public class DeleteTraceabilityLinkCommand : ICommand
{
    public Guid ProjectId { get; set; }

    public Guid TestSuiteId { get; set; }

    public Guid LinkId { get; set; }

    public Guid CurrentUserId { get; set; }
}

public class DeleteTraceabilityLinkCommandHandler : ICommandHandler<DeleteTraceabilityLinkCommand>
{
    private readonly IRepository<TestSuite, Guid> _suiteRepository;
    private readonly IRepository<TestCase, Guid> _testCaseRepository;
    private readonly IRepository<TestCaseRequirementLink, Guid> _linkRepository;

    public DeleteTraceabilityLinkCommandHandler(
        IRepository<TestSuite, Guid> suiteRepository,
        IRepository<TestCase, Guid> testCaseRepository,
        IRepository<TestCaseRequirementLink, Guid> linkRepository)
    {
        _suiteRepository = suiteRepository;
        _testCaseRepository = testCaseRepository;
        _linkRepository = linkRepository;
    }

    public async Task HandleAsync(DeleteTraceabilityLinkCommand command, CancellationToken cancellationToken = default)
    {
        var suite = await _suiteRepository.FirstOrDefaultAsync(
            _suiteRepository.GetQueryableSet()
                .Where(x => x.Id == command.TestSuiteId && x.ProjectId == command.ProjectId));

        if (suite == null)
        {
            throw new NotFoundException($"TestSuite {command.TestSuiteId} khong tim thay.");
        }

        var link = await _linkRepository.FirstOrDefaultAsync(
            _linkRepository.GetQueryableSet().Where(x => x.Id == command.LinkId));

        if (link == null)
        {
            throw new NotFoundException($"TraceabilityLink {command.LinkId} khong tim thay.");
        }

        // Validate that the link's test case belongs to this suite (authorization guard)
        var testCaseBelongsToSuite = await _testCaseRepository.GetQueryableSet()
            .AnyAsync(x => x.Id == link.TestCaseId && x.TestSuiteId == command.TestSuiteId, cancellationToken);

        if (!testCaseBelongsToSuite)
        {
            throw new NotFoundException($"TraceabilityLink {command.LinkId} khong tim thay.");
        }

        _linkRepository.Delete(link);
        await _linkRepository.UnitOfWork.SaveChangesAsync(cancellationToken);
    }
}
