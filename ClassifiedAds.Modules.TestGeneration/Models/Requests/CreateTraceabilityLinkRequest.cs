using System;

namespace ClassifiedAds.Modules.TestGeneration.Models.Requests;

public class CreateTraceabilityLinkRequest
{
    public Guid TestCaseId { get; set; }

    public Guid SrsRequirementId { get; set; }
}
