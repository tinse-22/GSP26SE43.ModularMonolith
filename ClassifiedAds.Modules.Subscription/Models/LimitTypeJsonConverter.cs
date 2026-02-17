using System.Text.Json.Serialization;

namespace ClassifiedAds.Modules.Subscription.Models;

public sealed class LimitTypeJsonConverter : JsonStringEnumConverter
{
    public LimitTypeJsonConverter()
        : base(namingPolicy: null, allowIntegerValues: false)
    {
    }
}
