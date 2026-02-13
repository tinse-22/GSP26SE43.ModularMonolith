using ClassifiedAds.Application;
using ClassifiedAds.Contracts.Subscription.DTOs;
using ClassifiedAds.Contracts.Subscription.Enums;
using ClassifiedAds.Contracts.Subscription.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Events;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.ApiDocumentation.Entities;
using ClassifiedAds.Modules.ApiDocumentation.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.ApiDocumentation.Commands;

public class AddUpdateEndpointCommand : ICommand
{
    public Guid ProjectId { get; set; }

    public Guid SpecId { get; set; }

    public Guid? EndpointId { get; set; }

    public Guid CurrentUserId { get; set; }

    public CreateUpdateEndpointModel Model { get; set; }

    public Guid SavedEndpointId { get; set; }
}

public class AddUpdateEndpointCommandHandler : ICommandHandler<AddUpdateEndpointCommand>
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

    private readonly Dispatcher _dispatcher;
    private readonly IRepository<Project, Guid> _projectRepository;
    private readonly IRepository<ApiSpecification, Guid> _specRepository;
    private readonly IRepository<ApiEndpoint, Guid> _endpointRepository;
    private readonly IRepository<EndpointParameter, Guid> _parameterRepository;
    private readonly IRepository<EndpointResponse, Guid> _responseRepository;
    private readonly ISubscriptionLimitGatewayService _subscriptionLimitService;

    public AddUpdateEndpointCommandHandler(
        Dispatcher dispatcher,
        IRepository<Project, Guid> projectRepository,
        IRepository<ApiSpecification, Guid> specRepository,
        IRepository<ApiEndpoint, Guid> endpointRepository,
        IRepository<EndpointParameter, Guid> parameterRepository,
        IRepository<EndpointResponse, Guid> responseRepository,
        ISubscriptionLimitGatewayService subscriptionLimitService)
    {
        _dispatcher = dispatcher;
        _projectRepository = projectRepository;
        _specRepository = specRepository;
        _endpointRepository = endpointRepository;
        _parameterRepository = parameterRepository;
        _responseRepository = responseRepository;
        _subscriptionLimitService = subscriptionLimitService;
    }

    public async Task HandleAsync(AddUpdateEndpointCommand command, CancellationToken cancellationToken = default)
    {
        // 1. Validate input
        if (command.Model == null)
        {
            throw new ValidationException("Thông tin endpoint là bắt buộc.");
        }

        if (string.IsNullOrWhiteSpace(command.Model.HttpMethod) || !ValidHttpMethods.Contains(command.Model.HttpMethod))
        {
            throw new ValidationException("HTTP method không hợp lệ. Chấp nhận: GET, POST, PUT, DELETE, PATCH, HEAD, OPTIONS.");
        }

        if (string.IsNullOrWhiteSpace(command.Model.Path))
        {
            throw new ValidationException("Path là bắt buộc.");
        }

        if (command.Model.Path.Length > 500)
        {
            throw new ValidationException("Path không được vượt quá 500 ký tự.");
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

        // 3. Load spec, verify belongs to project
        var spec = await _specRepository.FirstOrDefaultAsync(
            _specRepository.GetQueryableSet().Where(s => s.Id == command.SpecId && s.ProjectId == command.ProjectId));

        if (spec == null)
        {
            throw new NotFoundException($"Không tìm thấy specification với mã '{command.SpecId}'.");
        }

        bool isCreate = command.EndpointId == null || command.EndpointId == Guid.Empty;

        if (isCreate)
        {
            // 4a. Create path — atomically check + consume subscription limit
            var limitCheck = await _subscriptionLimitService.TryConsumeLimitAsync(
                command.CurrentUserId,
                LimitType.MaxEndpointsPerProject,
                incrementValue: 1,
                cancellationToken);

            if (!limitCheck.IsAllowed)
            {
                throw new ValidationException(limitCheck.DenialReason);
            }

            var endpoint = new ApiEndpoint
            {
                ApiSpecId = spec.Id,
                HttpMethod = HttpMethodMap[command.Model.HttpMethod],
                Path = command.Model.Path.Trim(),
                OperationId = command.Model.OperationId?.Trim(),
                Summary = command.Model.Summary?.Trim(),
                Description = command.Model.Description?.Trim(),
                Tags = command.Model.Tags != null && command.Model.Tags.Count > 0
                    ? JsonSerializer.Serialize(command.Model.Tags)
                    : null,
                IsDeprecated = command.Model.IsDeprecated,
            };

            await _endpointRepository.UnitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                await _endpointRepository.AddAsync(endpoint, ct);
                await CreateChildren(endpoint.Id, command.Model, ct);
                await _endpointRepository.UnitOfWork.SaveChangesAsync(ct);
            }, cancellationToken: cancellationToken);

            command.SavedEndpointId = endpoint.Id;

            await _dispatcher.DispatchAsync(new EntityCreatedEvent<ApiEndpoint>(endpoint, DateTime.UtcNow), cancellationToken);
        }
        else
        {
            // 4b. Update path
            var endpoint = await _endpointRepository.FirstOrDefaultAsync(
                _endpointRepository.GetQueryableSet().Where(e => e.Id == command.EndpointId.Value && e.ApiSpecId == command.SpecId));

            if (endpoint == null)
            {
                throw new NotFoundException($"Không tìm thấy endpoint với mã '{command.EndpointId.Value}'.");
            }

            await _endpointRepository.UnitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                // Update endpoint fields
                endpoint.HttpMethod = HttpMethodMap[command.Model.HttpMethod];
                endpoint.Path = command.Model.Path.Trim();
                endpoint.OperationId = command.Model.OperationId?.Trim();
                endpoint.Summary = command.Model.Summary?.Trim();
                endpoint.Description = command.Model.Description?.Trim();
                endpoint.Tags = command.Model.Tags != null && command.Model.Tags.Count > 0
                    ? JsonSerializer.Serialize(command.Model.Tags)
                    : null;
                endpoint.IsDeprecated = command.Model.IsDeprecated;

                await _endpointRepository.UpdateAsync(endpoint, ct);

                // Delete old children and recreate
                await DeleteChildren(endpoint.Id, ct);
                await CreateChildren(endpoint.Id, command.Model, ct);
                await _endpointRepository.UnitOfWork.SaveChangesAsync(ct);
            }, cancellationToken: cancellationToken);

            command.SavedEndpointId = endpoint.Id;

            await _dispatcher.DispatchAsync(new EntityUpdatedEvent<ApiEndpoint>(endpoint, DateTime.UtcNow), cancellationToken);
        }
    }

    private async Task CreateChildren(Guid endpointId, CreateUpdateEndpointModel model, CancellationToken ct)
    {
        if (model.Parameters != null)
        {
            foreach (var paramDef in model.Parameters)
            {
                if (!LocationMap.TryGetValue(paramDef.Location ?? "Query", out var location))
                {
                    location = ParameterLocation.Query;
                }

                await _parameterRepository.AddAsync(new EndpointParameter
                {
                    EndpointId = endpointId,
                    Name = paramDef.Name?.Trim(),
                    Location = location,
                    DataType = paramDef.DataType?.Trim(),
                    Format = paramDef.Format?.Trim(),
                    IsRequired = paramDef.IsRequired,
                    DefaultValue = paramDef.DefaultValue,
                    Schema = paramDef.Schema,
                    Examples = paramDef.Examples,
                }, ct);
            }
        }

        if (model.Responses != null)
        {
            foreach (var resDef in model.Responses)
            {
                await _responseRepository.AddAsync(new EndpointResponse
                {
                    EndpointId = endpointId,
                    StatusCode = resDef.StatusCode,
                    Description = resDef.Description?.Trim(),
                    Schema = resDef.Schema,
                    Examples = resDef.Examples,
                    Headers = resDef.Headers,
                }, ct);
            }
        }
    }

    private async Task DeleteChildren(Guid endpointId, CancellationToken ct)
    {
        // Delete existing parameters
        var existingParams = await _parameterRepository.GetQueryableSet()
            .Where(p => p.EndpointId == endpointId)
            .ToListAsync(ct);
        foreach (var p in existingParams)
        {
            _parameterRepository.Delete(p);
        }

        // Delete existing responses
        var existingResponses = await _responseRepository.GetQueryableSet()
            .Where(r => r.EndpointId == endpointId)
            .ToListAsync(ct);
        foreach (var r in existingResponses)
        {
            _responseRepository.Delete(r);
        }
    }
}
