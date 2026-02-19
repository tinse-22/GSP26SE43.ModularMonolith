using ClassifiedAds.Application;
using ClassifiedAds.Contracts.ApiDocumentation.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using ClassifiedAds.Modules.TestGeneration.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Commands;

public class AddUpdateTestSuiteScopeCommand : ICommand
{
    public Guid? SuiteId { get; set; }

    public Guid ProjectId { get; set; }

    public Guid CurrentUserId { get; set; }

    public string Name { get; set; }

    public string Description { get; set; }

    public Guid ApiSpecId { get; set; }

    public GenerationType GenerationType { get; set; }

    public IReadOnlyCollection<Guid> SelectedEndpointIds { get; set; } = Array.Empty<Guid>();

    public string RowVersion { get; set; }

    public TestSuiteScopeModel Result { get; set; }
}

public class AddUpdateTestSuiteScopeCommandHandler : ICommandHandler<AddUpdateTestSuiteScopeCommand>
{
    private readonly IRepository<TestSuite, Guid> _suiteRepository;
    private readonly IApiEndpointMetadataService _endpointMetadataService;
    private readonly ITestSuiteScopeService _scopeService;
    private readonly ILogger<AddUpdateTestSuiteScopeCommandHandler> _logger;

    public AddUpdateTestSuiteScopeCommandHandler(
        IRepository<TestSuite, Guid> suiteRepository,
        IApiEndpointMetadataService endpointMetadataService,
        ITestSuiteScopeService scopeService,
        ILogger<AddUpdateTestSuiteScopeCommandHandler> logger)
    {
        _suiteRepository = suiteRepository;
        _endpointMetadataService = endpointMetadataService;
        _scopeService = scopeService;
        _logger = logger;
    }

    public async Task HandleAsync(AddUpdateTestSuiteScopeCommand command, CancellationToken cancellationToken = default)
    {
        if (command.ProjectId == Guid.Empty)
        {
            throw new ValidationException("ProjectId là bắt buộc.");
        }

        if (command.CurrentUserId == Guid.Empty)
        {
            throw new ValidationException("CurrentUserId là bắt buộc.");
        }

        if (command.ApiSpecId == Guid.Empty)
        {
            throw new ValidationException("ApiSpecId là bắt buộc.");
        }

        if (string.IsNullOrWhiteSpace(command.Name))
        {
            throw new ValidationException("Tên test suite là bắt buộc.");
        }

        var normalizedEndpointIds = _scopeService.NormalizeEndpointIds(command.SelectedEndpointIds);

        if (normalizedEndpointIds.Count == 0)
        {
            throw new ValidationException("Phải chọn ít nhất một endpoint.");
        }

        // Validate that all endpoints belong to the spec via cross-module contract
        var validEndpoints = await _endpointMetadataService.GetEndpointMetadataAsync(
            command.ApiSpecId, normalizedEndpointIds, cancellationToken);

        var validEndpointIds = validEndpoints.Select(e => e.EndpointId).ToHashSet();
        var invalidIds = normalizedEndpointIds.Where(id => !validEndpointIds.Contains(id)).ToList();

        if (invalidIds.Count > 0)
        {
            throw new ValidationException(
                $"Các endpoint không thuộc specification đã chọn: {string.Join(", ", invalidIds)}.");
        }

        bool isUpdate = command.SuiteId.HasValue && command.SuiteId.Value != Guid.Empty;

        if (isUpdate)
        {
            await HandleUpdate(command, normalizedEndpointIds, cancellationToken);
        }
        else
        {
            await HandleCreate(command, normalizedEndpointIds, cancellationToken);
        }
    }

    private async Task HandleCreate(AddUpdateTestSuiteScopeCommand command, List<Guid> normalizedEndpointIds, CancellationToken cancellationToken)
    {
        var suite = new TestSuite
        {
            ProjectId = command.ProjectId,
            ApiSpecId = command.ApiSpecId,
            Name = command.Name.Trim(),
            Description = command.Description?.Trim(),
            GenerationType = command.GenerationType,
            Status = TestSuiteStatus.Draft,
            ApprovalStatus = ApprovalStatus.NotApplicable,
            CreatedById = command.CurrentUserId,
            SelectedEndpointIds = normalizedEndpointIds,
        };

        await _suiteRepository.AddAsync(suite, cancellationToken);
        await _suiteRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

        command.Result = TestSuiteScopeModel.FromEntity(suite);

        _logger.LogInformation(
            "Created test suite scope. SuiteId={SuiteId}, ProjectId={ProjectId}, EndpointCount={EndpointCount}, ActorUserId={ActorUserId}",
            suite.Id, command.ProjectId, normalizedEndpointIds.Count, command.CurrentUserId);
    }

    private async Task HandleUpdate(AddUpdateTestSuiteScopeCommand command, List<Guid> normalizedEndpointIds, CancellationToken cancellationToken)
    {
        var suite = await _suiteRepository.FirstOrDefaultAsync(
            _suiteRepository.GetQueryableSet()
                .Where(x => x.Id == command.SuiteId.Value && x.ProjectId == command.ProjectId));

        if (suite == null)
        {
            throw new NotFoundException($"Không tìm thấy test suite với mã '{command.SuiteId}'.");
        }

        if (suite.CreatedById != command.CurrentUserId)
        {
            throw new ValidationException("Bạn không có quyền chỉnh sửa test suite này.");
        }

        if (suite.Status == TestSuiteStatus.Archived)
        {
            throw new ValidationException("Test suite đã được archive và không thể cập nhật.");
        }

        if (string.IsNullOrEmpty(command.RowVersion))
        {
            throw new ValidationException("RowVersion là bắt buộc khi cập nhật.");
        }

        try
        {
            _suiteRepository.SetRowVersion(suite, Convert.FromBase64String(command.RowVersion));
        }
        catch (FormatException)
        {
            throw new ValidationException("RowVersion không hợp lệ.");
        }

        suite.Name = command.Name.Trim();
        suite.Description = command.Description?.Trim();
        suite.ApiSpecId = command.ApiSpecId;
        suite.GenerationType = command.GenerationType;
        suite.SelectedEndpointIds = normalizedEndpointIds;
        suite.LastModifiedById = command.CurrentUserId;

        try
        {
            await _suiteRepository.UpdateAsync(suite, cancellationToken);
            await _suiteRepository.UnitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConflictException("CONCURRENCY_CONFLICT", "Dữ liệu đã bị thay đổi bởi người khác. Vui lòng tải lại và thử lại.", ex);
        }

        command.Result = TestSuiteScopeModel.FromEntity(suite);

        _logger.LogInformation(
            "Updated test suite scope. SuiteId={SuiteId}, ProjectId={ProjectId}, EndpointCount={EndpointCount}, ActorUserId={ActorUserId}",
            suite.Id, command.ProjectId, normalizedEndpointIds.Count, command.CurrentUserId);
    }
}
