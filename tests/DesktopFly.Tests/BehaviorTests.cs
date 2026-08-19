using System.Numerics;
using DesktopFly.Core.Behavior;
using DesktopFly.Core.Data;
using DesktopFly.Core.Model3D;
using DesktopFly.Core.Models;
using DesktopFly.Core.Sim;
using Xunit;
using Xunit.Abstractions;

namespace DesktopFly.Tests;

public class BehaviorTests
{
    private readonly ITestOutputHelper _output;

    public BehaviorTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void RunBehaviorTest_All17ScenariosPass()
    {
        var data = DataLoader.LoadBrainData();
        Assert.NotNull(data);
        var circuit = data.Value.Circuit;

        var bounds = new Vector2(1512f, 982f);
        const float dt = 1.0f / 60.0f;
        int failures = 0;

        void Scenario(string name, Action<LIFSim> stim, float hold, Action<Fly>? setup,
                      Func<Fly, bool> check, Func<Fly, string> describe)
        {
            var sim = new LIFSim(circuit, null, seed: 42);
            var builder = new SignalBuilder();
            var fly = new Fly(Vector2.Zero)
            {
                State = Fly.FlyState.Idle,
                Speed = 0f
            };
            setup?.Invoke(fly);

            // Settle network
            sim.Step(400);
            sim.ConsumeGF();
            stim(sim);

            bool passed = false;
            int frames = (int)(hold / dt);
            while (frames > 0)
            {
                frames--;
                sim.Step((int)Math.Round(dt * 1000f));
                var s = builder.Make(sim, dt);
                fly.Update(dt, bounds, null, s);
                if (check(fly))
                {
                    passed = true;
                    break;
                }
            }
            if (!passed) failures++;
            _output.WriteLine($"{(passed ? "PASS" : "FAIL")}  {name}: {describe(fly)}");
            Assert.True(passed, $"{name} failed: {describe(fly)}");
        }

        void BodyCheck(string name, Func<(bool Ok, string Detail)> run)
        {
            var (ok, detail) = run();
            if (!ok) failures++;
            _output.WriteLine($"{(ok ? "PASS" : "FAIL")}  {name}: {detail}");
            Assert.True(ok, $"{name} failed: {detail}");
        }

        // 1. GF stim -> escape flight
        Scenario("GF stim -> escape flight",
                 stim: sim => sim.Stimulate(sim.GF, 0.5f, 40), hold: 0.5f, setup: null,
                 check: f => f.State == Fly.FlyState.Flying,
                 describe: f => $"state={f.State}");

        // 2. DNg11 stim -> grooming
        Scenario("DNg11 stim -> grooming",
                 stim: sim => sim.Stimulate(sim.Groom, 0.25f, 600), hold: 1.5f, setup: null,
                 check: f => f.State == Fly.FlyState.Grooming,
                 describe: f => $"state={f.State}");

        // 3. DNp09 stim -> walks, speed rises
        Scenario("DNp09 stim -> walks, speed rises (capped)",
                 stim: sim => sim.Stimulate(sim.Fwd, 0.25f, 1200), hold: 1.5f, setup: null,
                 check: f => f.State == Fly.FlyState.Walking && f.Speed > 40f && f.Speed < 100f,
                 describe: f => $"state={f.State} speed={(int)f.Speed}");

        // 4. MDN stim -> backward walk
        Scenario("MDN stim (from idle) -> backward walk",
                 stim: sim => sim.Stimulate(sim.MDN, 0.3f, 600), hold: 1.2f, setup: null,
                 check: f => f.BackwardTimer > 0f,
                 describe: f => $"backwardTimer={f.BackwardTimer:F2}");

        // 5. DNa-left stim -> left turn
        float heading0 = 0f;
        Scenario("DNa-left stim -> left (CCW) turn while walking",
                 stim: sim => sim.Stimulate(sim.DNaL, 0.3f, 900), hold: 1.4f,
                 setup: f => { f.State = Fly.FlyState.Walking; f.Speed = 30f; f.Heading = 0f; heading0 = 0f; },
                 check: f => f.Heading - heading0 > 0.25f,
                 describe: f => $"heading change {f.Heading - heading0:+0.00;-0.00;0.00} rad");

        // 6. Moderate loom -> fear response
        Scenario("moderate loom -> fear response (dart or escape)",
                 stim: sim => { sim.LoomL = 0.45f; sim.LoomR = 0.45f; }, hold: 1.0f, setup: null,
                 check: f => (f.State == Fly.FlyState.Walking && f.Speed > 100f) || f.State == Fly.FlyState.Flying,
                 describe: f => $"state={f.State} speed={(int)f.Speed}");

        // 7. Tap near fly -> startle escape
        Scenario("tap near fly -> startle escape via sensory pathway",
                 stim: sim => sim.Stimulate(sim.Sens, 0.45f, 150), hold: 0.8f, setup: null,
                 check: f => f.State == Fly.FlyState.Flying,
                 describe: f => $"state={f.State}");

        // Body checks
        var walkSignals = new BrainSignals { WalkDrive = 0.6f };

        // 8. Ledge attach
        BodyCheck("ledge attach + follow window edge", () =>
        {
            var fly = new Fly(new Vector2(0f, -55f))
            {
                State = Fly.FlyState.Walking,
                Speed = 30f,
                Heading = 0f,
                Terrain = new List<Ledge> { new Ledge(-40f, -300f, 300f, 1) }
            };
            for (int i = 0; i < 240; i++)
            {
                fly.Update(dt, bounds, null, walkSignals);
                if (fly.CurrentLedge.HasValue && Math.Abs(fly.Pos.Y + 40f) < 8f)
                    return (true, $"attached, y={(int)fly.Pos.Y}");
            }
            return (false, $"state={fly.State} y={(int)fly.Pos.Y} ledge={fly.CurrentLedge.HasValue}");
        });

        // 9. Window closes -> takeoff
        BodyCheck("window closes underfoot -> takeoff", () =>
        {
            var fly = new Fly(new Vector2(0f, -40f))
            {
                State = Fly.FlyState.Walking,
                Speed = 25f,
                Heading = 0f
            };
            var ledge = new Ledge(-40f, -300f, 300f, 1);
            fly.Terrain = new List<Ledge> { ledge };
            fly.CurrentLedge = ledge;
            fly.Terrain = new List<Ledge>();
            for (int i = 0; i < 60; i++)
            {
                fly.Update(dt, bounds, null, walkSignals);
                if (fly.State == Fly.FlyState.Flying) return (true, "took off");
            }
            return (false, $"state={fly.State}");
        });

        // 10. Sleep signal -> sleeping; wake -> grooming
        BodyCheck("sleep signal -> sleeping; wake -> grooming", () =>
        {
            var fly = new Fly(Vector2.Zero) { State = Fly.FlyState.Idle };
            var s = new BrainSignals { Sleep = true };
            for (int i = 0; i < 60; i++) fly.Update(dt, bounds, null, s);
            if (fly.State != Fly.FlyState.Sleeping) return (false, $"no sleep: {fly.State}");
            s.Sleep = false;
            fly.Update(dt, bounds, null, s);
            return (fly.State == Fly.FlyState.Grooming, $"woke to {fly.State}");
        });

        // 11. Thermal tempo scales walking speed
        BodyCheck("thermal tempo scales walking speed", () =>
        {
            var fly = new Fly(Vector2.Zero) { State = Fly.FlyState.Walking, Speed = 20f, Heading = 0f };
            var cool = walkSignals; cool.Tempo = 1.0f;
            for (int i = 0; i < 120; i++) fly.Update(dt, bounds, null, cool);
            float coolSpeed = fly.Speed;

            var hot = walkSignals; hot.Tempo = 1.5f;
            for (int i = 0; i < 120; i++) fly.Update(dt, bounds, null, hot);
            float hotSpeed = fly.Speed;

            return (fly.State == Fly.FlyState.Walking && hotSpeed > coolSpeed + 10f,
                    $"cool {(int)coolSpeed} -> hot {(int)hotSpeed} pt/s");
        });

        // 12. Flight: altitude drives scale
        BodyCheck("flight: altitude drives scale; escape flies higher than casual", () =>
        {
            (float Alt, float Scale) FlightTest(bool escape, float? effort)
            {
                var fly = new Fly(Vector2.Zero) { State = Fly.FlyState.Idle };
                fly.StartFlight(bounds, escape: escape, effort: effort);
                float maxAlt = 0f, maxScale = 0f;
                int frames = 0;
                while (fly.State == Fly.FlyState.Flying && frames < 400)
                {
                    frames++;
                    fly.Update(dt, bounds, null, new BrainSignals());
                    maxAlt = Math.Max(maxAlt, fly.Alt);
                    maxScale = Math.Max(maxScale, fly.Node.Scale.X);
                }
                return (maxAlt, maxScale);
            }
            var esc = FlightTest(true, null);
            var casual = FlightTest(false, 0.45f);
            bool ok = esc.Alt > casual.Alt + 0.15f && esc.Scale > FlyModel.FlyScale * 1.5f
                && Math.Abs(esc.Scale - FlyModel.FlyScale * (1f + 0.8f * esc.Alt)) < 0.15f;
            return (ok, $"escape alt {esc.Alt:F2} scale {esc.Scale:F2} | casual alt {casual.Alt:F2} scale {casual.Scale:F2}");
        });

        // 13. Wings beat
        BodyCheck("flight: wings actually beat", () =>
        {
            var fly = new Fly(Vector2.Zero) { State = Fly.FlyState.Idle };
            fly.StartFlight(bounds, effort: 0.8f);
            float lo = float.MaxValue, hi = float.MinValue;
            for (int i = 0; i < 30; i++)
            {
                if (fly.State != Fly.FlyState.Flying) break;
                fly.Update(dt, bounds, null, new BrainSignals());
                float z = fly.Model.FoldedWings.Children[0].EulerAngles.Z;
                lo = Math.Min(lo, z); hi = Math.Max(hi, z);
            }
            return (hi - lo > 0.25f, $"wing sweep {hi - lo:F2} rad over 0.5 s");
        });

        // 14. Escape-DN activity raises wing effort
        BodyCheck("escape-DN activity mid-flight raises wing-beat effort", () =>
        {
            var fly = new Fly(Vector2.Zero) { State = Fly.FlyState.Idle };
            fly.StartFlight(bounds, effort: 0.5f);
            var calm = new BrainSignals();
            for (int i = 0; i < 12; i++) fly.Update(dt, bounds, null, calm);
            float calmEffort = fly.EffortCurrent;

            var hot = new BrainSignals { WingDrive = 1.0f, Arousal = 0.6f };
            for (int i = 0; i < 12; i++)
            {
                if (fly.State != Fly.FlyState.Flying) break;
                fly.Update(dt, bounds, null, hot);
            }
            float hotEffort = fly.EffortCurrent;
            return (fly.State == Fly.FlyState.Flying && hotEffort > calmEffort + 0.2f,
                    $"effort {calmEffort:F2} -> {hotEffort:F2}");
        });

        // 15. Threat while grounded raises wings
        BodyCheck("threat while grounded raises the wings (no takeoff)", () =>
        {
            var fly = new Fly(Vector2.Zero) { State = Fly.FlyState.Walking, Speed = 20f, DartCooldown = 99f };
            var threat = new BrainSignals { WingDrive = 0.9f, WalkDrive = 0.4f };
            for (int i = 0; i < 40; i++) fly.Update(dt, bounds, null, threat);
            float x = fly.Model.FoldedWings.Children[0].EulerAngles.X;
            return (fly.State != Fly.FlyState.Flying && fly.WingRaise > 0.6f && x < -0.2f,
                    $"raise {fly.WingRaise:F2}, wing tilt {x:F2} rad");
        });

        // 16. Landing is smooth
        BodyCheck("landing is smooth: no scale/height snap at touchdown", () =>
        {
            var fly = new Fly(Vector2.Zero) { State = Fly.FlyState.Idle };
            fly.StartFlight(bounds, escape: true);
            float prevScale = fly.Node.Scale.X;
            float prevZ = fly.Node.Position.Z;
            float maxDS = 0f, maxDZ = 0f;
            int post = 20, frames = 0;
            bool landed = false;
            while (post > 0 && frames < 600)
            {
                frames++;
                fly.Update(dt, bounds, null, new BrainSignals());
                maxDS = Math.Max(maxDS, Math.Abs(fly.Node.Scale.X - prevScale));
                maxDZ = Math.Max(maxDZ, Math.Abs(fly.Node.Position.Z - prevZ));
                prevScale = fly.Node.Scale.X; prevZ = fly.Node.Position.Z;
                if (fly.State != Fly.FlyState.Flying) { landed = true; post--; }
            }
            return (landed && maxDS < 0.2f && maxDZ < 25f,
                    $"landed={(landed ? "yes" : "NO")}, max per-frame Δscale {maxDS:F2}, Δz {maxDZ:F1}");
        });

        // 17. Circadian curve
        BodyCheck("circadian curve: siesta + night dips, dawn/dusk peaks", () =>
        {
            float night = Circadian.Activity(3.0);
            float dawn = Circadian.Activity(9.0);
            float siesta = Circadian.Activity(14.0);
            float dusk = Circadian.Activity(18.0);
            bool ok = night < 0.4f && dawn > 0.9f && siesta < 0.7f && siesta > 0.3f && dusk > 0.9f;
            return (ok, $"3h {night:F2}, 9h {dawn:F2}, 14h {siesta:F2}, 18h {dusk:F2}");
        });

        Assert.Equal(0, failures);
    }
}
