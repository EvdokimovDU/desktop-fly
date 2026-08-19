using System.Numerics;
using DesktopFly.Core;
using DesktopFly.Core.Behavior;
using DesktopFly.Core.Data;
using DesktopFly.Core.Model3D;
using DesktopFly.Core.Models;
using DesktopFly.Core.Platform.Win32;
using DesktopFly.Core.Sim;
using DesktopFly.Rendering;

namespace DesktopFly;

public static class Program
{
    public static void Main(string[] args)
    {
        // 1. Snapshot mode
        int snapshotIdx = Array.IndexOf(args, "--snapshot");
        if (snapshotIdx >= 0)
        {
            string path = snapshotIdx + 1 < args.Length ? args[snapshotIdx + 1] : "preview.png";
            OffscreenRenderer.RenderSnapshot(path);
            return;
        }

        // 2. Brainshot mode
        int brainshotIdx = Array.IndexOf(args, "--brainshot");
        if (brainshotIdx >= 0)
        {
            string path = brainshotIdx + 1 < args.Length ? args[brainshotIdx + 1] : "brain.png";
            OffscreenRenderer.RenderBrainshot(path);
            return;
        }

        // 3. Simtest mode
        if (args.Contains("--simtest"))
        {
            RunSimtest();
            return;
        }

        // 4. Behaviortest mode
        if (args.Contains("--behaviortest"))
        {
            RunBehaviorTest();
            return;
        }

        // 5. GUI Application mode
        RunApp();
    }

    private static void RunSimtest()
    {
        var data = DataLoader.LoadBrainData();
        if (data == null)
        {
            Console.Error.WriteLine("no data/ — run etl.py first");
            Environment.Exit(1);
        }

        var sim = new LIFSim(data.Value.Circuit, null);
        Console.WriteLine($"circuit: {sim.N} neurons | loom L/R: {sim.LoomLeft.Count}/{sim.LoomRight.Count}"
              + $" | GF: {sim.GF.Count} | DNa L/R: {sim.DNaL.Count}/{sim.DNaR.Count} | MDN: {sim.MDN.Count}"
              + $" | DNp09: {sim.Fwd.Count} | DNg11: {sim.Groom.Count} | escW: {sim.EscW.Count}"
              + $" | ascend: {sim.Ascend.Count} | sens: {sim.Sens.Count}");

        int gfSpont = 0;
        for (int i = 0; i < 40; i++) { sim.Step(100); if (sim.ConsumeGF()) gfSpont++; }
        float popHz = (float)sim.TotalSpikes / 4.0f / sim.N;
        Console.WriteLine($"spontaneous 4s: pop {popHz:F2} Hz/neuron, LC {sim.RateLoom:F1} Hz, DNa02 L/R {sim.RateDNaL:F1}/{sim.RateDNaR:F1} Hz, MDN {sim.RateMDN:F1} Hz, GF spikes: {gfSpont}");

        int gfLatencyMs = -1;
        int gfLoom = 0;
        for (int ms = 0; ms < 400; ms++)
        {
            sim.LoomL = 1.0f; sim.LoomR = 0.5f; sim.Step(1);
            if (sim.ConsumeGF()) { gfLoom++; if (gfLatencyMs < 0) gfLatencyMs = ms; }
        }
        sim.LoomL = 0; sim.LoomR = 0;
        Console.WriteLine($"abrupt loom 0.4s: LC rate {sim.RateLoom:F1} Hz, GF spikes {gfLoom}, first at {gfLatencyMs} ms");

        int walkOn = 0, groomOn = 0, samples = 0;
        float fwdMin = float.MaxValue, fwdMax = 0;
        for (int ms = 0; ms < 20_000; ms++)
        {
            sim.GaitDrive = 0.5f; sim.GaitPhase = (float)(ms % 125) / 125f;
            sim.Step(1);
            if (ms % 10 == 0)
            {
                samples++;
                if (sim.RateFwd / 10f > 0.22f) walkOn++;
                if (sim.RateGroom / 8f > 0.5f) groomOn++;
                fwdMin = Math.Min(fwdMin, sim.RateFwd); fwdMax = Math.Max(fwdMax, sim.RateFwd);
            }
        }
        Console.WriteLine($"behavior 20s: walk-drive on {100f * walkOn / samples:F0}%, groom-drive on {100f * groomOn / samples:F0}%, DNp09 {fwdMin:F1}-{fwdMax:F1} Hz, pop {sim.RatePop:F1} Hz");

        sim.ActivityScale = 1f - (1f - 0.55f) * 0.35f;
        int siestaWalkOn = 0, siestaSamples = 0;
        for (int ms = 0; ms < 15_000; ms++)
        {
            sim.Step(1);
            if (ms % 10 == 0) { siestaSamples++; if (sim.RateFwd / 10f > 0.22f) siestaWalkOn++; }
        }
        sim.ActivityScale = 1f;
        float siestaPct = 100f * siestaWalkOn / siestaSamples;
        Console.WriteLine($"siesta 15s (scale 0.84): walk-drive on {siestaPct:F0}%");

        int gfPuff = 0;
        for (int i = 0; i < 1000; i++) { sim.AirPuff = 1.0f; sim.Step(1); if (sim.ConsumeGF()) gfPuff++; }
        sim.AirPuff = 0;
        Console.WriteLine($"air puff 1s: GF spikes {gfPuff}");

        for (int i = 0; i < 500; i++) { sim.Step(1); sim.ConsumeGF(); }
        float diff0 = sim.RateDNaL - sim.RateDNaR;
        for (int i = 0; i < 1000; i++) { sim.LoomL = 0.30f; sim.LoomR = 0f; sim.Step(1); sim.ConsumeGF(); }
        float diff1 = sim.RateDNaL - sim.RateDNaR;
        sim.LoomL = 0;
        Console.WriteLine($"left-eye loom: DNa L-R rate diff {diff0:+0.0;-0.0;0.0} -> {diff1:+0.0;-0.0;0.0} Hz, LC {sim.RateLoom:F1} Hz");

        sim.Stimulate(sim.GF, 0.5f, 40); sim.Step(60); bool gfStim = sim.ConsumeGF();
        sim.Stimulate(sim.Groom, 0.25f, 400); sim.Step(400); float groomStim = sim.RateGroom; sim.ConsumeGF();
        Console.WriteLine($"click probes: GF cluster -> spike {(gfStim ? "yes" : "NO")}, DNg11 cluster -> groom rate {groomStim:F0} Hz");

        bool pass = gfSpont == 0 && gfLoom > 0 && walkOn > 0 && gfStim && siestaPct > 3f;
        Console.WriteLine(pass ? "PASS: GF silent at rest, fires on loom; locomotor drive fluctuates; stim works; siesta alive"
                               : "FAIL: tune weights/noise");
        Environment.Exit(pass ? 0 : 1);
    }

