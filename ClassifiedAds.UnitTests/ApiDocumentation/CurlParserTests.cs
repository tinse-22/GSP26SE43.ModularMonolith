using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Modules.ApiDocumentation.Services;

namespace ClassifiedAds.UnitTests.ApiDocumentation;

public class CurlParserTests
{
    [Fact]
    public void Parse_SimpleGetRequest_Should_ExtractMethodAndUrl()
    {
        // Arrange
        var curl = "curl https://api.example.com/users";

        // Act
        var result = CurlParser.Parse(curl);

        // Assert
        result.Method.Should().Be("GET");
        result.Url.Should().Be("https://api.example.com/users");
        result.Host.Should().Be("api.example.com");
        result.Path.Should().Be("/users");
    }

    [Fact]
    public void Parse_ExplicitGetMethod_Should_SetMethodCorrectly()
    {
        // Arrange
        var curl = "curl -X GET https://api.example.com/users";

        // Act
        var result = CurlParser.Parse(curl);

        // Assert
        result.Method.Should().Be("GET");
        result.Path.Should().Be("/users");
    }

    [Fact]
    public void Parse_PostWithData_Should_AutoDetectPostMethod()
    {
        // Arrange
        var curl = "curl -d '{\"name\":\"test\"}' https://api.example.com/users";

        // Act
        var result = CurlParser.Parse(curl);

        // Assert
        result.Method.Should().Be("POST");
        result.Body.Should().Be("{\"name\":\"test\"}");
    }

    [Fact]
    public void Parse_ExplicitPutMethod_Should_OverrideAutoDetection()
    {
        // Arrange
        var curl = "curl -X PUT -d '{\"name\":\"updated\"}' https://api.example.com/users/1";

        // Act
        var result = CurlParser.Parse(curl);

        // Assert
        result.Method.Should().Be("PUT");
        result.Body.Should().Be("{\"name\":\"updated\"}");
    }

    [Fact]
    public void Parse_WithHeaders_Should_ExtractHeaders()
    {
        // Arrange
        var curl = "curl -H 'Content-Type: application/json' -H 'Authorization: Bearer token123' https://api.example.com/data";

        // Act
        var result = CurlParser.Parse(curl);

        // Assert
        result.Headers.Should().ContainKey("Content-Type");
        result.Headers["Content-Type"].Should().Be("application/json");
        result.Headers.Should().ContainKey("Authorization");
        result.Headers["Authorization"].Should().Be("Bearer token123");
        result.ContentType.Should().Be("application/json");
    }

    [Fact]
    public void Parse_WithQueryParameters_Should_ExtractParams()
    {
        // Arrange
        var curl = "curl 'https://api.example.com/search?q=hello&page=2&limit=10'";

        // Act
        var result = CurlParser.Parse(curl);

        // Assert
        result.Path.Should().Be("/search");
        result.QueryParams.Should().ContainKey("q").WhoseValue.Should().Be("hello");
        result.QueryParams.Should().ContainKey("page").WhoseValue.Should().Be("2");
        result.QueryParams.Should().ContainKey("limit").WhoseValue.Should().Be("10");
    }

    [Fact]
    public void Parse_HeadFlag_Should_SetMethodToHead()
    {
        // Arrange
        var curl = "curl -I https://api.example.com/health";

        // Act
        var result = CurlParser.Parse(curl);

        // Assert
        result.Method.Should().Be("HEAD");
    }

    [Fact]
    public void Parse_WithBasicAuth_Should_ExtractCredentials()
    {
        // Arrange
        var curl = "curl -u user:password https://api.example.com/protected";

        // Act
        var result = CurlParser.Parse(curl);

        // Assert
        result.AuthType.Should().Be("Basic");
        result.AuthCredentials.Should().Be("user:password");
    }

    [Fact]
    public void Parse_WithLineContinuations_Should_NormalizeAndParse()
    {
        // Arrange
        var curl = "curl \\\n  -X POST \\\n  -H 'Content-Type: application/json' \\\n  https://api.example.com/data";

        // Act
        var result = CurlParser.Parse(curl);

        // Assert
        result.Method.Should().Be("POST");
        result.ContentType.Should().Be("application/json");
        result.Path.Should().Be("/data");
    }

