using ClassifiedAds.Infrastructure.Notification;
using ClassifiedAds.Modules.Notification.EmailQueue;

namespace ClassifiedAds.Modules.Notification.ConfigurationOptions;

public class NotificationModuleOptions : NotificationOptions
{
    public ConnectionStringsOptions ConnectionStrings { get; set; }

    /// <summary>
    /// Configuration for the in-memory Channel-based email queue + worker.
    /// See <see cref="EmailQueueOptions"/> for defaults.
    /// </summary>
    public EmailQueueOptions EmailQueue { get; set; } = new();
}
