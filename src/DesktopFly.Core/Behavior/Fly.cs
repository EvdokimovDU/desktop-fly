using System.Numerics;
using DesktopFly.Core.Model3D;
using DesktopFly.Core.Models;

namespace DesktopFly.Core.Behavior;

public class Fly
{
    public enum FlyState { Walking, Idle, Grooming, Flying, Sleeping }

    public const float EdgeMargin = 50f;
    public const float ScareRadius = 110f;
    public const float NervousRadius = 240f;

    public FlyModel Model { get; }
    public SceneNode Node => Model.Root;

    public Vector2 Pos { get; set; }
    public float Heading { get; set; }
    public float Speed { get; set; } = 30f;
    public FlyState State { get; set; } = FlyState.Walking;
    public float StateTimer { get; set; }
    public float GaitPhase { get; set; }
    public float Time { get; set; }
    public float ScareCooldown { get; set; } = 0f;
    public float DartCooldown { get; set; } = 0f;
    public float BackwardTimer { get; set; } = 0f;
    public float DartTimer { get; set; } = 0f;
    public float StateAge { get; set; } = 0f;
    public List<Ledge> Terrain { get; set; } = new();
    public Ledge? CurrentLedge { get; set; }

    public float GaitPhasePublic => GaitPhase;
    public float WalkingIntensity => State == FlyState.Walking ? Clamp(Math.Abs(BackwardTimer > 0 ? 22f : Speed) / 60f, 0f, 1f) : 0f;

    public Vector2 FlightFrom { get; set; } = Vector2.Zero;
    public Vector2 FlightTo { get; set; } = Vector2.Zero;
    public float FlightT { get; set; } = 0f;
    public float FlightDur { get; set; } = 1f;
    public float FlightEffort { get; set; } = 0.6f;
    public float EffortCurrent { get; set; } = 0.6f;
    public float Alt { get; set; } = 0f;
    public float Pitch { get; set; } = 0f;
    public float FlapPhase { get; set; } = 0f;
    public float WingRaise { get; set; } = 0f;

    private bool _brainLive = false;
    private float _liveArousal = 0f;
    private float _liveWing = 0f;

    private readonly Random _rng = new();

    private float Rnd(float min, float max) => min + (float)_rng.NextDouble() * (max - min);
    private static float Clamp(float v, float lo, float hi) => Math.Min(hi, Math.Max(lo, v));
    private static float Smoothstep(float t) { float x = Clamp(t, 0f, 1f); return x * x * (3f - 2f * x); }
    private static float AngleDiff(float from, float to)
    {
        float d = (to - from) % (2 * MathF.PI);
        if (d > MathF.PI) d -= 2 * MathF.PI;
        if (d < -MathF.PI) d += 2 * MathF.PI;
        return d;
    }

    public Fly(Vector2 p)
    {
        Model = FlyModel.Create();
        Pos = p;
        Heading = Rnd(0f, 2 * MathF.PI);
        StateTimer = Rnd(1.5f, 4f);
        GaitPhase = Rnd(0f, 1f);
        Time = Rnd(0f, 100f);
        SyncNode();
    }

    public void SyncNode()
    {
        Node.Position = new Vector3(Pos.X, Pos.Y, Node.Position.Z);
        Node.EulerAngles = new Vector3(Pitch, 0f, Heading - MathF.PI / 2f);
    }

    public void StartFlight(Vector2 bounds, Vector2? awayFrom = null, bool escape = false, float? effort = null)
    {
        State = FlyState.Flying;
        CurrentLedge = null;
        FlightEffort = Clamp(effort ?? (escape ? 1.0f : Rnd(0.4f, 0.75f)), 0.25f, 1f);
        EffortCurrent = FlightEffort;
        FlapPhase = 0f;
        WingRaise = 0f;
        FlightFrom = Pos;

        float hw = bounds.X / 2f - EdgeMargin;
        float hh = bounds.Y / 2f - EdgeMargin;
        var target = Vector2.Zero;
        bool chosen = false;

        if (!escape && awayFrom == null && Terrain.Count > 0 && Rnd(0f, 1f) < 0.45f)
        {
            var l = Terrain[_rng.Next(Terrain.Count)];
            if (l.X1 - l.X0 > 90f)
            {
                target = new Vector2(Rnd(l.X0 + 25f, l.X1 - 25f), l.Y);
                chosen = Vector2.Distance(target, Pos) > 180f;
            }
        }

        if (!chosen)
        {
            for (int i = 0; i < 16; i++)
            {
                target = new Vector2(Rnd(-hw, hw), Rnd(-hh, hh));
                bool far = Vector2.Distance(target, Pos) > (escape ? 350f : 260f);
                if (!far) continue;
                if (awayFrom.HasValue)
                {
                    var toT = target - Pos;
                    var toA = awayFrom.Value - Pos;
                    if (toT.X * toA.X + toT.Y * toA.Y > 0) continue;
                }
                break;
            }
        }

        FlightTo = target;
        float dist = Vector2.Distance(target, Pos);
        FlightDur = escape ? Clamp(dist / 650f, 0.45f, 1.2f) : Clamp(dist / 420f, 0.7f, 2.0f);
        FlightT = 0f;
        ScareCooldown = escape ? 2.0f : 2.5f;

        Model.BlurWingL.IsHidden = false;
        Model.BlurWingR.IsHidden = false;
    }

