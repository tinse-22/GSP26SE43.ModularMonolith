using ClassifiedAds.Domain.Infrastructure.Messaging;
using ClassifiedAds.Modules.Storage.DTOs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.Storage.MessageBusConsumers;

public sealed class WebhookConsumer :
    IMessageBusConsumer<WebhookConsumer, FileUploadedEvent>,
    IMessageBusConsumer<WebhookConsumer, FileDeletedEvent>
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WebhookConsumer> _logger;
    private readonly IConfiguration _configuration;

    public WebhookConsumer(ILogger<WebhookConsumer> logger,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
    }

    public async Task HandleAsync(FileUploadedEvent data, MetaData metaData, CancellationToken cancellationToken = default)
    {
        var url = _configuration["Modules:Storage:Webhooks:FileUploadedEvent:PayloadUrl"];
        using var httpClient = _httpClientFactory.CreateClient(nameof(WebhookConsumer));
        await httpClient.PostAsJsonAsync(url, data.FileEntry, cancellationToken: cancellationToken);
    }

    public async Task HandleAsync(FileDeletedEvent data, MetaData metaData, CancellationToken cancellationToken = default)
    {
        var url = _configuration["Modules:Storage:Webhooks:FileDeletedEvent:PayloadUrl"];
        using var httpClient = _httpClientFactory.CreateClient(nameof(WebhookConsumer));
        await httpClient.PostAsJsonAsync(url, data.FileEntry, cancellationToken: cancellationToken);
    }
}
