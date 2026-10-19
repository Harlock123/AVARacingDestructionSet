namespace AVARacingDestructionSet.Engine;

/// <summary>
/// The race, one 1/60 s step at a time: the lights, driving (grip that runs out in the bends, more of it banked,
/// less on dirt and ice and in low gravity), the ground rising and falling under the cars and throwing them into
/// the air off ramps and crests, landing, the rough off the road, tows, cars knocking each other about, oil and
/// mines, damage and wrecks, laps and the finish.
/// </summary>
public static class Sim
{
    /// <summary>How hard a grip of 1 can pull a car round a bend on Earth, in units/s².</summary>
    public const double GripScale = 900;
    public const double TurnRate = 5.2, BrakeRate = 560, Coast = 80;
    /// <summary>Gravity on Earth, in units/s².</summary>
    public const double Fall = 420;
    public const double BankedGrip = 1.45, RoughGrip = 0.5, RoughDrag = 1.5, OilGrip = 0.12;
    public const double CountdownSeconds = 3, LateSeconds = 12, WreckSeconds = 4.5, GhostSeconds = 2, TowSeconds = 1.6;
    /// <summary>How far off the road a car can wander before a tow truck brings it back.</summary>
    public const double TowDistance = 90;
    public const double HardLanding = 200, RamFloor = 50, RamDamage = 0.32, MineDamage = 55, MineLaunch = 230, MineArm = 0.8, Cooldown = 0.5;
    public const int MaxDrops = 16;
    /// <summary>How long an oil slick lies on the road before it's worn away.</summary>
    public const double OilSeconds = 40;
    /// <summary>How far the road must fall away below where the car would fall to before it leaves the ground.</summary>
    public const double LaunchSlack = 0.002;
    /// <summary>The grid: how far behind the line the cars start, and how far either side of the middle.</summary>
    public const double GridBack = 34, GridSide = 13;

    // ───────────────────────────── a new race ─────────────────────────────

    public static void Start(World w, Track track, Course course, Build[] builds, bool[] humans, RaceRules rules, int seed)
    {
        w.Track = track;
        w.Course = course;
        w.Rules = rules;
        w.Dice = new Dice(seed);
        w.Drops.Clear();
        w.Effects.Clear();
        w.Heard.Clear();
        w.Phase = RacePhase.Countdown;
        w.PhaseTime = w.Clock = 0;
        w.Late = -1;
        w.Finishers = 0;
        w.Winner = -1;
        w.Cars = new Car[World.Players];
        for (int i = 0; i < World.Players; i++)
        {
            int at = course.IndexAt(-GridBack);
            var c = new Car
            {
                Id = i, Human = humans[i], Build = builds[i].Copy(), Index = at, LastRoad = at, Progress = -GridBack,
                Pos = course.Points[at] + course.Right(at) * (i == 0 ? -GridSide : GridSide), Heading = course.Dir(at).Angle, Z = course.Z[at],
            };
            c.Stats = Vehicles.StatsOf(c.Build, track.Surface);
            c.Ammo = rules.Destruction ? Vehicles.Ammo(c.Build.Weapon) : 0;
            w.Cars[i] = c;
        }
    }

    // ───────────────────────────── a step ─────────────────────────────