    private void Land()
    {
        State = FlyState.Idle;
        StateTimer = Rnd(0.3f, 0.8f);
        Speed = 0f;
        Alt = 0f;
        Pitch = 0f;
        Node.Scale = new Vector3(FlyModel.FlyScale, FlyModel.FlyScale, FlyModel.FlyScale);
        var p = Node.Position; p.Z = 0f; Node.Position = p;

        for (int i = 0; i < Model.FoldedWings.Children.Count; i++)
        {
            var wing = Model.FoldedWings.Children[i];
            float side = i == 0 ? -1f : 1f;
            wing.EulerAngles = new Vector3(0, 0, side * 0.13f);
        }
        Model.BlurWingL.IsHidden = true;
        Model.BlurWingR.IsHidden = true;
    }

    private void PickNextState()
    {
        switch (State)
        {
            case FlyState.Walking:
                float r = Rnd(0f, 1f);
                if (r < 0.30f) { State = FlyState.Idle; StateTimer = Rnd(0.8f, 3f); Speed = 0f; }
                else if (r < 0.55f)
                {
                    StateTimer = Rnd(0.3f, 0.8f); Speed = Rnd(95f, 150f);
                    Heading += Rnd(-1.2f, 1.2f);
                }
                else { StateTimer = Rnd(1.5f, 5f); Speed = Rnd(18f, 45f); }
                break;

            case FlyState.Idle:
                float r2 = Rnd(0f, 1f);
                if (r2 < 0.35f) { State = FlyState.Grooming; StateTimer = Rnd(1.0f, 2.5f); }
                else
                {
                    State = FlyState.Walking; StateTimer = Rnd(1.5f, 5f); Speed = Rnd(18f, 45f);
                    Heading += Rnd(-1.5f, 1.5f);
                }
                break;

            case FlyState.Grooming:
                State = FlyState.Idle; StateTimer = Rnd(0.3f, 1.0f);
                break;

            case FlyState.Flying:
            case FlyState.Sleeping:
                break;
        }
    }

    public void Update(float dt, Vector2 bounds, Vector2? mouse, BrainSignals? signals)
    {
        Time += dt;
        ScareCooldown = Math.Max(0f, ScareCooldown - dt);
        DartCooldown = Math.Max(0f, DartCooldown - dt);
        BackwardTimer = Math.Max(0f, BackwardTimer - dt);

        StateAge += dt;
        DartTimer = Math.Max(0f, DartTimer - dt);

        _brainLive = signals.HasValue;
        _liveArousal = signals?.Arousal ?? 0f;
        _liveWing = signals?.WingDrive ?? 0f;

        if (State == FlyState.Flying)
        {
            UpdateFlight(dt);
        }
        else if (signals.HasValue)
        {
            BrainBehavior(signals.Value, dt, bounds, mouse);
            if (State == FlyState.Walking) UpdateWalk(dt, bounds);
        }
        else
        {
            if (ScareCooldown == 0f && mouse.HasValue)
            {
                float mouseDist = Vector2.Distance(mouse.Value, Pos);
                if (mouseDist < ScareRadius)
                {
                    StartFlight(bounds, awayFrom: mouse.Value);
                }
                else if (mouseDist < NervousRadius && State != FlyState.Walking)
                {
                    SetState(FlyState.Walking);
                    Heading = MathF.Atan2(Pos.Y - mouse.Value.Y, Pos.X - mouse.Value.X) + Rnd(-0.4f, 0.4f);
                    Speed = Rnd(110f, 150f);
                    StateTimer = Rnd(0.4f, 0.9f);
                    ScareCooldown = 1.0f;
                }
            }
            if (State != FlyState.Flying)
            {
                StateTimer -= dt;
                if (StateTimer <= 0f)
                {
                    if (State == FlyState.Walking && Rnd(0f, 1f) < 0.10f) StartFlight(bounds);
                    else PickNextState();
                }
                if (State == FlyState.Walking) UpdateWalk(dt, bounds);
            }
        }

        UpdateLegs(dt);
        UpdateWings(dt);

        float breathe = State == FlyState.Sleeping
            ? (1f + 0.05f * MathF.Sin(Time * 1.1f))
            : (1f + 0.03f * MathF.Sin(Time * 3.0f));
        Model.Abdomen.Scale = new Vector3(0.9f, 1.5f, 0.75f * breathe);
        SyncNode();
    }

