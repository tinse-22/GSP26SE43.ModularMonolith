using ClassifiedAds.Modules.ApiDocumentation.Entities;
using ClassifiedAds.Modules.ApiDocumentation.Services;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.ApiDocumentation;

public class OpenApiSpecificationParserTests
{
    private readonly OpenApiSpecificationParser _parser = new();

    [Fact]
    public void CanParse_OpenAPI_Should_ReturnTrue()
    {
        _parser.CanParse(SourceType.OpenAPI).Should().BeTrue();
    }

    [Fact]
    public void CanParse_Postman_Should_ReturnFalse()
    {
        _parser.CanParse(SourceType.Postman).Should().BeFalse();
    }

    [Fact]
    public void CanParse_Manual_Should_ReturnFalse()
    {
        _parser.CanParse(SourceType.Manual).Should().BeFalse();
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
        var content = Encoding.UTF8.GetBytes("this is not valid json or yaml");

        // Act
        var result = await _parser.ParseAsync(content, "test.json");

        // Assert
        result.Success.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ParseAsync_ValidOpenApi30_Should_ParseEndpoints()
    {
        // Arrange
        var json = @"{
            ""openapi"": ""3.0.3"",
            ""info"": { ""title"": ""Pet Store"", ""version"": ""1.0.0"" },
            ""paths"": {
                ""/pets"": {
                    ""get"": {
                        ""operationId"": ""listPets"",
                        ""summary"": ""List all pets"",
                        ""description"": ""Returns all pets"",
                        ""tags"": [""pets""],
                        ""parameters"": [
                            {
                                ""name"": ""limit"",
                                ""in"": ""query"",
                                ""required"": false,
                                ""schema"": { ""type"": ""integer"", ""format"": ""int32"" }
                            }
                        ],
                        ""responses"": {
                            ""200"": {
                                ""description"": ""A list of pets""
                            }
                        }
                    },
                    ""post"": {
                        ""operationId"": ""createPet"",
                        ""summary"": ""Create a pet"",
                        ""requestBody"": {
                            ""required"": true,
                            ""content"": {
                                ""application/json"": {
                                    ""schema"": {
                                        ""type"": ""object"",
                                        ""properties"": {
                                            ""name"": { ""type"": ""string"" }
                                        }
                                    }
                                }
                            }
                        },
                        ""responses"": {
                            ""201"": { ""description"": ""Pet created"" }
                        }
                    }
                },
                ""/pets/{petId}"": {
                    ""get"": {
                        ""operationId"": ""getPet"",
                        ""summary"": ""Get a pet"",
                        ""parameters"": [
                            {
                                ""name"": ""petId"",
                                ""in"": ""path"",
                                ""required"": true,
                                ""schema"": { ""type"": ""string"" }
                            }
                        ],
                        ""responses"": {
                            ""200"": { ""description"": ""A pet"" },
                            ""404"": { ""description"": ""Pet not found"" }
                        }
                    }
                }
            }
        }";
        var content = Encoding.UTF8.GetBytes(json);

        // Act
        var result = await _parser.ParseAsync(content, "petstore.json");

        // Assert
        result.Success.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        result.DetectedVersion.Should().Be("1.0.0");
        result.Endpoints.Should().HaveCount(3);

        // Verify GET /pets
        var listPets = result.Endpoints.First(e => e.OperationId == "listPets");
        listPets.HttpMethod.Should().Be("GET");
        listPets.Path.Should().Be("/pets");
        listPets.Summary.Should().Be("List all pets");
        listPets.Description.Should().Be("Returns all pets");
        listPets.Tags.Should().Contain("pets");
        listPets.Parameters.Should().HaveCount(1);
        listPets.Parameters[0].Name.Should().Be("limit");
        listPets.Parameters[0].Location.Should().Be("Query");
        listPets.Parameters[0].DataType.Should().Be("integer");
        listPets.Parameters[0].Format.Should().Be("int32");
        listPets.Parameters[0].IsRequired.Should().BeFalse();
        listPets.Responses.Should().HaveCount(1);
        listPets.Responses[0].StatusCode.Should().Be(200);

        // Verify POST /pets
        var createPet = result.Endpoints.First(e => e.OperationId == "createPet");
        createPet.HttpMethod.Should().Be("POST");
        createPet.Path.Should().Be("/pets");
        createPet.Parameters.Should().Contain(p => p.Location == "Body" && p.Name == "body");

        // Verify GET /pets/{petId}
        var getPet = result.Endpoints.First(e => e.OperationId == "getPet");
        getPet.HttpMethod.Should().Be("GET");
        getPet.Path.Should().Be("/pets/{petId}");
        getPet.Parameters.Should().Contain(p => p.Location == "Path" && p.Name == "petId" && p.IsRequired);
        getPet.Responses.Should().HaveCount(2);
    }

