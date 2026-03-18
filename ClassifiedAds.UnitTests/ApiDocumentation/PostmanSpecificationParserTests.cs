using ClassifiedAds.Modules.ApiDocumentation.Entities;
using ClassifiedAds.Modules.ApiDocumentation.Services;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.ApiDocumentation;

public class PostmanSpecificationParserTests
{
    private readonly PostmanSpecificationParser _parser = new();

    [Fact]
    public void CanParse_Postman_Should_ReturnTrue()
    {
        _parser.CanParse(SourceType.Postman).Should().BeTrue();
    }

    [Fact]
    public void CanParse_OpenAPI_Should_ReturnFalse()
    {
        _parser.CanParse(SourceType.OpenAPI).Should().BeFalse();
    }

    [Fact]
    public async Task ParseAsync_NullContent_Should_ReturnFailure()
    {
        // Act
        var result = await _parser.ParseAsync(null, "test.json");

        // Assert
        result.Success.Should().BeFalse();
        result.Errors.Should().Contain("File content is empty.");
    }

    [Fact]
    public async Task ParseAsync_EmptyContent_Should_ReturnFailure()
    {
        // Act
        var result = await _parser.ParseAsync(Array.Empty<byte>(), "test.json");

        // Assert
        result.Success.Should().BeFalse();
        result.Errors.Should().Contain("File content is empty.");
    }

    [Fact]
    public async Task ParseAsync_InvalidJson_Should_ReturnFailure()
    {
        // Arrange
        var content = Encoding.UTF8.GetBytes("not valid json");

        // Act
        var result = await _parser.ParseAsync(content, "test.json");

        // Assert
        result.Success.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ParseAsync_MissingInfoProperty_Should_ReturnFailure()
    {
        // Arrange
        var json = @"{ ""item"": [] }";
        var content = Encoding.UTF8.GetBytes(json);

        // Act
        var result = await _parser.ParseAsync(content, "test.json");

        // Assert
        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("info"));
    }

