using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Modules.TestGeneration.ConfigurationOptions;
using ClassifiedAds.Modules.TestGeneration.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.TestGeneration;

public class N8nIntegrationServiceTests
{
    [Fact]
    public async Task TriggerWebhookAsync_Should_DeserializeValidJsonResponse()
    {
        HttpRequestMessage? capturedRequest = null;
        var sut = CreateSut(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"message\":\"ok\",\"count\":2}", Encoding.UTF8, "application/json"),
            },
            request => capturedRequest = request);

        var result = await sut.TriggerWebhookAsync<object, TestWebhookResponse>(
            "test-hook",
            new { Name = "payload" });

        result.Message.Should().Be("ok");
        result.Count.Should().Be(2);
        capturedRequest.Should().NotBeNull();
        capturedRequest.RequestUri.Should().Be(new Uri("https://example.test/webhook/my-hook"));
        capturedRequest.Headers.Should().Contain(header => header.Key == "x-api-key");
    }

    [Fact]
    public async Task TriggerWebhookAsync_Should_ThrowValidation_WhenResponseBodyIsEmpty()
    {
        var sut = CreateSut(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("   ", Encoding.UTF8, "application/json"),
            });

        var act = () => sut.TriggerWebhookAsync<object, TestWebhookResponse>(
            "test-hook",
            new { Name = "payload" });

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*body rong*");
    }

    [Fact]
    public async Task TriggerWebhookAsync_Should_ThrowValidation_WhenResponseIsNoContent()
    {
        var sut = CreateSut(new HttpResponseMessage(HttpStatusCode.NoContent));

        var act = () => sut.TriggerWebhookAsync<object, TestWebhookResponse>(
            "test-hook",
            new { Name = "payload" });

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*HTTP 204*");
    }

    [Fact]
    public async Task TriggerWebhookAsync_Should_ThrowValidation_WhenJsonIsMalformed()
    {
        var sut = CreateSut(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"message\":", Encoding.UTF8, "application/json"),
            });

        var act = () => sut.TriggerWebhookAsync<object, TestWebhookResponse>(
            "test-hook",
            new { Name = "payload" });

        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.InnerException.Should().BeOfType<System.Text.Json.JsonException>();
        exception.Which.Message.Should().Contain("JSON khong hop le");
    }

    [Fact]
    public async Task TriggerWebhookAsync_Should_ThrowTransientException_WhenStatusIs524()
    {
        var sut = CreateSut(
            new HttpResponseMessage((HttpStatusCode)524)
            {
                Content = new StringContent("upstream timeout", Encoding.UTF8, "text/plain"),
            });

        var act = () => sut.TriggerWebhookAsync<object, TestWebhookResponse>(
            "test-hook",
            new { Name = "payload" });

        var exception = await act.Should().ThrowAsync<N8nTransientException>();
        exception.Which.StatusCode.Should().Be(524);
        exception.Which.IsTimeout.Should().BeTrue();
        exception.Which.WebhookName.Should().Be("test-hook");
    }

    [Fact]
    public async Task TriggerWebhookAsync_Should_ThrowTransientException_WhenRequestTimesOut()
    {
        var sut = CreateSut((_, _) =>
            Task.FromException<HttpResponseMessage>(new TaskCanceledException("timeout", new TimeoutException())));

        var act = () => sut.TriggerWebhookAsync<object, TestWebhookResponse>(
            "test-hook",
            new { Name = "payload" });

        var exception = await act.Should().ThrowAsync<N8nTransientException>();
        exception.Which.IsTimeout.Should().BeTrue();
        exception.Which.WebhookName.Should().Be("test-hook");
    }

    private static N8nIntegrationService CreateSut(
        HttpResponseMessage response,
        Action<HttpRequestMessage>? onRequest = null)
    {
        return CreateSut((request, cancellationToken) => Task.FromResult(response), onRequest);
    }

    private static N8nIntegrationService CreateSut(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory,
        Action<HttpRequestMessage>? onRequest = null)
    {
        var handler = new StubHttpMessageHandler((request, cancellationToken) =>
        {
            onRequest?.Invoke(request);
            return responseFactory(request, cancellationToken);
        });

        var client = new HttpClient(handler);
        var options = Options.Create(new N8nIntegrationOptions
        {
            BaseUrl = "https://example.test/webhook",
            ApiKey = "secret-key",
            Webhooks = new Dictionary<string, string>
            {
                ["test-hook"] = "my-hook",
            },
        });

        return new N8nIntegrationService(
            client,
            options,
            new Mock<ILogger<N8nIntegrationService>>().Object);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _handler(request, cancellationToken);
        }
    }

    private sealed class TestWebhookResponse
    {
        public string Message { get; set; } = string.Empty;

        public int Count { get; set; }
    }
}