    private static void RunBehaviorTest()
    {
        var data = DataLoader.LoadBrainData();
        if (data == null)
        {
            Console.Error.WriteLine("no data/ — run etl.py first");
            Environment.Exit(1);
        }

        var bounds = new Vector2(1512f, 982f);
        const float dt = 1.0f / 60.0f;
        int failures = 0;

        void Scenario(string name, Action<LIFSim> stim, float hold, Action<Fly>? setup,
                      Func<Fly, bool> check, Func<Fly, string> describe)
        {
            var sim = new LIFSim(data.Value.Circuit, null);
            var builder = new SignalBuilder();
            var fly = new Fly(Vector2.Zero) { State = Fly.FlyState.Idle, Speed = 0f };
            setup?.Invoke(fly);

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
                if (check(fly)) { passed = true; break; }
            }
            if (!passed) failures++;
            Console.WriteLine($"{(passed ? "PASS" : "FAIL")}  {name}: {describe(fly)}");
        }

        void BodyCheck(string name, Func<(bool Ok, string Detail)> run)
        {
            var (ok, detail) = run();
            if (!ok) failures++;
            Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {name}: {detail}");
        }

        Scenario("GF stim -> escape flight", sim => sim.Stimulate(sim.GF, 0.5f, 40), 0.5f, null, f => f.State == Fly.FlyState.Flying, f => $"state={f.State}");
        Scenario("DNg11 stim -> grooming", sim => sim.Stimulate(sim.Groom, 0.25f, 600), 1.5f, null, f => f.State == Fly.FlyState.Grooming, f => $"state={f.State}");
        Scenario("DNp09 stim -> walks, speed rises (capped)", sim => sim.Stimulate(sim.Fwd, 0.25f, 1200), 1.5f, null, f => f.State == Fly.FlyState.Walking && f.Speed > 40f && f.Speed < 100f, f => $"state={f.State} speed={(int)f.Speed}");
        Scenario("MDN stim (from idle) -> backward walk", sim => sim.Stimulate(sim.MDN, 0.3f, 600), 1.2f, null, f => f.BackwardTimer > 0f, f => $"backwardTimer={f.BackwardTimer:F2}");

        float heading0 = 0f;
        Scenario("DNa-left stim -> left (CCW) turn while walking", sim => sim.Stimulate(sim.DNaL, 0.3f, 900), 1.4f, f => { f.State = Fly.FlyState.Walking; f.Speed = 30f; f.Heading = 0f; heading0 = 0f; }, f => f.Heading - heading0 > 0.25f, f => $"heading change {f.Heading - heading0:+0.00;-0.00;0.00} rad");
        Scenario("moderate loom -> fear response (dart or escape)", sim => { sim.LoomL = 0.45f; sim.LoomR = 0.45f; }, 1.0f, null, f => (f.State == Fly.FlyState.Walking && f.Speed > 100f) || f.State == Fly.FlyState.Flying, f => $"state={f.State} speed={(int)f.Speed}");
        Scenario("tap near fly -> startle escape via sensory pathway", sim => sim.Stimulate(sim.Sens, 0.45f, 150), 0.8f, null, f => f.State == Fly.FlyState.Flying, f => $"state={f.State}");

