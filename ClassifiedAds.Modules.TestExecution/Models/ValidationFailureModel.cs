namespace ClassifiedAds.Modules.TestExecution.Models;

public class ValidationFailureModel
{
    public string Code { get; set; }

    public string Message { get; set; }

    public string Target { get; set; }

    public string Expected { get; set; }

    public string Actual { get; set; }
}
