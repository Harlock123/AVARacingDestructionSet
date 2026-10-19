namespace AVARacingDestructionSet.Engine;

public readonly record struct Vec(double X, double Y)
{
    public static Vec operator +(Vec a, Vec b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec operator -(Vec a, Vec b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec operator -(Vec a) => new(-a.X, -a.Y);
    public static Vec operator *(Vec a, double k) => new(a.X * k, a.Y * k);
    public static Vec operator /(Vec a, double k) => new(a.X / k, a.Y / k);
    public double Length => Math.Sqrt(X * X + Y * Y);
    public double LengthSq => X * X + Y * Y;
    public Vec Unit => Length < 1e-9 ? new Vec(0, -1) : this * (1 / Length);
    public double Dot(Vec b) => X * b.X + Y * b.Y;
    /// <summary>A quarter turn clockwise (on the screen, where y runs down): the right of a heading.</summary>
    public Vec Right => new(-Y, X);
    public static double Distance(Vec a, Vec b) => (a - b).Length;

    /// <summary>A unit vector for a heading in radians: 0 is up the screen (north), turning clockwise.</summary>
    public static Vec FromAngle(double a) => new(Math.Sin(a), -Math.Cos(a));

    /// <summary>This vector's heading, in the same terms as <see cref="FromAngle"/>.</summary>
    public double Angle => Math.Atan2(X, -Y);

    // The generated one would print Unit, whose Unit it would print, and so on for ever.
    public override string ToString() => $"({X:0.#}, {Y:0.#})";
}

/// <summary>A small random number generator whose state can be copied with the world it's in.</summary>
public struct Dice
{
    private ulong _s;
    public Dice(int seed) => _s = (ulong)seed * 0x9E3779B97F4A7C15UL + 0x632BE59BD9B4E019UL;

    public double Next()
    {
        _s ^= _s << 13;
        _s ^= _s >> 7;
        _s ^= _s << 17;
        return (_s >> 11) * (1.0 / (1UL << 53));
    }

    public int Next(int n) => (int)(Next() * n);
}

public static class Field
{
    public const double Step = 1.0 / 60;
    public const double Width = 1024, Height = 768;

    /// <summary>The smallest signed difference from angle a to angle b, in -π..π.</summary>
    public static double AngleDiff(double a, double b)
    {
        double d = (b - a) % (2 * Math.PI);
        if (d > Math.PI) d -= 2 * Math.PI;
        if (d < -Math.PI) d += 2 * Math.PI;
        return d;
    }
}
