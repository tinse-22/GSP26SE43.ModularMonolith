using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
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

namespace ClassifiedAds.Modules.ApiDocumentation.Queries;

public class GetSpecificationQuery : IQuery<SpecificationDetailModel>
{
    public Guid SpecId { get; set; }

    public Guid ProjectId { get; set; }

    public Guid OwnerId { get; set; }
}

public class GetSpecificationQueryHandler : IQueryHandler<GetSpecificationQuery, SpecificationDetailModel>
{
    private readonly IRepository<Project, Guid> _projectRepository;
    private readonly IRepository<ApiSpecification, Guid> _specRepository;
    private readonly IRepository<ApiEndpoint, Guid> _endpointRepository;

    public GetSpecificationQueryHandler(
        IRepository<Project, Guid> projectRepository,
        IRepository<ApiSpecification, Guid> specRepository,
        IRepository<ApiEndpoint, Guid> endpointRepository)
    {
        _projectRepository = projectRepository;
        _specRepository = specRepository;
        _endpointRepository = endpointRepository;
    }

    public async Task<SpecificationDetailModel> HandleAsync(GetSpecificationQuery query, CancellationToken cancellationToken = default)
    {
        // Verify project exists and ownership
        var project = await _projectRepository.FirstOrDefaultAsync(
            _projectRepository.GetQueryableSet().Where(p => p.Id == query.ProjectId));

        if (project == null)
        {
            throw new NotFoundException($"Không tìm thấy project với mã '{query.ProjectId}'.");
        }

        if (project.OwnerId != query.OwnerId)
        {
            throw new NotFoundException($"Không tìm thấy project với mã '{query.ProjectId}'.");
        }

        // Load specification
        var spec = await _specRepository.FirstOrDefaultAsync(
            _specRepository.GetQueryableSet().Where(s => s.Id == query.SpecId && s.ProjectId == query.ProjectId));

        if (spec == null)
        {
            throw new NotFoundException($"Không tìm thấy specification với mã '{query.SpecId}'.");
        }

        // Count endpoints
        var endpointCount = await _endpointRepository.GetQueryableSet()
            .Where(e => e.ApiSpecId == spec.Id)
            .CountAsync(cancellationToken);

        // Parse errors from JSON
        List<string> parseErrors = null;
        if (!string.IsNullOrEmpty(spec.ParseErrors))
        {
            try
            {
                parseErrors = JsonSerializer.Deserialize<List<string>>(spec.ParseErrors);
            }
            catch
            {
                parseErrors = new List<string> { spec.ParseErrors };
            }
        }

        return new SpecificationDetailModel
        {
            Id = spec.Id,
            ProjectId = spec.ProjectId,
            Name = spec.Name,
            SourceType = spec.SourceType.ToString(),
            Version = spec.Version,
            IsActive = spec.IsActive,
            ParseStatus = spec.ParseStatus.ToString(),
            ParsedAt = spec.ParsedAt,
            OriginalFileId = spec.OriginalFileId,
            CreatedDateTime = spec.CreatedDateTime,
            UpdatedDateTime = spec.UpdatedDateTime,
            EndpointCount = endpointCount,
            ParseErrors = parseErrors,
        };
    }
}
