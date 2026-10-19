namespace AVARacingDestructionSet.Engine;

/// <summary>
/// How the computer drives: aim at a point on its line a little way up the road (further at speed) and steer for
/// it; keep to a speed the bends ahead allow with the grip it has here (the planet, the surface, its tyres, the
/// banking), braking in time; slow for a ramp so as to land on the road beyond it, which on the Moon is slow
/// indeed. In a destruction race it drops its oil or mines when the other car is close behind, steers round what's
/// been dropped ahead, and tries to ram a car just in front.
/// </summary>
public sealed class Pilot
{
    /// <summary>What it is up to, for the probe.</summary>
    public string Doing { get; private set; } = "";

    /// <summary>How much of the grip it dares use.</summary>
    public const double Margin = 0.86;

    public Controls Decide(World w, int car)
    {
        var c = w.Cars[car];
        if (w.Phase != RacePhase.Racing || !c.Active) { Doing = "waiting"; return default; }
        var course = w.Course;
        double lane = 0;
        bool fire = false;
        Doing = "racing";
        if (w.Rules.Destruction)
        {
            var o = w.Cars[1 - car];
            double gap = o.Progress - c.Progress;
            double side = course.Off(o.Pos, c.Index);
            if (o.Active && gap is > -220 and < -25 && c.Ammo > 0 && Math.Abs(side) < 30)
            {
                fire = !c.FirePrev;
                Doing = "dropping";
            }
            if (o.Active && gap is > 20 and < 110 && o.Ghost <= 0 && c.Speed > o.Speed + 25)
            {
                lane = Math.Clamp(course.Off(o.Pos, o.Index), -16, 16);
                Doing = "ramming";
            }
        }
        // Steer round what's on the road ahead: what it notices of it (about two things in three).
        foreach (var d in w.Drops)
        {
            if (d.Owner == car && d.Kind == DropKind.Oil && d.Age < 0.5 || !Noticed(d)) continue;
            int di = course.Nearest(d.Pos, c.Index, d.Z);
            double ahead = course.Along[di] - course.Along[c.Index];
            if (ahead < 0) ahead += course.Length;
            double off = course.Off(d.Pos, di);
            if (ahead is > 10 and < 160 && Math.Abs(off - lane) < 24)
            {
                lane = Math.Clamp(off > 0 ? off - 26 : off + 26, -18, 18);
                Doing = "dodging";
            }
        }
        return Drive(w, c, lane) with { Fire = fire };
    }

    /// <summary>Whether a computer driver sees something dropped on the road in time (the same answer every time for the same thing).</summary>
    public static bool Noticed(Drop d) => (uint)((int)(d.Pos.X * 7) ^ (int)(d.Pos.Y * 13)) % 3 != 0;

    /// <summary>The controls to follow a line a given distance right of the middle of the road, at a safe speed.</summary>
    public static Controls Drive(World w, Car c, double lane)
    {
        var course = w.Course;
        double speed = Math.Max(0, c.Speed), at = course.Along[c.Index];
        if (c.Airborne) return new Controls(0, true, false);
        double look = 30 + speed * 0.26;
        int ti = course.IndexAt(at + look);
        var aim = course.Points[ti] + course.Right(ti) * lane;
        double off = Field.AngleDiff(c.Heading, (aim - c.Pos).Angle);
        int steer = Math.Abs(off) < 0.04 ? 0 : Math.Sign(off);
        double safe = SafeSpeed(w, c, at, speed);
        if (c.Finished) safe = Math.Min(safe, 110);
        // Facing the wrong way (after a spin), it drives round in a tight circle rather than sitting there.
        bool gas = speed < safe - 4 && (Math.Abs(off) < 1.2 || speed < 70);
        bool brake = speed > safe + 10;
        return new Controls(steer, gas, brake);
    }

    /// <summary>The fastest it can go here and still make the bends and jumps within braking distance.</summary>
    public static double SafeSpeed(World w, Car c, double at, double speed)
    {
        var course = w.Course;
        double brake = Sim.BrakeRate * 0.8;
        double reach = speed * speed / (2 * brake) + 80;
        double safe = Sim.Top(w, c);
        for (double s = 0; s <= reach; s += Course.Spacing * 2)
        {
            int i = course.IndexAt(at + s);
            double bend = course.Bend[i];
            if (bend < 1e-4) continue;
            double v = Math.Sqrt(Sim.Lateral(w, c, i) * Margin / bend);
            safe = Math.Min(safe, Math.Sqrt(v * v + 2 * brake * Math.Max(0, s - 16)));
        }
        var jumps = course.JumpSpeeds(w.Gravity);
        for (int k = 0; k < course.Humps.Length; k++)
        {
            double d = course.Along[course.Humps[k].Start] - at;
            if (d < -Pieces.Cell / 2) d += course.Length;
            if (d > reach + 300 || jumps[k] >= safe) continue;
            safe = Math.Min(safe, Math.Sqrt(jumps[k] * jumps[k] + 2 * brake * Math.Max(0, d - 8)));
        }
        return Math.Max(safe, 50);
    }
}
