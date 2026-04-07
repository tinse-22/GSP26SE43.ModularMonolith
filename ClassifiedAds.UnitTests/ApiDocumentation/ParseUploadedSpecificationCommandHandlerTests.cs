using ClassifiedAds.Contracts.Storage.DTOs;
using ClassifiedAds.Contracts.Storage.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.ApiDocumentation.Commands;
using ClassifiedAds.Modules.ApiDocumentation.Entities;
using ClassifiedAds.Modules.ApiDocumentation.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.ApiDocumentation;

public class ParseUploadedSpecificationCommandHandlerTests
{
    private readonly Mock<IRepository<ApiSpecification, Guid>> _specRepoMock;
    private readonly Mock<IRepository<ApiEndpoint, Guid>> _endpointRepoMock;
    private readonly Mock<IRepository<EndpointParameter, Guid>> _parameterRepoMock;
    private readonly Mock<IRepository<EndpointResponse, Guid>> _responseRepoMock;
    private readonly Mock<IRepository<EndpointSecurityReq, Guid>> _securityReqRepoMock;
    private readonly Mock<IRepository<SecurityScheme, Guid>> _securitySchemeRepoMock;
    private readonly Mock<IStorageFileGatewayService> _storageGatewayMock;
    private readonly Mock<ISpecificationParser> _parserMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly ParseUploadedSpecificationCommandHandler _handler;

    private readonly Guid _specId = Guid.NewGuid();
    private readonly Guid _fileId = Guid.NewGuid();

    public ParseUploadedSpecificationCommandHandlerTests()
    {
        _specRepoMock = new Mock<IRepository<ApiSpecification, Guid>>();
        _endpointRepoMock = new Mock<IRepository<ApiEndpoint, Guid>>();
        _parameterRepoMock = new Mock<IRepository<EndpointParameter, Guid>>();
        _responseRepoMock = new Mock<IRepository<EndpointResponse, Guid>>();
        _securityReqRepoMock = new Mock<IRepository<EndpointSecurityReq, Guid>>();
        _securitySchemeRepoMock = new Mock<IRepository<SecurityScheme, Guid>>();
        _storageGatewayMock = new Mock<IStorageFileGatewayService>();
        _parserMock = new Mock<ISpecificationParser>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _specRepoMock.Setup(x => x.UnitOfWork).Returns(_unitOfWorkMock.Object);

        _unitOfWorkMock.Setup(x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<IsolationLevel>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, IsolationLevel, CancellationToken>(
                async (operation, _, ct) => await operation(ct));

        _endpointRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(new List<ApiEndpoint>().AsQueryable());
        _endpointRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<ApiEndpoint>>()))
            .ReturnsAsync(new List<ApiEndpoint>());

