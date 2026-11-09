namespace Burdock.Presentation.Controls;

/// <summary>
/// Rides of the shop (decision log item 22, spec 02 §3.10 «Транспорт»): a side-view vehicle in two layers. The far layer (body, roof, windows,
/// far parts) is drawn under the burr; the near layer (door panel, hull, wing, rim, wheels) is drawn over its lower edge, never higher than
/// `gy − 0.72 r`, so the mouth and cheeks stay visible and the burr reads as sitting inside or on top. Window pillars in the near layer hide the
/// hooks at the sides, two hook-arms hold the steering wheel, the handlebar or rest on the sill, and two-wheelers lean the body forward. Sizes use
/// `v` (the burr radius capped by the view) for the vehicle and `r` for where it meets the body; `gy` is the ground line. Same rendering language
/// as the body and the outfits.
/// </summary>
public sealed partial class MascotView
{
    private void DrawRideBehind(ICanvas canvas, float cx, float gy, float r, float v)
    {
        switch (Ride)
        {
            case MascotRide.Scooter:
                ScooterBehind(canvas, cx, gy, v);
                break;
            case MascotRide.Bicycle:
                BicycleBehind(canvas, cx, gy, v);
                break;
            case MascotRide.Car:
                CarBehind(canvas, cx, gy, v);
                break;
            case MascotRide.Van:
                VanBehind(canvas, cx, gy, v);
                break;
            case MascotRide.Bus:
                BusBehind(canvas, cx, gy, v);
                break;
            case MascotRide.Steamship:
                SteamshipBehind(canvas, cx, gy, v);
                break;
            case MascotRide.Airplane:
                AirplaneBehind(canvas, cx, gy, v);
                break;
            case MascotRide.Saucer:
                SaucerBehind(canvas, cx, gy, r, v);
                break;
            default:
                break;
        }
    }

    private void DrawRideFront(ICanvas canvas, float cx, float gy, float r, float v)
    {
        var lip = gy - 0.72f * r;
        switch (Ride)
        {
            case MascotRide.Scooter:
                ScooterFront(canvas, cx, gy, v);
                break;
            case MascotRide.Bicycle:
                BicycleFront(canvas, cx, gy, v);
                break;
            case MascotRide.Car:
                CarFront(canvas, cx, gy, lip, v);
                break;
            case MascotRide.Van:
                VanFront(canvas, cx, gy, lip, v);
                break;
            case MascotRide.Bus:
                BusFront(canvas, cx, gy, lip, v);
                break;
            case MascotRide.Steamship:
                SteamshipFront(canvas, cx, gy, lip, v);
                break;
            case MascotRide.Airplane:
                AirplaneFront(canvas, cx, gy, lip, v);
                break;
            case MascotRide.Saucer:
                SaucerFront(canvas, cx, gy, lip, v);
                break;
            default:
                break;
        }
    }

    /// <summary>Forward lean of the body on two-wheelers (degrees, clockwise = towards the front).</summary>
    private float RideTilt => Ride is MascotRide.Scooter or MascotRide.Bicycle ? 5f : 0f;

    /// <summary>Hook-arms drawn in the body transform over the vehicle: from the side of the body, with an elbow, to the wheel, the handlebar or the sill.</summary>
    private void DrawRideArms(ICanvas canvas, float cx, float cy, float gy, float r, float v)
    {
        var lip = gy - 0.72f * r;
        switch (Ride)
        {
            case MascotRide.Scooter:
                Arm(canvas, cx + 0.84f * r, cy + 0.02f * r, cx + 1.62f * v, gy - 1.95f * v, r, elbowUp: true);
                Arm(canvas, cx + 0.8f * r, cy + 0.32f * r, cx + 1.38f * v, gy - 1.95f * v, r, elbowUp: true);
                break;
            case MascotRide.Bicycle:
                Arm(canvas, cx + 0.84f * r, cy + 0.1f * r, cx + 1.36f * v, gy - 1.5f * v, r, elbowUp: true);
                Arm(canvas, cx + 0.8f * r, cy + 0.4f * r, cx + 1.14f * v, gy - 1.46f * v, r, elbowUp: true);
                break;
            case MascotRide.Car:
            case MascotRide.Van:
            case MascotRide.Bus:
                Arm(canvas, cx + 0.88f * r, cy + 0.3f * r, cx + 0.66f * r, lip - 0.34f * r, r, elbowUp: false);
                Arm(canvas, cx - 0.88f * r, cy + 0.36f * r, cx - 1.04f * r, lip + 0.1f * r, r, elbowUp: false);
                break;
            case MascotRide.Steamship:
            case MascotRide.Airplane:
            case MascotRide.Saucer:
                Arm(canvas, cx + 0.86f * r, cy + 0.38f * r, cx + 1.0f * r, lip + 0.05f * r, r, elbowUp: false);
                Arm(canvas, cx - 0.86f * r, cy + 0.38f * r, cx - 1.0f * r, lip + 0.05f * r, r, elbowUp: false);
                break;
            default:
                break;
        }
    }

