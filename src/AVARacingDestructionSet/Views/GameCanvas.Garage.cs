using Avalonia;
using Avalonia.Media;
using AVARacingDestructionSet.Engine;

namespace AVARacingDestructionSet.Views;

public sealed partial class GameCanvas
{
    public static Rect PlayerTab(int i) => new(12 + i * 248, 54, 244, 34);
    public static Rect VehicleTile(int i) => new(12 + i % 3 * 164, 94 + i / 3 * 112, 160, 108);
    public static readonly Rect PreviewRect = new(512, 54, 500, 196);
    public static readonly Rect SetupRect = new(12, 432, 492, 252);
    public static readonly Rect StatsRect = new(512, 470, 500, 150);
    public static readonly Rect RaceRect = new(764, 628, 248, 130), BackRect = new(512, 690, 240, 68);
    public static Rect OptionRect(int row, int i, int of) => new(640 + i * (368.0 / of), 258 + row * 42, 368.0 / of - 6, 34);
    public static Rect SetupButton(int row, int i, int of = 2) => new(150 + i * (342.0 / of), 446 + row * 46, 342.0 / of - 6, 36);

    private void DrawGarage(DrawingContext dc)
    {
        var g = Engine;
        DrawHeader(dc, g.Editor.Track.Name);
        int me = GaragePlayer;
        var b = g.Builds[me];
        for (int i = 0; i < 2; i++)
        {
            int p = i;
            string who = i == 0 ? "PLAYER 1" : g.Friend ? "PLAYER 2" : "THE COMPUTER";
            var r = PlayerTab(i);
            bool on = me == i;
            dc.FillRectangle(Brush(on ? Shade(CarColour(i), 0.55) : 0xFF241C60), r, 3);
            dc.DrawRectangle(null, Pen(on ? Shade(CarColour(i), 1.4) : LightBlue, on ? 2 : 1.2), r, 3, 3);
            Text(dc, $"{who}: {Vehicles.Of(g.Builds[i].Vehicle).Name}", r.Center, 21, on ? White : Dim, center: true);
            Hit(r, () => { GaragePlayer = p; g.Choose(() => { }); });
        }
        // The vehicles.
        for (int i = 0; i < Vehicles.All.Length; i++)
        {
            var v = Vehicles.All[i];
            var r = VehicleTile(i);
            bool on = b.Vehicle == v, hover = r.Contains(_mouse);
            dc.FillRectangle(Brush(on ? 0xFF5A4AB8 : hover ? 0xFF4A3CA0 : 0xFF241C60), r, 4);
            dc.DrawRectangle(null, Pen(on ? Yellow : hover ? White : LightBlue, on ? 2.4 : 1.2), r, 4, 4);
            double spin = on ? _clock * 1.2 : 0.9;
            using (dc.PushClip(r))
            using (dc.PushTransform(Matrix.CreateScale(2.2, 2.2) * Matrix.CreateTranslation(r.Center.X, r.Y + 58)))
            {
                dc.DrawEllipse(Brush(0x40000000), null, new Point(0, 0), 20, 11);
                DrawVehicle(dc, v, default, 0, spin, CarColour(me));
            }
            Text(dc, Vehicles.Of(v).Name, new Point(r.Center.X, r.Bottom - 12), 20, on ? Yellow : Ink, center: true);
            Hit(r, () => g.Choose(() => b.Vehicle = v));
        }
        // The vehicle chosen, big, on a turntable.
        var body = Vehicles.Of(b.Vehicle);
        Panel(dc, PreviewRect, 0xFF1A1450);
        using (dc.PushClip(PreviewRect))
        {
            var c = new Point(PreviewRect.X + 170, PreviewRect.Y + 120);
            for (int k = 0; k < 4; k++) dc.DrawEllipse(Brush(k % 2 == 0 ? 0xFF3A3070 : 0xFF2A2260), null, c, 130 - k * 26, 60 - k * 12);
            using (dc.PushTransform(Matrix.CreateScale(4.6, 4.6) * Matrix.CreateTranslation(c.X, c.Y)))
                DrawVehicle(dc, b.Vehicle, default, 0, _clock * 0.8, CarColour(me));
        }
        Text(dc, body.Name, new Point(PreviewRect.Right - 14, PreviewRect.Y + 30), 40, Yellow, right: true);
        double ay = PreviewRect.Y + 70;
        foreach (var line in Wrap(body.About, 190, 20)) { Text(dc, line, new Point(PreviewRect.Right - 200, ay), 20, Ink); ay += 22; }
        if (body.LowGravity) Text(dc, "LOW-GRAVITY GRIP", new Point(PreviewRect.Right - 14, PreviewRect.Bottom - 20), 18, Cyan, right: true);
        // Its parts.
        void Row(int row, string label, string[] names, int chosen, Action<int> set)
        {
            Text(dc, label, new Point(520, OptionRect(row, 0, 1).Center.Y), 22, Dim);
            for (int i = 0; i < names.Length; i++)
            {
                int k = i;
                Button(dc, OptionRect(row, i, names.Length), names[i], () => g.Choose(() => set(k)), on: chosen == i, size: 20);
            }
        }
        Row(0, "ENGINE", [.. Enum.GetValues<EngineKind>().Select(Vehicles.Name)], (int)b.Engine, k => b.Engine = (EngineKind)k);
        Row(1, "TYRES", [.. Enum.GetValues<TyreKind>().Select(Vehicles.Name)], (int)b.Tyres, k => b.Tyres = (TyreKind)k);
        Row(2, "WEIGHT", [.. Enum.GetValues<WeightKind>().Select(Vehicles.Name)], (int)b.Weight, k => b.Weight = (WeightKind)k);
        Row(3, "ARMOUR", [.. Enum.GetValues<ArmourKind>().Select(Vehicles.Name)], (int)b.Armour, k => b.Armour = (ArmourKind)k);
        Row(4, "WEAPON", [.. Enum.GetValues<WeaponKind>().Select(w => w == WeaponKind.None ? "NONE" : $"{Vehicles.Name(w)} x{Vehicles.Ammo(w)}")], (int)b.Weapon, k => b.Weapon = (WeaponKind)k);
        DrawStats(dc, b);
        DrawSetup(dc);
        Button(dc, BackRect, "<  THE TRACK", g.OpenEditor, size: 26);
        bool ready = g.Course != null;
        Button(dc, RaceRect, ready ? "RACE!" : "TRACK NOT READY", () => g.StartRace(), size: ready ? 56 : 26, accent: ready ? Green : Red);
    }

