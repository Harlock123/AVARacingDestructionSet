using Avalonia;
using Avalonia.Media;
using AVARacingDestructionSet.Engine;

namespace AVARacingDestructionSet.Views;

public sealed partial class GameCanvas
{
    /// <summary>How big the world is drawn in a race.</summary>
    public const double RaceScale = 1.35;

    /// <summary>Each car's half of the screen: player 1's on top, player 2's below.</summary>
    public static Rect HalfRect(int car) => car == 0 ? new Rect(4, 4, 1016, 374) : new Rect(4, 390, 1016, 374);

    private static readonly string[] Places = ["1ST", "2ND"];

    private void DrawRaceScreen(DrawingContext dc)
    {
        var w = Engine.World;
        if (w.Cars.Length < 2) return;
        for (int i = 0; i < World.Players; i++) DrawHalf(dc, w, i);
        // The bar between the halves.
        dc.FillRectangle(Brush(0xFF000000), new Rect(0, 378, Field.Width, 12));
        for (int i = 0; i < Bars.Length; i++) dc.FillRectangle(Brush(Bars[i]), new Rect(0, 379 + i * 1.25, Field.Width, 1));
        if (w.Phase == RacePhase.Countdown || w.Phase == RacePhase.Racing && w.PhaseTime < 1)
            for (int i = 0; i < World.Players; i++) DrawLights(dc, w, HalfRect(i));
        if (Engine.State == GameState.Results) DrawResults(dc, w);
    }

    /// <summary>Where a half's camera looks: a little ahead of its car, kept over the grid.</summary>
    private static Point Camera(World w, Car c, Rect half)
    {
        var at = c.Towed > 0 ? w.Course.Points[c.LastRoad] : c.Pos + c.Vel * 0.3;
        var p = P(at, c.Z * 0.5);
        double hw = half.Width / 2 / RaceScale, hh = half.Height / 2 / RaceScale;
        double gw = Track.Cols * Pieces.Cell, gh = Track.Rows * Pieces.Cell * Squash;
        return new Point(Math.Clamp(p.X, hw - 70, gw - hw + 70), Math.Clamp(p.Y, hh - 90, gh - hh + 70));
    }

    private void DrawHalf(DrawingContext dc, World w, int i)
    {
        var half = HalfRect(i);
        var car = w.Cars[i];
        var cam = Camera(w, car, half);
        using (dc.PushClip(half))
        {
            using (dc.PushTransform(Matrix.CreateTranslation(-cam.X, -cam.Y) * Matrix.CreateScale(RaceScale, RaceScale) * Matrix.CreateTranslation(half.Center.X, half.Center.Y)))
            {
                var visible = new Rect(cam.X - half.Width / 2 / RaceScale, cam.Y - half.Height / 2 / RaceScale, half.Width / RaceScale, half.Height / RaceScale);
                DrawRace(dc, w, visible);
                // A marker over this half's own car, so it's easy to find.
                if (car.Active && w.Phase != RacePhase.Finished)
                {
                    var top = P(car.Pos, car.Z + car.Body.Height + 10);
                    Poly(dc, Shade(CarColour(i), 1.3), 0xFF000000, 1, new Point(top.X - 5, top.Y - 7), new Point(top.X + 5, top.Y - 7), new Point(top.X, top.Y));
                }
            }
            DrawHud(dc, w, i, half);
        }
    }

