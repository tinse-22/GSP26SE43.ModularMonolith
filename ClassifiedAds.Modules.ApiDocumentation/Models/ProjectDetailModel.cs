namespace ClassifiedAds.Modules.ApiDocumentation.Models;

public class ProjectDetailModel : ProjectModel
{
    public int TotalSpecifications { get; set; }

    public SpecSummaryModel ActiveSpecSummary { get; set; }
}