        _securitySchemeRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(new List<SecurityScheme>().AsQueryable());
        _securitySchemeRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<SecurityScheme>>()))
            .ReturnsAsync(new List<SecurityScheme>());

        _endpointRepoMock.Setup(x => x.AddAsync(It.IsAny<ApiEndpoint>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _parserMock.Setup(x => x.CanParse(SourceType.OpenAPI)).Returns(true);

        var loggerMock = new Mock<ILogger<ParseUploadedSpecificationCommandHandler>>();

        _handler = new ParseUploadedSpecificationCommandHandler(
            _specRepoMock.Object,
            _endpointRepoMock.Object,
            _parameterRepoMock.Object,
            _responseRepoMock.Object,
            _securityReqRepoMock.Object,
            _securitySchemeRepoMock.Object,
            _storageGatewayMock.Object,
            new[] { _parserMock.Object },
            loggerMock.Object);
    }

    private ApiSpecification CreatePendingSpec(SourceType sourceType = SourceType.OpenAPI)
    {
        return new ApiSpecification
        {
            Id = _specId,
            ParseStatus = ParseStatus.Pending,
            SourceType = sourceType,
            OriginalFileId = _fileId,
            Name = "Test Spec",
        };
    }

    private void SetupSpecFound(ApiSpecification spec)
    {
        _specRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<ApiSpecification>>()))
            .ReturnsAsync(spec);
        _specRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(new List<ApiSpecification> { spec }.AsQueryable());
    }

    private void SetupFileDownload(string content = "{}", string fileName = "spec.json")
    {
        _storageGatewayMock.Setup(x => x.DownloadAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StorageDownloadResult
            {
                Content = Encoding.UTF8.GetBytes(content),
                FileName = fileName,
                ContentType = "application/json",
            });
    }

    private void SetupParseSuccess(int endpointCount = 1, int schemeCount = 0)
    {
        var endpoints = Enumerable.Range(0, endpointCount).Select(i => new ParsedEndpoint
        {
            HttpMethod = "GET",
            Path = $"/api/resource{i}",
            OperationId = $"getResource{i}",
            Summary = $"Get Resource {i}",
            Parameters = new List<ParsedParameter>
            {
                new() { Name = "id", Location = "Path", DataType = "string", IsRequired = true },
            },
            Responses = new List<ParsedResponse>
            {
                new() { StatusCode = 200, Description = "OK" },
            },
            SecurityRequirements = new List<ParsedSecurityRequirement>(),
        }).ToList();

        var schemes = Enumerable.Range(0, schemeCount).Select(i => new ParsedSecurityScheme
        {
            Name = $"scheme{i}",
            SchemeType = SchemeType.Http,
            Scheme = "bearer",
        }).ToList();

        _parserMock.Setup(x => x.ParseAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SpecificationParseResult
            {
                Success = true,
                DetectedVersion = "1.0.0",
                Endpoints = endpoints,
                SecuritySchemes = schemes,
            });
    }

    [Fact]
    public async Task HandleAsync_SpecNotFound_Should_ReturnWithoutProcessing()
    {
        // Arrange
        _specRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<ApiSpecification>>()))
            .ReturnsAsync((ApiSpecification)null);
        _specRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(new List<ApiSpecification>().AsQueryable());

        var command = new ParseUploadedSpecificationCommand { SpecificationId = _specId };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        _storageGatewayMock.Verify(x => x.DownloadAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _parserMock.Verify(x => x.ParseAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_SpecNotPending_Should_SkipProcessing()
    {
        // Arrange
        var spec = CreatePendingSpec();
        spec.ParseStatus = ParseStatus.Success;
        SetupSpecFound(spec);

        var command = new ParseUploadedSpecificationCommand { SpecificationId = _specId };

        // Act
        await _handler.HandleAsync(command);

        // Assert - should not download or parse
        _storageGatewayMock.Verify(x => x.DownloadAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _parserMock.Verify(x => x.ParseAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_SpecAlreadyFailed_Should_SkipProcessing()
    {
        // Arrange
        var spec = CreatePendingSpec();
        spec.ParseStatus = ParseStatus.Failed;
        SetupSpecFound(spec);

        var command = new ParseUploadedSpecificationCommand { SpecificationId = _specId };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        _storageGatewayMock.Verify(x => x.DownloadAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_NoOriginalFileId_Should_SetFailed()
    {
        // Arrange
        var spec = CreatePendingSpec();
        spec.OriginalFileId = null;
        SetupSpecFound(spec);

        var command = new ParseUploadedSpecificationCommand { SpecificationId = _specId };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        spec.ParseStatus.Should().Be(ParseStatus.Failed);
        spec.ParsedAt.Should().NotBeNull();
        spec.ParseErrors.Should().NotBeNullOrWhiteSpace();
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_NoParserAvailable_Should_SetFailed()
    {
        // Arrange
        var spec = CreatePendingSpec(SourceType.Postman);
        SetupSpecFound(spec);

        // parserMock.CanParse(Postman) returns false by default

        var command = new ParseUploadedSpecificationCommand { SpecificationId = _specId };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        spec.ParseStatus.Should().Be(ParseStatus.Failed);
        spec.ParseErrors.Should().Contain("parser");
    }

    [Fact]
    public async Task HandleAsync_FileNotFoundInStorage_Should_SetFailed()
    {
        // Arrange
        var spec = CreatePendingSpec();
        SetupSpecFound(spec);

        _storageGatewayMock.Setup(x => x.DownloadAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("File not found"));

        var command = new ParseUploadedSpecificationCommand { SpecificationId = _specId };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        spec.ParseStatus.Should().Be(ParseStatus.Failed);
        spec.ParseErrors.Should().Contain("not found");
    }

    [Fact]
    public async Task HandleAsync_ParseFailure_Should_SetFailedWithErrors()
    {
        // Arrange
        var spec = CreatePendingSpec();
        SetupSpecFound(spec);
        SetupFileDownload();

        _parserMock.Setup(x => x.ParseAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SpecificationParseResult
            {
                Success = false,
                Errors = new List<string> { "Invalid format", "Missing required field" },
            });

        var command = new ParseUploadedSpecificationCommand { SpecificationId = _specId };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        spec.ParseStatus.Should().Be(ParseStatus.Failed);
        spec.ParsedAt.Should().NotBeNull();
        spec.ParseErrors.Should().NotBeNullOrWhiteSpace();

        var errors = JsonSerializer.Deserialize<List<string>>(spec.ParseErrors);
        errors.Should().HaveCount(2);
        errors.Should().Contain("Invalid format");
        errors.Should().Contain("Missing required field");

        // Should not attempt to persist endpoints
        _endpointRepoMock.Verify(x => x.AddAsync(It.IsAny<ApiEndpoint>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ParseSuccess_Should_PersistEndpointsAndSetSuccess()
    {
        // Arrange
        var spec = CreatePendingSpec();
        SetupSpecFound(spec);
        SetupFileDownload();
        SetupParseSuccess(endpointCount: 2, schemeCount: 1);
        var createdEndpoints = new List<ApiEndpoint>();
        var createdParameters = new List<EndpointParameter>();
        var createdResponses = new List<EndpointResponse>();

        _endpointRepoMock.Setup(x => x.AddAsync(It.IsAny<ApiEndpoint>(), It.IsAny<CancellationToken>()))
            .Callback<ApiEndpoint, CancellationToken>((endpoint, _) => createdEndpoints.Add(endpoint))
            .Returns(Task.CompletedTask);
        _parameterRepoMock.Setup(x => x.AddAsync(It.IsAny<EndpointParameter>(), It.IsAny<CancellationToken>()))
            .Callback<EndpointParameter, CancellationToken>((parameter, _) => createdParameters.Add(parameter))
            .Returns(Task.CompletedTask);
        _responseRepoMock.Setup(x => x.AddAsync(It.IsAny<EndpointResponse>(), It.IsAny<CancellationToken>()))
            .Callback<EndpointResponse, CancellationToken>((response, _) => createdResponses.Add(response))
            .Returns(Task.CompletedTask);

        var command = new ParseUploadedSpecificationCommand { SpecificationId = _specId };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        spec.ParseStatus.Should().Be(ParseStatus.Success);
        spec.ParsedAt.Should().NotBeNull();
        spec.ParseErrors.Should().BeNull();
        spec.Version.Should().Be("1.0.0");

        // Verify endpoints were created
        _endpointRepoMock.Verify(
            x => x.AddAsync(It.IsAny<ApiEndpoint>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));

        // Verify parameters were created (one per endpoint)
        _parameterRepoMock.Verify(
            x => x.AddAsync(It.IsAny<EndpointParameter>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));

        // Verify responses were created (one per endpoint)
        _responseRepoMock.Verify(
            x => x.AddAsync(It.IsAny<EndpointResponse>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        createdEndpoints.Should().HaveCount(2);
        createdEndpoints.Select(x => x.Id).Should().OnlyContain(x => x != Guid.Empty);
        createdParameters.Should().HaveCount(2);
        createdParameters.Select(x => x.Id).Should().OnlyContain(x => x != Guid.Empty);
        createdParameters.Select(x => x.EndpointId).Should().BeEquivalentTo(createdEndpoints.Select(x => x.Id));
        createdResponses.Should().HaveCount(2);
        createdResponses.Select(x => x.Id).Should().OnlyContain(x => x != Guid.Empty);
        createdResponses.Select(x => x.EndpointId).Should().BeEquivalentTo(createdEndpoints.Select(x => x.Id));

        // Verify security schemes were created
        _securitySchemeRepoMock.Verify(
            x => x.AddAsync(It.IsAny<SecurityScheme>(), It.IsAny<CancellationToken>()),
            Times.Once);

        // Verify transaction was used
        _unitOfWorkMock.Verify(x => x.ExecuteInTransactionAsync(
            It.IsAny<Func<CancellationToken, Task>>(),
            It.IsAny<IsolationLevel>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ParseSuccess_Should_NormalizePlainTextExamplesBeforePersisting()
    {
        // Arrange
        var spec = CreatePendingSpec();
        SetupSpecFound(spec);
        SetupFileDownload();
        var createdParameters = new List<EndpointParameter>();
        var createdResponses = new List<EndpointResponse>();

        _parameterRepoMock.Setup(x => x.AddAsync(It.IsAny<EndpointParameter>(), It.IsAny<CancellationToken>()))
            .Callback<EndpointParameter, CancellationToken>((parameter, _) => createdParameters.Add(parameter))
            .Returns(Task.CompletedTask);
        _responseRepoMock.Setup(x => x.AddAsync(It.IsAny<EndpointResponse>(), It.IsAny<CancellationToken>()))
            .Callback<EndpointResponse, CancellationToken>((response, _) => createdResponses.Add(response))
            .Returns(Task.CompletedTask);

        _parserMock.Setup(x => x.ParseAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SpecificationParseResult
            {
                Success = true,
                DetectedVersion = "1.0.0",
                Endpoints = new List<ParsedEndpoint>
                {
                    new()
                    {
                        HttpMethod = "POST",
                        Path = "/api/users",
                        Parameters = new List<ParsedParameter>
                        {
                            new()
                            {
                                Name = "body",
                                Location = "Body",
                                DataType = "object",
                                Schema = "{ \"type\": \"object\" }",
                                Examples = "sample request body",
                            },
                        },
                        Responses = new List<ParsedResponse>
                        {
                            new()
                            {
                                StatusCode = 200,
                                Examples = "created",
                                Headers = "{ \"X-Trace-Id\": { \"schema\": { \"type\": \"string\" } } }",
                            },
                        },
                        SecurityRequirements = new List<ParsedSecurityRequirement>(),
                    },
                },
                SecuritySchemes = new List<ParsedSecurityScheme>(),
            });

        var command = new ParseUploadedSpecificationCommand { SpecificationId = _specId };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        createdParameters.Should().ContainSingle();
        createdParameters[0].Schema.Should().Be("{\"type\":\"object\"}");
        createdParameters[0].Examples.Should().Be("\"sample request body\"");

        createdResponses.Should().ContainSingle();
        createdResponses[0].Examples.Should().Be("\"created\"");
        createdResponses[0].Headers.Should().Be("{\"X-Trace-Id\":{\"schema\":{\"type\":\"string\"}}}");
    }

    [Fact]
    public async Task HandleAsync_ParseSuccess_Should_DeleteExistingEndpointsFirst()
    {
        // Arrange
        var spec = CreatePendingSpec();
        SetupSpecFound(spec);
        SetupFileDownload();
        SetupParseSuccess();

        var existingEndpoint = new ApiEndpoint
        {
            Id = Guid.NewGuid(),
            ApiSpecId = _specId,
            HttpMethod = ClassifiedAds.Modules.ApiDocumentation.Entities.HttpMethod.GET,
            Path = "/old",
        };
        _endpointRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<ApiEndpoint>>()))
            .ReturnsAsync(new List<ApiEndpoint> { existingEndpoint });

        var existingScheme = new SecurityScheme
        {
            Id = Guid.NewGuid(),
            ApiSpecId = _specId,
            Name = "oldScheme",
        };
        _securitySchemeRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<SecurityScheme>>()))
            .ReturnsAsync(new List<SecurityScheme> { existingScheme });

        var command = new ParseUploadedSpecificationCommand { SpecificationId = _specId };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        _endpointRepoMock.Verify(x => x.Delete(existingEndpoint), Times.Once);
        _securitySchemeRepoMock.Verify(x => x.Delete(existingScheme), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ParserThrowsException_Should_SetFailed()
    {
        // Arrange
        var spec = CreatePendingSpec();
        SetupSpecFound(spec);
        SetupFileDownload();

        _parserMock.Setup(x => x.ParseAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Unexpected parse error"));

        var command = new ParseUploadedSpecificationCommand { SpecificationId = _specId };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        spec.ParseStatus.Should().Be(ParseStatus.Failed);
        spec.ParseErrors.Should().Contain("Unexpected parse error");
    }

    [Fact]
    public async Task HandleAsync_InfraError_Should_RethrowForRetry()
    {
        // Arrange
        var spec = CreatePendingSpec();
        SetupSpecFound(spec);
        SetupFileDownload();
        SetupParseSuccess();

        // Simulate infrastructure error during transaction
        _unitOfWorkMock.Setup(x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<IsolationLevel>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("Database connection timeout"));

        var command = new ParseUploadedSpecificationCommand { SpecificationId = _specId };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert - infrastructure errors should bubble up
        await act.Should().ThrowAsync<TimeoutException>();
    }

    [Fact]
    public async Task HandleAsync_WithSecurityRequirements_Should_PersistSecurityReqs()
    {
        // Arrange
        var spec = CreatePendingSpec();
        SetupSpecFound(spec);
        SetupFileDownload();
        var createdEndpoints = new List<ApiEndpoint>();
        var createdSecurityRequirements = new List<EndpointSecurityReq>();

        _endpointRepoMock.Setup(x => x.AddAsync(It.IsAny<ApiEndpoint>(), It.IsAny<CancellationToken>()))
            .Callback<ApiEndpoint, CancellationToken>((endpoint, _) => createdEndpoints.Add(endpoint))
            .Returns(Task.CompletedTask);
        _securityReqRepoMock.Setup(x => x.AddAsync(It.IsAny<EndpointSecurityReq>(), It.IsAny<CancellationToken>()))
            .Callback<EndpointSecurityReq, CancellationToken>((securityRequirement, _) => createdSecurityRequirements.Add(securityRequirement))
            .Returns(Task.CompletedTask);

        _parserMock.Setup(x => x.ParseAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SpecificationParseResult
            {
                Success = true,
                DetectedVersion = "1.0.0",
                Endpoints = new List<ParsedEndpoint>
                {
                    new()
                    {
                        HttpMethod = "GET",
                        Path = "/protected",
                        OperationId = "getProtected",
                        Parameters = new List<ParsedParameter>(),
                        Responses = new List<ParsedResponse>(),
                        SecurityRequirements = new List<ParsedSecurityRequirement>
                        {
                            new() { SecurityType = SecurityType.Bearer, SchemeName = "bearerAuth" },
                        },
                    },
                },
                SecuritySchemes = new List<ParsedSecurityScheme>
                {
                    new()
                    {
                        Name = "bearerAuth",
                        SchemeType = SchemeType.Http,
                        Scheme = "bearer",
                        BearerFormat = "JWT",
                    },
                },
            });

        var command = new ParseUploadedSpecificationCommand { SpecificationId = _specId };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        spec.ParseStatus.Should().Be(ParseStatus.Success);

        _securityReqRepoMock.Verify(
            x => x.AddAsync(It.Is<EndpointSecurityReq>(r =>
                r.SecurityType == SecurityType.Bearer && r.SchemeName == "bearerAuth"),
                It.IsAny<CancellationToken>()),
            Times.Once);
        createdEndpoints.Should().ContainSingle();
        createdEndpoints[0].Id.Should().NotBe(Guid.Empty);
        createdSecurityRequirements.Should().ContainSingle();
        createdSecurityRequirements[0].Id.Should().NotBe(Guid.Empty);
        createdSecurityRequirements[0].EndpointId.Should().Be(createdEndpoints[0].Id);

        _securitySchemeRepoMock.Verify(
            x => x.AddAsync(It.Is<SecurityScheme>(s =>
                s.Name == "bearerAuth" && s.Type == SchemeType.Http),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