    [Fact]
    public async Task ParseAsync_WithSecuritySchemes_Should_ParseSchemes()
    {
        // Arrange
        var json = @"{
            ""openapi"": ""3.0.3"",
            ""info"": { ""title"": ""Secured API"", ""version"": ""2.0.0"" },
            ""components"": {
                ""securitySchemes"": {
                    ""bearerAuth"": {
                        ""type"": ""http"",
                        ""scheme"": ""bearer"",
                        ""bearerFormat"": ""JWT""
                    },
                    ""apiKeyAuth"": {
                        ""type"": ""apiKey"",
                        ""in"": ""header"",
                        ""name"": ""X-API-Key""
                    },
                    ""basicAuth"": {
                        ""type"": ""http"",
                        ""scheme"": ""basic""
                    }
                }
            },
            ""paths"": {
                ""/secure"": {
                    ""get"": {
                        ""operationId"": ""getSecure"",
                        ""security"": [
                            { ""bearerAuth"": [] }
                        ],
                        ""responses"": {
                            ""200"": { ""description"": ""OK"" }
                        }
                    }
                }
            }
        }";
        var content = Encoding.UTF8.GetBytes(json);

        // Act
        var result = await _parser.ParseAsync(content, "secured.json");

        // Assert
        result.Success.Should().BeTrue();
        result.SecuritySchemes.Should().HaveCount(3);

        var bearer = result.SecuritySchemes.First(s => s.Name == "bearerAuth");
        bearer.SchemeType.Should().Be(SchemeType.Http);
        bearer.Scheme.Should().Be("bearer");
        bearer.BearerFormat.Should().Be("JWT");

        var apiKey = result.SecuritySchemes.First(s => s.Name == "apiKeyAuth");
        apiKey.SchemeType.Should().Be(SchemeType.ApiKey);
        apiKey.ParameterName.Should().Be("X-API-Key");
        apiKey.ApiKeyLocation.Should().Be(ApiKeyLocation.Header);

        var basic = result.SecuritySchemes.First(s => s.Name == "basicAuth");
        basic.SchemeType.Should().Be(SchemeType.Http);
        basic.Scheme.Should().Be("basic");

