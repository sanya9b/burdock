using System.Reflection;
using Burdock.Server.Modules.Analytics;
using Burdock.Server.Modules.Content;
using Burdock.Server.Modules.Entitlements;
using Burdock.Server.Modules.Identity;
using Burdock.Server.Modules.LearnerProgress;
using Burdock.Server.Modules.Notifications;
using Burdock.Server.Modules.Rewards;
using Burdock.Server.Modules.SettingsSync;
using Burdock.Server.Shared.Modules;
using NetArchTest.Rules;
using ArchResult = NetArchTest.Rules.TestResult;

namespace Burdock.Server.ArchitectureTests;

/// <summary>Modules never reference each other, and no server code uses the FSRS scheduler (spec 09 §1.1, §1.3, T23).</summary>
public sealed class ModuleIsolationTests
{
    private static readonly IReadOnlyDictionary<string, Assembly> Modules = new Dictionary<string, Assembly>(StringComparer.Ordinal)
    {
        ["Burdock.Server.Modules.Identity"] = typeof(IdentityModule).Assembly,
        ["Burdock.Server.Modules.Content"] = typeof(ContentModule).Assembly,
        ["Burdock.Server.Modules.LearnerProgress"] = typeof(LearnerProgressModule).Assembly,
        ["Burdock.Server.Modules.Rewards"] = typeof(RewardsModule).Assembly,
        ["Burdock.Server.Modules.Entitlements"] = typeof(EntitlementsModule).Assembly,
        ["Burdock.Server.Modules.SettingsSync"] = typeof(SettingsSyncModule).Assembly,
        ["Burdock.Server.Modules.Notifications"] = typeof(NotificationsModule).Assembly,
        ["Burdock.Server.Modules.Analytics"] = typeof(AnalyticsModule).Assembly,
    };

    public static TheoryData<string> ModuleNames => [.. Modules.Keys];

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void A_module_does_not_depend_on_other_modules(string moduleName)
    {
        var others = Modules.Keys.Where(k => !string.Equals(k, moduleName, StringComparison.Ordinal)).ToArray();

        var result = Types.InAssembly(Modules[moduleName])
            .ShouldNot()
            .HaveDependencyOnAny(others)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    [Fact]
    public void Server_code_never_uses_the_scheduler_namespace()
    {
        var assemblies = Modules.Values.Append(typeof(IModule).Assembly).Append(typeof(Program).Assembly).ToArray();

        var result = Types.InAssemblies(assemblies)
            .ShouldNot()
            .HaveDependencyOn("Burdock.Domain.Scheduling")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    [Fact]
    public void Shared_kernel_does_not_depend_on_modules_or_the_host()
    {
        var result = Types.InAssembly(typeof(IModule).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny([.. Modules.Keys, "Burdock.Server.Api"])
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    [Fact]
    public void Every_module_keeps_its_types_in_the_expected_folders()
    {
        // Domain/ Application/ Infrastructure/ Endpoints/ map to namespaces; the module root may hold only the IModule implementation.
        foreach (var (name, assembly) in Modules)
        {
            var rootTypes = assembly.GetTypes()
                .Where(t => t.IsPublic && string.Equals(t.Namespace, name, StringComparison.Ordinal))
                .ToArray();

            rootTypes.ShouldAllBe(t => typeof(IModule).IsAssignableFrom(t), $"{name} root namespace may contain only the module class");
        }
    }

    private static string Describe(ArchResult result)
        => result.IsSuccessful ? string.Empty : "Violations: " + string.Join(", ", result.FailingTypeNames ?? []);
}
