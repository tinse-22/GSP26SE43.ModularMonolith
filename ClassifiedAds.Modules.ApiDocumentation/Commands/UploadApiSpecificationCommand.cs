using ClassifiedAds.Application;
using ClassifiedAds.Contracts.Storage.DTOs;
using ClassifiedAds.Contracts.Storage.Enums;
using ClassifiedAds.Contracts.Storage.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.ApiDocumentation.Entities;
using ClassifiedAds.Modules.ApiDocumentation.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.ApiDocumentation.Commands;

public class UploadApiSpecificationCommand : ICommand
{
    public Guid ProjectId { get; set; }

    public Guid CurrentUserId { get; set; }

    public SpecificationUploadMethod UploadMethod { get; set; } = SpecificationUploadMethod.StorageGatewayContract;

    public IFormFile File { get; set; }

    public string Name { get; set; }

    public SourceType SourceType { get; set; }

    public string Version { get; set; }

    public bool AutoActivate { get; set; }

    public Guid SavedSpecId { get; set; }
}

public class UploadApiSpecificationCommandHandler : ICommandHandler<UploadApiSpecificationCommand>
{
    private static readonly string[] AllowedExtensions = { ".json", ".yaml", ".yml" };
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10MB

    private readonly IRepository<Project, Guid> _projectRepository;
    private readonly IRepository<ApiSpecification, Guid> _specRepository;
    private readonly ICrudService<ApiSpecification> _specService;
    private readonly IStorageFileGatewayService _storageFileGatewayService;
    private readonly ILogger<UploadApiSpecificationCommandHandler> _logger;

    public UploadApiSpecificationCommandHandler(
        IRepository<Project, Guid> projectRepository,
        IRepository<ApiSpecification, Guid> specRepository,
        ICrudService<ApiSpecification> specService,
        IStorageFileGatewayService storageFileGatewayService,
        ILogger<UploadApiSpecificationCommandHandler> logger)
    {
        _projectRepository = projectRepository;
        _specRepository = specRepository;
        _specService = specService;
        _storageFileGatewayService = storageFileGatewayService;
        _logger = logger;
    }

    public async Task HandleAsync(UploadApiSpecificationCommand command, CancellationToken cancellationToken = default)
    {
        // 1. Validate input
        if (command.UploadMethod != SpecificationUploadMethod.StorageGatewayContract)
        {
            throw new ValidationException("Upload method không hợp lệ. Chỉ hỗ trợ StorageGatewayContract.");
        }

        if (command.File == null || command.File.Length == 0)
        {
            throw new ValidationException("File là bắt buộc.");
        }

        if (command.File.Length > MaxFileSizeBytes)
        {
            throw new ValidationException("Kích thước file không được vượt quá 10MB.");
        }

        var extension = Path.GetExtension(command.File.FileName)?.ToLowerInvariant();
        if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
        {
            throw new ValidationException("Chỉ hỗ trợ file .json, .yaml, .yml.");
        }

        if (string.IsNullOrWhiteSpace(command.Name))
        {
            throw new ValidationException("Tên specification là bắt buộc.");
        }

        if (command.Name.Length > 200)
        {
            throw new ValidationException("Tên specification không được vượt quá 200 ký tự.");
        }

        if (command.SourceType != SourceType.OpenAPI && command.SourceType != SourceType.Postman)
        {
            throw new ValidationException("Loại nguồn phải là OpenAPI hoặc Postman.");
        }

        // 2. Load project, verify exists and ownership
        var project = await _projectRepository.FirstOrDefaultAsync(
            _projectRepository.GetQueryableSet().Where(p => p.Id == command.ProjectId));

        if (project == null)
        {
            throw new NotFoundException($"Không tìm thấy project với mã '{command.ProjectId}'.");
        }

        if (project.OwnerId != command.CurrentUserId)
        {
            throw new ValidationException("Project không tồn tại hoặc bạn không có quyền.");
        }

        // 3. Upload file to Storage module via gateway contract
        Guid? fileEntryId;
        try
        {
            using var stream = command.File.OpenReadStream();

            var uploadResult = await _storageFileGatewayService.UploadAsync(new StorageUploadFileRequest
            {
                FileName = command.File.FileName,
                ContentType = string.IsNullOrWhiteSpace(command.File.ContentType) ? "application/octet-stream" : command.File.ContentType,
                FileSize = command.File.Length,
                FileCategory = FileCategory.ApiSpec,
                OwnerId = command.CurrentUserId,
                Content = stream,
            }, cancellationToken);

            fileEntryId = uploadResult.Id;

            _logger.LogInformation("File uploaded to storage gateway. FileEntryId={FileId}, Size={Size}.",
                fileEntryId, command.File.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "File upload to storage gateway failed.");
            throw new ValidationException("Không thể upload file. Vui lòng thử lại.");
        }

        // 4. Create spec + optionally activate (in transaction)
        var spec = new ApiSpecification
        {
            ProjectId = command.ProjectId,
            OriginalFileId = fileEntryId,
            Name = command.Name.Trim(),
            SourceType = command.SourceType,
            Version = command.Version?.Trim(),
            ParseStatus = ParseStatus.Pending,
            IsActive = false,
        };

        await _specRepository.UnitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _specService.AddAsync(spec, ct);

            if (command.AutoActivate)
            {
                // Deactivate old spec if any
                if (project.ActiveSpecId.HasValue)
                {
                    var oldSpec = await _specRepository.FirstOrDefaultAsync(
                        _specRepository.GetQueryableSet().Where(s => s.Id == project.ActiveSpecId.Value));
                    if (oldSpec != null)
                    {
                        oldSpec.IsActive = false;
                    }
                }

                spec.IsActive = true;
                project.ActiveSpecId = spec.Id;
                await _projectRepository.UpdateAsync(project, ct);
            }

            await _specRepository.UnitOfWork.SaveChangesAsync(ct);
        }, cancellationToken: cancellationToken);

        command.SavedSpecId = spec.Id;
    }
}