    /// <summary>One hook-arm: a bent stroke in the hook colours from the side of the body to a grip point, with a rounded tip. The elbow sits at the
    /// start height (reaching out, then up to a handlebar) or at the grip height (dropping onto a wheel or sill).</summary>
    private void Arm(ICanvas canvas, float x1, float y1, float x2, float y2, float r, bool elbowUp)
    {
        var path = new PathF();
        path.MoveTo(x1, y1);
        if (elbowUp)
        {
            path.QuadTo(x2 - (x2 - x1) * 0.2f, y1 + 0.05f * r, x2, y2);
        }
        else
        {
            path.QuadTo(x1 + (x2 - x1) * 0.2f, y2, x2, y2);
        }

        canvas.StrokeColor = HookColor;
        canvas.StrokeSize = 0.11f * r;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.DrawPath(path);
        canvas.FillColor = HookTipColor;
        canvas.FillCircle(x2, y2, 0.075f * r);
    }

    /// <summary>A steering wheel in the near layer: a dark ring with a hub, where the right arm lands.</summary>
    private void SteeringWheel(ICanvas canvas, float x, float y, float radius)
    {
        canvas.StrokeColor = OutfitInkColor;
        canvas.StrokeSize = radius * 0.3f;
        canvas.DrawCircle(x, y, radius);
        canvas.FillColor = OutfitInkColor;
        canvas.FillCircle(x, y, radius * 0.3f);
        canvas.StrokeSize = radius * 0.16f;
        canvas.DrawLine(x - radius, y, x + radius, y);
    }

    /// <summary>Two window pillars in the vehicle colour, from the roof line down to the sill; they hide the hooks at the sides of the body.</summary>
    private static void Pillars(ICanvas canvas, float cx, float top, float bottom, float r, float v, Color color)
    {
        canvas.FillColor = color;
        canvas.FillRoundedRectangle(cx - 0.98f * r - 0.2f * v, top, 0.2f * v, bottom - top + 0.05f * v, 0.04f * v);
        canvas.FillRoundedRectangle(cx + 0.98f * r, top, 0.2f * v, bottom - top + 0.05f * v, 0.04f * v);
    }

    private void Wheel(ICanvas canvas, float x, float y, float radius)
    {
        canvas.FillColor = OutfitInkColor;
        canvas.FillCircle(x, y, radius);
        canvas.FillColor = OutfitLightColor;
        canvas.FillCircle(x, y, radius * 0.48f);
        canvas.FillColor = OutfitInkColor;
        canvas.FillCircle(x, y, radius * 0.16f);
    }

    private void Glass(ICanvas canvas, float x, float y, float w, float h, float radius)
    {
        canvas.FillColor = OutfitLightColor;
        canvas.FillRoundedRectangle(x, y, w, h, radius);
        canvas.SaveState();
        canvas.Alpha = 0.55f;
        canvas.FillColor = SceneSkyColor;
        canvas.FillRoundedRectangle(x, y, w, h, radius);
        canvas.RestoreState();
    }

    private void Headlight(ICanvas canvas, float x, float y, float radius)
    {
        canvas.FillColor = OutfitSunColor;
        canvas.FillCircle(x, y, radius);
        canvas.FillColor = OutfitLightColor;
        canvas.FillCircle(x - radius * 0.25f, y - radius * 0.25f, radius * 0.35f);
    }

