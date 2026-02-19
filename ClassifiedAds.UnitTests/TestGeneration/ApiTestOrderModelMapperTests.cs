using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using ClassifiedAds.Modules.TestGeneration.Services;
using System;
using System.Collections.Generic;

namespace ClassifiedAds.UnitTests.TestGeneration;

public class ApiTestOrderModelMapperTests
{
    private readonly Mock<IApiTestOrderService> _orderServiceMock;

    public ApiTestOrderModelMapperTests()
    {
        _orderServiceMock = new Mock<IApiTestOrderService>();
    }

    [Fact]
    public void ToModel_Should_ReturnNullUserModifiedOrder_WhenEntityFieldIsNull()
    {
        // Arrange
        var proposal = CreateProposal(userModifiedOrder: null, appliedOrder: null);
        SetupDeserialization();

        // Act
        var result = ApiTestOrderModelMapper.ToModel(proposal, _orderServiceMock.Object);

        // Assert
        result.UserModifiedOrder.Should().BeNull();
    }

    [Fact]
    public void ToModel_Should_ReturnNullAppliedOrder_WhenEntityFieldIsNull()
    {
        // Arrange
        var proposal = CreateProposal(userModifiedOrder: null, appliedOrder: null);
        SetupDeserialization();

        // Act
        var result = ApiTestOrderModelMapper.ToModel(proposal, _orderServiceMock.Object);

        // Assert
        result.AppliedOrder.Should().BeNull();
    }

    [Fact]
    public void ToModel_Should_ReturnPopulatedUserModifiedOrder_WhenEntityFieldHasData()
    {
        // Arrange
        var json = "[{\"endpointId\":\"00000000-0000-0000-0000-000000000001\",\"orderIndex\":1}]";
        var proposal = CreateProposal(userModifiedOrder: json, appliedOrder: null);

        _orderServiceMock.Setup(x => x.DeserializeOrderJson(json))
            .Returns(new List<ApiOrderItemModel>
            {
                new() { EndpointId = Guid.NewGuid(), OrderIndex = 1 },
            });
        _orderServiceMock.Setup(x => x.DeserializeOrderJson(It.Is<string>(s => string.IsNullOrWhiteSpace(s))))
            .Returns(new List<ApiOrderItemModel>());
        _orderServiceMock.Setup(x => x.DeserializeOrderJson(It.Is<string>(s => s != null && s == proposal.ProposedOrder)))
            .Returns(new List<ApiOrderItemModel>
            {
                new() { EndpointId = Guid.NewGuid(), OrderIndex = 1 },
            });

        // Act
        var result = ApiTestOrderModelMapper.ToModel(proposal, _orderServiceMock.Object);

        // Assert
        result.UserModifiedOrder.Should().NotBeNull();
        result.UserModifiedOrder.Should().HaveCount(1);
    }

    [Fact]
    public void ToModel_Should_ReturnNull_WhenProposalIsNull()
    {
        // Act
        var result = ApiTestOrderModelMapper.ToModel(null, _orderServiceMock.Object);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ToModel_Should_ConvertRowVersionToBase64()
    {
        // Arrange
        var rowVersionBytes = new byte[] { 1, 2, 3, 4, 5 };
        var proposal = CreateProposal(userModifiedOrder: null, appliedOrder: null);
        proposal.RowVersion = rowVersionBytes;
        SetupDeserialization();

        // Act
        var result = ApiTestOrderModelMapper.ToModel(proposal, _orderServiceMock.Object);

        // Assert
        result.RowVersion.Should().Be(Convert.ToBase64String(rowVersionBytes));
    }

    [Fact]
    public void ParseRowVersion_Should_ThrowValidationException_WhenNull()
    {
        // Act
        var act = () => ApiTestOrderModelMapper.ParseRowVersion(null);

        // Assert
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void ParseRowVersion_Should_ThrowValidationException_WhenInvalidBase64()
    {
        // Act
        var act = () => ApiTestOrderModelMapper.ParseRowVersion("not-valid-base64!@#$");

        // Assert
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void ParseRowVersion_Should_ReturnBytes_WhenValidBase64()
    {
        // Arrange
        var expected = new byte[] { 1, 2, 3 };
        var base64 = Convert.ToBase64String(expected);

        // Act
        var result = ApiTestOrderModelMapper.ParseRowVersion(base64);

        // Assert
        result.Should().BeEquivalentTo(expected);
    }

    #region Helpers

    private static TestOrderProposal CreateProposal(string userModifiedOrder, string appliedOrder)
    {
        return new TestOrderProposal
        {
            Id = Guid.NewGuid(),
            TestSuiteId = Guid.NewGuid(),
            ProposalNumber = 1,
            Status = ProposalStatus.Pending,
            Source = ProposalSource.Ai,
            ProposedOrder = "[{\"endpointId\":\"00000000-0000-0000-0000-000000000001\",\"orderIndex\":1}]",
            UserModifiedOrder = userModifiedOrder,
            AppliedOrder = appliedOrder,
        };
    }

    private void SetupDeserialization()
    {
        _orderServiceMock.Setup(x => x.DeserializeOrderJson(It.Is<string>(s => !string.IsNullOrWhiteSpace(s))))
            .Returns(new List<ApiOrderItemModel>
            {
                new() { EndpointId = Guid.NewGuid(), OrderIndex = 1 },
            });
        _orderServiceMock.Setup(x => x.DeserializeOrderJson(It.Is<string>(s => string.IsNullOrWhiteSpace(s))))
            .Returns(new List<ApiOrderItemModel>());
    }

    #endregion
}
