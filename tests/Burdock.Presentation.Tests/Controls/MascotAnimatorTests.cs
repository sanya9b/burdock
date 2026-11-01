using Burdock.Presentation.Controls;
using Burdock.Presentation.Controls.Mascot;

namespace Burdock.Presentation.Tests.Controls;

/// <summary>
/// The life of Реп’яшок (spec 02 §3.8 «Життя Реп’яшка», §3.10) as a deterministic function of time: breathing keeps the volume, blinks stay under
/// the flicker limit, gestures are short and return to rest, moods crossfade, feeding ends with a gulp, growth springs without shrinking.
/// </summary>
public sealed class MascotAnimatorTests
{
    private const double Frame = 1 / 60.0;

    [Fact]
    public void Rest_pose_before_any_tick_matches_the_mood()
    {
        var animator = new MascotAnimator(MascotMood.Cheer, 0.4, new Random(1));

        animator.Pose.ShouldBe(MascotPose.Rest(MascotMood.Cheer, 0.4));
        animator.IsBusy.ShouldBeFalse();
    }

    [Fact]
    public void Same_seed_gives_the_same_life()
    {
        var a = new MascotAnimator(MascotMood.Proud, 0.5, new Random(7)) { Idle = true };
        var b = new MascotAnimator(MascotMood.Proud, 0.5, new Random(7)) { Idle = true };

        for (var i = 0; i < 60 * 30; i++)
        {
            a.Advance(Frame);
            b.Advance(Frame);
            a.Pose.ShouldBe(b.Pose);
        }
    }

    [Fact]
    public void Advance_clamps_a_stalled_frame_to_fifty_milliseconds()
    {
        var animator = new MascotAnimator(MascotMood.Proud, 0.5, new Random(1));

        animator.Advance(5);

        animator.Time.ShouldBe(0.05, 1e-9);
    }

    [Fact]
    public void Without_idle_nothing_moves_on_its_own()
    {
        var animator = new MascotAnimator(MascotMood.Cheer, 0.5, new Random(1));

        Run(animator, 10, pose =>
        {
            pose.ScaleX.ShouldBe(1);
            pose.ScaleY.ShouldBe(1);
            pose.OffsetY.ShouldBe(0);
            pose.Tilt.ShouldBe(0);
            pose.Face.ShouldBe(FaceParams.For(MascotMood.Cheer));
        });
        animator.IsBusy.ShouldBeFalse();
        animator.IsLively.ShouldBeFalse();
    }

    [Fact]
    public void Breathing_is_subtle_and_keeps_the_volume()
    {
        // Sad has no gestures, so only the breath moves the body.
        var animator = new MascotAnimator(MascotMood.Sad, 0.5, new Random(2)) { Idle = true };
        var minY = double.MaxValue;
        var maxY = double.MinValue;

        Run(animator, 12, pose =>
        {
            minY = Math.Min(minY, pose.ScaleY);
            maxY = Math.Max(maxY, pose.ScaleY);
            (pose.ScaleX * pose.ScaleY).ShouldBe(1, 0.012);
            Math.Abs(pose.Tilt).ShouldBeLessThanOrEqualTo(MascotChoreography.SwayAmplitude + 1e-9);
        });

        maxY.ShouldBeGreaterThan(1.015);
        minY.ShouldBeLessThan(0.985);
        maxY.ShouldBeLessThanOrEqualTo(1 + MascotChoreography.BreathAmplitude + 1e-9);
    }

    [Fact]
    public void Open_eyes_blink_often_enough_but_never_flicker()
    {
        var animator = new MascotAnimator(MascotMood.Cheer, 0.5, new Random(3)) { Idle = true };
        var starts = new List<double>();
        var closed = false;
        var closedSince = 0.0;

        Run(animator, 30, pose =>
        {
            var shut = pose.Face.EyeOpen < 0.5;
            if (shut)
            {
                animator.IsLively.ShouldBeTrue();
            }

            if (shut && !closed)
            {
                starts.Add(animator.Time);
                closedSince = animator.Time;
            }
            else if (!shut && closed)
            {
                (animator.Time - closedSince).ShouldBeLessThan(0.3);
            }

            closed = shut;
            pose.Face.EyeOpen.ShouldBeInRange(0, 1);
        });

        starts.Count.ShouldBeGreaterThanOrEqualTo(5);
        foreach (var start in starts)
        {
            starts.Count(s => s >= start && s < start + 1).ShouldBeLessThanOrEqualTo(3);
        }
    }