    public static void Step(World w, Controls[] controls)
    {
        double dt = Field.Step;
        w.PhaseTime += dt;
        foreach (var e in w.Effects)
        {
            e.Age += dt;
            e.Pos += e.Vel * dt;
            if (e.Kind == EffectKind.Debris)
            {
                e.VZ -= Fall * w.Gravity * dt;
                e.Z = Math.Max(0, e.Z + e.VZ * dt);
                if (e.Z <= 0) e.Vel *= 0.8;
            }
        }
        w.Effects.RemoveAll(e => e.Done);
        for (int i = 0; i < World.Players; i++)
            if (w.MessageTimes[i] > 0 && (w.MessageTimes[i] -= dt) <= 0) w.Messages[i] = null;
        switch (w.Phase)
        {
            case RacePhase.Countdown:
                // The lights: a beep a second, then go.
                if ((int)w.PhaseTime != (int)(w.PhaseTime - dt) || w.PhaseTime <= dt) w.Heard.Add(w.PhaseTime < CountdownSeconds ? Sfx.Beep : Sfx.Go);
                if (w.PhaseTime >= CountdownSeconds)
                {
                    w.Phase = RacePhase.Racing;
                    w.PhaseTime = 0;
                }
                return;
            case RacePhase.Racing:
                w.Clock += dt;
                Race(w, controls, dt);
                if (w.Late >= 0 && (w.Late -= dt) < 0) End(w);
                return;
            case RacePhase.Finished:
                // The cars roll on behind the results.
                w.Clock += dt;
                Race(w, new Controls[World.Players], dt);
                return;
        }
    }

    private static void Race(World w, Controls[] controls, double dt)
    {
        for (int i = 0; i < w.Cars.Length; i++)
        {
            var c = w.Cars[i];
            var ctl = c.Finished || w.Over ? new Controls(0, false, true) : controls[i];
            Move(w, c, ctl, dt);
            Weapons(w, c, ctl, dt);
        }
        Collide(w);
        Hazards(w, dt);
        foreach (var c in w.Cars) Round(w, c);
    }

    // ───────────────────────────── driving ─────────────────────────────

    /// <summary>How the planet's gravity changes the grip (a car pressed harder onto the road grips harder): as its square root, or its fourth root for a vehicle built for low gravity.</summary>
    public static double GravityGrip(double g, bool lowGravity) => lowGravity ? Math.Pow(g, 0.25) : Math.Sqrt(g);

    /// <summary>How quick off the mark the planet lets a car be: wheels spin in low gravity; on Jupiter it's all heavier.</summary>
    public static double GravityAccel(double g, bool lowGravity) => g < 1 ? Math.Min(1, 0.35 + GravityGrip(g, lowGravity)) : 1 / Math.Pow(g, 0.2);

    /// <summary>How hard a car can be pulled round a bend at a sample, in units/s² (its grip, the gravity, banking).</summary>
    public static double Lateral(World w, Car c, int index, bool onRoad = true)
    {
        double a = GripScale * c.Stats.Grip * GravityGrip(w.Gravity, c.Stats.LowGravity);
        if (onRoad && w.Course.Banked[index]) a *= BankedGrip;
        if (!onRoad) a *= RoughGrip;
        if (c.Slick > 0) a *= OilGrip;
        return a;
    }

    /// <summary>A car's top speed on this track.</summary>
    public static double Top(World w, Car c) => c.Stats.Top * (w.Gravity > 1.5 ? 0.95 : 1);

    /// <summary>
    /// One step of driving on the ground: steering (more once it's moving, a little less flat out), the gas and the
    /// brake along the way it points, rolling to a stop off them, the rough's drag, and the grip killing off
    /// sideways sliding, as far as it can. Whatever's left of the slide is the skid.
    /// </summary>
    public static void Drive(Car c, Controls ctl, double dt, double lateral, double accel, double top, bool rough)
    {
        var f = c.Forward;
        double fwd = c.Vel.Dot(f);
        double turn = TurnRate * Math.Clamp(Math.Abs(fwd) / 70, 0.15, 1) * (1 - 0.2 * Math.Min(1, Math.Abs(fwd) / 400)) * (fwd < -1 ? -1 : 1);
        c.Heading += ctl.Steer * turn * dt;
        f = c.Forward;
        var right = f.Right;
        fwd = c.Vel.Dot(f);
        double side = c.Vel.Dot(right);
        if (ctl.Gas && fwd < top) fwd = Math.Min(top, fwd + accel * (1 - 0.45 * Math.Max(0, fwd) / top) * dt);
        if (ctl.Brake) fwd = fwd > 0 ? fwd - Math.Min(fwd, BrakeRate * dt) : fwd > -70 ? fwd - BrakeRate * 0.3 * dt : fwd;
        if (!ctl.Gas && !ctl.Brake) fwd -= Math.Sign(fwd) * Math.Min(Math.Abs(fwd), Coast * dt);
        if (rough) fwd *= 1 - RoughDrag * dt;
        double kill = lateral * dt;
        side = Math.Abs(side) <= kill ? 0 : side - Math.Sign(side) * kill;
        c.Skid = Math.Abs(side);
        c.Vel = f * fwd + right * side;
        c.Pos += c.Vel * dt;
    }

