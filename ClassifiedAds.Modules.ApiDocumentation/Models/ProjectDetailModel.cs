using System.Collections.Generic;

namespace ClassifiedAds.Modules.ApiDocumentation.Models;

public class ProjectDetailModel : ProjectModel
{
    public int TotalSpecifications { get; set; }

    public SpecSummaryModel ActiveSpecSummary { get; set; }

    public List<SpecificationModel> Specifications { get; set; } = new();
}
