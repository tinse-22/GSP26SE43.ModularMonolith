using ClassifiedAds.Modules.TestExecution.Models;
using ClassifiedAds.Modules.TestExecution.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.TestExecution;

public class HttpTestExecutorTests
{
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly HttpTestExecutor _executor;

    public HttpTestExecutorTests()
    {
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _executor = new HttpTestExecutor(
            _httpClientFactoryMock.Object,
            new Mock<ILogger<HttpTestExecutor>>().Object);
    }

    [Fact]
    public async Task ExecuteAsync_SuccessfulGet_ShouldReturnStatusCodeAndBody()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"id\":1}", System.Text.Encoding.UTF8, "application/json"),
        });
        var client = new HttpClient(handler);
        _httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(client);

        var request = CreateRequest("GET", "https://api.example.com/items/1");

        // Act
        var result = await _executor.ExecuteAsync(request);

        // Assert
        result.StatusCode.Should().Be(200);
        result.Body.Should().Contain("\"id\":1");
        result.TransportError.Should().BeNullOrEmpty();
        result.LatencyMs.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task ExecuteAsync_PostWithBody_ShouldSendBody()
    {
        // Arrange
        HttpRequestMessage capturedRequest = null;
        var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = new StringContent("{\"created\":true}"),
        }, msg => capturedRequest = CloneRequest(msg));

        var client = new HttpClient(handler);
        _httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(client);

        var request = CreateRequest("POST", "https://api.example.com/items",
            body: "{\"name\":\"test\"}", bodyType: "JSON");

        // Act
        var result = await _executor.ExecuteAsync(request);

        // Assert
        result.StatusCode.Should().Be(201);
    }

    [Fact]
    public async Task ExecuteAsync_DeleteWithBody_ShouldSendBody()
    {
        // Arrange
        string capturedBody = null;
        string capturedContentType = null;

        var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"deleted\":true}"),
        }, msg =>
        {
            if (msg.Content != null)
            {
                capturedBody = msg.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                capturedContentType = msg.Content.Headers.ContentType?.MediaType;
            }
        });

        var client = new HttpClient(handler);
        _httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(client);

        var request = CreateRequest(
            "DELETE",
            "https://api.example.com/items/1",
            body: "{\"reason\":\"cleanup\"}",
            bodyType: "JSON");

        // Act
        var result = await _executor.ExecuteAsync(request);

        // Assert
        result.StatusCode.Should().Be(200);
        capturedBody.Should().Be("{\"reason\":\"cleanup\"}");
        capturedContentType.Should().Be("application/json");
    }

    [Fact]
    public async Task ExecuteAsync_WithQueryParams_ShouldAppendToUrl()
    {
        // Arrange
        Uri capturedUri = null;
        var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}"),
        }, msg => capturedUri = msg.RequestUri);

        var client = new HttpClient(handler);
        _httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(client);

        var request = CreateRequest("GET", "https://api.example.com/search");
        request.QueryParams = new Dictionary<string, string>
        {
            ["q"] = "test",
            ["page"] = "1",
        };

        // Act
        await _executor.ExecuteAsync(request);

        // Assert
        capturedUri.Should().NotBeNull();
        capturedUri.Query.Should().Contain("q=test");
        capturedUri.Query.Should().Contain("page=1");
    }

    [Fact]
    public async Task ExecuteAsync_WithNullQueryParamValue_ShouldOmitThatParam()
    {
        // Arrange
        Uri capturedUri = null;
        var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}"),
        }, msg => capturedUri = msg.RequestUri);

        var client = new HttpClient(handler);
        _httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(client);

        var request = CreateRequest("GET", "https://api.example.com/search");
        request.QueryParams = new Dictionary<string, string>
        {
            ["q"] = "test",
            ["optional"] = null,
        };

        // Act
        var result = await _executor.ExecuteAsync(request);

        // Assert
        result.TransportError.Should().BeNullOrEmpty();
        capturedUri.Should().NotBeNull();
        capturedUri.Query.Should().Contain("q=test");
        capturedUri.Query.Should().NotContain("optional");
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyQueryParamValue_ShouldKeepEmptyAssignment()
    {
        // Arrange
        Uri capturedUri = null;
        var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}"),
        }, msg => capturedUri = msg.RequestUri);

        var client = new HttpClient(handler);
        _httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(client);

        var request = CreateRequest("GET", "https://api.example.com/search");
        request.QueryParams = new Dictionary<string, string>
        {
            ["q"] = string.Empty,
        };

        // Act
        var result = await _executor.ExecuteAsync(request);

        // Assert
        result.TransportError.Should().BeNullOrEmpty();
        capturedUri.Should().NotBeNull();
        capturedUri.Query.Should().Contain("q=");
    }

    [Fact]
    public async Task ExecuteAsync_HttpRequestException_ShouldReturnTransportError()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler(exception: new HttpRequestException("Connection refused"));
        var client = new HttpClient(handler);
        _httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(client);

        var request = CreateRequest("GET", "https://api.example.com/down");

        // Act
        var result = await _executor.ExecuteAsync(request);

        // Assert
        result.StatusCode.Should().BeNull();
        result.TransportError.Should().NotBeNullOrEmpty();
        result.TransportError.Should().Contain("Connection refused");
    }

    [Fact]
    public async Task ExecuteAsync_Timeout_ShouldReturnTransportError()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler(
            exception: new TaskCanceledException("timeout", new TimeoutException()));
        var client = new HttpClient(handler);
        _httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(client);

        var request = CreateRequest("GET", "https://api.example.com/slow");
        request.TimeoutMs = 1000;

        // Act
        var result = await _executor.ExecuteAsync(request);

        // Assert
        result.StatusCode.Should().BeNull();
        result.TransportError.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ResponseHeaders_ShouldBeCaptured()
    {
        // Arrange
        var responseMsg = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("ok"),
        };
        responseMsg.Headers.Add("X-Request-Id", "abc-123");

        var handler = new FakeHttpMessageHandler(responseMsg);
        var client = new HttpClient(handler);
        _httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(client);

        var request = CreateRequest("GET", "https://api.example.com/test");

        // Act
        var result = await _executor.ExecuteAsync(request);

        // Assert
        result.Headers.Should().ContainKey("X-Request-Id");
        result.Headers["X-Request-Id"].Should().Be("abc-123");
    }

    [Fact]
    public async Task ExecuteAsync_CustomHeaders_ShouldBeSent()
    {
        // Arrange
        HttpRequestMessage capturedRequest = null;
        var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("ok"),
        }, msg =>
        {
            capturedRequest = new HttpRequestMessage();
            foreach (var h in msg.Headers)
            {
                capturedRequest.Headers.TryAddWithoutValidation(h.Key, h.Value);
            }
        });

        var client = new HttpClient(handler);
        _httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(client);

        var request = CreateRequest("GET", "https://api.example.com/test");
        request.Headers = new Dictionary<string, string>
        {
            ["Authorization"] = "Bearer test-token",
            ["X-Custom"] = "value",
        };

        // Act
        await _executor.ExecuteAsync(request);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest.Headers.Contains("Authorization").Should().BeTrue();
        capturedRequest.Headers.Contains("X-Custom").Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_UrlEncodedBody_ShouldSendFormUrlEncodedContent()
    {
        string capturedBody = null;
        string capturedContentType = null;

        var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}"),
        }, msg =>
        {
            capturedBody = msg.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            capturedContentType = msg.Content?.Headers.ContentType?.MediaType;
        });

        var client = new HttpClient(handler);
        _httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(client);

        var request = CreateRequest(
            "POST",
            "https://api.example.com/pet/1",
            body: "{\"name\":\"Sample Name\",\"status\":\"available\"}",
            bodyType: "UrlEncoded");

        var result = await _executor.ExecuteAsync(request);

        result.StatusCode.Should().Be(200);
        capturedContentType.Should().Be("application/x-www-form-urlencoded");
        capturedBody.Should().Contain("name=Sample+Name");
        capturedBody.Should().Contain("status=available");
    }

    [Fact]
    public async Task ExecuteAsync_FormDataBody_ShouldSendMultipartContent()
    {
        string capturedBody = null;
        string capturedContentType = null;

        var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}"),
        }, msg =>
        {
            capturedBody = msg.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            capturedContentType = msg.Content?.Headers.ContentType?.MediaType;
        });

        var client = new HttpClient(handler);
        _httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(client);

        var request = CreateRequest(
            "POST",
            "https://api.example.com/pet/1/uploadImage",
            body: "{\"additionalMetadata\":\"sample meta\",\"file\":\"sample-file.txt\"}",
            bodyType: "FormData");

        var result = await _executor.ExecuteAsync(request);

        result.StatusCode.Should().Be(200);
        capturedContentType.Should().Be("multipart/form-data");
        capturedBody.Should().Contain("name=additionalMetadata");
        capturedBody.Should().Contain("sample meta");
        capturedBody.Should().Contain("filename=sample-file.txt");
    }

    #region Helpers

    private static ResolvedTestCaseRequest CreateRequest(
        string method,
        string url,
        string body = null,
        string bodyType = null)
    {
        return new ResolvedTestCaseRequest
        {
            TestCaseId = Guid.NewGuid(),
            Name = "Test",
            HttpMethod = method,
            ResolvedUrl = url,
            Headers = new Dictionary<string, string>(),
            QueryParams = new Dictionary<string, string>(),
            Body = body,
            BodyType = bodyType,
            TimeoutMs = 30000,
        };
    }

    private static HttpRequestMessage CloneRequest(HttpRequestMessage msg)
    {
        var clone = new HttpRequestMessage(msg.Method, msg.RequestUri);
        foreach (var h in msg.Headers)
        {
            clone.Headers.TryAddWithoutValidation(h.Key, h.Value);
        }

        return clone;
    }

    private class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;
        private readonly Exception _exception;
        private readonly Action<HttpRequestMessage> _onSend;

        public FakeHttpMessageHandler(
            HttpResponseMessage response = null,
            Action<HttpRequestMessage> onSend = null,
            Exception exception = null)
        {
            _response = response;
            _onSend = onSend;
            _exception = exception;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            _onSend?.Invoke(request);

            if (_exception != null)
            {
                throw _exception;
            }

            return Task.FromResult(_response);
        }
    }

    #endregion
}
