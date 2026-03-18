using ClassifiedAds.Application;
using ClassifiedAds.Contracts.Storage.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.ApiDocumentation.Entities;
using ClassifiedAds.Modules.ApiDocumentation.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.ApiDocumentation.Commands;

public class ParseUploadedSpecificationCommand : ICommand
{
    public Guid SpecificationId { get; set; }
}

public class ParseUploadedSpecificationCommandHandler : ICommandHandler<ParseUploadedSpecificationCommand>
{
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

    private readonly IRepository<ApiSpecification, Guid> _specRepository;
    private readonly IRepository<ApiEndpoint, Guid> _endpointRepository;
    private readonly IRepository<EndpointParameter, Guid> _parameterRepository;
    private readonly IRepository<EndpointResponse, Guid> _responseRepository;
    private readonly IRepository<EndpointSecurityReq, Guid> _securityReqRepository;
    private readonly IRepository<SecurityScheme, Guid> _securitySchemeRepository;
    private readonly IStorageFileGatewayService _storageGateway;
    private readonly IEnumerable<ISpecificationParser> _parsers;
    private readonly ILogger<ParseUploadedSpecificationCommandHandler> _logger;

    public ParseUploadedSpecificationCommandHandler(
        IRepository<ApiSpecification, Guid> specRepository,
        IRepository<ApiEndpoint, Guid> endpointRepository,
        IRepository<EndpointParameter, Guid> parameterRepository,
        IRepository<EndpointResponse, Guid> responseRepository,
        IRepository<EndpointSecurityReq, Guid> securityReqRepository,
        IRepository<SecurityScheme, Guid> securitySchemeRepository,
        IStorageFileGatewayService storageGateway,
        IEnumerable<ISpecificationParser> parsers,
        ILogger<ParseUploadedSpecificationCommandHandler> logger)
    {
        _specRepository = specRepository;
        _endpointRepository = endpointRepository;
        _parameterRepository = parameterRepository;
        _responseRepository = responseRepository;
        _securityReqRepository = securityReqRepository;
        _securitySchemeRepository = securitySchemeRepository;
        _storageGateway = storageGateway;
        _parsers = parsers;
        _logger = logger;
    }