    /// <summary>The bars: what the build can do on this track (its surface, its gravity).</summary>
    private void DrawStats(DrawingContext dc, Build b)
    {
        var t = Engine.Editor.Track;
        var s = Vehicles.StatsOf(b, t.Surface);
        double g = Track.Gravity(t.Planet);
        Panel(dc, StatsRect);
        double grip = s.Grip * Sim.GravityGrip(g, s.LowGravity);
        (string Name, double Value, string Shown)[] bars =
        [
            ("TOP SPEED", s.Top / 450, $"{s.Top * 0.4:0} MPH"),
            ("ACCELERATION", s.Accel * Sim.GravityAccel(g, s.LowGravity) / 450, $"{s.Accel * Sim.GravityAccel(g, s.LowGravity) / s.Top:0.0}"),
            ($"GRIP ({Track.Describe(t.Surface)}, {Track.Describe(t.Planet)})", grip / 1.8, $"{grip:0.00}"),
            ("TOUGHNESS", s.Toughness / (1 - s.Protection) / 4.5, $"{s.Toughness / (1 - s.Protection):0.0}"),
            ("WEIGHT", s.Mass / 3.2, $"{s.Mass:0.00} T"),
        ];
        for (int i = 0; i < bars.Length; i++)
        {
            double y = StatsRect.Y + 18 + i * 28;
            Text(dc, bars[i].Name, new Point(StatsRect.X + 12, y), 19, Ink);
            var bar = new Rect(StatsRect.X + 250, y - 8, 180, 16);
            dc.FillRectangle(Brush(0xFF1A1450), bar);
            int cells = (int)Math.Round(Math.Clamp(bars[i].Value, 0, 1) * 18);
            for (int k = 0; k < cells; k++) dc.FillRectangle(Brush(Bars[Math.Min(Bars.Length - 1, k * Bars.Length / 18)]), new Rect(bar.X + 1 + k * 10, bar.Y + 2, 8, 12));
            Text(dc, bars[i].Shown, new Point(StatsRect.Right - 10, y), 19, White, right: true);
        }
    }