    private void ScooterBehind(ICanvas canvas, float cx, float gy, float v)
    {
        // scooter.deck with a shade and a front fender, scooter.wheels; the stem and handlebar are in the near layer, beside the burr.
        canvas.FillColor = OutfitWarmColor;
        canvas.FillRoundedRectangle(cx - 1.4f * v, gy - 0.4f * v, 2.6f * v, 0.16f * v, 0.08f * v);
        canvas.FillColor = OutfitWarmEdgeColor;
        canvas.FillRoundedRectangle(cx - 1.35f * v, gy - 0.28f * v, 2.5f * v, 0.06f * v, 0.03f * v);
        canvas.StrokeColor = OutfitWarmEdgeColor;
        canvas.StrokeSize = 0.07f * v;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.DrawArc(cx + 1.07f * v, gy - 0.5f * v, 0.56f * v, 0.56f * v, 20, 160, clockwise: false, closed: false);
        Wheel(canvas, cx - 1.3f * v, gy - 0.2f * v, 0.2f * v);
        Wheel(canvas, cx + 1.35f * v, gy - 0.2f * v, 0.2f * v);
    }

    private void ScooterFront(ICanvas canvas, float cx, float gy, float v)
    {
        // scooter.stem rises beside the burr, scooter.handlebar with grips at eye level.
        canvas.StrokeColor = OutfitCoolEdgeColor;
        canvas.StrokeSize = 0.1f * v;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.DrawLine(cx + 1.25f * v, gy - 0.36f * v, cx + 1.52f * v, gy - 1.95f * v);
        canvas.StrokeColor = OutfitInkColor;
        canvas.StrokeSize = 0.08f * v;
        canvas.DrawLine(cx + 1.22f * v, gy - 2.0f * v, cx + 1.8f * v, gy - 2.0f * v);
        canvas.FillColor = OutfitWarmColor;
        canvas.FillCircle(cx + 1.22f * v, gy - 2.0f * v, 0.075f * v);
        canvas.FillCircle(cx + 1.8f * v, gy - 2.0f * v, 0.075f * v);
    }

    private void BicycleBehind(ICanvas canvas, float cx, float gy, float v)
    {
        // bicycle.wheels with spokes, bicycle.frame (diamond), bicycle.saddle, bicycle.handlebar, bicycle.crank.
        var rearHub = new PointF(cx - 1.25f * v, gy - 0.36f * v);
        var frontHub = new PointF(cx + 1.25f * v, gy - 0.36f * v);
        var bottom = new PointF(cx + 0.05f * v, gy - 0.32f * v);
        var seat = new PointF(cx - 0.3f * v, gy - 1.0f * v);
        var head = new PointF(cx + 0.95f * v, gy - 1.0f * v);
        foreach (var hub in (ReadOnlySpan<PointF>)[rearHub, frontHub])
        {
            canvas.StrokeColor = OutfitInkColor;
            canvas.StrokeSize = 0.075f * v;
            canvas.DrawCircle(hub, 0.36f * v);
            canvas.SaveState();
            canvas.Alpha = 0.6f;
            canvas.StrokeColor = SceneStoneColor;
            canvas.StrokeSize = 0.025f * v;
            for (var i = 0; i < 6; i++)
            {
                var a = i * Math.PI / 6;
                canvas.DrawLine(hub.X - (float)Math.Cos(a) * 0.34f * v, hub.Y - (float)Math.Sin(a) * 0.34f * v, hub.X + (float)Math.Cos(a) * 0.34f * v, hub.Y + (float)Math.Sin(a) * 0.34f * v);
            }

            canvas.RestoreState();
            canvas.FillColor = OutfitInkColor;
            canvas.FillCircle(hub, 0.06f * v);
        }

        canvas.StrokeColor = OutfitWarmColor;
        canvas.StrokeSize = 0.085f * v;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.StrokeLineJoin = LineJoin.Round;
        canvas.DrawLine(rearHub, bottom);
        canvas.DrawLine(bottom, seat);
        canvas.DrawLine(rearHub, seat);
        canvas.DrawLine(seat, head);
        canvas.DrawLine(bottom, head);
        canvas.DrawLine(head, frontHub);
        canvas.FillColor = OutfitInkColor;
        canvas.FillRoundedRectangle(seat.X - 0.24f * v, seat.Y - 0.1f * v, 0.46f * v, 0.11f * v, 0.05f * v);
        canvas.StrokeColor = OutfitInkColor;
        canvas.StrokeSize = 0.05f * v;
        canvas.DrawCircle(bottom, 0.1f * v);
        canvas.DrawLine(bottom.X - 0.12f * v, bottom.Y + 0.12f * v, bottom.X + 0.12f * v, bottom.Y - 0.12f * v);
        canvas.FillRoundedRectangle(bottom.X + 0.08f * v, bottom.Y - 0.16f * v, 0.14f * v, 0.05f * v, 0.02f * v);
    }