    private static bool OnGrid(Vec p) => p.X >= 0 && p.Y >= 0 && p.X <= Track.Cols * Pieces.Cell && p.Y <= Track.Rows * Pieces.Cell;

    private static void Move(World w, Car c, Controls ctl, double dt)
    {
        if (c.Out) return;
        if (c.Wrecked > 0)
        {
            if ((c.Wrecked -= dt) <= 0) Respawn(w, c);
            else if (w.Dice.Next() < 0.25) w.Effects.Add(new Effect { Kind = EffectKind.Smoke, Pos = c.Pos, Z = c.Z + 8, Vel = new Vec(6, -14), Life = 1.4, Size = 1.6, Colour = 0xFF3A3A3A });
            return;
        }
        if (c.Towed > 0)
        {
            if ((c.Towed -= dt) <= 0) Respawn(w, c, tow: true);
            return;
        }
        c.Ghost = Math.Max(0, c.Ghost - dt);
        c.Slick = Math.Max(0, c.Slick - dt);
        c.Cooldown = Math.Max(0, c.Cooldown - dt);
        if (c.Spin > 0)
        {
            c.Spin -= dt;
            c.Heading += c.SpinDir * 7 * dt;
            ctl = default;
        }
        if (w.Phase == RacePhase.Countdown) ctl = default;
        var course = w.Course;
        double g = w.Gravity;
        if (c.Airborne)
        {
            c.Pos += c.Vel * dt;
            c.Vel *= 1 - 0.04 * dt;
            c.Skid = 0;
        }
        else
        {
            double lateral = Lateral(w, c, c.Index, c.OnRoad);
            double accel = c.Stats.Accel * GravityAccel(g, c.Stats.LowGravity);
            Drive(c, ctl, dt, lateral, accel, Top(w, c), rough: !c.OnRoad);
        }
        c.Index = course.Nearest(c.Pos, c.Index, c.Z);
        double off = Math.Abs(course.Off(c.Pos, c.Index)) - Pieces.HalfWidth;
        bool onRoad = off <= 0 && OnGrid(c.Pos);
        double ground = onRoad ? course.Height(c.Pos, c.Index) : 0;
        double fall = Fall * g;
        if (c.Airborne)
        {
            c.VZ -= fall * dt;
            c.Z += c.VZ * dt;
            c.AirTime += dt;
            // Over the road it lands on the road; elsewhere on the ground (the road's edge is a drop off a bridge).
            if (c.Z <= ground) Land(w, c, ground, onRoad);
        }
        else
        {
            // The car follows the ground, unless the ground falls away faster than gravity can pull it down.
            double ballistic = Ballistic(c.Z, c.VZ, fall);
            if (ground < ballistic - LaunchSlack)
            {
                c.Airborne = true;
                c.AirTime = 0;
                c.VZ -= fall * dt;
                c.Z = ballistic;
                if (c.VZ > 30)
                {
                    w.Heard.Add(Sfx.Jump);
                    w.Effects.Add(new Effect { Kind = EffectKind.Dust, Pos = c.Pos - c.Forward * (c.Body.Length / 2), Z = ground, Life = 0.8, Size = 1.4, Colour = DustColour(w) });
                }
            }
            else if (ground - c.Z > 9)
            {
                // Into the face of a ramp the wrong way, or up a bridge's side: a jolt.
                c.Vel *= 0.35;
                c.Z = ground;
                c.VZ = 0;
                Hurt(w, c, 8);
                w.Heard.Add(Sfx.Bump);
            }
            else
            {
                c.VZ = Math.Clamp((ground - c.Z) / dt, -400, 400);
                c.Z = ground;
            }
        }
        c.OnRoad = onRoad;
        if (onRoad && !c.Airborne) c.LastRoad = c.Index;
        if (!c.Airborne && (off > TowDistance || !OnGrid(c.Pos)) && c.Active && !w.Over)
        {
            Tow(w, c);
            return;
        }
        // Tyre smoke from a slide, dust in the rough.
        double speed = c.Vel.Length;
        if (!c.Airborne && (c.Skid > 70 || c.Slick > 0 && speed > 60) && Tick(w, 14))
        {
            w.Effects.Add(new Effect { Kind = EffectKind.Smoke, Pos = c.Pos - c.Forward * (c.Body.Length / 2), Z = c.Z, Life = 0.7, Colour = 0xFFE8E8E8 });
            if (c.Human && Tick(w, 4)) w.Heard.Add(Sfx.Skid);
        }
        if (!c.Airborne && (!onRoad || w.Track.Surface == Surface.Dirt) && speed > 90 && Tick(w, 10))
            w.Effects.Add(new Effect { Kind = EffectKind.Dust, Pos = c.Pos - c.Forward * (c.Body.Length / 2), Z = 0, Life = 0.9, Size = onRoad ? 0.8 : 1.2, Colour = DustColour(w) });
    }