    private void SetState(FlyState s)
    {
        if (s == State) return;
        State = s;
        StateAge = 0f;
    }

    private void BrainBehavior(BrainSignals s, float dt, Vector2 bounds, Vector2? mouse)
    {
        if (s.Escape && ScareCooldown == 0f)
        {
            StartFlight(bounds, awayFrom: mouse, escape: true);
            return;
        }

        if (s.Sleep)
        {
            if (State != FlyState.Sleeping) { SetState(FlyState.Sleeping); Speed = 0f; DartTimer = 0f; BackwardTimer = 0f; }
            return;
        }
        else if (State == FlyState.Sleeping)
        {
            SetState(FlyState.Grooming);
            return;
        }

        if (s.Nervous > 0.40f && DartCooldown == 0f)
        {
            CurrentLedge = null;
            SetState(FlyState.Walking);
            if (mouse.HasValue)
            {
                Heading = MathF.Atan2(Pos.Y - mouse.Value.Y, Pos.X - mouse.Value.X) + Rnd(-0.4f, 0.4f);
            }
            else
            {
                Heading += Rnd(-1.5f, 1.5f);
            }
            Speed = Rnd(110f, 155f);
            DartTimer = Rnd(0.4f, 0.9f);
            DartCooldown = 1.2f;
        }

        if (State != FlyState.Walking || DartTimer == 0f)
        {
            if (State != FlyState.Grooming && s.GroomDrive > 0.5f && s.Nervous < 0.3f && StateAge > 0.4f)
            {
                SetState(FlyState.Grooming);
            }
            else if (State == FlyState.Grooming && s.GroomDrive < 0.3f && StateAge > 0.6f)
            {
                SetState(FlyState.Idle);
            }
        }

        if (State == FlyState.Idle && s.WalkDrive > 0.22f && StateAge > 0.4f)
        {
            SetState(FlyState.Walking);
            Heading += Rnd(-0.8f, 0.8f);
        }
        else if (State == FlyState.Walking && DartTimer == 0f && s.WalkDrive < 0.08f && StateAge > 0.5f)
        {
            SetState(FlyState.Idle);
            Speed = 0f;
        }

        if (s.Backward && BackwardTimer == 0f && DartTimer == 0f)
        {
            if (State != FlyState.Walking) { SetState(FlyState.Walking); Speed = 0f; }
            BackwardTimer = 0.5f;
        }

        if (State == FlyState.Walking)
        {
            if (DartTimer == 0f && BackwardTimer == 0f)
            {
                float target = (14f + s.WalkDrive * 55f) * s.Tempo;
                Speed += (target - Speed) * Math.Min(1f, 3f * dt);
            }
            if (CurrentLedge == null) Heading += s.TurnBias * dt;
        }

        float flightChance = s.Arousal > 0.5f ? 0.6f : 0.005f;
        if (State == FlyState.Walking && Rnd(0f, 1f) < flightChance * dt)
        {
            StartFlight(bounds, effort: 0.35f + s.Arousal * 0.6f);
        }
    }

    private float EffectiveSpeed => BackwardTimer > 0f ? -22f : Speed;

