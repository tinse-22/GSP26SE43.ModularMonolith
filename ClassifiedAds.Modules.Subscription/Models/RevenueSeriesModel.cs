using System.Collections.Generic;

namespace ClassifiedAds.Modules.Subscription.Models;

public class RevenuePointModel
{
    public string Period { get; set; }

    public decimal Amount { get; set; }

    public int TransactionCount { get; set; }
}

public class RevenueSeriesModel
{
    public string Currency { get; set; }

    public string GroupBy { get; set; }

    public string From { get; set; }

    public string To { get; set; }

    public decimal TotalAmount { get; set; }

    public int TotalTransactions { get; set; }

    public List<RevenuePointModel> Points { get; set; } = new();
}