    /// <summary>True once every 1/n of a second of the race clock.</summary>
    private static bool Tick(World w, int n) => (int)(w.Clock * n) != (int)((w.Clock - Field.Step) * n);

    public static uint DustColour(World w) => w.Track.Planet switch
    {
        Planet.Moon => 0xFFB8B8C0, Planet.Mars => 0xFFC8724A, Planet.Venus => 0xFFD8C070, Planet.Jupiter => 0xFFC89A6A,
        _ => w.Track.Surface == Surface.Ice ? 0xFFE8F4FF : 0xFFB89A6A,
    };

    /// <summary>
    /// Where a car on the ground would be a step on if the ground fell away under it. Its upward speed is worked out
    /// from the step before (how the road has been rising), so a whole step of gravity is the fair comparison: then it
    /// leaves a crest just when the road curves away faster than gravity pulls (speed² × curvature &gt; g).
    /// </summary>
    public static double Ballistic(double z, double vz, double fall) => z + vz * Field.Step - fall * Field.Step * Field.Step;

    /// <summary>Back on the ground (following its slope from now): a hard landing hurts (in a destruction race), and shakes off some speed.</summary>
    private static void Land(World w, Car c, double ground, bool onRoad)
    {
        double hit = -c.VZ;
        c.Z = ground;
        c.VZ = onRoad ? (w.Course.Height(c.Pos + c.Vel * Field.Step, c.Index) - ground) / Field.Step : 0;
        c.Airborne = false;
        if (hit > 80) w.Heard.Add(Sfx.Land);
        if (hit > HardLanding)
        {
            Hurt(w, c, (hit - HardLanding) * 0.15);
            c.Vel *= 0.85;
        }
        if (hit > 60) w.Effects.Add(new Effect { Kind = EffectKind.Dust, Pos = c.Pos, Z = ground, Life = 0.7, Size = 1.6, Colour = DustColour(w) });
    }

    /// <summary>Too far off the road: the tow truck takes it back to where it left the road.</summary>
    private static void Tow(World w, Car c)
    {
        c.Towed = TowSeconds;
        c.Vel = default;
        c.Airborne = false;
        c.VZ = 0;
        w.Heard.Add(Sfx.Tow);
        w.Say(c.Id, "OFF THE TRACK!");
    }

