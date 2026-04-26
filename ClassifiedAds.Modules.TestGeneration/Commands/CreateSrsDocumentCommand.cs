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

public class CreateSrsDocumentCommand : ICommand
{
    public Guid ProjectId { get; set; }

    public Guid CurrentUserId { get; set; }

    public Guid? TestSuiteId { get; set; }

    public string Title { get; set; }

    public SrsSourceType SourceType { get; set; }

    public string RawContent { get; set; }

    public Guid? StorageFileId { get; set; }

    public SrsDocumentModel Result { get; set; }
}

public class CreateSrsDocumentCommandHandler : ICommandHandler<CreateSrsDocumentCommand>
{
    private readonly IRepository<SrsDocument, Guid> _srsDocumentRepository;
    private readonly IRepository<TestSuite, Guid> _suiteRepository;

    public CreateSrsDocumentCommandHandler(
        IRepository<SrsDocument, Guid> srsDocumentRepository,
        IRepository<TestSuite, Guid> suiteRepository)
    {
        _srsDocumentRepository = srsDocumentRepository;
        _suiteRepository = suiteRepository;
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

        TestSuite suite = null;
        if (command.TestSuiteId.HasValue)
        {
            suite = await _suiteRepository.FirstOrDefaultAsync(
                _suiteRepository.GetQueryableSet()
                    .Where(x => x.Id == command.TestSuiteId.Value && x.ProjectId == command.ProjectId));

            if (suite == null)
            {
                throw new NotFoundException($"TestSuite {command.TestSuiteId.Value} khong tim thay.");
            }
        }

        var doc = new SrsDocument
        {
            ProjectId = command.ProjectId,
            TestSuiteId = command.TestSuiteId,
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

        if (suite != null)
        {
            suite.SrsDocumentId = doc.Id;
            suite.LastModifiedById = command.CurrentUserId;
            suite.RowVersion = Guid.NewGuid().ToByteArray();
            await _suiteRepository.UpdateAsync(suite, cancellationToken);
            await _suiteRepository.UnitOfWork.SaveChangesAsync(cancellationToken);
        }

        command.Result = SrsDocumentModel.FromEntity(doc);
    }
}
