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

public class UpdateSrsRequirementCommand : ICommand
{
    public Guid ProjectId { get; set; }

    public Guid SrsDocumentId { get; set; }

    public Guid RequirementId { get; set; }

    public Guid CurrentUserId { get; set; }

    public string Title { get; set; }

    public string TestableConstraints { get; set; }

    public Guid? EndpointId { get; set; }

    public bool? IsReviewed { get; set; }

    public SrsRequirementModel Result { get; set; }
}

public class UpdateSrsRequirementCommandHandler : ICommandHandler<UpdateSrsRequirementCommand>
{
    private readonly IRepository<SrsRequirement, Guid> _requirementRepository;

    public UpdateSrsRequirementCommandHandler(IRepository<SrsRequirement, Guid> requirementRepository)
    {
        _requirementRepository = requirementRepository;
    }

    public async Task HandleAsync(UpdateSrsRequirementCommand command, CancellationToken cancellationToken = default)
    {
        var req = await _requirementRepository.FirstOrDefaultAsync(
            _requirementRepository.GetQueryableSet()
                .Where(x => x.Id == command.RequirementId && x.SrsDocumentId == command.SrsDocumentId));

        if (req == null)
        {
            throw new NotFoundException($"SrsRequirement {command.RequirementId} khong tim thay.");
        }

        if (!string.IsNullOrWhiteSpace(command.Title))
        {
            req.Title = command.Title;
        }

        if (command.TestableConstraints != null)
        {
            req.TestableConstraints = command.TestableConstraints;
        }

        if (command.EndpointId.HasValue)
        {
            req.EndpointId = command.EndpointId;
        }

        if (command.IsReviewed.HasValue)
        {
            req.IsReviewed = command.IsReviewed.Value;
            if (command.IsReviewed.Value)
            {
                req.ReviewedById = command.CurrentUserId;
                req.ReviewedAt = DateTimeOffset.UtcNow;
            }
        }

        await _requirementRepository.UpdateAsync(req, cancellationToken);
        await _requirementRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

        command.Result = SrsRequirementModel.FromEntity(req);
    }
}