    /// <summary>A fresh start in the middle of the road: after a tow where it left the road, after a wreck where it was.</summary>
    private static void Respawn(World w, Car c, bool tow = false)
    {
        int at = tow ? c.LastRoad : w.Course.Nearest(c.Pos, c.LastRoad, c.Z);
        // Not on a ramp's lip or a bridge's slope: a little further on, where it's flat.
        for (int k = 0; k < 40 && w.Course.Rises[at] is Rise.Ramp or Rise.Bridge; k++) at = w.Course.IndexAt(w.Course.Along[at] + Course.Spacing * 2);
        c.Index = c.LastRoad = at;
        c.Pos = w.Course.Points[at];
        c.Z = w.Course.Z[at];
        c.VZ = 0;
        c.Heading = w.Course.Dir(at).Angle;
        c.Vel = default;
        c.Wrecked = c.Towed = c.Spin = c.Slick = 0;
        c.Airborne = false;
        c.OnRoad = true;
        if (!tow)
        {
            c.Damage = 0;
            w.Heard.Add(Sfx.Respawn);
        }
        c.Ghost = GhostSeconds;
    }

    // ───────────────────────────── damage ─────────────────────────────

    /// <summary>A knock: in a destruction race it's damage (less for a tough or armoured car), and enough of it wrecks the car.</summary>
    public static void Hurt(World w, Car c, double raw)
    {
        if (!w.Rules.Destruction || raw <= 0 || !c.Active || c.Ghost > 0 || w.Over) return;
        c.Damage = Math.Min(100, c.Damage + c.Stats.Hurt(raw));
        if (c.Damage >= 100) Wreck(w, c);
    }

    private static void Wreck(World w, Car c)
    {
        c.Wrecks++;
        c.Vel = default;
        c.Airborne = false;
        c.Z = c.OnRoad ? w.Course.Height(c.Pos, c.Index) : 0;
        c.VZ = 0;
        c.Spin = c.Slick = 0;
        Boom(w, c.Pos, c.Z, 1.8);
        for (int k = 0; k < 10; k++)
        {
            double a = w.Dice.Next() * Math.PI * 2, v = 40 + w.Dice.Next() * 90;
            w.Effects.Add(new Effect { Kind = EffectKind.Debris, Pos = c.Pos, Z = c.Z + 6, Vel = Vec.FromAngle(a) * v, VZ = 120 + w.Dice.Next() * 160, Life = 1.6, Size = 0.6 + w.Dice.Next(), Colour = k % 3 == 0 ? 0xFF303030 : c.Id == 0 ? 0xFFE8382A : 0xFF3A7AE8 });
        }
        var other = w.Cars[1 - c.Id];
        if (w.Rules.Wrecks == WreckRule.Out)
        {
            c.Out = true;
            w.Say(c.Id, "WRECKED: YOU'RE OUT!", 4);
            if (!other.Out)
            {
                w.Winner = other.Id;
                w.Say(other.Id, "YOU WIN: THE OTHER CAR IS WRECKED!", 4);
            }
            End(w);
        }
        else
        {
            c.Wrecked = WreckSeconds;
            w.Say(c.Id, "WRECKED!", 2.5);
            w.Say(other.Id, "GOT 'EM!", 1.6);
        }
    }

    private static void Boom(World w, Vec at, double z, double size)
    {
        w.Effects.Add(new Effect { Kind = EffectKind.Boom, Pos = at, Z = z, Life = 0.9, Size = size });
        w.Heard.Add(Sfx.Explosion);
    }

    // ───────────────────────────── the cars together ─────────────────────────────