        // Verify security requirement on endpoint
        var endpoint = result.Endpoints.First();
        endpoint.SecurityRequirements.Should().HaveCount(1);
        endpoint.SecurityRequirements[0].SchemeName.Should().Be("bearerAuth");
        endpoint.SecurityRequirements[0].SecurityType.Should().Be(SecurityType.Bearer);
    }

    [Fact]
    public async Task ParseAsync_WithRequestBody_Should_MapAsBodyParameter()
    {
        // Arrange
        var json = @"{
            ""openapi"": ""3.0.0"",
            ""info"": { ""title"": ""Test"", ""version"": ""1.0.0"" },
            ""paths"": {
                ""/users"": {
                    ""post"": {
                        ""operationId"": ""createUser"",
                        ""requestBody"": {
                            ""required"": true,
                            ""content"": {
                                ""application/json"": {
                                    ""schema"": {
                                        ""type"": ""object"",
                                        ""properties"": {
                                            ""name"": { ""type"": ""string"" },
                                            ""email"": { ""type"": ""string"", ""format"": ""email"" }
                                        },
                                        ""required"": [""name"", ""email""]
                                    }
                                }
                            }
                        },
                        ""responses"": {
                            ""201"": { ""description"": ""Created"" }
                        }
                    }
                }
            }
        }";
        var content = Encoding.UTF8.GetBytes(json);

        // Act
        var result = await _parser.ParseAsync(content, "test.json");

        // Assert
        result.Success.Should().BeTrue();
        var endpoint = result.Endpoints.First();
        var bodyParam = endpoint.Parameters.First(p => p.Location == "Body");
        bodyParam.Name.Should().Be("body");
        bodyParam.DataType.Should().Be("object");
        bodyParam.IsRequired.Should().BeTrue();
        bodyParam.Schema.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ParseAsync_DefaultResponseCode_Should_MapToZero()
    {
        // Arrange
        var json = @"{
            ""openapi"": ""3.0.0"",
            ""info"": { ""title"": ""Test"", ""version"": ""1.0.0"" },
            ""paths"": {
                ""/health"": {
                    ""get"": {
                        ""operationId"": ""healthCheck"",
                        ""responses"": {
                            ""200"": { ""description"": ""OK"" },
                            ""default"": { ""description"": ""Unexpected error"" }
                        }
                    }
                }
            }
        }";
        var content = Encoding.UTF8.GetBytes(json);

        // Act
        var result = await _parser.ParseAsync(content, "test.json");

        // Assert
        result.Success.Should().BeTrue();
        var endpoint = result.Endpoints.First();
        endpoint.Responses.Should().HaveCount(2);
        endpoint.Responses.Should().Contain(r => r.StatusCode == 200);
        endpoint.Responses.Should().Contain(r => r.StatusCode == 0 && r.Description == "Unexpected error");
    }

    [Fact]
    public async Task ParseAsync_DeprecatedOperation_Should_SetIsDeprecated()
    {
        // Arrange
        var json = @"{
            ""openapi"": ""3.0.0"",
            ""info"": { ""title"": ""Test"", ""version"": ""1.0.0"" },
            ""paths"": {
                ""/old"": {
                    ""get"": {
                        ""operationId"": ""oldEndpoint"",
                        ""deprecated"": true,
                        ""responses"": {
                            ""200"": { ""description"": ""OK"" }
                        }
                    }
                }
            }
        }";
        var content = Encoding.UTF8.GetBytes(json);

        // Act
        var result = await _parser.ParseAsync(content, "test.json");

        // Assert
        result.Success.Should().BeTrue();
        result.Endpoints.First().IsDeprecated.Should().BeTrue();
    }

    [Fact]
    public async Task ParseAsync_PathLevelParameters_Should_BeMergedWithOperationParameters()
    {
        // Arrange
        var json = @"{
            ""openapi"": ""3.0.0"",
            ""info"": { ""title"": ""Test"", ""version"": ""1.0.0"" },
            ""paths"": {
                ""/users/{userId}/orders"": {
                    ""parameters"": [
                        {
                            ""name"": ""userId"",
                            ""in"": ""path"",
                            ""required"": true,
                            ""schema"": { ""type"": ""string"" }
                        }
                    ],
                    ""get"": {
                        ""operationId"": ""getUserOrders"",
                        ""parameters"": [
                            {
                                ""name"": ""status"",
                                ""in"": ""query"",
                                ""schema"": { ""type"": ""string"" }
                            }
                        ],
                        ""responses"": {
                            ""200"": { ""description"": ""OK"" }
                        }
                    }
                }
            }
        }";
        var content = Encoding.UTF8.GetBytes(json);

        // Act
        var result = await _parser.ParseAsync(content, "test.json");

        // Assert
        result.Success.Should().BeTrue();
        var endpoint = result.Endpoints.First();
        endpoint.Parameters.Should().HaveCount(2);
        endpoint.Parameters.Should().Contain(p => p.Name == "userId" && p.Location == "Path");
        endpoint.Parameters.Should().Contain(p => p.Name == "status" && p.Location == "Query");
    }

    [Fact]
    public async Task ParseAsync_MultipleHttpMethods_Should_CreateSeparateEndpoints()
    {
        // Arrange
        var json = @"{
            ""openapi"": ""3.0.0"",
            ""info"": { ""title"": ""Test"", ""version"": ""1.0.0"" },
            ""paths"": {
                ""/items"": {
                    ""get"": {
                        ""operationId"": ""listItems"",
                        ""responses"": { ""200"": { ""description"": ""OK"" } }
                    },
                    ""post"": {
                        ""operationId"": ""createItem"",
                        ""responses"": { ""201"": { ""description"": ""Created"" } }
                    },
                    ""delete"": {
                        ""operationId"": ""deleteItems"",
                        ""responses"": { ""204"": { ""description"": ""Deleted"" } }
                    }
                }
            }
        }";
        var content = Encoding.UTF8.GetBytes(json);

        // Act
        var result = await _parser.ParseAsync(content, "test.json");

        // Assert
        result.Success.Should().BeTrue();
        result.Endpoints.Should().HaveCount(3);
        result.Endpoints.Select(e => e.HttpMethod).Should().Contain(new[] { "GET", "POST", "DELETE" });
    }

    [Fact]
    public async Task ParseAsync_NoPathsDefined_Should_ReturnSuccessWithEmptyEndpoints()
    {
        // Arrange
        var json = @"{
            ""openapi"": ""3.0.0"",
            ""info"": { ""title"": ""Empty"", ""version"": ""0.1.0"" },
            ""paths"": {}
        }";
        var content = Encoding.UTF8.GetBytes(json);

        // Act
        var result = await _parser.ParseAsync(content, "test.json");

        // Assert
        result.Success.Should().BeTrue();
        result.Endpoints.Should().BeEmpty();
        result.DetectedVersion.Should().Be("0.1.0");
    }

    [Fact]
    public async Task ParseAsync_HeaderAndCookieParameters_Should_MapLocations()
    {
        // Arrange
        var json = @"{
            ""openapi"": ""3.0.0"",
            ""info"": { ""title"": ""Test"", ""version"": ""1.0.0"" },
            ""paths"": {
                ""/data"": {
                    ""get"": {
                        ""operationId"": ""getData"",
                        ""parameters"": [
                            {
                                ""name"": ""X-Request-Id"",
                                ""in"": ""header"",
                                ""required"": true,
                                ""schema"": { ""type"": ""string"" }
                            },
                            {
                                ""name"": ""session"",
                                ""in"": ""cookie"",
                                ""schema"": { ""type"": ""string"" }
                            }
                        ],
                        ""responses"": { ""200"": { ""description"": ""OK"" } }
                    }
                }
            }
        }";
        var content = Encoding.UTF8.GetBytes(json);

        // Act
        var result = await _parser.ParseAsync(content, "test.json");

        // Assert
        result.Success.Should().BeTrue();
        var endpoint = result.Endpoints.First();
        endpoint.Parameters.Should().Contain(p => p.Name == "X-Request-Id" && p.Location == "Header" && p.IsRequired);
        endpoint.Parameters.Should().Contain(p => p.Name == "session" && p.Location == "Cookie");
    }
}