        var walkSignals = new BrainSignals { WalkDrive = 0.6f };
        BodyCheck("ledge attach + follow window edge", () =>
        {
            var fly = new Fly(new Vector2(0f, -55f)) { State = Fly.FlyState.Walking, Speed = 30f, Heading = 0f, Terrain = new List<Ledge> { new Ledge(-40f, -300f, 300f, 1) } };
            for (int i = 0; i < 240; i++)
            {
                fly.Update(dt, bounds, null, walkSignals);
                if (fly.CurrentLedge.HasValue && Math.Abs(fly.Pos.Y + 40f) < 8f) return (true, $"attached, y={(int)fly.Pos.Y}");
            }
            return (false, $"state={fly.State} y={(int)fly.Pos.Y} ledge={fly.CurrentLedge.HasValue}");
        });

        BodyCheck("window closes underfoot -> takeoff", () =>
        {
            var fly = new Fly(new Vector2(0f, -40f)) { State = Fly.FlyState.Walking, Speed = 25f, Heading = 0f };
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

        BodyCheck("thermal tempo scales walking speed", () =>
        {
            var fly = new Fly(Vector2.Zero) { State = Fly.FlyState.Walking, Speed = 20f, Heading = 0f };
            var cool = walkSignals; cool.Tempo = 1.0f;
            for (int i = 0; i < 120; i++) fly.Update(dt, bounds, null, cool);
            float coolSpeed = fly.Speed;
            var hot = walkSignals; hot.Tempo = 1.5f;
            for (int i = 0; i < 120; i++) fly.Update(dt, bounds, null, hot);
            float hotSpeed = fly.Speed;
            return (fly.State == Fly.FlyState.Walking && hotSpeed > coolSpeed + 10f, $"cool {(int)coolSpeed} -> hot {(int)hotSpeed} pt/s");
        });

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
            bool ok = esc.Alt > casual.Alt + 0.15f && esc.Scale > FlyModel.FlyScale * 1.5f && Math.Abs(esc.Scale - FlyModel.FlyScale * (1f + 0.8f * esc.Alt)) < 0.15f;
            return (ok, $"escape alt {esc.Alt:F2} scale {esc.Scale:F2} | casual alt {casual.Alt:F2} scale {casual.Scale:F2}");
        });

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
            return (fly.State == Fly.FlyState.Flying && hotEffort > calmEffort + 0.2f, $"effort {calmEffort:F2} -> {hotEffort:F2}");
        });

        BodyCheck("threat while grounded raises the wings (no takeoff)", () =>
        {
            var fly = new Fly(Vector2.Zero) { State = Fly.FlyState.Walking, Speed = 20f, DartCooldown = 99f };
            var threat = new BrainSignals { WingDrive = 0.9f, WalkDrive = 0.4f };
            for (int i = 0; i < 40; i++) fly.Update(dt, bounds, null, threat);
            float x = fly.Model.FoldedWings.Children[0].EulerAngles.X;
            return (fly.State != Fly.FlyState.Flying && fly.WingRaise > 0.6f && x < -0.2f, $"raise {fly.WingRaise:F2}, wing tilt {x:F2} rad");
        });

        BodyCheck("landing is smooth: no scale/height snap at touchdown", () =>
        {
            var fly = new Fly(Vector2.Zero) { State = Fly.FlyState.Idle };
            fly.StartFlight(bounds, escape: true);
            float prevScale = fly.Node.Scale.X, prevZ = fly.Node.Position.Z;
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
            return (landed && maxDS < 0.2f && maxDZ < 25f, $"landed={(landed ? "yes" : "NO")}, max per-frame Δscale {maxDS:F2}, Δz {maxDZ:F1}");
        });

        BodyCheck("circadian curve: siesta + night dips, dawn/dusk peaks", () =>
        {
            float night = Circadian.Activity(3.0), dawn = Circadian.Activity(9.0), siesta = Circadian.Activity(14.0), dusk = Circadian.Activity(18.0);
            bool ok = night < 0.4f && dawn > 0.9f && siesta < 0.7f && siesta > 0.3f && dusk > 0.9f;
            return (ok, $"3h {night:F2}, 9h {dawn:F2}, 14h {siesta:F2}, 18h {dusk:F2}");
        });

        Console.WriteLine(failures == 0 ? "ALL BEHAVIOR TESTS PASS" : $"{failures} FAILURES");
        Environment.Exit(failures == 0 ? 0 : 1);
    }

    private static void RunApp()
    {
        var app = new DesktopFlyApp();
        app.Run();
    }
}