    private void UpdateWalk(float dt, Vector2 bounds)
    {
        if (CurrentLedge.HasValue)
        {
            var l = CurrentLedge.Value;
            var cur = Terrain.FirstOrDefault(t => t.Id == l.Id);
            if (cur.Id != 0 && Math.Abs(cur.Y - l.Y) < 40f)
            {
                CurrentLedge = cur;
            }
            else
            {
                CurrentLedge = null;
                StartFlight(bounds);
                return;
            }
        }

        if (CurrentLedge.HasValue)
        {
            var l = CurrentLedge.Value;
            Heading += Rnd(-1f, 1f) * 0.2f * dt;
            float along = MathF.Cos(Heading) >= 0 ? 0 : MathF.PI;
            Heading += AngleDiff(Heading, along) * Math.Min(1f, 6f * dt);
            Pos = new Vector2(
                Clamp(Pos.X + MathF.Cos(Heading) * EffectiveSpeed * dt, l.X0, l.X1),
                Pos.Y + (l.Y - Pos.Y) * Math.Min(1f, 10f * dt)
            );
            if (Pos.X <= l.X0 + 6f && MathF.Cos(Heading) < 0) Heading = 0f;
            if (Pos.X >= l.X1 - 6f && MathF.Cos(Heading) > 0) Heading = MathF.PI;
            if (Rnd(0f, 1f) < 0.05f * dt) CurrentLedge = null;
        }
        else
        {
            Heading += Rnd(-1f, 1f) * 1.6f * dt;
            float hw = bounds.X / 2f - EdgeMargin;
            float hh = bounds.Y / 2f - EdgeMargin;
            if (Math.Abs(Pos.X) > hw || Math.Abs(Pos.Y) > hh)
            {
                float toCenter = MathF.Atan2(-Pos.Y, -Pos.X);
                Heading += AngleDiff(Heading, toCenter) * Math.Min(1f, 4f * dt);
            }
            float v = EffectiveSpeed;
            Pos = new Vector2(
                Clamp(Pos.X + MathF.Cos(Heading) * v * dt, -bounds.X / 2f + 20f, bounds.X / 2f - 20f),
                Clamp(Pos.Y + MathF.Sin(Heading) * v * dt, -bounds.Y / 2f + 20f, bounds.Y / 2f - 20f)
            );

            foreach (var l in Terrain)
            {
                if (Pos.X > l.X0 - 8f && Pos.X < l.X1 + 8f && Math.Abs(Pos.Y - l.Y) < 20f)
                {
                    if (Rnd(0f, 1f) < 0.9f * dt)
                    {
                        CurrentLedge = l;
                        Heading = MathF.Cos(Heading) >= 0 ? 0 : MathF.PI;
                        break;
                    }
                }
            }
        }

        var p = Node.Position;
        p.Z = 0.35f * Math.Abs(MathF.Sin(GaitPhase * MathF.PI * 2f));
        Node.Position = p;
    }

    private void ApplyAltitude()
    {
        float s = FlyModel.FlyScale * (1f + 0.8f * Alt);
        Node.Scale = new Vector3(s, s, s);
        var p = Node.Position;
        p.Z = 90f * Alt;
        Node.Position = p;
    }

    private void UpdateFlight(float dt)
    {
        FlightT = Math.Min(1f, FlightT + dt / FlightDur);
        if (FlightT >= 1f)
        {
            Pos = new Vector2(
                FlightTo.X + MathF.Sin(Time * 26f) * 1.2f,
                FlightTo.Y + MathF.Cos(Time * 22f) * 1.0f
            );
            Pitch = Clamp(Alt * 0.4f, 0f, 0.35f);
            Alt += (0f - Alt) * Math.Min(1f, 9f * dt);
            ApplyAltitude();
            if (Alt < 0.035f) { Pos = FlightTo; Land(); }
            return;
        }

        float e = Smoothstep(FlightT);
        float dx = FlightTo.X - FlightFrom.X;
        float dy = FlightTo.Y - FlightFrom.Y;
        float len = Math.Max(1f, MathF.Sqrt(dx * dx + dy * dy));
        float px = -dy / len;
        float py = dx / len;
        float wob = MathF.Sin(Time * 32f) * 4f * MathF.Sin(FlightT * MathF.PI);

        Pos = new Vector2(
            FlightFrom.X + dx * e + px * wob,
            FlightFrom.Y + dy * e + py * wob
        );
        Heading = MathF.Atan2(dy, dx) + MathF.Sin(Time * 18f) * 0.12f;

        EffortCurrent = _brainLive
            ? Clamp(Math.Max(FlightEffort, FlightEffort * 0.55f + _liveArousal * 0.25f + _liveWing * 0.6f), 0.25f, 1.3f)
            : FlightEffort;

        float riseEnv = Math.Min(FlightT / 0.25f, 1f);
        float fallEnv = Math.Min((1f - FlightT) / 0.3f, 1f);
        float target = EffortCurrent * Math.Min(riseEnv, fallEnv) * (0.85f + 0.15f * MathF.Sin(Time * 7f));
        Pitch = Clamp((target - Alt) * 2.5f, -0.45f, 0.45f);
        Alt += (target - Alt) * Math.Min(1f, 6f * dt);
        ApplyAltitude();
    }

