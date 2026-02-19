using ClassifiedAds.CrossCuttingConcerns.Exceptions;

namespace ClassifiedAds.UnitTests.CrossCuttingConcerns;

public class ConflictExceptionTests
{
    [Fact]
    public void Constructor_WithMessage_ShouldSetMessage()
    {
        var ex = new ConflictException("Conflict happened.");

        ex.Message.Should().Be("Conflict happened.");
        ex.ReasonCode.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithReasonCode_ShouldSetReasonCodeAndMessage()
    {
        var ex = new ConflictException("ORDER_CONFIRMATION_REQUIRED", "Order confirmation is required.");

        ex.Message.Should().Be("Order confirmation is required.");
        ex.ReasonCode.Should().Be("ORDER_CONFIRMATION_REQUIRED");
    }
}