    /// <summary>Cars that touch (at the same height: one can pass under a bridge as the other crosses it) push apart, the lighter more, and a hard knock is damage.</summary>
    private static void Collide(World w)
    {
        var (a, b) = (w.Cars[0], w.Cars[1]);
        if (!a.Active || !b.Active || a.Ghost > 0 || b.Ghost > 0 || Math.Abs(a.Z - b.Z) > 12) return;
        var d = b.Pos - a.Pos;
        double dist = d.Length, reach = a.Radius + b.Radius;
        if (dist >= reach || dist < 1e-6) return;
        var n = d / dist;
        double ma = a.Stats.Mass, mb = b.Stats.Mass, overlap = reach - dist;
        a.Pos -= n * (overlap * mb / (ma + mb));
        b.Pos += n * (overlap * ma / (ma + mb));
        double closing = (a.Vel - b.Vel).Dot(n);
        if (closing <= 0) return;
        double j = 1.3 * closing / (1 / ma + 1 / mb);
        a.Vel -= n * (j / ma);
        b.Vel += n * (j / mb);
        if (closing > 40)
        {
            w.Heard.Add(closing > 150 ? Sfx.Crash : Sfx.Bump);
            w.Effects.Add(new Effect { Kind = EffectKind.Spark, Pos = a.Pos + n * a.Radius, Z = (a.Z + b.Z) / 2 + 5, Life = 0.35, Size = Math.Min(2, closing / 120) });
        }
        if (closing > RamFloor)
        {
            double raw = (closing - RamFloor) * RamDamage * 2;
            Hurt(w, a, raw * mb / (ma + mb));
            Hurt(w, b, raw * ma / (ma + mb));
        }
    }

    // ───────────────────────────── oil and mines ─────────────────────────────

    /// <summary>The weapon button: an oil slick or a land mine dropped behind the car.</summary>
    private static void Weapons(World w, Car c, Controls ctl, double dt)
    {
        bool pressed = ctl.Fire && !c.FirePrev;
        c.FirePrev = ctl.Fire;
        if (!pressed || !c.Active || w.Phase != RacePhase.Racing) return;
        if (!w.Rules.Destruction || c.Ammo <= 0 || c.Cooldown > 0 || c.Airborne)
        {
            if (c.Human && w.Rules.Destruction) w.Heard.Add(Sfx.Empty);
            return;
        }
        c.Ammo--;
        c.Cooldown = Cooldown;
        var at = c.Pos - c.Forward * (c.Body.Length / 2 + 10);
        var kind = c.Build.Weapon == WeaponKind.Mines ? DropKind.Mine : DropKind.Oil;
        w.Drops.Add(new Drop { Kind = kind, Pos = at, Z = c.Z, Owner = c.Id });
        if (w.Drops.Count > MaxDrops) w.Drops.RemoveAt(0);
        w.Heard.Add(kind == DropKind.Mine ? Sfx.Mine : Sfx.Oil);
        if (kind == DropKind.Oil) w.Effects.Add(new Effect { Kind = EffectKind.Splat, Pos = at, Z = c.Z, Life = 0.4 });
    }

    private static void Hazards(World w, double dt)
    {
        foreach (var d in w.Drops) d.Age += dt;
        w.Drops.RemoveAll(d => d.Kind == DropKind.Oil && d.Age > OilSeconds);
        foreach (var c in w.Cars)
        {
            if (!c.Active || c.Ghost > 0 || c.Airborne) continue;
            for (int i = w.Drops.Count - 1; i >= 0; i--)
            {
                var d = w.Drops[i];
                if (Math.Abs(d.Z - c.Z) > 6) continue;
                double dist = (d.Pos - c.Pos).Length;
                if (d.Kind == DropKind.Oil && dist < 16 + c.Radius * 0.5)
                {
                    if (c.Slick <= 0)
                    {
                        c.Spin = 0.3;
                        c.SpinDir = w.Dice.Next() < 0.5 ? -1 : 1;
                        w.Heard.Add(Sfx.Skid);
                        if (c.Id != d.Owner) w.Say(c.Id, "OIL!", 1);
                    }
                    c.Slick = 0.9;
                }
                else if (d.Kind == DropKind.Mine && (d.Age > MineArm || d.Owner != c.Id) && dist < 10 + c.Radius)
                {
                    w.Drops.RemoveAt(i);
                    Boom(w, d.Pos, d.Z, 1.3);
                    c.Airborne = true;
                    c.AirTime = 0;
                    c.VZ = MineLaunch;
                    c.Vel *= 0.4;
                    c.Spin = 0.8;
                    c.SpinDir = w.Dice.Next() < 0.5 ? -1 : 1;
                    w.Say(c.Id, "MINE!", 1.2);
                    Hurt(w, c, MineDamage);
                    break;
                }
            }
        }
    }

