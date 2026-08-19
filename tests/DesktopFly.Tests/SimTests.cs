using DesktopFly.Core.Data;
using DesktopFly.Core.Sim;
using Xunit;
using Xunit.Abstractions;

namespace DesktopFly.Tests;

public class SimTests
{
    private readonly ITestOutputHelper _output;

    public SimTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void RunSimTest_AllInvariantsHold()
    {
        var data = DataLoader.LoadBrainData();
        Assert.NotNull(data);
        var circuit = data.Value.Circuit;

        var sim = new LIFSim(circuit, null);
        _output.WriteLine($"circuit: {sim.N} neurons | loom L/R: {sim.LoomLeft.Count}/{sim.LoomRight.Count}"
              + $" | GF: {sim.GF.Count} | DNa L/R: {sim.DNaL.Count}/{sim.DNaR.Count} | MDN: {sim.MDN.Count}"
              + $" | DNp09: {sim.Fwd.Count} | DNg11: {sim.Groom.Count} | escW: {sim.EscW.Count}"
              + $" | ascend: {sim.Ascend.Count} | sens: {sim.Sens.Count}");

        // Phase 1: 4 s spontaneous activity
        int gfSpont = 0;
        for (int i = 0; i < 40; i++)
        {
            sim.Step(100);
            if (sim.ConsumeGF()) gfSpont++;
        }
        float popHz = (float)sim.TotalSpikes / 4.0f / sim.N;
        _output.WriteLine($"spontaneous 4s: pop {popHz:F2} Hz/neuron, LC {sim.RateLoom:F1} Hz, DNa02 L/R {sim.RateDNaL:F1}/{sim.RateDNaR:F1} Hz, MDN {sim.RateMDN:F1} Hz, GF spikes: {gfSpont}");

        // Phase 2: abrupt loom
        int gfLatencyMs = -1;
        int gfLoom = 0;
        for (int ms = 0; ms < 400; ms++)
        {
            sim.LoomL = 1.0f;
            sim.LoomR = 0.5f;
            sim.Step(1);
            if (sim.ConsumeGF())
            {
                gfLoom++;
                if (gfLatencyMs < 0) gfLatencyMs = ms;
            }
        }
        sim.LoomL = 0; sim.LoomR = 0;
        _output.WriteLine($"abrupt loom 0.4s: LC rate {sim.RateLoom:F1} Hz, GF spikes {gfLoom}, first at {gfLatencyMs} ms");

        // Phase 3: 20 s with walking proprioception
        int walkOn = 0, groomOn = 0, samples = 0;
        float fwdMin = float.MaxValue, fwdMax = 0;
        for (int ms = 0; ms < 20_000; ms++)
        {
            sim.GaitDrive = 0.5f;
            sim.GaitPhase = (float)(ms % 125) / 125f; // 8 Hz gait
            sim.Step(1);
            if (ms % 10 == 0)
            {
                samples++;
                if (sim.RateFwd / 10f > 0.22f) walkOn++;
                if (sim.RateGroom / 8f > 0.5f) groomOn++;
                fwdMin = Math.Min(fwdMin, sim.RateFwd);
                fwdMax = Math.Max(fwdMax, sim.RateFwd);
            }
        }
        _output.WriteLine($"behavior 20s: walk-drive on {100f * walkOn / samples:F0}%, groom-drive on {100f * groomOn / samples:F0}%, DNp09 {fwdMin:F1}-{fwdMax:F1} Hz, pop {sim.RatePop:F1} Hz");

        // Phase 3b: midday siesta
        sim.ActivityScale = 1f - (1f - 0.55f) * 0.35f; // = 0.84
        int siestaWalkOn = 0, siestaSamples = 0;
        for (int ms = 0; ms < 15_000; ms++)
        {
            sim.Step(1);
            if (ms % 10 == 0)
            {
                siestaSamples++;
                if (sim.RateFwd / 10f > 0.22f) siestaWalkOn++;
            }
        }
        sim.ActivityScale = 1f;
        float siestaPct = 100f * siestaWalkOn / siestaSamples;
        _output.WriteLine($"siesta 15s (scale 0.84): walk-drive on {siestaPct:F0}%");

        // Phase 4: air puff 1s
        int gfPuff = 0;
        for (int i = 0; i < 1000; i++)
        {
            sim.AirPuff = 1.0f;
            sim.Step(1);
            if (sim.ConsumeGF()) gfPuff++;
        }
        sim.AirPuff = 0;
        _output.WriteLine($"air puff 1s: GF spikes {gfPuff}");

        // Phase 5: left-eye loom 1s
        for (int i = 0; i < 500; i++) { sim.Step(1); sim.ConsumeGF(); }
        float diff0 = sim.RateDNaL - sim.RateDNaR;
        for (int i = 0; i < 1000; i++)
        {
            sim.LoomL = 0.30f; sim.LoomR = 0f;
            sim.Step(1);
            sim.ConsumeGF();
        }
        float diff1 = sim.RateDNaL - sim.RateDNaR;
        sim.LoomL = 0;
        _output.WriteLine($"left-eye loom: DNa L-R rate diff {diff0:+0.0;-0.0;0.0} -> {diff1:+0.0;-0.0;0.0} Hz, LC {sim.RateLoom:F1} Hz");

        // Phase 6: click-stimulation probes
        sim.Stimulate(sim.GF, 0.5f, 40);
        sim.Step(60);
        bool gfStim = sim.ConsumeGF();
        sim.Stimulate(sim.Groom, 0.25f, 400);
        sim.Step(400);
        float groomStim = sim.RateGroom;
        sim.ConsumeGF();
        _output.WriteLine($"click probes: GF cluster -> spike {(gfStim ? "yes" : "NO")}, DNg11 cluster -> groom rate {groomStim:F0} Hz");

        Assert.Equal(0, gfSpont);
        Assert.True(gfLoom > 0, "GF must fire on abrupt loom");
        Assert.True(walkOn > 0, "Walk drive must be active during gait");
        Assert.True(gfStim, "GF stimulation must trigger spike");
        Assert.True(siestaPct > 3f, "Siesta walk drive must be >3%");
    }
}
