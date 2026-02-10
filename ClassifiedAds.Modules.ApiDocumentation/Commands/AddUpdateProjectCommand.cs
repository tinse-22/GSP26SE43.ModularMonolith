using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.ApiDocumentation.Entities;
using ClassifiedAds.Modules.ApiDocumentation.Models;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.ApiDocumentation.Commands;

public class AddUpdateProjectCommand : ICommand
{
    public CreateUpdateProjectModel Model { get; set; }

    public Guid CurrentUserId { get; set; }

    public Guid? ProjectId { get; set; }

    public Guid SavedProjectId { get; set; }
}

public class AddUpdateProjectCommandHandler : ICommandHandler<AddUpdateProjectCommand>
{
    private readonly ICrudService<Project> _projectService;
    private readonly IRepository<Project, Guid> _projectRepository;

    public AddUpdateProjectCommandHandler(
        ICrudService<Project> projectService,
        IRepository<Project, Guid> projectRepository)
    {
        _projectService = projectService;
        _projectRepository = projectRepository;
    }

    public async Task HandleAsync(AddUpdateProjectCommand command, CancellationToken cancellationToken = default)
    {
        if (command.Model == null)
        {
            throw new ValidationException("Thông tin project là bắt buộc.");
        }

        if (string.IsNullOrWhiteSpace(command.Model.Name))
        {
            throw new ValidationException("Tên project là bắt buộc.");
        }

        if (command.Model.Name.Length > 200)
        {
            throw new ValidationException("Tên project không được vượt quá 200 ký tự.");
        }

        if (!string.IsNullOrWhiteSpace(command.Model.BaseUrl) &&
            !Uri.TryCreate(command.Model.BaseUrl, UriKind.Absolute, out _))
        {
            throw new ValidationException("URL cơ sở không hợp lệ.");
        }

        if (command.ProjectId == null || command.ProjectId == Guid.Empty)
        {
            // Create
            var project = new Project
            {
                Name = command.Model.Name.Trim(),
                Description = command.Model.Description?.Trim(),
                BaseUrl = command.Model.BaseUrl?.Trim(),
                OwnerId = command.CurrentUserId,
                Status = ProjectStatus.Active,
            };

            await _projectService.AddAsync(project, cancellationToken);
            command.SavedProjectId = project.Id;
        }
        else
        {
            // Update
            var project = await _projectRepository.FirstOrDefaultAsync(
                _projectRepository.GetQueryableSet().Where(p => p.Id == command.ProjectId.Value));

            if (project == null)
            {
                throw new NotFoundException($"Không tìm thấy project với mã '{command.ProjectId.Value}'.");
            }

            if (project.OwnerId != command.CurrentUserId)
            {
                throw new ValidationException("Bạn không có quyền chỉnh sửa project này.");
            }

            project.Name = command.Model.Name.Trim();
            project.Description = command.Model.Description?.Trim();
            project.BaseUrl = command.Model.BaseUrl?.Trim();

            await _projectService.UpdateAsync(project, cancellationToken);
            command.SavedProjectId = project.Id;
        }
    }
}