    public async Task HandleAsync(ParseUploadedSpecificationCommand command, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("APIDOC_PARSE_STARTED: SpecId={SpecId}", command.SpecificationId);

        // 1. Load specification
        var spec = await _specRepository.FirstOrDefaultAsync(
            _specRepository.GetQueryableSet().Where(x => x.Id == command.SpecificationId));

        if (spec == null)
        {
            _logger.LogWarning("APIDOC_PARSE_SKIPPED_NOT_FOUND: SpecId={SpecId}", command.SpecificationId);
            return;
        }

        // 2. Idempotency guard: only parse if Pending
        if (spec.ParseStatus != ParseStatus.Pending)
        {
            _logger.LogInformation(
                "APIDOC_PARSE_SKIPPED_NOT_PENDING: SpecId={SpecId}, CurrentStatus={Status}",
                command.SpecificationId, spec.ParseStatus);
            return;
        }

        // 3. Ensure we have a file to download
        if (!spec.OriginalFileId.HasValue)
        {
            await SetFailedStatusAsync(spec, new List<string> { "No file associated with this specification." }, cancellationToken);
            return;
        }

        // 4. Select parser
        var parser = _parsers.FirstOrDefault(p => p.CanParse(spec.SourceType));
        if (parser == null)
        {
            await SetFailedStatusAsync(spec, new List<string> { $"No parser available for source type '{spec.SourceType}'." }, cancellationToken);
            return;
        }

        // 5. Download file from Storage module (transient failures will rethrow for outbox retry)
        byte[] fileContent;
        string fileName;
        try
        {
            var downloadResult = await _storageGateway.DownloadAsync(spec.OriginalFileId.Value, cancellationToken);
            fileContent = downloadResult.Content;
            fileName = downloadResult.FileName;
        }
        catch (NotFoundException)
        {
            await SetFailedStatusAsync(spec, new List<string> { $"File '{spec.OriginalFileId.Value}' not found in storage." }, cancellationToken);
            return;
        }

        // 6. Parse the file
        SpecificationParseResult parseResult;
        try
        {
            parseResult = await parser.ParseAsync(fileContent, fileName, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "APIDOC_PARSE_FAILED_VALIDATION: SpecId={SpecId}, SourceType={SourceType}",
                command.SpecificationId, spec.SourceType);
            await SetFailedStatusAsync(spec, new List<string> { $"Parse error: {ex.Message}" }, cancellationToken);
            return;
        }

        // 7. Handle parse failure
        if (!parseResult.Success)
        {
            _logger.LogWarning(
                "APIDOC_PARSE_FAILED_VALIDATION: SpecId={SpecId}, Errors={Errors}",
                command.SpecificationId, string.Join("; ", parseResult.Errors));
            await SetFailedStatusAsync(spec, parseResult.Errors, cancellationToken);
            return;
        }

        // 8. Persist parsed data (replace-all in transaction)
        try
        {
            await _specRepository.UnitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                // Delete existing children
                var existingEndpoints = await _endpointRepository.ToListAsync(
                    _endpointRepository.GetQueryableSet().Where(x => x.ApiSpecId == spec.Id));

                foreach (var endpoint in existingEndpoints)
                {
                    // Cascade delete handles parameters/responses/security reqs via DB config
                    _endpointRepository.Delete(endpoint);
                }

                var existingSchemes = await _securitySchemeRepository.ToListAsync(
                    _securitySchemeRepository.GetQueryableSet().Where(x => x.ApiSpecId == spec.Id));

                foreach (var scheme in existingSchemes)
                {
                    _securitySchemeRepository.Delete(scheme);
                }

                // Create new security schemes
                foreach (var parsedScheme in parseResult.SecuritySchemes)
                {
                    var scheme = new SecurityScheme
                    {
                        ApiSpecId = spec.Id,
                        Name = parsedScheme.Name,
                        Type = parsedScheme.SchemeType,
                        Scheme = parsedScheme.Scheme,
                        BearerFormat = parsedScheme.BearerFormat,
                        In = parsedScheme.ApiKeyLocation,
                        ParameterName = parsedScheme.ParameterName,
                        Configuration = parsedScheme.Configuration,
                    };

                    await _securitySchemeRepository.AddAsync(scheme, ct);
                }

                // Create new endpoints with children
                foreach (var parsedEndpoint in parseResult.Endpoints)
                {
                    if (!HttpMethodMap.TryGetValue(parsedEndpoint.HttpMethod ?? "GET", out var httpMethod))
                    {
                        httpMethod = Entities.HttpMethod.GET;
                    }

                    var endpoint = new ApiEndpoint
                    {
                        ApiSpecId = spec.Id,
                        HttpMethod = httpMethod,
                        Path = parsedEndpoint.Path?.Trim(),
                        OperationId = parsedEndpoint.OperationId?.Trim(),
                        Summary = parsedEndpoint.Summary?.Trim(),
                        Description = parsedEndpoint.Description?.Trim(),
                        Tags = parsedEndpoint.Tags?.Count > 0
                            ? JsonSerializer.Serialize(parsedEndpoint.Tags)
                            : null,
                        IsDeprecated = parsedEndpoint.IsDeprecated,
                    };

                    await _endpointRepository.AddAsync(endpoint, ct);

                    // Add parameters
                    foreach (var parsedParam in parsedEndpoint.Parameters)
                    {
                        if (!LocationMap.TryGetValue(parsedParam.Location ?? "Query", out var location))
                        {
                            location = ParameterLocation.Query;
                        }

                        var parameter = new EndpointParameter
                        {
                            EndpointId = endpoint.Id,
                            Name = parsedParam.Name?.Trim(),
                            Location = location,
                            DataType = parsedParam.DataType ?? "string",
                            Format = parsedParam.Format?.Trim(),
                            IsRequired = parsedParam.IsRequired,
                            DefaultValue = parsedParam.DefaultValue,
                            Schema = parsedParam.Schema,
                            Examples = parsedParam.Examples,
                        };

                        await _parameterRepository.AddAsync(parameter, ct);
                    }

                    // Add responses
                    foreach (var parsedResponse in parsedEndpoint.Responses)
                    {
                        var response = new EndpointResponse
                        {
                            EndpointId = endpoint.Id,
                            StatusCode = parsedResponse.StatusCode,
                            Description = parsedResponse.Description?.Trim(),
                            Schema = parsedResponse.Schema,
                            Examples = parsedResponse.Examples,
                            Headers = parsedResponse.Headers,
                        };

                        await _responseRepository.AddAsync(response, ct);
                    }

                    // Add security requirements
                    foreach (var parsedSecReq in parsedEndpoint.SecurityRequirements)
                    {
                        var secReq = new EndpointSecurityReq
                        {
                            EndpointId = endpoint.Id,
                            SecurityType = parsedSecReq.SecurityType,
                            SchemeName = parsedSecReq.SchemeName,
                            Scopes = parsedSecReq.Scopes,
                        };

                        await _securityReqRepository.AddAsync(secReq, ct);
                    }
                }

                // Update spec metadata
                spec.ParseStatus = ParseStatus.Success;
                spec.ParsedAt = DateTimeOffset.UtcNow;
                spec.ParseErrors = null;

                if (!string.IsNullOrWhiteSpace(parseResult.DetectedVersion))
                {
                    spec.Version = parseResult.DetectedVersion;
                }

                await _specRepository.UnitOfWork.SaveChangesAsync(ct);
            }, cancellationToken: cancellationToken);

            _logger.LogInformation(
                "APIDOC_PARSE_SUCCEEDED: SpecId={SpecId}, EndpointCount={EndpointCount}, SecuritySchemeCount={SchemeCount}",
                command.SpecificationId, parseResult.Endpoints.Count, parseResult.SecuritySchemes.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "APIDOC_PARSE_FAILED_INFRA: SpecId={SpecId}", command.SpecificationId);

            // Rethrow to trigger outbox retry for transient infrastructure errors
            throw;
        }
    }

    private async Task SetFailedStatusAsync(ApiSpecification spec, List<string> errors, CancellationToken cancellationToken)
    {
        spec.ParseStatus = ParseStatus.Failed;
        spec.ParsedAt = DateTimeOffset.UtcNow;
        spec.ParseErrors = JsonSerializer.Serialize(errors);

        await _specRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "APIDOC_PARSE_FAILED_VALIDATION: SpecId={SpecId}, Errors={Errors}",
            spec.Id, string.Join("; ", errors));
    }
}
