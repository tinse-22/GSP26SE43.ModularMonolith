using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Commands;

public class AnswerSrsRequirementClarificationCommand : ICommand
{
    public Guid SrsRequirementId { get; set; }

    public Guid ClarificationId { get; set; }

    public Guid CurrentUserId { get; set; }

    public string UserAnswer { get; set; }

    public SrsRequirementClarificationModel Result { get; set; }
}

public class AnswerSrsRequirementClarificationCommandHandler : ICommandHandler<AnswerSrsRequirementClarificationCommand>
{
    private readonly IRepository<SrsRequirementClarification, Guid> _clarificationRepository;

    public AnswerSrsRequirementClarificationCommandHandler(IRepository<SrsRequirementClarification, Guid> clarificationRepository)
    {
        _clarificationRepository = clarificationRepository;
    }

    public async Task HandleAsync(AnswerSrsRequirementClarificationCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.UserAnswer))
        {
            throw new ValidationException("UserAnswer la bat buoc.");
        }

        var clar = await _clarificationRepository.FirstOrDefaultAsync(
            _clarificationRepository.GetQueryableSet()
                .Where(x => x.Id == command.ClarificationId && x.SrsRequirementId == command.SrsRequirementId));

        if (clar == null)
        {
            throw new NotFoundException($"Clarification {command.ClarificationId} khong tim thay.");
        }

        clar.UserAnswer = command.UserAnswer;
        clar.IsAnswered = true;
        clar.AnsweredAt = DateTimeOffset.UtcNow;
        clar.AnsweredById = command.CurrentUserId;

        await _clarificationRepository.UpdateAsync(clar, cancellationToken);
        await _clarificationRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

        command.Result = SrsRequirementClarificationModel.FromEntity(clar);
    }
}
