using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ClassifiedAds.Modules.TestExecution.Models.Requests;

public class CreateExecutionEnvironmentRequest
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; }

    [Required]
    [MaxLength(500)]
    public string BaseUrl { get; set; }

    public Dictionary<string, string> Variables { get; set; }

    public Dictionary<string, string> Headers { get; set; }

    public ExecutionAuthConfigModel AuthConfig { get; set; }

    public bool IsDefault { get; set; }
}