    [Fact]
    public void Parse_WithDoubleQuotedStrings_Should_HandleCorrectly()
    {
        // Arrange
        var curl = "curl -X POST -H \"Content-Type: application/json\" -d \"{\\\"key\\\": \\\"value\\\"}\" https://api.example.com/data";

        // Act
        var result = CurlParser.Parse(curl);

        // Assert
        result.Method.Should().Be("POST");
        result.ContentType.Should().Be("application/json");
        result.Body.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Parse_WithDataRawFlag_Should_ExtractBody()
    {
        // Arrange
        var curl = "curl --data-raw '{\"name\":\"test\"}' https://api.example.com/users";

        // Act
        var result = CurlParser.Parse(curl);

        // Assert
        result.Method.Should().Be("POST");
        result.Body.Should().Be("{\"name\":\"test\"}");
    }

    [Fact]
    public void Parse_NullOrEmpty_Should_ThrowValidationException()
    {
        // Act & Assert
        var act1 = () => CurlParser.Parse(null);
        act1.Should().Throw<ValidationException>();

        var act2 = () => CurlParser.Parse("");
        act2.Should().Throw<ValidationException>();

        var act3 = () => CurlParser.Parse("   ");
        act3.Should().Throw<ValidationException>();
    }

    [Fact]
    public void Parse_NotStartingWithCurl_Should_ThrowValidationException()
    {
        // Arrange
        var input = "wget https://example.com";

        // Act
        var act = () => CurlParser.Parse(input);

        // Assert
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void Parse_WithNoUrl_Should_ThrowValidationException()
    {
        // Arrange
        var input = "curl -X GET -H 'Authorization: Bearer token'";

        // Act
        var act = () => CurlParser.Parse(input);

        // Assert
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void Parse_WithFileReference_Should_ThrowValidationException()
    {
        // Arrange
        var curl = "curl -d @data.json https://api.example.com/data";

        // Act
        var act = () => CurlParser.Parse(curl);

        // Assert
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void Parse_DefaultContentTypeForBody_Should_BeFormUrlEncoded()
    {
        // Arrange
        var curl = "curl -d 'param=value' https://api.example.com/data";

        // Act
        var result = CurlParser.Parse(curl);

        // Assert
        result.ContentType.Should().Be("application/x-www-form-urlencoded");
    }

    [Fact]
    public void Parse_DeleteMethod_Should_ParseCorrectly()
    {
        // Arrange
        var curl = "curl -X DELETE https://api.example.com/users/123";

        // Act
        var result = CurlParser.Parse(curl);

        // Assert
        result.Method.Should().Be("DELETE");
        result.Path.Should().Be("/users/123");
    }

    [Fact]
    public void Parse_PatchMethod_Should_ParseCorrectly()
    {
        // Arrange
        var curl = "curl -X PATCH -d '{\"status\":\"active\"}' -H 'Content-Type: application/json' https://api.example.com/users/1";

        // Act
        var result = CurlParser.Parse(curl);

        // Assert
        result.Method.Should().Be("PATCH");
        result.Body.Should().Be("{\"status\":\"active\"}");
        result.ContentType.Should().Be("application/json");
    }

    [Fact]
    public void Parse_WithBooleanFlags_Should_IgnoreThem()
    {
        // Arrange
        var curl = "curl -s -k -L --compressed https://api.example.com/data";

        // Act
        var result = CurlParser.Parse(curl);

        // Assert
        result.Method.Should().Be("GET");
        result.Url.Should().Be("https://api.example.com/data");
    }

    [Fact]
    public void Parse_ExplicitMethodWithHeadFlag_Should_UseExplicitMethod()
    {
        // Arrange: -X GET takes priority over -I
        var curl = "curl -X GET -I https://api.example.com/health";

        // Act
        var result = CurlParser.Parse(curl);

        // Assert
        result.Method.Should().Be("GET");
    }
}
