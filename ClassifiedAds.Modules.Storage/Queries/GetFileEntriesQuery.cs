using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.Storage.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.Storage.Queries;

public class GetFileEntriesQuery : IQuery<List<FileEntry>>
{
    public Guid CurrentUserId { get; set; }
}

public class GetFileEntriesQueryHandler : IQueryHandler<GetFileEntriesQuery, List<FileEntry>>
{
    private readonly IRepository<FileEntry, Guid> _repository;

    public GetFileEntriesQueryHandler(IRepository<FileEntry, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<List<FileEntry>> HandleAsync(GetFileEntriesQuery query, CancellationToken cancellationToken = default)
    {
        if (query.CurrentUserId == Guid.Empty)
        {
            throw new ValidationException("CurrentUserId la bat buoc.");
        }

        return await _repository.ToListAsync(
            _repository.GetQueryableSet()
                .Where(x => !x.Deleted && x.OwnerId == query.CurrentUserId));
    }
}
