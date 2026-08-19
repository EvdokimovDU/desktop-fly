using DesktopFly.Core.Models;

namespace DesktopFly.Core.Sim;

public class SignalBuilder
{
    private float _dnaBaseline = 0f;

    private static float Clamp(float v, float lo, float hi) => Math.Min(hi, Math.Max(lo, v));

    public BrainSignals Make(LIFSim sim, float dt)
    {
        float diff = sim.RateDNaL - sim.RateDNaR;
        _dnaBaseline += (diff - _dnaBaseline) * Math.Min(1f, dt / 8f);

        return new BrainSignals
        {
            Escape = sim.ConsumeGF(),
            Nervous = Clamp(sim.RateLoom / 80f, 0f, 1f),
            TurnBias = Clamp((diff - _dnaBaseline) * 0.04f, -1.0f, 1.0f),
            Backward = sim.RateMDN > 8f,
            WalkDrive = Clamp(sim.RateFwd / 10f, 0f, 1.3f),
            GroomDrive = sim.RateGroom / 8f,
            WingDrive = Clamp(sim.RateEscW / 10f, 0f, 1.3f),
            Arousal = Clamp(sim.RatePop / 20f, 0f, 1f)
        };
    }
}