    private void BicycleFront(ICanvas canvas, float cx, float gy, float v)
    {
        // bicycle.handlebar: the stem rises from the head tube beside the burr, the bar bends forward with a grip.
        var head = new PointF(cx + 0.95f * v, gy - 1.0f * v);
        canvas.StrokeColor = OutfitInkColor;
        canvas.StrokeSize = 0.07f * v;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.DrawLine(head.X, head.Y, head.X + 0.1f * v, head.Y - 0.5f * v);
        canvas.DrawLine(head.X + 0.1f * v, head.Y - 0.5f * v, head.X + 0.45f * v, head.Y - 0.56f * v);
        canvas.FillColor = OutfitWarmColor;
        canvas.FillCircle(head.X + 0.45f * v, head.Y - 0.56f * v, 0.065f * v);
    }

    private void CarBehind(ICanvas canvas, float cx, float gy, float v)
    {
        // car.seatBack behind the burr, car.windshield with its frame on the right.
        canvas.FillColor = OutfitWarmEdgeColor;
        canvas.FillRoundedRectangle(cx - 1.2f * v, gy - 1.55f * v, 0.75f * v, 0.85f * v, 0.14f * v);
        var glass = new PathF();
        glass.MoveTo(cx + 0.5f * v, gy - 0.8f * v);
        glass.LineTo(cx + 0.82f * v, gy - 1.42f * v);
        glass.LineTo(cx + 1.12f * v, gy - 0.8f * v);
        glass.Close();
        canvas.SaveState();
        canvas.Alpha = 0.7f;
        canvas.FillColor = SceneSkyColor;
        canvas.FillPath(glass);
        canvas.RestoreState();
        canvas.StrokeColor = OutfitWarmEdgeColor;
        canvas.StrokeSize = 0.07f * v;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.DrawLine(cx + 0.5f * v, gy - 0.8f * v, cx + 0.82f * v, gy - 1.42f * v);
        canvas.DrawLine(cx + 0.82f * v, gy - 1.42f * v, cx + 1.1f * v, gy - 1.42f * v);
    }

    private void CarFront(ICanvas canvas, float cx, float gy, float lip, float v)
    {
        // car.body (near side) with a stripe, door line and handle, car.lights, car.bumper, car.wheels.
        var top = Math.Min(lip, gy - 0.5f * v);
        canvas.FillColor = OutfitWarmColor;
        canvas.FillRoundedRectangle(cx - 1.6f * v, top, 3.2f * v, gy - 0.12f * v - top, 0.2f * v);
        canvas.FillColor = OutfitWarmEdgeColor;
        canvas.FillRectangle(cx - 1.6f * v, gy - 0.4f * v, 3.2f * v, 0.06f * v);
        canvas.StrokeColor = OutfitWarmEdgeColor;
        canvas.StrokeSize = 0.04f * v;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.DrawLine(cx - 0.3f * v, top + 0.08f * v, cx - 0.3f * v, gy - 0.44f * v);
        canvas.FillColor = OutfitLightColor;
        canvas.FillRoundedRectangle(cx - 0.2f * v, top + 0.16f * v, 0.22f * v, 0.06f * v, 0.03f * v);
        Headlight(canvas, cx + 1.44f * v, top + 0.22f * v, 0.1f * v);
        canvas.FillColor = OutfitWarmEdgeColor;
        canvas.FillRoundedRectangle(cx - 1.56f * v, top + 0.14f * v, 0.14f * v, 0.16f * v, 0.04f * v);
        canvas.FillColor = SceneStoneColor;
        canvas.FillRoundedRectangle(cx - 1.66f * v, gy - 0.3f * v, 3.32f * v, 0.09f * v, 0.04f * v);
        Wheel(canvas, cx - 0.95f * v, gy - 0.26f * v, 0.27f * v);
        Wheel(canvas, cx + 0.95f * v, gy - 0.26f * v, 0.27f * v);
        SteeringWheel(canvas, cx + 0.66f * (gy - lip) / 0.72f, lip - 0.14f * (gy - lip) / 0.72f, 0.2f * (gy - lip) / 0.72f);
    }

