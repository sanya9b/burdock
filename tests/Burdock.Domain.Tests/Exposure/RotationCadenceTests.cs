using Burdock.Domain.Exposure;
using Burdock.Domain.SharedKernel;

namespace Burdock.Domain.Tests.Exposure;

public sealed class RotationCadenceTests
{
    [Theory]
    [InlineData(15)]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(120)]
    [InlineData(240)]
    public void Create_accepts_every_preset(int minutes)
    {
        var cadence = RotationCadence.Create(minutes);

        cadence.IsSuccess.ShouldBeTrue();
        cadence.Value.Minutes.ShouldBe(minutes);
        cadence.Value.Interval.ShouldBe(TimeSpan.FromMinutes(minutes));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(45)]
    [InlineData(300)]
    public void Create_rejects_values_outside_the_presets(int minutes)
    {
        var cadence = RotationCadence.Create(minutes);

        cadence.IsFailure.ShouldBeTrue();
        cadence.Error!.Kind.ShouldBe(ErrorKind.Validation);
        cadence.Error.Code.ShouldBe("cadence.notPreset");
    }

    [Fact]
    public void Default_and_free_tier_are_sixty_minutes()
    {
        RotationCadence.Default.Minutes.ShouldBe(60);
        RotationCadence.FreeTier.ShouldBe(RotationCadence.Default);
        RotationCadence.PresetMinutes.ShouldBe([15, 30, 60, 120, 240]);
    }
}