    private void UpdateLegs(float dt)
    {
        float v = Math.Abs(EffectiveSpeed);
        bool walking = State == FlyState.Walking && v > 1f;

        if (walking)
        {
            float amp = Clamp(0.20f + v * 0.0022f, 0.20f, 0.50f);
            float stride = Math.Max(5f, 2f * amp * 13f);
            float freq = Clamp(v / stride, 3f, 11f);
            GaitPhase = (GaitPhase + freq * dt) % 1f;
            float stanceFrac = 0.6f;

            foreach (var leg in Model.Legs)
            {
                float p = (GaitPhase + leg.Phase) % 1f;
                if (p < stanceFrac)
                {
                    leg.Angle = amp * (1f - 2f * (p / stanceFrac));
                    leg.Lift = 0f;
                }
                else
                {
                    float s = (p - stanceFrac) / (1f - stanceFrac);
                    leg.Angle = -amp + 2f * amp * Smoothstep(s);
                    leg.Lift = MathF.Sin(s * MathF.PI) * 0.55f;
                }
                if (BackwardTimer > 0f) leg.Angle = -leg.Angle;
                leg.Apply();
            }
        }
        else if (State == FlyState.Grooming)
        {
            foreach (var leg in Model.Legs)
            {
                if (leg.IsFront)
                {
                    leg.Angle = 0.45f + 0.25f * MathF.Sin(Time * 20f + leg.SwingSign * 1.3f);
                    leg.Lift = 0.55f + 0.15f * MathF.Sin(Time * 22f);
                }
                else
                {
                    leg.Angle += (0f - leg.Angle) * Math.Min(1f, 8f * dt);
                    leg.Lift += (0f - leg.Lift) * Math.Min(1f, 8f * dt);
                }
                leg.Apply();
            }
        }
        else if (State == FlyState.Flying)
        {
            foreach (var leg in Model.Legs)
            {
                leg.Angle += (-0.35f - leg.Angle) * Math.Min(1f, 6f * dt);
                leg.Lift += (0.5f - leg.Lift) * Math.Min(1f, 6f * dt);
                leg.Apply();
            }
        }
        else
        {
            foreach (var leg in Model.Legs)
            {
                leg.Angle += (0f - leg.Angle) * Math.Min(1f, 10f * dt);
                leg.Lift += (0f - leg.Lift) * Math.Min(1f, 10f * dt);
                leg.Apply();
            }
        }
    }

    private void UpdateWings(float dt)
    {
        if (State != FlyState.Flying)
        {
            if (!Model.FoldedWings.IsHidden)
            {
                float raiseTarget = (State != FlyState.Sleeping && (_liveWing > 0.7f || (_brainLive && DartTimer > 0f))) ? 1f : 0f;
                WingRaise += (raiseTarget - WingRaise) * Math.Min(1f, 8f * dt);
                if (WingRaise > 0.01f)
                {
                    for (int i = 0; i < Model.FoldedWings.Children.Count; i++)
                    {
                        var wing = Model.FoldedWings.Children[i];
                        float side = i == 0 ? -1f : 1f;
                        wing.EulerAngles = new Vector3(-0.5f * WingRaise, 0f, side * (0.13f + 0.3f * WingRaise));
                    }
                }
            }
            return;
        }

        FlapPhase = (FlapPhase + dt * (14f + 10f * EffortCurrent)) % 1f;
        float stroke = MathF.Sin(FlapPhase * 2f * MathF.PI);

        for (int i = 0; i < Model.FoldedWings.Children.Count; i++)
        {
            var wing = Model.FoldedWings.Children[i];
            float side = i == 0 ? -1f : 1f;
            wing.EulerAngles = new Vector3(stroke * 0.35f, 0f, side * (0.45f + 0.35f * (0.5f + 0.5f * stroke)));
        }

        float flick = 0.10f + 0.14f * Math.Abs(stroke);
        Model.BlurWingL.Opacity = flick;
        Model.BlurWingR.Opacity = flick;
        Model.BlurWingL.EulerAngles = new Vector3(0f, 0f, 0.45f + stroke * 0.2f);
        Model.BlurWingR.EulerAngles = new Vector3(0f, 0f, -0.45f - stroke * 0.2f);
    }
}
