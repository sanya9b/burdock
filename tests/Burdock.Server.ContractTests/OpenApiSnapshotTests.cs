using System.Text;
using System.Text.Json;
using Burdock.Server.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace Burdock.Server.ContractTests;

/// <summary>One PostgreSQL 18 container for the contract run; the host migrates every module schema on startup.</summary>
public sealed class ContractPostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18")
        .WithDatabase("burdock_contract")
        .WithUsername("burdock")
        .WithPassword("burdock-test")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync() => await _container.StartAsync().ConfigureAwait(false);

    public async ValueTask DisposeAsync() => await _container.DisposeAsync().ConfigureAwait(false);
}

/// <summary>
/// The published contract (spec 09 FR-09-25, NFR-09-12; spec 16 FR-16-11): the OpenAPI document the real host serves
/// must equal docs/api/openapi.v1.json. A difference means the API changed without updating the snapshot; set
/// BURDOCK_UPDATE_SNAPSHOTS=1 to rewrite it, review the diff, and get an ADR for anything breaking.
/// </summary>
public sealed class OpenApiSnapshotTests(ContractPostgresFixture postgres) : IClassFixture<ContractPostgresFixture>
{
    public const string UpdateVariable = "BURDOCK_UPDATE_SNAPSHOTS";
    private const string SnapshotRelativePath = "docs/api/openapi.v1.json";

    private static readonly string[] ExpectedPaths =
    [
        "/v1/config",
        "/v1/identity/anonymous",
        "/v1/identity/me",
        "/v1/content/manifest",
        "/v1/progress/events",
        "/v1/settings",
        "/v1/pronunciation/voices",
    ];

    [Fact]
    public async Task The_served_document_equals_the_committed_snapshot()
    {
        var actual = await FetchDocumentAsync();
        var snapshotPath = Path.Combine(RepositoryRoot(), SnapshotRelativePath);

        if (string.Equals(Environment.GetEnvironmentVariable(UpdateVariable), "1", StringComparison.Ordinal))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(snapshotPath)!);
            await File.WriteAllTextAsync(snapshotPath, actual, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), TestContext.Current.CancellationToken);
        }

        File.Exists(snapshotPath).ShouldBeTrue($"{SnapshotRelativePath} is missing; run the contract tests once with {UpdateVariable}=1 to create it.");
        var expected = Normalize(await File.ReadAllTextAsync(snapshotPath, TestContext.Current.CancellationToken));
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            throw new ShouldAssertException($"{SnapshotRelativePath} differs from the document the host serves.\n{FirstDifference(expected, actual)}\nIf the change is intended, rerun with {UpdateVariable}=1, review the diff and commit the snapshot (breaking changes need an ADR and a new route version, spec 16 FR-16-11).");
        }
    }

    [Fact]
    public async Task The_document_describes_v1_with_a_bearer_scheme_and_secured_endpoints()
    {
        using var document = JsonDocument.Parse(await FetchDocumentAsync());
        var root = document.RootElement;

        root.GetProperty("openapi").GetString().ShouldStartWith("3.");
        root.GetProperty("info").GetProperty("title").GetString().ShouldBe("Burdock API");
        root.GetProperty("info").GetProperty("version").GetString().ShouldBe(OpenApiContract.DocumentName);
        root.GetProperty("servers")[0].GetProperty("url").GetString().ShouldBe(OpenApiContract.PublicServerUrl);
        root.GetProperty("components").GetProperty("securitySchemes").TryGetProperty(OpenApiContract.BearerScheme, out _).ShouldBeTrue();

        var paths = root.GetProperty("paths");
        foreach (var path in ExpectedPaths)
        {
            paths.TryGetProperty(path, out _).ShouldBeTrue($"missing path {path}");
        }

        paths.TryGetProperty("/v1/connect/token", out _).ShouldBeFalse("the OpenIddict token endpoint is excluded from the description on purpose");
        foreach (var operation in paths.GetProperty("/v1/progress/events").EnumerateObject().Concat(paths.GetProperty("/v1/settings").EnumerateObject()))
        {
            operation.Value.TryGetProperty("security", out var security).ShouldBeTrue($"{operation.Name} /v1/progress|settings must require a token");
            security.EnumerateArray().Any(r => r.TryGetProperty(OpenApiContract.BearerScheme, out _)).ShouldBeTrue();
        }

        paths.GetProperty("/v1/identity/anonymous").GetProperty("post").TryGetProperty("security", out _).ShouldBeFalse("registration is anonymous");
    }

    private async Task<string> FetchDocumentAsync()
    {
        using var factory = new ContractApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(new Uri($"/openapi/{OpenApiContract.DocumentName}.json", UriKind.Relative), TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.IsSuccessStatusCode.ShouldBeTrue($"openapi endpoint answered {(int)response.StatusCode}: {body}");
        return Normalize(body);
    }

    /// <summary>Indented JSON with LF line endings, so the snapshot diffs line by line regardless of the serializer's whitespace.</summary>
    private static string Normalize(string json)
    {
        using var document = JsonDocument.Parse(json);
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            document.WriteTo(writer);
        }

        return Encoding.UTF8.GetString(buffer.ToArray()).Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd() + "\n";
    }

    private static string FirstDifference(string expected, string actual)
    {
        var expectedLines = expected.Split('\n');
        var actualLines = actual.Split('\n');
        for (var i = 0; i < Math.Max(expectedLines.Length, actualLines.Length); i++)
        {
            var e = i < expectedLines.Length ? expectedLines[i] : "<end>";
            var a = i < actualLines.Length ? actualLines[i] : "<end>";
            if (!string.Equals(e, a, StringComparison.Ordinal))
            {
                return $"First difference at line {i + 1}:\n  snapshot: {e.Trim()}\n  served:   {a.Trim()}";
            }
        }

        return "Documents differ only in length.";
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Burdock.Server.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root (Burdock.Server.slnx) not found above " + AppContext.BaseDirectory);
    }

    private sealed class ContractApiFactory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("ConnectionStrings:Burdock", connectionString);
            builder.UseSetting("Database:MigrateOnStartup", "true");
            builder.UseSetting("Auth:AllowInsecureHttp", "true");
        }
    }
}