    [Fact]
    public void Closed_eyed_moods_peek_now_and_then_and_close_again()
    {
        var animator = new MascotAnimator(MascotMood.Proud, 0.5, new Random(3)) { Idle = true };
        var openSpans = new List<double>();
        var openSince = double.NaN;

        Run(animator, 90, pose =>
        {
            var open = pose.Face.EyeOpen > 0.9;
            if (open && double.IsNaN(openSince))
            {
                openSince = animator.Time;
            }
            else if (!open && !double.IsNaN(openSince))
            {
                openSpans.Add(animator.Time - openSince);
                openSince = double.NaN;
            }
        });

        openSpans.ShouldNotBeEmpty();
        openSpans.ShouldAllBe(span => span < MascotChoreography.PeekDuration);
        animator.Pose.Face.EyeOpen.ShouldBeLessThan(0.2);
    }

    [Fact]
    public void Gestures_are_short_stay_inside_the_frame_and_return_to_rest()
    {
        var animator = new MascotAnimator(MascotMood.Party, 0.5, new Random(5)) { Idle = true };
        var airborneSince = double.NaN;
        var hops = 0;

        Run(animator, 90, pose =>
        {
            pose.OffsetY.ShouldBeInRange(-MascotChoreography.HopHeight - 1e-9, 0);
            Math.Abs(pose.Tilt).ShouldBeLessThan(MascotChoreography.SwayAmplitude + MascotChoreography.WiggleAmplitude + 1e-9);
            pose.ScaleY.ShouldBeInRange(0.8, 1.2);
            if (pose.OffsetY < 0 && double.IsNaN(airborneSince))
            {
                airborneSince = animator.Time;
                hops++;
            }
            else if (pose.OffsetY == 0 && !double.IsNaN(airborneSince))
            {
                (animator.Time - airborneSince).ShouldBeLessThan(MascotChoreography.HopDuration);
                airborneSince = double.NaN;
            }
        });

        hops.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Mood_change_crossfades_face_and_mouth_over_the_base_duration()
    {
        var animator = new MascotAnimator(MascotMood.Cheer, 0.5, new Random(1));

        animator.SetMood(MascotMood.Sad, animate: true);
        animator.IsBusy.ShouldBeTrue();
        Run(animator, 0.1);

        var mid = animator.Pose;
        mid.MoodBlend.ShouldBeInRange(0.01, 0.99);
        mid.MouthFrom.ShouldBe(MouthKind.Smile);
        mid.MouthTo.ShouldBe(MouthKind.Flat);
        mid.Face.BrowTiltL.ShouldBeInRange(-5, -0.01);

        Run(animator, 0.2);
        animator.Pose.MoodBlend.ShouldBe(1);
        animator.Pose.Face.ShouldBe(FaceParams.For(MascotMood.Sad));
        animator.IsBusy.ShouldBeFalse();
    }

    [Fact]
    public void Mood_change_without_animation_snaps()
    {
        var animator = new MascotAnimator(MascotMood.Cheer, 0.5, new Random(1));

        animator.SetMood(MascotMood.Sleep, animate: false);

        animator.Pose.Face.ShouldBe(FaceParams.For(MascotMood.Sleep));
        animator.Pose.MoodBlend.ShouldBe(1);
        animator.IsBusy.ShouldBeFalse();
    }

    [Fact]
    public void Growth_springs_up_with_a_small_overshoot_and_never_dips_below_the_start()
    {
        var animator = new MascotAnimator(MascotMood.Party, 0.3, new Random(1));
        var min = double.MaxValue;
        var max = double.MinValue;

        animator.SetGrowth(0.5, animate: true, settleSeconds: 0.6);
        Run(animator, 1.2, pose =>
        {
            min = Math.Min(min, pose.Growth);
            max = Math.Max(max, pose.Growth);
        });

        min.ShouldBeGreaterThanOrEqualTo(0.3 - 1e-9);
        max.ShouldBeGreaterThan(0.5);
        max.ShouldBeLessThan(0.5 + 0.2 * 0.2);
        animator.Pose.Growth.ShouldBe(0.5, 1e-3);
        animator.Growth.ShouldBe(0.5);
    }

    [Fact]
    public void Growth_without_animation_snaps()
    {
        var animator = new MascotAnimator(MascotMood.Party, 0.3, new Random(1));

        animator.SetGrowth(0.9, animate: false, settleSeconds: 0.6);

        animator.Pose.Growth.ShouldBe(0.9);
        animator.IsBusy.ShouldBeFalse();
    }

    [Fact]
    public void Feeding_opens_the_eyes_rounds_the_mouth_gulps_and_finishes_once()
    {
        var animator = new MascotAnimator(MascotMood.Party, 0.5, new Random(4));
        var finished = 0;
        animator.FeedFinished += () => finished++;
        var mouthOpened = false;
        var eyesOpenMidFlight = false;
        var minScaleAfterLast = double.MaxValue;
        double? finishedAt = null;

        animator.StartFeed(8, 0.6);
        animator.IsFeeding.ShouldBeTrue();
        animator.Seeds.Count.ShouldBe(8);
        Run(animator, 2, pose =>
        {
            mouthOpened |= pose.MouthOpen > 0.5;
            if (animator.Time is > 0.25 and < 0.35)
            {
                eyesOpenMidFlight |= pose.Face.EyeOpen > 0.9;
            }

            if (animator.Time is > 0.6 and < 0.9)
            {
                minScaleAfterLast = Math.Min(minScaleAfterLast, pose.ScaleY);
            }

            if (finished == 1 && finishedAt is null)
            {
                finishedAt = animator.Time;
            }
        });

        mouthOpened.ShouldBeTrue();
        eyesOpenMidFlight.ShouldBeTrue();
        minScaleAfterLast.ShouldBeLessThan(0.97);
        finished.ShouldBe(1);
        finishedAt.ShouldNotBeNull();
        finishedAt.Value.ShouldBeInRange(0.6, 1.3);
        animator.IsFeeding.ShouldBeFalse();
        animator.Seeds.ShouldBeEmpty();
        animator.IsBusy.ShouldBeFalse();
        animator.Pose.ScaleY.ShouldBe(1, 0.005);
    }

    [Fact]
    public void Poke_hops_lifts_the_brows_and_settles()
    {
        var animator = new MascotAnimator(MascotMood.Cheer, 0.5, new Random(1));
        var rest = FaceParams.For(MascotMood.Cheer);
        var lift = double.MinValue;
        var peak = double.MaxValue;

        animator.Poke();
        animator.IsBusy.ShouldBeTrue();
        Run(animator, 2.5, pose =>
        {
            peak = Math.Min(peak, pose.OffsetY);
            lift = Math.Max(lift, pose.Face.BrowLiftL - rest.BrowLiftL);
        });

        peak.ShouldBeLessThan(-0.1);
        lift.ShouldBeGreaterThan(3);
        animator.IsBusy.ShouldBeFalse();
        animator.Pose.OffsetY.ShouldBe(0);
        animator.Pose.ScaleY.ShouldBe(1, 0.005);
        animator.Pose.Face.ShouldBe(rest);
    }

    [Fact]
    public void Poke_while_asleep_only_stirs()
    {
        var animator = new MascotAnimator(MascotMood.Sleep, 0.5, new Random(1));
        var tilted = false;

        animator.Poke();
        Run(animator, 1.5, pose =>
        {
            pose.OffsetY.ShouldBe(0);
            tilted |= Math.Abs(pose.Tilt) > 1;
        });

        tilted.ShouldBeTrue();
    }

    [Fact]
    public void Spring_overshoots_by_its_damping_ratio_and_settles_in_time()
    {
        var spring = new Spring(0.6, 0.5, 0) { Target = 1 };
        var max = 0.0;

        for (var t = 0.0; t < 0.6; t += Frame)
        {
            spring.Step(Frame);
            max = Math.Max(max, spring.Value);
        }

        max.ShouldBeInRange(1.1, 1.2);
        spring.Value.ShouldBe(1, 0.03);
        for (var t = 0.0; t < 1.0; t += Frame)
        {
            spring.Step(Frame);
        }

        spring.IsActive.ShouldBeFalse();
        spring.Value.ShouldBe(1);
    }

    [Fact]
    public void Metamorphosis_holds_the_old_form_gathers_light_bursts_pops_the_new_form_and_finishes_once()
    {
        var animator = new MascotAnimator(MascotMood.Party, 0.62, new Random(9));
        var bursts = 0;
        var finished = 0;
        double? finishedAt = null;
        animator.Burst += () => bursts++;
        animator.MetamorphosisFinished += () => { finished++; finishedAt ??= animator.Time; };
        var glowBeforeBurst = 0.0;
        var flash = 0.0;
        var ringSeen = false;
        var pop = 0.0;
        var growth = 0.0;
        var eyesShutBeforeBurst = false;
        var eyesWideAfterBurst = false;
        var sparksSeen = false;

        animator.StartMetamorphosis(0.76);
        animator.IsEvolving.ShouldBeTrue();
        animator.IsBusy.ShouldBeTrue();
        Run(animator, 4.5, pose =>
        {
            var t = animator.Time;
            if (t < 1.1)
            {
                pose.Growth.ShouldBe(0.62);
                pose.Ring.ShouldBe(0);
                glowBeforeBurst = Math.Max(glowBeforeBurst, pose.Glow);
                sparksSeen |= animator.Sparks.Count > 0 && animator.Sparks.Any(sp => sp.Alpha > 0.5);
            }

            if (t is > 0.9 and < 1.05)
            {
                eyesShutBeforeBurst |= pose.Face.EyeOpen < 0.05;
            }

            if (t is > 1.2 and < 1.5)
            {
                eyesWideAfterBurst |= pose.Face.EyeOpen > 0.9 && pose.Face.EyeScale > 1.1 && pose.MouthOpen > 0.5;
            }

            flash = Math.Max(flash, pose.Flash);
            ringSeen |= pose.Ring is > 0 and < 1;
            pop = Math.Max(pop, pose.Pop);
            growth = Math.Max(growth, pose.Growth);
            pose.Pop.ShouldBeGreaterThanOrEqualTo(0);
        });

        glowBeforeBurst.ShouldBeGreaterThan(0.9);
        sparksSeen.ShouldBeTrue();
        eyesShutBeforeBurst.ShouldBeTrue();
        flash.ShouldBeGreaterThan(0.8);
        ringSeen.ShouldBeTrue();
        eyesWideAfterBurst.ShouldBeTrue();
        pop.ShouldBeGreaterThan(1.1);
        growth.ShouldBeGreaterThan(0.76);
        growth.ShouldBeLessThan(0.76 + 0.14 * 0.5);
        bursts.ShouldBe(1);
        finished.ShouldBe(1);
        finishedAt.ShouldNotBeNull();
        finishedAt.Value.ShouldBeInRange(2.5, 2.8);
        animator.IsEvolving.ShouldBeFalse();
        animator.Sparks.ShouldBeEmpty();
        animator.Growth.ShouldBe(0.76);
        animator.Pose.Growth.ShouldBe(0.76, 1e-3);
        animator.Pose.Pop.ShouldBe(1, 1e-2);
        animator.Pose.Flash.ShouldBe(0);
        animator.IsBusy.ShouldBeFalse();
    }

    [Fact]
    public void Growth_set_during_a_metamorphosis_only_moves_its_target()
    {
        var animator = new MascotAnimator(MascotMood.Party, 0.5, new Random(2));

        animator.StartMetamorphosis(0.63);
        animator.SetGrowth(0.66, animate: false, settleSeconds: 0.6);
        Run(animator, 0.5);

        animator.Pose.Growth.ShouldBe(0.5);
        Run(animator, 3.5);
        animator.Pose.Growth.ShouldBe(0.66, 1e-3);
    }

    private static void Run(MascotAnimator animator, double seconds, Action<MascotPose>? each = null)
    {
        var steps = (int)Math.Round(seconds / Frame);
        for (var i = 0; i < steps; i++)
        {
            animator.Advance(Frame);
            each?.Invoke(animator.Pose);
        }
    }
}