    private void DrawHud(DrawingContext dc, World w, int i, Rect half)
    {
        var c = w.Cars[i];
        uint colour = Shade(CarColour(i), 1.25);
        var bar = new Rect(half.X, half.Y, half.Width, 34);
        dc.FillRectangle(Brush(0xC0000000), bar);
        dc.FillRectangle(Brush(colour), new Rect(bar.X + 8, bar.Y + 5, 46, 24), 3);
        Text(dc, c.Human ? $"P{i + 1}" : "CPU", new Point(bar.X + 31, bar.Y + 17), 24, 0xFF000000, center: true, shadow: false);
        string lap = c.Finished ? "FINISHED" : $"LAP {Sim.LapOf(w, c)}/{w.Rules.Laps}";
        Text(dc, lap, new Point(bar.X + 66, bar.Y + 17), 26, White);
        double now = w.Phase == RacePhase.Countdown ? 0 : c.Finished ? c.LapTimes.LastOrDefault() : w.Clock - c.LapStart;
        Text(dc, LapEntry.Format(now), new Point(bar.X + 210, bar.Y + 17), 26, Yellow);
        Text(dc, "BEST " + LapEntry.Format(c.BestLap), new Point(bar.X + 320, bar.Y + 17), 22, Cyan);
        Text(dc, Vehicles.Of(c.Build.Vehicle).Name, new Point(bar.X + 470, bar.Y + 17), 20, Dim);
        // The speedometer.
        double speed = Math.Abs(c.Speed) * 0.4;
        Text(dc, $"{speed:0} MPH", new Point(bar.Right - 270, bar.Y + 17), 22, White, right: true);
        int place = Sim.Standings(w).IndexOf(c);
        Text(dc, c.Out ? "OUT" : Places[Math.Clamp(c.Finished ? c.Place - 1 : place, 0, 1)], new Point(bar.Right - 12, bar.Y + 18), 32, c.Out ? Red : place == 0 ? Yellow : White, right: true);
        if (w.Rules.Destruction)
        {
            // Damage, in ten lamps from green to red, and what's left to drop.
            Text(dc, "DMG", new Point(bar.Right - 250, bar.Y + 17), 18, Dim);
            for (int k = 0; k < 10; k++)
            {
                bool lit = c.Damage >= (k + 1) * 10 - 5;
                uint lamp = k < 4 ? Green : k < 7 ? Yellow : Red;
                dc.FillRectangle(Brush(lit ? lamp : 0xFF2A2A2A), new Rect(bar.Right - 214 + k * 9, bar.Y + 9, 7, 16), 1);
            }
            var weapon = c.Build.Weapon;
            for (int k = 0; k < c.Ammo; k++)
            {
                var at = new Point(bar.Right - 116 + k * 11, bar.Y + 17);
                if (weapon == WeaponKind.Mines) { dc.DrawEllipse(Brush(0xFF5A5A48), null, at, 4.5, 4); dc.DrawEllipse(Brush(Red), null, at, 1.6, 1.6); }
                else dc.DrawEllipse(Brush(0xFF8A7AE8), Pen(0xFF101014, 1), at, 4.5, 3.5);
            }
        }
        DrawMiniMap(dc, w, new Rect(half.Right - 158, half.Bottom - 122, 150, 114), i);
        // The word for this car.
        if (w.Messages[i] is { } msg)
        {
            double size = msg.Length > 16 ? 44 : 60;
            Text(dc, msg, new Point(half.Center.X, half.Y + 120), size, msg.Contains("WRECK") || msg.Contains("OUT") || msg == "MINE!" ? Red : Yellow, center: true);
        }
        if (c.Towed > 0) Text(dc, "TOWING YOU BACK...", new Point(half.Center.X, half.Y + 170), 26, White, center: true);
        if (c.Wrecked > 0) Text(dc, $"BACK IN {Math.Ceiling(c.Wrecked)}", new Point(half.Center.X, half.Y + 170), 30, White, center: true);
        if (w.Late > 0 && !c.Finished && !c.Out) Text(dc, $"FINISH IN {Math.Ceiling(w.Late)}!", new Point(half.Center.X, half.Bottom - 30), 30, Orange, center: true);
    }

    /// <summary>The whole track, small, with both cars on it.</summary>
    private static void DrawMiniMap(DrawingContext dc, World w, Rect r, int me)
    {
        dc.FillRectangle(Brush(0xA0000000), r, 3);
        dc.DrawRectangle(null, Pen(0x807869C4, 1), r, 3, 3);
        double k = Math.Min((r.Width - 12) / (Track.Cols * Pieces.Cell), (r.Height - 12) / (Track.Rows * Pieces.Cell * Squash));
        Point M(Vec v) => new(r.X + 6 + v.X * k, r.Y + 6 + v.Y * Squash * k);
        var course = w.Course;
        var pts = new List<Point>();
        for (int i = 0; i < course.Points.Length; i += 6) pts.Add(M(course.Points[i]));
        pts.Add(pts[0]);
        Polyline(dc, Pen(0xFF9A9AA8, 3), pts);
        dc.DrawEllipse(Brush(White), null, M(course.Points[0]), 2.5, 2.5);
        foreach (var c in w.Cars.OrderBy(c => c.Id == me ? 1 : 0))
        {
            if (c.Out) continue;
            var at = M(c.Towed > 0 ? course.Points[c.LastRoad] : c.Pos);
            dc.DrawEllipse(Brush(Shade(CarColour(c.Id), 1.25)), Pen(0xFF000000, 1), at, c.Id == me ? 4.5 : 3.5, c.Id == me ? 4.5 : 3.5);
        }
    }