    // ───────────────────────────── laps and the finish ─────────────────────────────

    /// <summary>
    /// Keeps count of how far round a car has gone, laps and lap times. Only steady progress counts: if the car is
    /// suddenly somewhere else (it took a short cut through the rough), that's not progress.
    /// </summary>
    private static void Round(World w, Car c)
    {
        if (!c.Active || w.Phase != RacePhase.Racing && !w.Over) return;
        var course = w.Course;
        double was = c.Progress, at = course.Along[c.Index], lapAt = ((was % course.Length) + course.Length) % course.Length;
        double step = at - lapAt;
        if (step < -course.Length / 2) step += course.Length;
        if (step > course.Length / 2) step -= course.Length;
        if (Math.Abs(step) > 60) return;
        c.Progress += step;
        int before = (int)Math.Floor(was / course.Length), now = (int)Math.Floor(c.Progress / course.Length);
        if (now <= before || c.Finished || now < 1 || now <= c.Laps) return;
        c.Laps = now;
        c.LapTimes.Add(w.Clock - c.LapStart);
        c.LapStart = w.Clock;
        if (w.Over) return;
        if (c.Laps >= w.Rules.Laps)
        {
            Finish(w, c);
            return;
        }
        w.Heard.Add(c.Laps == w.Rules.Laps - 1 ? Sfx.FinalLap : Sfx.Lap);
        bool best = c.LapTimes[^1] <= c.LapTimes.Min();
        w.Say(c.Id, c.Laps == w.Rules.Laps - 1 ? "FINAL LAP!" : $"LAP {c.Laps + 1}" + (best && c.Laps > 1 ? "  BEST LAP!" : ""), 1.6);
    }

    private static void Finish(World w, Car c)
    {
        c.Finished = true;
        c.Place = ++w.Finishers;
        c.FinishTime = w.Clock;
        w.Heard.Add(Sfx.Flag);
        if (c.Place == 1)
        {
            w.Winner = c.Id;
            w.Say(c.Id, "THE CHEQUERED FLAG: YOU WIN!", 4);
            w.Late = LateSeconds;
        }
        else w.Say(c.Id, "HOME IN SECOND", 4);
        if (w.Cars.All(x => x.Finished || x.Out)) End(w);
    }

    private static void End(World w)
    {
        if (w.Over) return;
        w.Phase = RacePhase.Finished;
        w.PhaseTime = 0;
        w.Late = -1;
        if (w.Winner < 0)
        {
            // Nobody home (both wrecked out): whoever got further.
            var lead = w.Cars.Where(c => !c.Out).OrderByDescending(c => c.Progress).FirstOrDefault();
            w.Winner = lead?.Id ?? -1;
        }
        w.Heard.Add(w.Winner >= 0 && w.Cars[w.Winner].Human ? Sfx.Win : Sfx.Lose);
    }

    /// <summary>The order the cars stand in: those home by when they finished, then by how far round they are.</summary>
    public static List<Car> Standings(World w) =>
        [.. w.Cars.OrderBy(c => c.Finished ? c.Place : c.Out ? 200 : 100).ThenByDescending(c => c.Progress)];

    public static int LapOf(World w, Car c) => Math.Clamp(c.Laps + 1, 1, w.Rules.Laps);
}