    private void VanBehind(ICanvas canvas, float cx, float gy, float v)
    {
        // van.box with a roof rail, van.windows (the burr pokes out of the open roof), van.mirror.
        canvas.FillColor = OutfitCoolColor;
        canvas.FillRoundedRectangle(cx - 1.6f * v, gy - 1.9f * v, 3.2f * v, 1.8f * v, 0.2f * v);
        canvas.SaveState();
        canvas.Alpha = 0.35f;
        canvas.FillColor = OutfitLightColor;
        canvas.FillRoundedRectangle(cx - 1.5f * v, gy - 1.9f * v, 3.0f * v, 0.09f * v, 0.04f * v);
        canvas.RestoreState();
        Glass(canvas, cx - 1.45f * v, gy - 1.68f * v, 0.6f * v, 0.5f * v, 0.06f * v);
        Glass(canvas, cx - 0.75f * v, gy - 1.68f * v, 0.6f * v, 0.5f * v, 0.06f * v);
        Glass(canvas, cx + 0.55f * v, gy - 1.68f * v, 0.92f * v, 0.56f * v, 0.08f * v);
        canvas.FillColor = OutfitCoolEdgeColor;
        canvas.FillRoundedRectangle(cx + 1.58f * v, gy - 1.5f * v, 0.14f * v, 0.18f * v, 0.04f * v);
    }

    private void VanFront(ICanvas canvas, float cx, float gy, float lip, float v)
    {
        var top = Math.Min(lip, gy - 0.5f * v);
        canvas.FillColor = OutfitCoolColor;
        canvas.FillRoundedRectangle(cx - 1.6f * v, top, 3.2f * v, gy - 0.12f * v - top, 0.1f * v);
        canvas.FillColor = OutfitCoolEdgeColor;
        canvas.FillRectangle(cx - 1.6f * v, top + 0.2f * v, 3.2f * v, 0.08f * v);
        canvas.StrokeColor = OutfitCoolEdgeColor;
        canvas.StrokeSize = 0.04f * v;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.DrawLine(cx - 0.2f * v, top + 0.04f * v, cx - 0.2f * v, gy - 0.4f * v);
        canvas.FillColor = OutfitLightColor;
        canvas.FillRoundedRectangle(cx - 0.1f * v, top + 0.36f * v, 0.2f * v, 0.06f * v, 0.03f * v);
        Headlight(canvas, cx + 1.46f * v, top + 0.1f * v, 0.09f * v);
        canvas.FillColor = SceneStoneColor;
        canvas.FillRoundedRectangle(cx - 1.66f * v, gy - 0.3f * v, 3.32f * v, 0.09f * v, 0.04f * v);
        Wheel(canvas, cx - 1.05f * v, gy - 0.26f * v, 0.27f * v);
        Wheel(canvas, cx + 1.05f * v, gy - 0.26f * v, 0.27f * v);
        Pillars(canvas, cx, gy - 1.9f * v, top, (gy - lip) / 0.72f, v, OutfitCoolColor);
        SteeringWheel(canvas, cx + 0.66f * (gy - lip) / 0.72f, lip - 0.14f * (gy - lip) / 0.72f, 0.2f * (gy - lip) / 0.72f);
    }

    private void BusBehind(ICanvas canvas, float cx, float gy, float v)
    {
        // bus.body, bus.windows row, bus.windshield, bus.sign.
        canvas.FillColor = AccentColor;
        canvas.FillRoundedRectangle(cx - 1.7f * v, gy - 2.0f * v, 3.4f * v, 1.9f * v, 0.18f * v);
        foreach (var x in (ReadOnlySpan<float>)[-1.56f, -0.9f, -0.24f, 0.42f])
        {
            Glass(canvas, cx + x * v, gy - 1.76f * v, 0.58f * v, 0.5f * v, 0.06f * v);
        }

        Glass(canvas, cx + 1.12f * v, gy - 1.76f * v, 0.5f * v, 0.56f * v, 0.08f * v);
        canvas.FillColor = OutfitInkColor;
        canvas.FillRoundedRectangle(cx + 0.9f * v, gy - 1.97f * v, 0.72f * v, 0.15f * v, 0.04f * v);
        canvas.FillColor = AccentColor;
        canvas.FillRoundedRectangle(cx + 0.98f * v, gy - 1.93f * v, 0.42f * v, 0.07f * v, 0.03f * v);
    }

