using ClassifiedAds.Application;
using ClassifiedAds.Contracts.Subscription.DTOs;
using ClassifiedAds.Contracts.Subscription.Enums;
using ClassifiedAds.Contracts.Subscription.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.ApiDocumentation.Entities;
using ClassifiedAds.Modules.ApiDocumentation.Models;
using ClassifiedAds.Modules.ApiDocumentation.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.ApiDocumentation.Commands;

public class ImportCurlCommand : ICommand
{
    public Guid ProjectId { get; set; }

    public Guid CurrentUserId { get; set; }

    public ImportCurlModel Model { get; set; }

    public Guid SavedSpecId { get; set; }
}

public class ImportCurlCommandHandler : ICommandHandler<ImportCurlCommand>
{
    private static readonly HashSet<string> SkippedHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Host", "User-Agent", "Accept",
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

    private readonly IRepository<Project, Guid> _projectRepository;
    private readonly IRepository<ApiSpecification, Guid> _specRepository;
    private readonly IRepository<ApiEndpoint, Guid> _endpointRepository;
    private readonly IRepository<EndpointParameter, Guid> _parameterRepository;
    private readonly IRepository<EndpointResponse, Guid> _responseRepository;
    private readonly ICrudService<ApiSpecification> _specService;
    private readonly ISubscriptionLimitGatewayService _subscriptionLimitService;
    private readonly Services.IPathParameterTemplateService _pathParamService;

    public ImportCurlCommandHandler(
        IRepository<Project, Guid> projectRepository,
        IRepository<ApiSpecification, Guid> specRepository,
        IRepository<ApiEndpoint, Guid> endpointRepository,
        IRepository<EndpointParameter, Guid> parameterRepository,
        IRepository<EndpointResponse, Guid> responseRepository,
        ICrudService<ApiSpecification> specService,
        ISubscriptionLimitGatewayService subscriptionLimitService,
        Services.IPathParameterTemplateService pathParamService)
    {
        _projectRepository = projectRepository;
        _specRepository = specRepository;
        _endpointRepository = endpointRepository;
        _parameterRepository = parameterRepository;
        _responseRepository = responseRepository;
        _specService = specService;
        _subscriptionLimitService = subscriptionLimitService;
        _pathParamService = pathParamService;
    }

    public async Task HandleAsync(ImportCurlCommand command, CancellationToken cancellationToken = default)
    {
        // 1. Validate input
        if (command.Model == null)
        {
            throw new ValidationException("Thông tin import là bắt buộc.");
        }

        if (string.IsNullOrWhiteSpace(command.Model.Name))
        {
            throw new ValidationException("Tên specification là bắt buộc.");
        }

        if (command.Model.Name.Length > 200)
        {
            throw new ValidationException("Tên specification không được vượt quá 200 ký tự.");
        }

        if (string.IsNullOrWhiteSpace(command.Model.CurlCommand))
        {
            throw new ValidationException("Lệnh cURL là bắt buộc.");
        }

        // 2. Parse cURL command
        var parseResult = CurlParser.Parse(command.Model.CurlCommand);

        // 3. Load project, verify ownership
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

        // 4. Atomically check + consume subscription limit
        var limitCheck = await _subscriptionLimitService.TryConsumeLimitAsync(
            command.CurrentUserId,
            LimitType.MaxEndpointsPerProject,
            incrementValue: 1,
            cancellationToken);

        if (!limitCheck.IsAllowed)
        {
            throw new ValidationException(limitCheck.DenialReason);
        }

        // 5. Map HTTP method
        if (!HttpMethodMap.TryGetValue(parseResult.Method, out var httpMethod))
        {
            httpMethod = Entities.HttpMethod.GET;
        }

        // 6. Create spec and endpoint in transaction
        var spec = new ApiSpecification
        {
            ProjectId = command.ProjectId,
            OriginalFileId = null,
            Name = command.Model.Name.Trim(),
            SourceType = SourceType.cURL,
            Version = command.Model.Version?.Trim(),
            ParseStatus = ParseStatus.Success,
            ParsedAt = DateTimeOffset.UtcNow,
            IsActive = false,
        };

        await _specRepository.UnitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _specService.AddAsync(spec, ct);

            var endpoint = new ApiEndpoint
            {
                ApiSpecId = spec.Id,
                HttpMethod = httpMethod,
                Path = parseResult.Path ?? "/",
                Summary = "Imported from cURL",
            };

            await _endpointRepository.AddAsync(endpoint, ct);

            // Create parameters from parsed result

            // Path parameters: extract {param} segments using shared service
            var pathParams = _pathParamService.ExtractPathParameters(parseResult.Path ?? string.Empty);
            foreach (var pathParam in pathParams)
            {
                await _parameterRepository.AddAsync(new EndpointParameter
                {
                    EndpointId = endpoint.Id,
                    Name = pathParam.Name,
                    Location = ParameterLocation.Path,
                    DataType = EndpointParameterDataType.String.ToStorageValue(),
                    IsRequired = true,
                }, ct);
            }

            // Query parameters
            foreach (var kvp in parseResult.QueryParams)
            {
                await _parameterRepository.AddAsync(new EndpointParameter
                {
                    EndpointId = endpoint.Id,
                    Name = kvp.Key,
                    Location = ParameterLocation.Query,
                    DataType = EndpointParameterDataType.String.ToStorageValue(),
                    DefaultValue = kvp.Value,
                    IsRequired = false,
                }, ct);
            }

            // Headers (excluding common ones)
            foreach (var kvp in parseResult.Headers)
            {
                if (SkippedHeaders.Contains(kvp.Key))
                {
                    continue;
                }

                if (kvp.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                {
                    continue; // Content-Type is captured separately
                }

                await _parameterRepository.AddAsync(new EndpointParameter
                {
                    EndpointId = endpoint.Id,
                    Name = kvp.Key,
                    Location = ParameterLocation.Header,
                    DataType = EndpointParameterDataType.String.ToStorageValue(),
                    DefaultValue = kvp.Value,
                    IsRequired = false,
                }, ct);
            }

            // Body parameter
            if (!string.IsNullOrEmpty(parseResult.Body))
            {
                await _parameterRepository.AddAsync(new EndpointParameter
                {
                    EndpointId = endpoint.Id,
                    Name = "body",
                    Location = ParameterLocation.Body,
                    DataType = parseResult.ContentType ?? "application/x-www-form-urlencoded",
                    Schema = parseResult.Body,
                    IsRequired = true,
                }, ct);
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