    /// <summary>The starting lights, over each half: three reds coming on a second apart, then green.</summary>
    private static void DrawLights(DrawingContext dc, World w, Rect half)
    {
        var r = new Rect(half.Center.X - 96, half.Y + 64, 192, 64);
        dc.FillRectangle(Brush(0xE0101010), r, 8);
        dc.DrawRectangle(null, Pen(LightBlue, 2), r, 8, 8);
        bool go = w.Phase == RacePhase.Racing;
        for (int i = 0; i < 3; i++)
        {
            bool on = go || w.PhaseTime >= i;
            uint col = go ? 0xFF3AE85A : on ? 0xFFFF3A2A : 0xFF3A1A1A;
            var c = new Point(r.X + 36 + i * 60, r.Center.Y);
            if (on) dc.DrawEllipse(Brush(Alpha(col, 0.35)), null, c, 26, 26);
            dc.DrawEllipse(Brush(col), Pen(0xFF000000, 2), c, 19, 19);
            dc.DrawEllipse(Brush(0x50FFFFFF), null, new Point(c.X - 6, c.Y - 6), 5, 4);
        }
        if (go) Text(dc, "GO!", new Point(r.Center.X, r.Bottom + 34), 56, 0xFF3AE85A, center: true);
    }

    private void DrawResults(DrawingContext dc, World w)
    {
        var r = new Rect(Field.Width / 2 - 330, 214, 660, 340);
        dc.FillRectangle(Brush(0xF02E2470), r, 6);
        dc.DrawRectangle(null, Pen(LightBlue, 2), r, 6, 6);
        for (int i = 0; i < Bars.Length; i++) dc.FillRectangle(Brush(Bars[i]), new Rect(r.X + 2, r.Y + 64 + i * 1.5, r.Width - 4, 1));
        string Who(Car c) => c.Human ? $"PLAYER {c.Id + 1}" : "THE COMPUTER";
        string title = w.Winner < 0 ? "NOBODY WINS" : $"{Who(w.Cars[w.Winner])} WINS!";
        Text(dc, title, new Point(r.Center.X, r.Y + 36), 50, Yellow, center: true);
        Text(dc, "                       TIME     BEST LAP  WRECKS", new Point(r.X + 20, r.Y + 92), 22, Dim);
        var order = Sim.Standings(w);
        for (int k = 0; k < order.Count; k++)
        {
            var c = order[k];
            double y = r.Y + 134 + k * 64;
            dc.FillRectangle(Brush(Shade(CarColour(c.Id), 0.8)), new Rect(r.X + 18, y - 24, 6, 48));
            Text(dc, c.Out ? "OUT" : c.Finished ? Places[c.Place - 1] : "DNF", new Point(r.X + 34, y - 8), 34, k == 0 && !c.Out ? Yellow : White);
            Text(dc, Who(c), new Point(r.X + 110, y - 12), 26, Shade(CarColour(c.Id), 1.4));
            Text(dc, c.Body.Name, new Point(r.X + 110, y + 12), 20, Ink);
            Text(dc, c.Finished ? LapEntry.Format(c.FinishTime) : "-:--.--", new Point(r.X + 300, y - 2), 28, White);
            bool record = c.Human && c.BestLap > 0 && Engine.BestLaps.Qualifies((int)Math.Round(c.BestLap * 1000));
            Text(dc, LapEntry.Format(c.BestLap), new Point(r.X + 420, y - 2), 28, record ? Green : White);
            if (record && Blink(3)) Text(dc, "A RECORD!", new Point(r.X + 420, y + 22), 18, Green);
            Text(dc, c.Wrecks.ToString(), new Point(r.X + 600, y - 2), 28, c.Wrecks > 0 ? Red : White, right: true);
        }
        Text(dc, $"{w.Track.Name}  -  {w.Rules.Laps} LAPS  -  {(w.Rules.Destruction ? "DESTRUCTION" : "RACING")}", new Point(r.Center.X, r.Bottom - 54), 20, Cyan, center: true);
        if (Blink(2)) Text(dc, "PRESS ENTER TO CARRY ON", new Point(r.Center.X, r.Bottom - 24), 26, White, center: true);
    }
}