    private void BusFront(ICanvas canvas, float cx, float gy, float lip, float v)
    {
        var top = Math.Min(lip, gy - 0.5f * v);
        canvas.FillColor = AccentColor;
        canvas.FillRoundedRectangle(cx - 1.7f * v, top, 3.4f * v, gy - 0.12f * v - top, 0.1f * v);
        canvas.FillColor = OutfitSunEdgeColor;
        canvas.FillRectangle(cx - 1.7f * v, top + 0.22f * v, 3.4f * v, 0.08f * v);
        canvas.StrokeColor = OutfitLightColor;
        canvas.StrokeSize = 0.04f * v;
        canvas.DrawRoundedRectangle(cx + 0.62f * v, top + 0.05f * v, 0.46f * v, gy - 0.3f * v - top, 0.04f * v);
        canvas.DrawLine(cx + 0.85f * v, top + 0.05f * v, cx + 0.85f * v, gy - 0.25f * v);
        Headlight(canvas, cx + 1.56f * v, top + 0.1f * v, 0.08f * v);
        canvas.FillColor = SceneStoneColor;
        canvas.FillRoundedRectangle(cx - 1.76f * v, gy - 0.3f * v, 3.52f * v, 0.09f * v, 0.04f * v);
        Wheel(canvas, cx - 1.15f * v, gy - 0.26f * v, 0.27f * v);
        Wheel(canvas, cx + 1.15f * v, gy - 0.26f * v, 0.27f * v);
        Pillars(canvas, cx, gy - 2.0f * v, top, (gy - lip) / 0.72f, v, AccentColor);
        SteeringWheel(canvas, cx + 0.66f * (gy - lip) / 0.72f, lip - 0.14f * (gy - lip) / 0.72f, 0.2f * (gy - lip) / 0.72f);
    }

    private void SteamshipBehind(ICanvas canvas, float cx, float gy, float v)
    {
        // ship.cabin, ship.funnel with smoke, ship.mast with a flag.
        canvas.FillColor = OutfitLightColor;
        canvas.FillRoundedRectangle(cx - 0.75f * v, gy - 1.6f * v, 1.5f * v, 0.9f * v, 0.08f * v);
        Glass(canvas, cx - 0.6f * v, gy - 1.46f * v, 0.22f * v, 0.22f * v, 0.04f * v);
        Glass(canvas, cx + 0.38f * v, gy - 1.46f * v, 0.22f * v, 0.22f * v, 0.04f * v);
        canvas.FillColor = OutfitWarmColor;
        canvas.FillRoundedRectangle(cx - 1.12f * v, gy - 2.05f * v, 0.4f * v, 1.5f * v, 0.06f * v);
        canvas.FillColor = OutfitInkColor;
        canvas.FillRoundedRectangle(cx - 1.14f * v, gy - 2.08f * v, 0.44f * v, 0.16f * v, 0.04f * v);
        canvas.SaveState();
        canvas.Alpha = 0.8f;
        canvas.FillColor = OutfitLightColor;
        canvas.FillCircle(cx - 0.98f * v, gy - 2.22f * v, 0.12f * v);
        canvas.FillCircle(cx - 1.15f * v, gy - 2.4f * v, 0.14f * v);
        canvas.FillCircle(cx - 1.4f * v, gy - 2.56f * v, 0.17f * v);
        canvas.RestoreState();
        canvas.StrokeColor = OutfitInkColor;
        canvas.StrokeSize = 0.05f * v;
        canvas.DrawLine(cx + 1.15f * v, gy - 2.2f * v, cx + 1.15f * v, gy - 0.8f * v);
        var flag = new PathF();
        flag.MoveTo(cx + 1.17f * v, gy - 2.2f * v);
        flag.LineTo(cx + 1.5f * v, gy - 2.08f * v);
        flag.LineTo(cx + 1.17f * v, gy - 1.96f * v);
        flag.Close();
        canvas.FillColor = OutfitWarmColor;
        canvas.FillPath(flag);
    }