    /// <summary>How the race is to be run: plain or destruction, how many laps, against whom, and what a wreck means.</summary>
    private void DrawSetup(DrawingContext dc)
    {
        var g = Engine;
        Panel(dc, SetupRect);
        var rules = g.Rules;
        void Label(int row, string s) => Text(dc, s, new Point(SetupRect.X + 12, SetupButton(row, 0).Center.Y), 21, Dim);
        Label(0, "THE RACE");
        Button(dc, SetupButton(0, 0), "RACING", () => g.Choose(() => g.Rules = g.Rules with { Mode = Mode.Racing }), on: rules.Mode == Mode.Racing, size: 22);
        Button(dc, SetupButton(0, 1), "DESTRUCTION", () => g.Choose(() => g.Rules = g.Rules with { Mode = Mode.Destruction }), on: rules.Destruction, size: 22, accent: Red);
        Label(1, "LAPS");
        Button(dc, SetupButton(1, 0, 4), "-", () => g.Choose(() => g.Rules = g.Rules with { Laps = Math.Max(1, g.Rules.Laps - 1) }), size: 26, enabled: rules.Laps > 1);
        Text(dc, rules.Laps.ToString(), new Point((SetupButton(1, 1, 4).Center.X + SetupButton(1, 2, 4).Center.X) / 2, SetupButton(1, 0).Center.Y), 34, Yellow, center: true);
        Button(dc, SetupButton(1, 3, 4), "+", () => g.Choose(() => g.Rules = g.Rules with { Laps = Math.Min(RaceRules.MaxLaps, g.Rules.Laps + 1) }), size: 26, enabled: rules.Laps < RaceRules.MaxLaps);
        Label(2, "AGAINST");
        Button(dc, SetupButton(2, 0), "THE COMPUTER", () => g.Choose(() => g.Friend = false), on: !g.Friend, size: 22);
        Button(dc, SetupButton(2, 1), "A FRIEND", () => g.Choose(() => g.Friend = true), on: g.Friend, size: 22);
        Label(3, "WRECKS");
        Button(dc, SetupButton(3, 0), "COME BACK", () => g.Choose(() => g.Rules = g.Rules with { Wrecks = WreckRule.Respawn }), on: rules.Wrecks == WreckRule.Respawn, size: 22, enabled: rules.Destruction);
        Button(dc, SetupButton(3, 1), "ARE OUT", () => g.Choose(() => g.Rules = g.Rules with { Wrecks = WreckRule.Out }), on: rules.Wrecks == WreckRule.Out, size: 22, enabled: rules.Destruction);
        string line = rules.Destruction
            ? "OIL, MINES AND RAMMING DO DAMAGE. AT 100% THE CAR IS WRECKED."
            : "A PLAIN RACE: NO WEAPONS, NO DAMAGE. FASTEST HOME WINS.";
        Text(dc, line, new Point(SetupRect.X + 12, SetupRect.Bottom - 20), 17, Cyan);
        // The track.
        var check = g.Editor.Check;
        Text(dc, $"{g.Editor.Track.Name}: {Track.Describe(g.Editor.Track.Surface)}, {Track.Describe(g.Editor.Track.Planet)}", new Point(SetupRect.X + 4, 706), 21, White);
        double size = 17;
        while (TextWidth(check.Why, size) > 480 && size > 11) size -= 0.5;
        Text(dc, check.Why, new Point(SetupRect.X + 4, 732), size, check.Ok ? Green : Red);
    }
}
