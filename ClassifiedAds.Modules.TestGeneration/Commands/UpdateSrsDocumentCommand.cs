using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Commands;

public class UpdateSrsDocumentCommand : ICommand
{
    public Guid ProjectId { get; set; }

    public Guid SrsDocumentId { get; set; }

    public Guid CurrentUserId { get; set; }

    /// <summary>If set, links this SRS document to the given test suite.</summary>
    public Guid? TestSuiteId { get; set; }

    /// <summary>If true, explicitly clears the test suite link.</summary>
    public bool ClearTestSuiteId { get; set; }

    public SrsDocumentModel Result { get; set; }
}

public class UpdateSrsDocumentCommandHandler : ICommandHandler<UpdateSrsDocumentCommand>
{
    private readonly IRepository<SrsDocument, Guid> _srsDocumentRepository;
    private readonly IRepository<TestSuite, Guid> _suiteRepository;

    public UpdateSrsDocumentCommandHandler(
        IRepository<SrsDocument, Guid> srsDocumentRepository,
        IRepository<TestSuite, Guid> suiteRepository)
    {
        _srsDocumentRepository = srsDocumentRepository;
        _suiteRepository = suiteRepository;
    }

    public async Task HandleAsync(UpdateSrsDocumentCommand command, CancellationToken cancellationToken = default)
    {
        var doc = await _srsDocumentRepository.FirstOrDefaultAsync(
            _srsDocumentRepository.GetQueryableSet()
                .Where(x => x.Id == command.SrsDocumentId && x.ProjectId == command.ProjectId && !x.IsDeleted));

        if (doc == null)
        {
            throw new NotFoundException("SrsDocument khong tim thay.");
        }

        var previousSuiteId = doc.TestSuiteId;

        if (command.ClearTestSuiteId)
        {
            doc.TestSuiteId = null;
        }
        else if (command.TestSuiteId.HasValue)
        {
            doc.TestSuiteId = command.TestSuiteId.Value;
        }

        // Sync the reverse FK on TestSuite so that traceability queries work correctly.
        // Clear SrsDocumentId from the previously-linked suite.
        if (previousSuiteId.HasValue && previousSuiteId != doc.TestSuiteId)
        {
            var oldSuite = await _suiteRepository.FirstOrDefaultAsync(
                _suiteRepository.GetQueryableSet()
                    .Where(x => x.Id == previousSuiteId.Value));
            if (oldSuite != null && oldSuite.SrsDocumentId == command.SrsDocumentId)
            {
                oldSuite.SrsDocumentId = null;
                await _suiteRepository.UpdateAsync(oldSuite, cancellationToken);
            }
        }

        // Set SrsDocumentId on the newly-linked suite, validating it belongs to the same project.
        if (doc.TestSuiteId.HasValue)
        {
            var newSuite = await _suiteRepository.FirstOrDefaultAsync(
                _suiteRepository.GetQueryableSet()
                    .Where(x => x.Id == doc.TestSuiteId.Value && x.ProjectId == command.ProjectId));

            if (newSuite == null)
            {
                throw new NotFoundException($"TestSuite {doc.TestSuiteId.Value} khong tim thay trong project {command.ProjectId}.");
            }

            newSuite.SrsDocumentId = command.SrsDocumentId;
            await _suiteRepository.UpdateAsync(newSuite, cancellationToken);
        }

        await _srsDocumentRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

        command.Result = SrsDocumentModel.FromEntity(doc);
    }
}