    private void SteamshipFront(ICanvas canvas, float cx, float gy, float lip, float v)
    {
        // ship.hull with a deck stripe and portholes, ship.waves at the waterline.
        var top = Math.Min(lip, gy - 0.55f * v);
        var hull = new PathF();
        hull.MoveTo(cx - 1.85f * v, top);
        hull.LineTo(cx + 1.85f * v, top);
        hull.LineTo(cx + 1.45f * v, gy - 0.04f * v);
        hull.LineTo(cx - 1.45f * v, gy - 0.04f * v);
        hull.Close();
        canvas.FillColor = OutfitCoolEdgeColor;
        canvas.FillPath(hull);
        canvas.FillColor = OutfitLightColor;
        canvas.FillRectangle(cx - 1.85f * v, top, 3.7f * v, 0.1f * v);
        foreach (var x in (ReadOnlySpan<float>)[-1.1f, -0.55f, 0f, 0.55f, 1.1f])
        {
            canvas.FillColor = OutfitLightColor;
            canvas.FillCircle(cx + x * v, top + 0.3f * v, 0.085f * v);
            canvas.FillColor = SceneSkyColor;
            canvas.FillCircle(cx + x * v, top + 0.3f * v, 0.055f * v);
        }

        canvas.StrokeColor = OutfitCoolColor;
        canvas.StrokeSize = 0.09f * v;
        canvas.StrokeLineCap = LineCap.Round;
        for (var x = cx - 2.3f * v; x < cx + 2.2f * v; x += 0.5f * v)
        {
            canvas.DrawArc(x, gy - 0.2f * v, 0.5f * v, 0.28f * v, 0, 180, clockwise: false, closed: false);
        }

        canvas.SaveState();
        canvas.Alpha = 0.7f;
        canvas.StrokeColor = OutfitLightColor;
        canvas.StrokeSize = 0.03f * v;
        for (var x = cx - 2.05f * v; x < cx + 2.2f * v; x += 0.5f * v)
        {
            canvas.DrawArc(x, gy - 0.12f * v, 0.5f * v, 0.2f * v, 0, 180, clockwise: false, closed: false);
        }

        canvas.RestoreState();
    }

    private void AirplaneBehind(ICanvas canvas, float cx, float gy, float v)
    {
        // plane.tailFin, plane.stabiliser, plane.fuselage (upper half shows around the burr), plane.windshield.
        var fin = new PathF();
        fin.MoveTo(cx - 1.75f * v, gy - 0.9f * v);
        fin.LineTo(cx - 1.55f * v, gy - 1.95f * v);
        fin.LineTo(cx - 1.18f * v, gy - 1.95f * v);
        fin.LineTo(cx - 0.95f * v, gy - 0.9f * v);
        fin.Close();
        canvas.FillColor = OutfitWarmColor;
        canvas.FillPath(fin);
        canvas.FillColor = OutfitWarmEdgeColor;
        canvas.FillRoundedRectangle(cx - 1.95f * v, gy - 1.08f * v, 0.75f * v, 0.1f * v, 0.05f * v);
        canvas.FillColor = OutfitLightColor;
        canvas.FillRoundedRectangle(cx - 1.75f * v, gy - 1.35f * v, 3.5f * v, 1.0f * v, 0.5f * v);
        var glass = new PathF();
        glass.MoveTo(cx + 0.62f * v, gy - 1.3f * v);
        glass.LineTo(cx + 0.82f * v, gy - 1.66f * v);
        glass.LineTo(cx + 1.12f * v, gy - 1.66f * v);
        glass.LineTo(cx + 1.2f * v, gy - 1.3f * v);
        glass.Close();
        canvas.SaveState();
        canvas.Alpha = 0.75f;
        canvas.FillColor = SceneSkyColor;
        canvas.FillPath(glass);
        canvas.RestoreState();
        canvas.StrokeColor = OutfitCoolEdgeColor;
        canvas.StrokeSize = 0.05f * v;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.DrawLine(cx + 0.62f * v, gy - 1.3f * v, cx + 0.82f * v, gy - 1.66f * v);
        canvas.DrawLine(cx + 0.82f * v, gy - 1.66f * v, cx + 1.12f * v, gy - 1.66f * v);
    }

    private void AirplaneFront(ICanvas canvas, float cx, float gy, float lip, float v)
    {
        // plane.fuselage (lower half) with a cheat line, plane.wing (near), plane.propeller, plane.gear.
        var top = Math.Min(lip, gy - 0.6f * v);
        canvas.FillColor = OutfitLightColor;
        canvas.FillRoundedRectangle(cx - 1.75f * v, top, 3.5f * v, gy - 0.35f * v - top, 0.3f * v);
        canvas.FillColor = OutfitCoolEdgeColor;
        canvas.FillRectangle(cx - 1.6f * v, top + 0.08f * v, 3.2f * v, 0.06f * v);
        var wing = new PathF();
        wing.MoveTo(cx - 1.1f * v, top + 0.02f * v);
        wing.LineTo(cx + 0.25f * v, top + 0.02f * v);
        wing.LineTo(cx + 0.08f * v, top + 0.42f * v);
        wing.LineTo(cx - 1.5f * v, top + 0.42f * v);
        wing.Close();
        canvas.FillColor = OutfitCoolColor;
        canvas.FillPath(wing);
        canvas.StrokeColor = OutfitInkColor;
        canvas.StrokeSize = 0.045f * v;
        canvas.StrokeLineCap = LineCap.Round;
        foreach (var x in (ReadOnlySpan<float>)[-0.75f, 0.7f])
        {
            canvas.DrawLine(cx + x * v, gy - 0.4f * v, cx + x * v, gy - 0.14f * v);
            Wheel(canvas, cx + x * v, gy - 0.13f * v, 0.13f * v);
        }

        canvas.SaveState();
        canvas.Alpha = 0.5f;
        canvas.FillColor = OutfitInkColor;
        canvas.FillEllipse(cx + 1.74f * v, gy - 1.5f * v, 0.12f * v, 1.3f * v);
        canvas.RestoreState();
        canvas.FillColor = OutfitWarmColor;
        canvas.FillCircle(cx + 1.8f * v, gy - 0.85f * v, 0.17f * v);
        canvas.FillColor = OutfitLightColor;
        canvas.FillCircle(cx + 1.76f * v, gy - 0.9f * v, 0.05f * v);
    }

