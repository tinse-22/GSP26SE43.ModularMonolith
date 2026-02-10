using ClassifiedAds.Modules.ApiDocumentation.Entities;
using Microsoft.AspNetCore.Http;

namespace ClassifiedAds.Modules.ApiDocumentation.Models;

public class UploadSpecificationModel
{
    public SpecificationUploadMethod UploadMethod { get; set; } = SpecificationUploadMethod.StorageGatewayContract;

    public IFormFile File { get; set; }

    public string Name { get; set; }

    public SourceType SourceType { get; set; }

    public string Version { get; set; }

    public bool AutoActivate { get; set; }
}
