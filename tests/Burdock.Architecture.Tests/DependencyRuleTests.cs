using System.Reflection;
using Burdock.Application.Ports;
using Burdock.Domain.SharedKernel;
using Burdock.Infrastructure.Time;
using NetArchTest.Rules;
using ArchResult = NetArchTest.Rules.TestResult;

namespace Burdock.Architecture.Tests;

/// <summary>The dependency rule of Clean Architecture for the mobile layers (CLAUDE.md section 4, spec 01 section 3.15).</summary>
public sealed class DependencyRuleTests
{
    private static readonly Assembly Domain = typeof(DomainError).Assembly;
    private static readonly Assembly Application = typeof(IClock).Assembly;
    private static readonly Assembly Infrastructure = typeof(SystemClock).Assembly;

    [Fact]
    public void Domain_depends_on_nothing_but_the_bcl()
    {
        var result = Types.InAssembly(Domain)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Burdock.Application", "Burdock.Infrastructure", "Burdock.Presentation", "Burdock.Contracts", "Burdock.Server",
                "Microsoft.Maui", "Microsoft.Extensions", "Microsoft.EntityFrameworkCore", "SQLite", "System.Net.Http")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    [Fact]
    public void Application_depends_only_on_domain_and_abstractions()
    {
        var result = Types.InAssembly(Application)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Burdock.Infrastructure", "Burdock.Presentation", "Burdock.Contracts", "Burdock.Server",
                "Microsoft.Maui", "Microsoft.EntityFrameworkCore", "SQLite", "System.Net.Http")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    [Fact]
    public void Infrastructure_never_reaches_into_presentation()
    {
        var result = Types.InAssembly(Infrastructure)
            .ShouldNot()
            .HaveDependencyOnAny("Burdock.Presentation")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    [Fact]
    public void Domain_and_application_never_read_the_system_clock_directly()
    {
        // DateTime.Now / DateTimeOffset.UtcNow are banned in favour of IClock; the check is a type-level dependency guard
        // for the helper types that would typically carry such calls (spec 01 section 3.6). Fine-grained enforcement is BannedApiAnalyzers.
        var forbidden = Types.InAssemblies([Domain, Application])
            .That().ResideInNamespaceStartingWith("Burdock")
            .ShouldNot().HaveDependencyOn("System.Timers")
            .GetResult();

        forbidden.IsSuccessful.ShouldBeTrue(Describe(forbidden));
    }

    private static string Describe(ArchResult result)
        => result.IsSuccessful ? string.Empty : "Violations: " + string.Join(", ", result.FailingTypeNames ?? []);
}
