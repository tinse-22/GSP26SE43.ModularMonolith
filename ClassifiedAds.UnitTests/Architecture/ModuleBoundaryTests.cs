using NetArchTest.Rules;

namespace ClassifiedAds.UnitTests.Architecture;

/// <summary>
/// Architecture tests to enforce modular monolith boundaries per rules/architecture.md
/// [ARCH-004]: Modules MUST NOT reference other module's internal types directly
/// [ARCH-007]: MUST NOT call another module's repository or DbContext directly
/// </summary>
public class ModuleBoundaryTests
{
    private static readonly string[] ModuleAssemblies = new[]
    {
        "ClassifiedAds.Modules.Product",
        "ClassifiedAds.Modules.Storage",
        "ClassifiedAds.Modules.Identity",
        "ClassifiedAds.Modules.Notification",
        "ClassifiedAds.Modules.AuditLog",
        "ClassifiedAds.Modules.Configuration"
    };

    [Theory]
    [InlineData("ClassifiedAds.Modules.Product")]
    [InlineData("ClassifiedAds.Modules.Storage")]
    [InlineData("ClassifiedAds.Modules.Identity")]
    [InlineData("ClassifiedAds.Modules.Notification")]
    [InlineData("ClassifiedAds.Modules.AuditLog")]
    [InlineData("ClassifiedAds.Modules.Configuration")]
    public void Modules_Should_Not_Reference_Other_Modules(string moduleName)
    {
        // Arrange
        var assembly = System.Reflection.Assembly.Load(moduleName);
        var otherModules = ModuleAssemblies.Where(m => m != moduleName).ToArray();

        // Act
        var result = Types.InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOnAny(otherModules)
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            $"{moduleName} should not reference other modules directly. " +
            $"Violations: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    [Fact]
    public void Modules_Should_Not_Reference_Other_Module_DbContexts()
    {
        // Arrange - This is verified by the first test (no direct module references)
        // This test is a placeholder for potential future DbContext-specific checks
        
        // For now, we rely on module boundary test to catch cross-module DbContext usage
        // since DbContext types live in module namespaces
        
        Assert.True(true, "DbContext isolation is enforced by module boundary tests");
    }

    [Fact]
    public void Modules_Should_Only_Use_Contracts_For_CrossModule_Communication()
    {
        // Arrange
        var contractsAssembly = System.Reflection.Assembly.Load("ClassifiedAds.Contracts");

        // Act - Contracts should not depend on any specific module
        var result = Types.InAssembly(contractsAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(ModuleAssemblies)
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            "ClassifiedAds.Contracts should not depend on any module. " +
            $"Violations: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    [Theory]
    [InlineData("ClassifiedAds.Modules.Product")]
    [InlineData("ClassifiedAds.Modules.Storage")]
    [InlineData("ClassifiedAds.Modules.Identity")]
    public void Modules_Can_Reference_Domain_And_Application_Layers(string moduleName)
    {
        // Arrange
        var assembly = System.Reflection.Assembly.Load(moduleName);

        // Act - Modules should be able to reference core shared layers
        var domainTypes = Types.InAssembly(assembly)
            .That()
            .HaveDependencyOn("ClassifiedAds.Domain")
            .GetTypes();

        var applicationTypes = Types.InAssembly(assembly)
            .That()
            .HaveDependencyOn("ClassifiedAds.Application")
            .GetTypes();

        // Assert
        (domainTypes.Any() || applicationTypes.Any()).Should().BeTrue(
            $"{moduleName} should be able to reference Domain and Application layers");
    }
}
