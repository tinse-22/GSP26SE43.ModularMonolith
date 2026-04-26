using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Commands;

public class CreateSrsDocumentCommand : ICommand
{
    public Guid ProjectId { get; set; }

    public Guid CurrentUserId { get; set; }

    public string Title { get; set; }

    public SrsSourceType SourceType { get; set; }

    public string RawContent { get; set; }

    public Guid? StorageFileId { get; set; }

    public SrsDocumentModel Result { get; set; }
}

public class CreateSrsDocumentCommandHandler : ICommandHandler<CreateSrsDocumentCommand>
{
    private readonly IRepository<SrsDocument, Guid> _srsDocumentRepository;

    public CreateSrsDocumentCommandHandler(IRepository<SrsDocument, Guid> srsDocumentRepository)
    {
        _srsDocumentRepository = srsDocumentRepository;
    }

    public async Task HandleAsync(CreateSrsDocumentCommand command, CancellationToken cancellationToken = default)
    {
        if (command.ProjectId == Guid.Empty)
        {
            throw new ValidationException("ProjectId la bat buoc.");
        }

        if (string.IsNullOrWhiteSpace(command.Title))
        {
            throw new ValidationException("Title la bat buoc.");
        }

        if (command.SourceType == SrsSourceType.TextInput && string.IsNullOrWhiteSpace(command.RawContent))
        {
            throw new ValidationException("RawContent la bat buoc khi SourceType la TextInput.");
        }

        if (command.SourceType == SrsSourceType.FileUpload && command.StorageFileId == null)
        {
            throw new ValidationException("StorageFileId la bat buoc khi SourceType la FileUpload.");
        }

        var doc = new SrsDocument
        {
            ProjectId = command.ProjectId,
            Title = command.Title,
            SourceType = command.SourceType,
            RawContent = command.RawContent,
            StorageFileId = command.StorageFileId,
            AnalysisStatus = SrsAnalysisStatus.Pending,
            CreatedById = command.CurrentUserId,
            RowVersion = Guid.NewGuid().ToByteArray(),
            IsDeleted = false,
        };

        await _srsDocumentRepository.AddAsync(doc, cancellationToken);
        await _srsDocumentRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

        command.Result = SrsDocumentModel.FromEntity(doc);
    }
}
