using System.Security.Cryptography;
using Burdock.Domain.SharedKernel;
using Burdock.Server.Modules.Identity.Application;
using Burdock.Server.Modules.Identity.Domain;

namespace Burdock.Server.Tests;

/// <summary>Pure rules of the Identity module (spec 09 §3.3): credentials, platforms, nicknames.</summary>
public sealed class IdentityDomainTests
{
    [Fact]
    public void A_secret_parses_from_standard_and_url_safe_base64_and_hashes_the_same()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var standard = Convert.ToBase64String(bytes);
        var urlSafe = standard.TrimEnd('=').Replace('+', '-').Replace('/', '_');

        var a = InstallationSecret.Parse(standard);
        var b = InstallationSecret.Parse(urlSafe);

        a.IsSuccess.ShouldBeTrue();
        b.IsSuccess.ShouldBeTrue();
        a.Value.Hash().ShouldBe(b.Value.Hash());
        a.Value.Hash().ShouldBe(SHA256.HashData(bytes));
        b.Value.Matches(a.Value.Hash()).ShouldBeTrue();
    }

    [Theory]
    [InlineData(null, "identity.secret_missing")]
    [InlineData("", "identity.secret_missing")]
    [InlineData("not base64!", "identity.secret_invalid")]
    [InlineData("c2hvcnQ=", "identity.secret_invalid")]
    public void Bad_secrets_are_rejected(string? encoded, string code)
    {
        var result = InstallationSecret.Parse(encoded);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(code);
    }

    [Fact]
    public void An_installation_matches_only_its_own_secret()
    {
        var secret = InstallationSecret.Parse(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))).Value;
        var other = InstallationSecret.Parse(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))).Value;
        var installation = Installation.Register(Guid.NewGuid(), LearnerId.From(Guid.NewGuid()), secret, ClientPlatform.Ios, " 1.2.3 ", DateTimeOffset.UnixEpoch).Value;

        installation.Matches(secret).ShouldBeTrue();
        installation.Matches(other).ShouldBeFalse();
        installation.AppVersion.ShouldBe("1.2.3");
        installation.IsActive.ShouldBeTrue();
    }

    [Theory]
    [InlineData("ios", ClientPlatform.Ios)]
    [InlineData("Android", ClientPlatform.Android)]
    public void Platforms_parse_case_insensitively(string raw, ClientPlatform expected) => Installation.ParsePlatform(raw).Value!.ShouldBe(expected);

    [Fact]
    public void Unknown_platforms_are_rejected() => Installation.ParsePlatform("web").Error!.Code.ShouldBe("identity.platform_invalid");

    [Fact]
    public void Generated_nicknames_follow_the_onboarding_pattern()
    {
        var random = new SequenceRandom(0, 1000);

        Nicknames.Generate(random).ShouldBe("Хоробрий Реп’ях #1000");
        Nicknames.Compose("Швидкий", 9999).ShouldBe("Швидкий Реп’ях #9999");
        Nicknames.Adjectives.Take(10).ShouldBe(["Хоробрий", "Уважний", "Веселий", "Кмітливий", "Допитливий", "Впертий", "Сонячний", "Меткий", "Спокійний", "Швидкий"]);
    }

    [Fact]
    public void Renaming_applies_the_shared_profile_rules()
    {
        var account = UserAccount.Register(LearnerId.From(Guid.NewGuid()), "Хоробрий Реп’ях #1234", DateTimeOffset.UnixEpoch);

        account.Rename("  Олесь ").IsSuccess.ShouldBeTrue();
        account.Nickname.ShouldBe("Олесь");
        account.Rename("x").Error!.Code.ShouldBe("profile.nickname.length");
        account.Rename("tab\there").Error!.Code.ShouldBe("profile.nickname.characters");
        account.Nickname.ShouldBe("Олесь");
    }

    private sealed class SequenceRandom(params int[] values) : IRandomSource
    {
        private int _index;

        public int Next(int minInclusive, int maxExclusive) => values[_index++ % values.Length];
    }
}