    private void SaucerBehind(ICanvas canvas, float cx, float gy, float r, float v)
    {
        // saucer.dome (glass behind the burr), saucer.discBack.
        var domeTop = gy - 3.05f * r;
        var domeBottom = gy - 0.72f * r + 0.4f * v;
        canvas.SaveState();
        canvas.Alpha = 0.3f;
        canvas.FillColor = SceneSkyColor;
        canvas.FillEllipse(cx - 1.3f * v, domeTop, 2.6f * v, domeBottom - domeTop);
        canvas.RestoreState();
        canvas.FillColor = OutfitCoolEdgeColor;
        canvas.FillEllipse(cx - 1.9f * v, gy - 1.0f * v, 3.8f * v, 0.7f * v);
    }

    private void SaucerFront(ICanvas canvas, float cx, float gy, float lip, float v)
    {
        // saucer.discFront with running lights and a rim highlight, saucer.belly, saucer.beam, saucer.domeGlass over the burr.
        var top = Math.Min(lip, gy - 0.6f * v);
        var beam = new PathF();
        beam.MoveTo(cx - 0.55f * v, top + 0.55f * v);
        beam.LineTo(cx + 0.55f * v, top + 0.55f * v);
        beam.LineTo(cx + 1.15f * v, gy + 0.08f * v);
        beam.LineTo(cx - 1.15f * v, gy + 0.08f * v);
        beam.Close();
        canvas.SaveState();
        canvas.Alpha = 0.22f;
        canvas.FillColor = AccentColor;
        canvas.FillPath(beam);
        canvas.RestoreState();
        canvas.FillColor = OutfitCoolColor;
        canvas.FillEllipse(cx - 1.9f * v, top, 3.8f * v, 0.64f * v);
        canvas.SaveState();
        canvas.Alpha = 0.5f;
        canvas.StrokeColor = OutfitLightColor;
        canvas.StrokeSize = 0.05f * v;
        canvas.DrawArc(cx - 1.75f * v, top + 0.05f * v, 3.5f * v, 0.54f * v, 200, 340, clockwise: false, closed: false);
        canvas.RestoreState();
        var i = 0;
        foreach (var x in (ReadOnlySpan<float>)[-1.4f, -0.7f, 0f, 0.7f, 1.4f])
        {
            canvas.FillColor = i++ % 2 == 0 ? AccentColor : OutfitWarmColor;
            canvas.FillCircle(cx + x * v, top + 0.34f * v, 0.085f * v);
        }

        canvas.FillColor = OutfitCoolEdgeColor;
        canvas.FillEllipse(cx - 0.9f * v, top + 0.5f * v, 1.8f * v, 0.36f * v);
        var r = (gy - lip) / 0.72f;
        var domeTop = gy - 3.05f * r;
        var domeBottom = lip + 0.4f * v;
        canvas.SaveState();
        canvas.Alpha = 0.14f;
        canvas.FillColor = SceneSkyColor;
        canvas.FillEllipse(cx - 1.3f * v, domeTop, 2.6f * v, domeBottom - domeTop);
        canvas.RestoreState();
        canvas.SaveState();
        canvas.Alpha = 0.6f;
        canvas.StrokeColor = OutfitLightColor;
        canvas.StrokeSize = 0.06f * v;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.DrawArc(cx - 1.12f * v, domeTop + 0.14f * v, 2.24f * v, domeBottom - domeTop - 0.3f * v, 105, 150, clockwise: false, closed: false);
        canvas.RestoreState();
    }
}