    [Fact]
    public async Task ParseAsync_MissingItemProperty_Should_ReturnFailure()
    {
        // Arrange
        var json = @"{ ""info"": { ""name"": ""Test"" } }";
        var content = Encoding.UTF8.GetBytes(json);

        // Act
        var result = await _parser.ParseAsync(content, "test.json");

        // Assert
        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("item"));
    }

    [Fact]
    public async Task ParseAsync_ValidCollection_Should_ParseEndpoints()
    {
        // Arrange
        var json = @"{
            ""info"": {
                ""name"": ""Test API"",
                ""schema"": ""https://schema.getpostman.com/json/collection/v2.1.0/collection.json""
            },
            ""item"": [
                {
                    ""name"": ""List Users"",
                    ""request"": {
                        ""method"": ""GET"",
                        ""url"": {
                            ""raw"": ""https://api.example.com/api/users?page=1"",
                            ""path"": [""api"", ""users""],
                            ""query"": [
                                { ""key"": ""page"", ""value"": ""1"" }
                            ]
                        }
                    }
                },
                {
                    ""name"": ""Create User"",
                    ""request"": {
                        ""method"": ""POST"",
                        ""url"": {
                            ""raw"": ""https://api.example.com/api/users"",
                            ""path"": [""api"", ""users""]
                        },
                        ""body"": {
                            ""mode"": ""raw"",
                            ""raw"": ""{\""name\"": \""John\""}""
                        }
                    }
                }
            ]
        }";
        var content = Encoding.UTF8.GetBytes(json);

        // Act
        var result = await _parser.ParseAsync(content, "collection.json");

        // Assert
        result.Success.Should().BeTrue();
        result.DetectedVersion.Should().Be("2.1.0");
        result.Endpoints.Should().HaveCount(2);

        var listUsers = result.Endpoints.First(e => e.Summary == "List Users");
        listUsers.HttpMethod.Should().Be("GET");
        listUsers.Path.Should().Be("/api/users");
        listUsers.Parameters.Should().Contain(p => p.Name == "page" && p.Location == "Query");

        var createUser = result.Endpoints.First(e => e.Summary == "Create User");
        createUser.HttpMethod.Should().Be("POST");
        createUser.Parameters.Should().Contain(p => p.Location == "Body");
    }

    [Fact]
    public async Task ParseAsync_NestedFolders_Should_FlattenItems()
    {
        // Arrange
        var json = @"{
            ""info"": {
                ""name"": ""Nested API"",
                ""schema"": ""https://schema.getpostman.com/json/collection/v2.1.0/collection.json""
            },
            ""item"": [
                {
                    ""name"": ""Users Folder"",
                    ""item"": [
                        {
                            ""name"": ""Get User"",
                            ""request"": {
                                ""method"": ""GET"",
                                ""url"": { ""path"": [""api"", ""users"", "":id""] }
                            }
                        },
                        {
                            ""name"": ""Orders Subfolder"",
                            ""item"": [
                                {
                                    ""name"": ""Get Orders"",
                                    ""request"": {
                                        ""method"": ""GET"",
                                        ""url"": { ""path"": [""api"", ""users"", "":userId"", ""orders""] }
                                    }
                                }
                            ]
                        }
                    ]
                },
                {
                    ""name"": ""Health Check"",
                    ""request"": {
                        ""method"": ""GET"",
                        ""url"": { ""path"": [""health""] }
                    }
                }
            ]
        }";
        var content = Encoding.UTF8.GetBytes(json);

        // Act
        var result = await _parser.ParseAsync(content, "collection.json");

        // Assert
        result.Success.Should().BeTrue();
        result.Endpoints.Should().HaveCount(3);
        result.Endpoints.Select(e => e.Summary).Should()
            .Contain(new[] { "Get User", "Get Orders", "Health Check" });
    }

    [Fact]
    public async Task ParseAsync_PathVariables_Should_ConvertToTemplateFormat()
    {
        // Arrange
        var json = @"{
            ""info"": {
                ""name"": ""Test"",
                ""schema"": ""https://schema.getpostman.com/json/collection/v2.1.0/collection.json""
            },
            ""item"": [
                {
                    ""name"": ""Get User"",
                    ""request"": {
                        ""method"": ""GET"",
                        ""url"": {
                            ""path"": [""api"", ""users"", "":userId""],
                            ""variable"": [
                                { ""key"": ""userId"", ""value"": ""123"" }
                            ]
                        }
                    }
                }
            ]
        }";
        var content = Encoding.UTF8.GetBytes(json);

        // Act
        var result = await _parser.ParseAsync(content, "collection.json");

        // Assert
        result.Success.Should().BeTrue();
        var endpoint = result.Endpoints.First();
        endpoint.Path.Should().Be("/api/users/{userId}");
        endpoint.Parameters.Should().Contain(p =>
            p.Name == "userId" && p.Location == "Path" && p.IsRequired && p.DefaultValue == "123");
    }

    [Fact]
    public async Task ParseAsync_WithHeaders_Should_ParseNonStandardHeaders()
    {
        // Arrange
        var json = @"{
            ""info"": {
                ""name"": ""Test"",
                ""schema"": ""https://schema.getpostman.com/json/collection/v2.1.0/collection.json""
            },
            ""item"": [
                {
                    ""name"": ""Get Data"",
                    ""request"": {
                        ""method"": ""GET"",
                        ""url"": { ""path"": [""data""] },
                        ""header"": [
                            { ""key"": ""Content-Type"", ""value"": ""application/json"" },
                            { ""key"": ""Accept"", ""value"": ""application/json"" },
                            { ""key"": ""X-Custom-Header"", ""value"": ""custom-value"" }
                        ]
                    }
                }
            ]
        }";
        var content = Encoding.UTF8.GetBytes(json);

        // Act
        var result = await _parser.ParseAsync(content, "collection.json");

        // Assert
        result.Success.Should().BeTrue();
        var endpoint = result.Endpoints.First();

        // Content-Type and Accept should be filtered out
        endpoint.Parameters.Should().NotContain(p => p.Name == "Content-Type");
        endpoint.Parameters.Should().NotContain(p => p.Name == "Accept");

        // Custom header should be included
        endpoint.Parameters.Should().Contain(p =>
            p.Name == "X-Custom-Header" && p.Location == "Header");
    }

    [Fact]
    public async Task ParseAsync_WithBearerAuth_Should_ParseSecurityScheme()
    {
        // Arrange
        var json = @"{
            ""info"": {
                ""name"": ""Test"",
                ""schema"": ""https://schema.getpostman.com/json/collection/v2.1.0/collection.json""
            },
            ""auth"": {
                ""type"": ""bearer"",
                ""bearer"": [
                    { ""key"": ""token"", ""value"": ""my-token"" }
                ]
            },
            ""item"": [
                {
                    ""name"": ""Get Protected"",
                    ""request"": {
                        ""method"": ""GET"",
                        ""url"": { ""path"": [""protected""] }
                    }
                }
            ]
        }";
        var content = Encoding.UTF8.GetBytes(json);

        // Act
        var result = await _parser.ParseAsync(content, "collection.json");

        // Assert
        result.Success.Should().BeTrue();
        result.SecuritySchemes.Should().HaveCount(1);
        result.SecuritySchemes[0].Name.Should().Be("bearerAuth");
        result.SecuritySchemes[0].SchemeType.Should().Be(SchemeType.Http);
        result.SecuritySchemes[0].Scheme.Should().Be("bearer");

        // Endpoint inherits collection-level auth
        var endpoint = result.Endpoints.First();
        endpoint.SecurityRequirements.Should().HaveCount(1);
        endpoint.SecurityRequirements[0].SecurityType.Should().Be(SecurityType.Bearer);
    }

    [Fact]
    public async Task ParseAsync_WithApiKeyAuth_Should_ParseSecurityScheme()
    {
        // Arrange
        var json = @"{
            ""info"": {
                ""name"": ""Test"",
                ""schema"": ""https://schema.getpostman.com/json/collection/v2.1.0/collection.json""
            },
            ""auth"": {
                ""type"": ""apikey"",
                ""apikey"": [
                    { ""key"": ""key"", ""value"": ""X-API-Key"" },
                    { ""key"": ""value"", ""value"": ""my-api-key"" }
                ]
            },
            ""item"": [
                {
                    ""name"": ""Get Data"",
                    ""request"": {
                        ""method"": ""GET"",
                        ""url"": { ""path"": [""data""] }
                    }
                }
            ]
        }";
        var content = Encoding.UTF8.GetBytes(json);

        // Act
        var result = await _parser.ParseAsync(content, "collection.json");

        // Assert
        result.Success.Should().BeTrue();
        result.SecuritySchemes.Should().HaveCount(1);
        result.SecuritySchemes[0].SchemeType.Should().Be(SchemeType.ApiKey);
        result.SecuritySchemes[0].ParameterName.Should().Be("X-API-Key");
    }

    [Fact]
    public async Task ParseAsync_RequestOverridesCollectionAuth_Should_UseRequestAuth()
    {
        // Arrange
        var json = @"{
            ""info"": {
                ""name"": ""Test"",
                ""schema"": ""https://schema.getpostman.com/json/collection/v2.1.0/collection.json""
            },
            ""auth"": {
                ""type"": ""bearer""
            },
            ""item"": [
                {
                    ""name"": ""Public Endpoint"",
                    ""request"": {
                        ""method"": ""GET"",
                        ""url"": { ""path"": [""public""] },
                        ""auth"": {
                            ""type"": ""basic""
                        }
                    }
                }
            ]
        }";
        var content = Encoding.UTF8.GetBytes(json);

        // Act
        var result = await _parser.ParseAsync(content, "collection.json");

        // Assert
        result.Success.Should().BeTrue();
        var endpoint = result.Endpoints.First();
        endpoint.SecurityRequirements.Should().HaveCount(1);
        endpoint.SecurityRequirements[0].SecurityType.Should().Be(SecurityType.Basic);
    }

    [Fact]
    public async Task ParseAsync_WithResponses_Should_ParseStatusCodesAndBodies()
    {
        // Arrange
        var json = @"{
            ""info"": {
                ""name"": ""Test"",
                ""schema"": ""https://schema.getpostman.com/json/collection/v2.1.0/collection.json""
            },
            ""item"": [
                {
                    ""name"": ""Get User"",
                    ""request"": {
                        ""method"": ""GET"",
                        ""url"": { ""path"": [""users"", "":id""] }
                    },
                    ""response"": [
                        {
                            ""name"": ""Success"",
                            ""code"": 200,
                            ""body"": ""{\""id\"": 1, \""name\"": \""John\""}""
                        },
                        {
                            ""name"": ""Not Found"",
                            ""code"": 404,
                            ""body"": ""{\""error\"": \""User not found\""}""
                        }
                    ]
                }
            ]
        }";
        var content = Encoding.UTF8.GetBytes(json);

        // Act
        var result = await _parser.ParseAsync(content, "collection.json");

        // Assert
        result.Success.Should().BeTrue();
        var endpoint = result.Endpoints.First();
        endpoint.Responses.Should().HaveCount(2);
        endpoint.Responses.Should().Contain(r => r.StatusCode == 200 && r.Description == "Success");
        endpoint.Responses.Should().Contain(r => r.StatusCode == 404 && r.Description == "Not Found");
    }

    [Fact]
    public async Task ParseAsync_FormDataBody_Should_ParseAsBodyParameter()
    {
        // Arrange
        var json = @"{
            ""info"": {
                ""name"": ""Test"",
                ""schema"": ""https://schema.getpostman.com/json/collection/v2.1.0/collection.json""
            },
            ""item"": [
                {
                    ""name"": ""Upload"",
                    ""request"": {
                        ""method"": ""POST"",
                        ""url"": { ""path"": [""upload""] },
                        ""body"": {
                            ""mode"": ""formdata"",
                            ""formdata"": [
                                { ""key"": ""file"", ""type"": ""file"" },
                                { ""key"": ""description"", ""type"": ""text"" }
                            ]
                        }
                    }
                }
            ]
        }";
        var content = Encoding.UTF8.GetBytes(json);

        // Act
        var result = await _parser.ParseAsync(content, "collection.json");

        // Assert
        result.Success.Should().BeTrue();
        var endpoint = result.Endpoints.First();
        endpoint.Parameters.Should().Contain(p => p.Location == "Body" && p.Name == "body");
    }

    [Fact]
    public async Task ParseAsync_UrlWithPostmanVariables_Should_NormalizePath()
    {
        // Arrange
        var json = @"{
            ""info"": {
                ""name"": ""Test"",
                ""schema"": ""https://schema.getpostman.com/json/collection/v2.1.0/collection.json""
            },
            ""item"": [
                {
                    ""name"": ""Get Resource"",
                    ""request"": {
                        ""method"": ""GET"",
                        ""url"": {
                            ""raw"": ""{{baseUrl}}/api/resources/{{resourceId}}""
                        }
                    }
                }
            ]
        }";
        var content = Encoding.UTF8.GetBytes(json);

        // Act
        var result = await _parser.ParseAsync(content, "collection.json");

        // Assert
        result.Success.Should().BeTrue();
        var endpoint = result.Endpoints.First();
        // {{baseUrl}} should be normalized to {baseUrl} and {{resourceId}} to {resourceId}
        endpoint.Path.Should().Contain("{resourceId}");
        endpoint.Path.Should().NotContain("{{");
    }

    [Fact]
    public async Task ParseAsync_NoAuthType_Should_NotAddSecuritySchemes()
    {
        // Arrange
        var json = @"{
            ""info"": {
                ""name"": ""Test"",
                ""schema"": ""https://schema.getpostman.com/json/collection/v2.1.0/collection.json""
            },
            ""auth"": {
                ""type"": ""noauth""
            },
            ""item"": [
                {
                    ""name"": ""Public"",
                    ""request"": {
                        ""method"": ""GET"",
                        ""url"": { ""path"": [""public""] }
                    }
                }
            ]
        }";
        var content = Encoding.UTF8.GetBytes(json);

        // Act
        var result = await _parser.ParseAsync(content, "collection.json");

        // Assert
        result.Success.Should().BeTrue();
        result.SecuritySchemes.Should().BeEmpty();
        result.Endpoints.First().SecurityRequirements.Should().BeEmpty();
    }

    [Fact]
    public async Task ParseAsync_EmptyItemArray_Should_ReturnSuccessWithNoEndpoints()
    {
        // Arrange
        var json = @"{
            ""info"": {
                ""name"": ""Empty"",
                ""schema"": ""https://schema.getpostman.com/json/collection/v2.1.0/collection.json""
            },
            ""item"": []
        }";
        var content = Encoding.UTF8.GetBytes(json);

        // Act
        var result = await _parser.ParseAsync(content, "collection.json");

        // Assert
        result.Success.Should().BeTrue();
        result.Endpoints.Should().BeEmpty();
    }

    [Fact]
    public async Task ParseAsync_V20Schema_Should_DetectVersion()
    {
        // Arrange
        var json = @"{
            ""info"": {
                ""name"": ""V2 Collection"",
                ""schema"": ""https://schema.getpostman.com/json/collection/v2.0.0/collection.json""
            },
            ""item"": []
        }";
        var content = Encoding.UTF8.GetBytes(json);

        // Act
        var result = await _parser.ParseAsync(content, "collection.json");

        // Assert
        result.Success.Should().BeTrue();
        result.DetectedVersion.Should().Be("2.0.0");
    }
}
