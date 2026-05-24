namespace ClassifiedAds.Modules.TestGeneration.Models;

public class ScenarioBudget
{
    public int SoftLimit { get; set; }

    public int HardLimit { get; set; }

    public int Target { get; set; }

    public string Reason { get; set; } = string.Empty;
}
