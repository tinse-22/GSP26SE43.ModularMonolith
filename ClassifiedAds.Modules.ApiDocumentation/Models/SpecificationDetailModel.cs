using System.Collections.Generic;

namespace ClassifiedAds.Modules.ApiDocumentation.Models;

public class SpecificationDetailModel : SpecificationModel
{
    public int EndpointCount { get; set; }

    public List<string> ParseErrors { get; set; }

    public string OriginalFileName { get; set; }
}
