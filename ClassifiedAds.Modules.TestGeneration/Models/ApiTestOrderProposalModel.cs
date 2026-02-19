using ClassifiedAds.Modules.TestGeneration.Entities;
using System;
using System.Collections.Generic;

namespace ClassifiedAds.Modules.TestGeneration.Models;

public class ApiTestOrderProposalModel
{
    public Guid ProposalId { get; set; }

    public Guid TestSuiteId { get; set; }

    public int ProposalNumber { get; set; }

    public ProposalStatus Status { get; set; }

    public ProposalSource Source { get; set; }

    public List<ApiOrderItemModel> ProposedOrder { get; set; } = new();

    public List<ApiOrderItemModel> UserModifiedOrder { get; set; }

    public List<ApiOrderItemModel> AppliedOrder { get; set; }

    public string AiReasoning { get; set; }

    public object ConsideredFactors { get; set; }

    public Guid? ReviewedById { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }

    public string ReviewNotes { get; set; }

    public DateTimeOffset? AppliedAt { get; set; }

    public string RowVersion { get; set; }
}
