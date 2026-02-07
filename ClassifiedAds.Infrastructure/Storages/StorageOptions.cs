using ClassifiedAds.Infrastructure.Storages.Amazon;
using ClassifiedAds.Infrastructure.Storages.Azure;
using ClassifiedAds.Infrastructure.Storages.Firebase;
using ClassifiedAds.Infrastructure.Storages.Local;

namespace ClassifiedAds.Infrastructure.Storages;

public class StorageOptions
{
    public string Provider { get; set; }

    public string MasterEncryptionKey { get; set; }

    public LocalOptions Local { get; set; }

    public AzureBlobOption Azure { get; set; }

    public AmazonOptions Amazon { get; set; }

    public FirebaseOptions Firebase { get; set; }

    public bool UsedLocal()
    {
        return Provider == "Local";
    }

    public bool UsedAzure()
    {
        return Provider == "Azure";
    }

    public bool UsedAmazon()
    {
        return Provider == "Amazon";
    }

    public bool UsedFirebase()
    {
        return Provider == "Firebase";
    }

    public bool UsedFake()
    {
        return Provider == "Fake";
    }
}
