using ClassifiedAds.Modules.TestReporting.Services;

namespace ClassifiedAds.UnitTests.TestReporting;

public class CoverageCalculatorTests
{
    [Fact]
    public void Calculate_ShouldUseScopedEndpointsAndMetadataDeterministically()
    {
        // Arrange
        var service = new CoverageCalculator();
        var context = ReportTestData.CreateContext();
        var metadata = ReportTestData.CreateMetadata();

        // Act
        var result = service.Calculate(context, metadata);

        // Assert
        result.TestRunId.Should().Be(ReportTestData.RunId);
        result.TotalEndpoints.Should().Be(3);
        result.TestedEndpoints.Should().Be(2);
        result.CoveragePercent.Should().Be(66.67m);
        result.ByMethod.Should().Equal(new Dictionary<string, decimal>
        {
            ["GET"] = 50m,
            ["POST"] = 100m,
        });
        result.ByTag.Should().Equal(new Dictionary<string, decimal>
        {
            ["orders"] = 100m,
            ["payments"] = 0m,
            ["users"] = 100m,
        });
        result.UncoveredPaths.Should().Equal("/api/payments");
    }
}
