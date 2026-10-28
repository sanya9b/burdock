using Burdock.Domain.Exposure;
using Burdock.Presentation.Navigation;
using Burdock.Presentation.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Burdock.Presentation.Tests.Navigation;

/// <summary>burdock:// links from widgets (spec 04 §3.4, spec 02 §3.12): parsing, surface mapping and dispatch timing.</summary>
public sealed class DeepLinkTests
{
    private static readonly Guid Lexeme = Guid.Parse("0199a1c2-7f3e-7d21-9a44-2c0f5e4b8a10");

    [Theory]
    [InlineData("burdock://word/0199a1c2-7f3e-7d21-9a44-2c0f5e4b8a10?src=widget.small", ExposureSurface.WidgetHome)]
    [InlineData("burdock://word/0199A1C2-7F3E-7D21-9A44-2C0F5E4B8A10?src=widget.large", ExposureSurface.WidgetHome)]
    [InlineData("burdock://word/0199a1c2-7f3e-7d21-9a44-2c0f5e4b8a10?src=widget.accessoryRectangular", ExposureSurface.WidgetLock)]
    [InlineData("burdock://word/0199a1c2-7f3e-7d21-9a44-2c0f5e4b8a10?src=widget.standby", ExposureSurface.StandBy)]
    [InlineData("burdock://word/0199a1c2-7f3e-7d21-9a44-2c0f5e4b8a10?src=notification.tap", ExposureSurface.Notification)]
    [InlineData("burdock://word/0199a1c2-7f3e-7d21-9a44-2c0f5e4b8a10", ExposureSurface.InApp)]
    public void Word_links_carry_the_lexeme_and_the_surface_of_the_tap(string url, ExposureSurface surface)
    {
        DeepLink.TryParse(new Uri(url), out var link).ShouldBeTrue();

        link!.Kind.ShouldBe(DeepLinkKind.Word);
        link.LexemeId.ShouldBe(Lexeme);
        link.Surface.ShouldBe(surface);
        link.AdvanceCursor.ShouldBeFalse();
    }

    [Fact]
    public void Quiz_today_and_the_iOS16_next_action_parse_and_foreign_links_do_not()
    {
        DeepLink.TryParse(new Uri("burdock://quiz?src=widget"), out var quiz).ShouldBeTrue();
        DeepLink.TryParse(new Uri("burdock://today?src=widget.fallback"), out var today).ShouldBeTrue();
        DeepLink.TryParse(new Uri($"burdock://word/{Lexeme:D}?src=widget.small&action=next"), out var next).ShouldBeTrue();

        quiz!.Kind.ShouldBe(DeepLinkKind.Quiz);
        today!.Kind.ShouldBe(DeepLinkKind.Today);
        next!.AdvanceCursor.ShouldBeTrue();
        DeepLink.TryParse(new Uri("https://burdock.org/word/abc"), out _).ShouldBeFalse();
        DeepLink.TryParse(new Uri("burdock://word/not-a-guid"), out _).ShouldBeFalse();
        DeepLink.TryParse(null, out _).ShouldBeFalse();
    }

    [Fact]
    public void Destinations_follow_the_route_table()
    {
        var word = DeepLinkRouter.Destination(new DeepLink(DeepLinkKind.Word, Lexeme, "widget.small", ExposureSurface.WidgetHome, false));
        var quiz = DeepLinkRouter.Destination(new DeepLink(DeepLinkKind.Quiz, null, "widget", ExposureSurface.WidgetHome, false));
        var today = DeepLinkRouter.Destination(new DeepLink(DeepLinkKind.Today, null, string.Empty, ExposureSurface.InApp, false));

        word.Route.ShouldBe(Routes.Today.Word);
        word.Parameters!["id"].ShouldBe(Lexeme.ToString("D"));
        word.Parameters["surface"].ShouldBe("WidgetHome");
        quiz.Route.ShouldBe(Routes.Learn.Session);
        quiz.Parameters!["size"].ShouldBe("5");
        today.Route.ShouldBe(Routes.Today.Root);
        today.Parameters.ShouldBeNull();
    }

    [Fact]
    public async Task A_cold_start_link_waits_for_the_shell_and_a_warm_link_dispatches_at_once()
    {
        var navigation = new FakeNavigationService();
        var router = new DeepLinkRouter(navigation, NullLogger<DeepLinkRouter>.Instance);

        router.Accept(new Uri($"burdock://word/{Lexeme:D}?src=widget.small"));
        navigation.Visits.ShouldBeEmpty();
        router.Pending.ShouldNotBeNull();

        await router.FlushAsync(TestContext.Current.CancellationToken);
        navigation.Visits.Count.ShouldBe(1);
        navigation.Last.Route.ShouldBe(Routes.Today.Word);
        router.Pending.ShouldBeNull();

        router.Accept(new Uri("burdock://quiz?src=widget"));
        await Task.Yield();
        navigation.Visits.Count.ShouldBe(2);
        navigation.Last.Route.ShouldBe(Routes.Learn.Session);

        router.Accept(new Uri("https://example.org/ignored"));
        navigation.Visits.Count.ShouldBe(2);
    }
}
