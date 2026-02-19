using ClassifiedAds.Modules.TestGeneration.Entities;
using System;

namespace ClassifiedAds.Modules.TestGeneration.Models;

public class ApiTestOrderGateStatusModel
{
    public Guid TestSuiteId { get; set; }

    public bool IsGatePassed { get; set; }

    public string ReasonCode { get; set; }

    public Guid? ActiveProposalId { get; set; }

    public ProposalStatus? ActiveProposalStatus { get; set; }

    public int OrderSize { get; set; }

    public DateTimeOffset EvaluatedAt { get; set; }
}
