using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Entities;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Commands;

public class DeleteSrsDocumentCommand : ICommand
{
    public Guid ProjectId { get; set; }

    public Guid SrsDocumentId { get; set; }

    public Guid CurrentUserId { get; set; }
}

public class DeleteSrsDocumentCommandHandler : ICommandHandler<DeleteSrsDocumentCommand>
{
    private readonly IRepository<SrsDocument, Guid> _srsDocumentRepository;

    public DeleteSrsDocumentCommandHandler(IRepository<SrsDocument, Guid> srsDocumentRepository)
    {
        _srsDocumentRepository = srsDocumentRepository;
    }

    public async Task HandleAsync(DeleteSrsDocumentCommand command, CancellationToken cancellationToken = default)
    {
        var doc = await _srsDocumentRepository.FirstOrDefaultAsync(
            _srsDocumentRepository.GetQueryableSet()
                .Where(x => x.Id == command.SrsDocumentId && x.ProjectId == command.ProjectId && !x.IsDeleted));

        if (doc == null)
        {
            throw new NotFoundException($"SrsDocument {command.SrsDocumentId} khong tim thay.");
        }

        doc.IsDeleted = true;
        doc.DeletedAt = DateTimeOffset.UtcNow;

        await _srsDocumentRepository.UpdateAsync(doc, cancellationToken);
        await _srsDocumentRepository.UnitOfWork.SaveChangesAsync(cancellationToken);
    }
}
