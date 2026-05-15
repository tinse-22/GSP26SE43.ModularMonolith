using ClassifiedAds.Application;
using ClassifiedAds.Contracts.Storage.DTOs;
using ClassifiedAds.Contracts.Storage.Enums;
using ClassifiedAds.Contracts.Storage.Services;
using ClassifiedAds.Contracts.Subscription.DTOs;
using ClassifiedAds.Contracts.Subscription.Enums;
using ClassifiedAds.Contracts.Subscription.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.ApiDocumentation.Entities;
using ClassifiedAds.Modules.ApiDocumentation.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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
        ["path"] = ParameterLocation.Path,
        ["query"] = ParameterLocation.Query,
        ["header"] = ParameterLocation.Header,
        ["cookie"] = ParameterLocation.Cookie,
        ["body"] = ParameterLocation.Body,
    };

    private readonly IRepository<Project, Guid> _projectRepository;
    private readonly IRepository<ApiSpecification, Guid> _specRepository;
    private readonly IRepository<ApiEndpoint, Guid> _endpointRepository;
    private readonly IRepository<EndpointParameter, Guid> _parameterRepository;
    private readonly IRepository<EndpointResponse, Guid> _responseRepository;
    private readonly ICrudService<ApiSpecification> _specService;
    private readonly IStorageFileGatewayService _storageFileGatewayService;
    private readonly ISubscriptionLimitGatewayService _subscriptionLimitService;
    private readonly ILogger<UploadApiSpecificationCommandHandler> _logger;

    public UploadApiSpecificationCommandHandler(
        IRepository<Project, Guid> projectRepository,
        IRepository<ApiSpecification, Guid> specRepository,
        IRepository<ApiEndpoint, Guid> endpointRepository,
        IRepository<EndpointParameter, Guid> parameterRepository,
        IRepository<EndpointResponse, Guid> responseRepository,
        ICrudService<ApiSpecification> specService,
        IStorageFileGatewayService storageFileGatewayService,
        ISubscriptionLimitGatewayService subscriptionLimitService,
        ILogger<UploadApiSpecificationCommandHandler> logger)
    {
        _projectRepository = projectRepository;
        _specRepository = specRepository;
        _endpointRepository = endpointRepository;
        _parameterRepository = parameterRepository;
        _responseRepository = responseRepository;
        _specService = specService;
        _storageFileGatewayService = storageFileGatewayService;
        _subscriptionLimitService = subscriptionLimitService;
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

        // 1b. Validate file content matches declared SourceType
        string fileContent;
        using (var reader = new StreamReader(command.File.OpenReadStream()))
        {
            fileContent = await reader.ReadToEndAsync(cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(fileContent))
        {
            throw new ValidationException("File không được trống.");
        }

        if (command.SourceType == SourceType.OpenAPI)
        {
            ValidateOpenApiContent(fileContent, extension);
        }
        else if (command.SourceType == SourceType.Postman)
        {
            ValidatePostmanContent(fileContent);
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

        // 3. Atomically check + consume subscription storage limit before uploading
        var fileSizeMB = (decimal)command.File.Length / (1024m * 1024m);
        var storageLimitCheck = await _subscriptionLimitService.TryConsumeLimitAsync(
            command.CurrentUserId,
            LimitType.MaxStorageMB,
            incrementValue: fileSizeMB,
            cancellationToken);

        if (!storageLimitCheck.IsAllowed)
        {
            throw new ValidationException(storageLimitCheck.DenialReason);
        }

        // 4. Upload file to Storage module via gateway contract
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
            _logger.LogError(ex, "File upload to storage gateway failed. Type={ExType}, Message={ExMsg}", ex.GetType().FullName, ex.Message);
            throw new ValidationException($"Không thể upload file: [{ex.GetType().Name}] {ex.Message}");
        }

        // 5. Parse the OpenAPI/Postman file content into endpoints
        List<ParsedEndpoint> parsedEndpoints = new();
        List<string> parseErrors = new();
        var parseStatus = ParseStatus.Pending;

        if (command.SourceType == SourceType.OpenAPI && extension == ".json")
        {
            try
            {
                parsedEndpoints = await ParseOpenApiJsonEndpointsAsync(fileContent, cancellationToken);
                parseStatus = ParseStatus.Success;
                _logger.LogInformation("Parsed {Count} endpoints from OpenAPI JSON.", parsedEndpoints.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse OpenAPI JSON content for spec '{Name}'.", command.Name);
                parseErrors.Add(ex.Message);
                parseStatus = ParseStatus.Failed;
            }
        }

        // 5a. Check endpoint subscription limit before persisting
        if (parseStatus == ParseStatus.Success && parsedEndpoints.Count > 0)
        {
            var endpointLimitCheck = await _subscriptionLimitService.TryConsumeLimitAsync(
                command.CurrentUserId,
                LimitType.MaxEndpointsPerProject,
                incrementValue: parsedEndpoints.Count,
                cancellationToken);

            if (!endpointLimitCheck.IsAllowed)
            {
                throw new ValidationException(endpointLimitCheck.DenialReason);
            }
        }

        // 6. Create spec + endpoints + optionally activate (in transaction)
        var spec = new ApiSpecification
        {
            Id = Guid.NewGuid(),
            ProjectId = command.ProjectId,
            OriginalFileId = fileEntryId,
            Name = command.Name.Trim(),
            SourceType = command.SourceType,
            Version = command.Version?.Trim(),
            ParseStatus = parseStatus,
            ParsedAt = parseStatus != ParseStatus.Pending ? DateTimeOffset.UtcNow : null,
            ParseErrors = parseErrors.Count > 0 ? JsonSerializer.Serialize(parseErrors) : null,
            IsActive = false,
        };

        await _specRepository.UnitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _specService.AddAsync(spec, ct);

            // Persist parsed endpoints
            foreach (var pe in parsedEndpoints)
            {
                pe.Endpoint.Id = Guid.NewGuid();
                pe.Endpoint.ApiSpecId = spec.Id;
                await _endpointRepository.AddAsync(pe.Endpoint, ct);

                foreach (var param in pe.Parameters)
                {
                    param.Id = Guid.NewGuid();
                    param.EndpointId = pe.Endpoint.Id;
                    await _parameterRepository.AddAsync(param, ct);
                }

                foreach (var resp in pe.Responses)
                {
                    resp.Id = Guid.NewGuid();
                    resp.EndpointId = pe.Endpoint.Id;
                    await _responseRepository.AddAsync(resp, ct);
                }
            }

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

    /// <summary>
    /// Parses the paths object of an OpenAPI 3.x / Swagger 2.x JSON spec and returns
    /// a flat list of endpoints with their parameters and responses.
    /// </summary>
    private static async Task<List<ParsedEndpoint>> ParseOpenApiJsonEndpointsAsync(
        string content,
        CancellationToken cancellationToken)
    {
        var parser = new Services.OpenApiSpecificationParser();
        var parseResult = await parser.ParseAsync(
            Encoding.UTF8.GetBytes(content),
            "uploaded-openapi.json",
            cancellationToken);

        if (!parseResult.Success)
        {
            throw new ValidationException(parseResult.Errors.FirstOrDefault()
                ?? "Không thể parse nội dung OpenAPI.");
        }

        return parseResult.Endpoints.Select(parsedEndpoint =>
        {
            if (!HttpMethodMap.TryGetValue(parsedEndpoint.HttpMethod ?? "GET", out var httpMethod))
            {
                httpMethod = Entities.HttpMethod.GET;
            }

            var endpoint = new ApiEndpoint
            {
                HttpMethod = httpMethod,
                Path = parsedEndpoint.Path,
                OperationId = parsedEndpoint.OperationId,
                Summary = parsedEndpoint.Summary,
                Description = parsedEndpoint.Description,
                Tags = parsedEndpoint.Tags?.Count > 0
                    ? JsonSerializer.Serialize(parsedEndpoint.Tags)
                    : null,
                IsDeprecated = parsedEndpoint.IsDeprecated,
            };

            var parameters = (parsedEndpoint.Parameters ?? new List<Services.ParsedParameter>())
                .Select(MapParsedParameter)
                .ToList();

            var responses = (parsedEndpoint.Responses ?? new List<Services.ParsedResponse>())
                .Select(parsedResponse => new EndpointResponse
                {
                    StatusCode = parsedResponse.StatusCode,
                    Description = parsedResponse.Description,
                    Schema = parsedResponse.Schema,
                    Examples = parsedResponse.Examples,
                    Headers = parsedResponse.Headers,
                })
                .ToList();

            return new ParsedEndpoint(endpoint, parameters, responses);
        }).ToList();
    }

    private static EndpointParameter MapParsedParameter(Services.ParsedParameter parsedParameter)
    {
        if (!LocationMap.TryGetValue(parsedParameter.Location ?? "Query", out var location))
        {
            location = ParameterLocation.Query;
        }

        return new EndpointParameter
        {
            Name = parsedParameter.Name,
            Location = location,
            DataType = parsedParameter.DataType ?? "string",
            Format = parsedParameter.Format,
            ContentType = parsedParameter.ContentType,
            IsRequired = parsedParameter.IsRequired || location == ParameterLocation.Path,
            DefaultValue = parsedParameter.DefaultValue,
            Schema = parsedParameter.Schema,
            Examples = parsedParameter.Examples,
        };
    }

    private sealed record ParsedEndpoint(
        ApiEndpoint Endpoint,
        List<EndpointParameter> Parameters,
        List<EndpointResponse> Responses);

    private static void ValidateOpenApiContent(string content, string extension)
    {
        if (extension == ".json")
        {
            try
            {
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;

                bool hasOpenApiKey = root.TryGetProperty("openapi", out _);
                bool hasSwaggerKey = root.TryGetProperty("swagger", out _);

                if (!hasOpenApiKey && !hasSwaggerKey)
                {
                    throw new ValidationException(
                        "File JSON không phải định dạng OpenAPI hợp lệ. File phải chứa thuộc tính 'openapi' hoặc 'swagger' ở cấp cao nhất.");
                }
            }
            catch (JsonException)
            {
                throw new ValidationException("File không phải JSON hợp lệ.");
            }
        }
        else
        {
            // YAML: basic text check without YAML library dependency
            bool hasOpenApi = Regex.IsMatch(content, @"^openapi\s*:", RegexOptions.Multiline | RegexOptions.IgnoreCase);
            bool hasSwagger = Regex.IsMatch(content, @"^swagger\s*:", RegexOptions.Multiline | RegexOptions.IgnoreCase);

            if (!hasOpenApi && !hasSwagger)
            {
                throw new ValidationException(
                    "File YAML không phải định dạng OpenAPI hợp lệ. File phải chứa thuộc tính 'openapi' hoặc 'swagger' ở cấp cao nhất.");
            }
        }
    }

    private static void ValidatePostmanContent(string content)
    {
        try
        {
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            bool hasInfo = root.TryGetProperty("info", out _);
            bool hasItem = root.TryGetProperty("item", out _);

            if (!hasInfo || !hasItem)
            {
                throw new ValidationException(
                    "File không phải định dạng Postman Collection hợp lệ. File phải chứa thuộc tính 'info' và 'item'.");
            }
        }
        catch (JsonException)
        {
            throw new ValidationException("File không phải JSON hợp lệ. Postman Collection phải là file JSON.");
        }
    }
}
