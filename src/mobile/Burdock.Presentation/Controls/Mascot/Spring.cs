namespace Burdock.Presentation.Controls.Mascot;

/// <summary>
/// Damped harmonic spring integrated per tick (semi-implicit Euler), the same model as motion.easing.overshoot and the SwiftUI spring of the
/// widgets. Used for the growth, the squash of a gulp or a landing, and the settle after a hop, so every bounce shares one physical feel.
/// </summary>
public struct Spring
{
    private readonly double _stiffness;
    private readonly double _damping;

    /// <param name="settleSeconds">Time to come within ~2 % of the target after a step.</param>
    /// <param name="dampingRatio">Below 1 overshoots (0.5 ≈ 16 % of the step), 1 is critical.</param>
    public Spring(double settleSeconds, double dampingRatio, double value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(settleSeconds);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(dampingRatio);
        var omega = 4.0 / (dampingRatio * settleSeconds);
        _stiffness = omega * omega;
        _damping = 2 * dampingRatio * omega;
        Value = value;
        Target = value;
        Velocity = 0;
    }

    public double Value { get; private set; }

    public double Target { get; set; }

    public double Velocity { get; private set; }

    /// <summary>True while the spring still moves visibly (half a thousandth of the unit is below anything the drawing can show).</summary>
    public readonly bool IsActive => Math.Abs(Value - Target) > 5e-4 || Math.Abs(Velocity) > 5e-3;

    /// <summary>Kicks the spring without moving it (a landing, a gulp); the sign follows the value axis.</summary>
    public void Impulse(double velocity) => Velocity += velocity;

    public void Snap(double value)
    {
        Value = value;
        Target = value;
        Velocity = 0;
    }

    public void Step(double dt)
    {
        if (dt <= 0)
        {
            return;
        }

        // Sub-step so a long frame (after a stall) cannot blow the integration up.
        var steps = Math.Max(1, (int)Math.Ceiling(dt / 0.008));
        var h = dt / steps;
        for (var i = 0; i < steps; i++)
        {
            var acceleration = -_stiffness * (Value - Target) - _damping * Velocity;
            Velocity += acceleration * h;
            Value += Velocity * h;
        }

        if (!IsActive)
        {
            Value = Target;
            Velocity = 0;
        }
    }
}
