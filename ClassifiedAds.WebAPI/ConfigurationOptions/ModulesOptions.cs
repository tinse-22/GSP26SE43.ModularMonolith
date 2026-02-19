using ClassifiedAds.Modules.AuditLog.ConfigurationOptions;
using ClassifiedAds.Modules.Configuration.ConfigurationOptions;
using ClassifiedAds.Modules.Identity.ConfigurationOptions;
using ClassifiedAds.Modules.Notification.ConfigurationOptions;
using ClassifiedAds.Modules.Storage.ConfigurationOptions;
using ClassifiedAds.Modules.TestGeneration.ConfigurationOptions;

namespace ClassifiedAds.WebAPI.ConfigurationOptions;

public class ModulesOptions
{
    public AuditLogModuleOptions AuditLog { get; set; }

    public ConfigurationModuleOptions Configuration { get; set; }

    public IdentityModuleOptions Identity { get; set; }

    public NotificationModuleOptions Notification { get; set; }

    public StorageModuleOptions Storage { get; set; }

    public TestGenerationModuleOptions TestGeneration { get; set; }
}
