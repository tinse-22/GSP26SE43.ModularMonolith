using ClassifiedAds.Application;
using ClassifiedAds.Contracts.Subscription.DTOs;
using ClassifiedAds.Contracts.Subscription.Enums;
using ClassifiedAds.Contracts.Subscription.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.ApiDocumentation.Entities;
using ClassifiedAds.Modules.ApiDocumentation.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.ApiDocumentation.Commands;

public class CreateManualSpecificationCommand : ICommand
{
    public Guid ProjectId { get; set; }

    public Guid CurrentUserId { get; set; }

    public CreateManualSpecificationModel Model { get; set; }

    public Guid SavedSpecId { get; set; }
}

public class CreateManualSpecificationCommandHandler : ICommandHandler<CreateManualSpecificationCommand>
{
    private static readonly HashSet<string> ValidHttpMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "GET", "POST", "PUT", "DELETE", "PATCH", "HEAD", "OPTIONS",
    };

    private static readonly Dictionary<string, Entities.HttpMethod> HttpMethodMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["GET"] = Entities.HttpMethod.GET,
        ["POST"] = Entities.HttpMethod.POST,
        ["PUT"] = Entities.HttpMethod.PUT,
        ["DELETE"] = Entities.HttpMethod.DELETE,
        ["PATCH"] = Entities.HttpMethod.PATCH,
        ["HEAD"] = Entities.HttpMethod.HEAD,
        ["OPTIONS"] = Entities.HttpMethod.OPTIONS,
    };

    private static readonly Dictionary<string, ParameterLocation> LocationMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Path"] = ParameterLocation.Path,
        ["Query"] = ParameterLocation.Query,
        ["Header"] = ParameterLocation.Header,
        ["Body"] = ParameterLocation.Body,
        ["Cookie"] = ParameterLocation.Cookie,
    };

    private readonly IRepository<Project, Guid> _projectRepository;
    private readonly IRepository<ApiSpecification, Guid> _specRepository;
    private readonly IRepository<ApiEndpoint, Guid> _endpointRepository;
    private readonly IRepository<EndpointParameter, Guid> _parameterRepository;
    private readonly IRepository<EndpointResponse, Guid> _responseRepository;
    private readonly ICrudService<ApiSpecification> _specService;
    private readonly ISubscriptionLimitGatewayService _subscriptionLimitService;

    public CreateManualSpecificationCommandHandler(
        IRepository<Project, Guid> projectRepository,
        IRepository<ApiSpecification, Guid> specRepository,
        IRepository<ApiEndpoint, Guid> endpointRepository,
        IRepository<EndpointParameter, Guid> parameterRepository,
        IRepository<EndpointResponse, Guid> responseRepository,
        ICrudService<ApiSpecification> specService,
        ISubscriptionLimitGatewayService subscriptionLimitService)
    {
        _projectRepository = projectRepository;
        _specRepository = specRepository;
        _endpointRepository = endpointRepository;
        _parameterRepository = parameterRepository;
        _responseRepository = responseRepository;
        _specService = specService;
        _subscriptionLimitService = subscriptionLimitService;
    }

    public async Task HandleAsync(CreateManualSpecificationCommand command, CancellationToken cancellationToken = default)
    {
        // 1. Validate input
        if (command.Model == null)
        {
            throw new ValidationException("Thông tin specification là bắt buộc.");
        }

        if (string.IsNullOrWhiteSpace(command.Model.Name))
        {
            throw new ValidationException("Tên specification là bắt buộc.");
        }

        if (command.Model.Name.Length > 200)
        {
            throw new ValidationException("Tên specification không được vượt quá 200 ký tự.");
        }

        if (command.Model.Endpoints == null || command.Model.Endpoints.Count == 0)
        {
            throw new ValidationException("Danh sách endpoint là bắt buộc. Vui lòng thêm ít nhất một endpoint.");
        }

        // Validate each endpoint
        for (int i = 0; i < command.Model.Endpoints.Count; i++)
        {
            var ep = command.Model.Endpoints[i];
            if (string.IsNullOrWhiteSpace(ep.HttpMethod) || !ValidHttpMethods.Contains(ep.HttpMethod))
            {
                throw new ValidationException($"Endpoint #{i + 1}: HTTP method không hợp lệ. Chấp nhận: GET, POST, PUT, DELETE, PATCH, HEAD, OPTIONS.");
            }

            if (string.IsNullOrWhiteSpace(ep.Path))
            {
                throw new ValidationException($"Endpoint #{i + 1}: Path là bắt buộc.");
            }

            if (ep.Path.Length > 500)
            {
                throw new ValidationException($"Endpoint #{i + 1}: Path không được vượt quá 500 ký tự.");
            }
        }

        // 2. Load project, verify ownership
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

        // 3. Atomically check + consume subscription limit
        var limitCheck = await _subscriptionLimitService.TryConsumeLimitAsync(
            command.CurrentUserId,
            LimitType.MaxEndpointsPerProject,
            incrementValue: command.Model.Endpoints.Count,
            cancellationToken);

        if (!limitCheck.IsAllowed)
        {
            throw new ValidationException(limitCheck.DenialReason);
        }

        // 4. Create spec and endpoints in transaction
        var spec = new ApiSpecification
        {
            ProjectId = command.ProjectId,
            OriginalFileId = null,
            Name = command.Model.Name.Trim(),
            SourceType = SourceType.Manual,
            Version = command.Model.Version?.Trim(),
            ParseStatus = ParseStatus.Success,
            ParsedAt = DateTimeOffset.UtcNow,
            IsActive = false,
        };

        await _specRepository.UnitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _specService.AddAsync(spec, ct);

            foreach (var epDef in command.Model.Endpoints)
            {
                var endpoint = new ApiEndpoint
                {
                    ApiSpecId = spec.Id,
                    HttpMethod = HttpMethodMap[epDef.HttpMethod],
                    Path = epDef.Path.Trim(),
                    OperationId = epDef.OperationId?.Trim(),
                    Summary = epDef.Summary?.Trim(),
                    Description = epDef.Description?.Trim(),
                    Tags = epDef.Tags != null && epDef.Tags.Count > 0
                        ? JsonSerializer.Serialize(epDef.Tags)
                        : null,
                    IsDeprecated = epDef.IsDeprecated,
                };

                await _endpointRepository.AddAsync(endpoint, ct);

                // Add parameters
                if (epDef.Parameters != null)
                {
                    foreach (var paramDef in epDef.Parameters)
                    {
                        if (!LocationMap.TryGetValue(paramDef.Location ?? "Query", out var location))
                        {
                            location = ParameterLocation.Query;
                        }

                        var parameter = new EndpointParameter
                        {
                            EndpointId = endpoint.Id,
                            Name = paramDef.Name?.Trim(),
                            Location = location,
                            DataType = paramDef.DataType.ToStorageValue(),
                            Format = paramDef.Format?.Trim(),
                            IsRequired = paramDef.IsRequired,
                            DefaultValue = paramDef.DefaultValue,
                            Schema = paramDef.Schema,
                            Examples = paramDef.Examples,
                        };

                        await _parameterRepository.AddAsync(parameter, ct);
                    }
                }

                // Add responses
                if (epDef.Responses != null)
                {
                    foreach (var resDef in epDef.Responses)
                    {
                        var response = new EndpointResponse
                        {
                            EndpointId = endpoint.Id,
                            StatusCode = resDef.StatusCode,
                            Description = resDef.Description?.Trim(),
                            Schema = resDef.Schema,
                            Examples = resDef.Examples,
                            Headers = resDef.Headers,
                        };

                        await _responseRepository.AddAsync(response, ct);
                    }
                }
            }

            // Auto-activate if requested
            if (command.Model.AutoActivate)
            {
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
