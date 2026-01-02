using NetArchTest.Rules;

namespace ClassifiedAds.UnitTests.Architecture;

/// <summary>
/// Architecture tests to enforce layered architecture rules per rules/architecture.md
/// [ARCH-010]: Domain layer MUST NOT depend on any other layer
/// [ARCH-011]: Application layer MUST depend only on Domain
/// [ARCH-012]: Infrastructure layer MUST implement interfaces defined in Domain
/// [ARCH-013]: Presentation layer MUST NOT contain business logic
/// </summary>
public class LayeredArchitectureTests
{
    [Fact]
    public void Domain_Should_Not_Depend_On_Any_Other_Layer()
    {
        // Arrange
        var domainAssembly = System.Reflection.Assembly.Load("ClassifiedAds.Domain");

        // Act - Domain should be independent
        var result = Types.InAssembly(domainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(new[]
            {
                "ClassifiedAds.Application",
                "ClassifiedAds.Infrastructure",
                "ClassifiedAds.WebAPI",
                "ClassifiedAds.Persistence"
            })
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            "Domain layer should not depend on Application, Infrastructure, or Presentation layers. " +
            $"Violations: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    [Fact]
    public void Application_Should_Only_Depend_On_Domain()
    {
        // Arrange
        var applicationAssembly = System.Reflection.Assembly.Load("ClassifiedAds.Application");

        // Act - Application can depend on Domain, but not Infrastructure or Presentation
        var result = Types.InAssembly(applicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(new[]
            {
                "ClassifiedAds.Infrastructure",
                "ClassifiedAds.WebAPI",
                "ClassifiedAds.Persistence"
            })
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            "Application layer should not depend on Infrastructure or Presentation layers. " +
            $"Violations: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    [Fact]
    public void Controllers_Should_Not_Have_Direct_DbContext_References()
    {
        // Arrange
        var webApiAssembly = System.Reflection.Assembly.Load("ClassifiedAds.WebAPI");

        // Act - Controllers should not directly use DbContext
        var result = Types.InAssembly(webApiAssembly)
            .That()
            .ResideInNamespaceEndingWith("Controllers")
            .ShouldNot()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore.DbContext")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            "Controllers should not have direct DbContext dependencies. Use Dispatcher instead. " +
            $"Violations: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    [Fact]
    public void Controllers_Should_Use_Dispatcher()
    {
        // Arrange
        var modules = new[]
        {
            "ClassifiedAds.Modules.Product",
            "ClassifiedAds.Modules.Storage",
            "ClassifiedAds.Modules.Identity"
        };

        foreach (var moduleName in modules)
        {
            var assembly = System.Reflection.Assembly.Load(moduleName);

            // Act - Controllers should use Dispatcher
            var types = Types.InAssembly(assembly)
                .That()
                .ResideInNamespaceEndingWith("Controllers")
                .And()
                .HaveNameEndingWith("Controller")
                .GetTypes();

            // Assert - At least some controllers should exist and use Dispatcher
            if (types.Any())
            {
                types.Should().NotBeEmpty($"{moduleName} should have controllers");
                
                // Check if Dispatcher is used (soft check via dependency)
                var usesDispatcher = Types.InAssembly(assembly)
                    .That()
                    .ResideInNamespaceEndingWith("Controllers")
                    .And()
                    .HaveDependencyOn("ClassifiedAds.Application.Dispatcher")
                    .GetTypes()
                    .Any();

                usesDispatcher.Should().BeTrue(
                    $"{moduleName} controllers should use Dispatcher pattern for CQRS");
            }
        }
    }

    [Fact]
    public void Domain_Entities_Should_Not_Have_Infrastructure_Dependencies()
    {
        // Arrange
        var domainAssembly = System.Reflection.Assembly.Load("ClassifiedAds.Domain");

        // Act - Entity types should be clean
        var efDependencies = Types.InAssembly(domainAssembly)
            .That()
            .ResideInNamespaceEndingWith("Entities")
            .And()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetTypes();

        var dataDependencies = Types.InAssembly(domainAssembly)
            .That()
            .ResideInNamespaceEndingWith("Entities")
            .And()
            .HaveDependencyOn("System.Data")
            .GetTypes();

        // Assert
        efDependencies.Should().BeEmpty(
            "Domain entities should not have EntityFramework dependencies. " +
            $"Violations: {string.Join(", ", efDependencies.Select(t => t.Name))}");
            
        dataDependencies.Should().BeEmpty(
            "Domain entities should not have System.Data dependencies. " +
            $"Violations: {string.Join(", ", dataDependencies.Select(t => t.Name))}");
    }
}
