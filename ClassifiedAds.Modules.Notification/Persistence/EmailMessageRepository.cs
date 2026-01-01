using ClassifiedAds.CrossCuttingConcerns.DateTimes;
using ClassifiedAds.Modules.Notification.Entities;
using EntityFrameworkCore.PostgreSQL.SimpleBulks.BulkDelete;
using EntityFrameworkCore.PostgreSQL.SimpleBulks.BulkInsert;
using MapItEasy;
using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.Notification.Persistence;

public class EmailMessageRepository : Repository<EmailMessage, Guid>, IEmailMessageRepository
{
    private static readonly IMapper _mapper = new ExpressionMapper();
    private readonly NotificationDbContext _dbContext;

    public EmailMessageRepository(NotificationDbContext dbContext,
        IDateTimeProvider dateTimeProvider)
        : base(dbContext, dateTimeProvider)
    {
        _dbContext = dbContext;
    }

    public async Task<int> ArchiveMessagesAsync(CancellationToken cancellationToken = default)
    {
        var archivedDate = DateTime.Now.AddDays(-30);

        var messagesToArchive = _dbContext.Set<EmailMessage>()
        .Where(x => x.CreatedDateTime < archivedDate)
        .ToList();

        if (messagesToArchive.Count == 0)
        {
            return 0;
        }

        var archivedMessages = messagesToArchive.Select(x => _mapper.Map<EmailMessage, ArchivedEmailMessage>(x)).ToList();

        await UnitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _dbContext.BulkInsertAsync(archivedMessages,
                new BulkInsertOptions
                {
                    KeepIdentity = true
                }, cancellationToken: ct);
            await _dbContext.BulkDeleteAsync(messagesToArchive, cancellationToken: ct);
        }, IsolationLevel.ReadCommitted, cancellationToken);

        return messagesToArchive.Count;
    }
}
