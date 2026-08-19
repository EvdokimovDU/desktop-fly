using System.Numerics;
using DesktopFly.Core.Models;

namespace DesktopFly.Core.Sim;

public class LIFSim
{
    public int N { get; }
    public string[] Roles { get; }
    public string[] Types { get; }
    public Vector3[] Positions { get; }

    // LIF state
    private readonly float[] _v;
    private readonly float[] _refr;
    private readonly float[] _baseline;

    // CSR adjacency, weights pre-scaled
    private readonly int[] _rowStart;
    private readonly int[] _colIdx;
    private readonly float[] _w;

    // groups
    public List<int> LoomLeft { get; } = new();
    public List<int> LoomRight { get; } = new();
    public List<int> GF { get; } = new();
    public List<int> DNaL { get; } = new();      // DNa01 + DNa02, left
    public List<int> DNaR { get; } = new();      // DNa01 + DNa02, right
    public List<int> MDN { get; } = new();
    public List<int> Fwd { get; } = new();       // DNp09
    public List<int> Groom { get; } = new();     // DNg11
    public List<int> EscW { get; } = new();      // DNp02/04/11 escape-maneuver (wing) DNs
    public List<int> Ascend { get; } = new();    // ascending partners (leg proprioception)
    public List<int> Sens { get; } = new();      // sensory partners (air-puff pathway)
    private readonly float[] _ascendPhase;       // per-ascending-neuron gait phase offset

    // inputs (0..1), set each frame by coordinator
    public float LoomL { get; set; } = 0f;
    public float LoomR { get; set; } = 0f;
    public float GaitDrive { get; set; } = 0f;
    public float GaitPhase { get; set; } = 0f;
    public float AirPuff { get; set; } = 0f;
    public float ActivityScale { get; set; } = 1f;
    public float SensoryGate { get; set; } = 1f;

    // outputs
    public float RateLoom { get; private set; } = 0f;
    public float RateDNaL { get; private set; } = 0f;
    public float RateDNaR { get; private set; } = 0f;
    public float RateMDN { get; private set; } = 0f;
    public float RateFwd { get; private set; } = 0f;
    public float RateGroom { get; private set; } = 0f;
    public float RateEscW { get; private set; } = 0f;
    public float RatePop { get; private set; } = 0f;
    private bool _gfLatch = false;
    public int SimMs { get; private set; } = 0;
    public int TotalSpikes { get; private set; } = 0;

    // Delayed inhibition
    private const int InhDelayMs = 4;
    private readonly float[][] _inhQueue;
    private int _qHead = 0;

    // params
    private const float Decay = 0.9512f;          // exp(-1/20): 20 ms membrane tau, 1 ms step
    private const float Threshold = 1.0f;
    private const float RefractoryMs = 2.0f;
    private const float WeightScale = 0.0008f;
    private const float PNoise = 0.0022f;
    private const float NoiseKick = 0.42f;
    private const float LoomGain = 0.30f;
    private const float RateAlpha = 1.0f / 120.0f;
    private int _burstUntil = 0;
    private int _burstNext = 12_000;

    public SpikeBus? SpikeBus { get; }
    private readonly Random _rng;

    private record struct Stim(int[] Idx, float Strength, int DurationMs, int UntilMs);
    private readonly List<Stim> _pendingStims = new();
    private readonly List<Stim> _activeStims = new();
    private readonly object _stimLock = new();

    public void Stimulate(IReadOnlyList<int> indices, float strength, int durationMs)
    {
        if (indices.Count == 0) return;
        lock (_stimLock)
        {
            _pendingStims.Add(new Stim(indices.ToArray(), strength, durationMs, 0));
            if (_pendingStims.Count > 8) _pendingStims.RemoveAt(0);
        }
    }

    public LIFSim(CircuitFile circuit, SpikeBus? spikeBus = null, int? seed = null)
    {
        _rng = seed.HasValue ? new Random(seed.Value) : new Random();
        SpikeBus = spikeBus;
        N = circuit.Neurons.Length;
        Roles = new string[N];
        Types = new string[N];
        Positions = new Vector3[N];

        _v = new float[N];
        _refr = new float[N];
        _inhQueue = new float[5][];
        for (int i = 0; i < 5; i++) _inhQueue[i] = new float[N];

        for (int i = 0; i < N; i++)
        {
            var nr = circuit.Neurons[i];
            Roles[i] = nr.Role;
            Types[i] = nr.Type;
            Positions[i] = new Vector3(
                nr.Pos.Length > 0 ? nr.Pos[0] : 0,
                nr.Pos.Length > 1 ? nr.Pos[1] : 0,
                nr.Pos.Length > 2 ? nr.Pos[2] : 0
            );

            switch (nr.Role)
            {
                case "lc4" or "lplc2":
                    if (nr.Side == "left") LoomLeft.Add(i); else LoomRight.Add(i);
                    break;
                case "gf":
                    GF.Add(i);
                    break;
                case "dna01" or "dna02":
                    if (nr.Side == "left") DNaL.Add(i); else DNaR.Add(i);
                    break;
                case "mdn":
                    MDN.Add(i);
                    break;
                case "dnp09":
                    Fwd.Add(i);
                    break;
                case "dng11":
                    Groom.Add(i);
                    break;
                case "escw":
                    EscW.Add(i);
                    break;
                case "other":
                    if (nr.Type == "ascending") Ascend.Add(i);
                    else if (nr.Type == "sensory") Sens.Add(i);
                    break;
            }
        }

        _ascendPhase = new float[Ascend.Count];
        for (int i = 0; i < Ascend.Count; i++)
        {
            _ascendPhase[i] = (float)(_rng.NextDouble() * 2 * Math.PI);
        }

        _baseline = new float[N];
        for (int i = 0; i < N; i++)
        {
            switch (circuit.Neurons[i].Role)
            {
                case "other":
                    _baseline[i] = (float)(0.010 + _rng.NextDouble() * (0.070 - 0.010));
                    break;
                case "lc4" or "lplc2":
                    _baseline[i] = 0.004f;
                    break;
                case "dna01" or "dna02" or "mdn" or "dng11" or "escw":
                    _baseline[i] = 0.036f;
                    break;
                case "dnp09":
                    _baseline[i] = 0.038f;
                    break;
                default:
                    _baseline[i] = 0.002f;
                    break;
            }
        }

        // CSR
        var counts = new int[N];
        foreach (var e in circuit.Edges) counts[(int)e[0]]++;

        _rowStart = new int[N + 1];
        for (int i = 0; i < N; i++) _rowStart[i + 1] = _rowStart[i] + counts[i];

        _colIdx = new int[circuit.Edges.Length];
        _w = new float[circuit.Edges.Length];

        const float gapJunctionBoost = 6.0f;
        var fill = (int[])_rowStart.Clone();
        foreach (var e in circuit.Edges)
        {
            int pre = (int)e[0];
            int post = (int)e[1];
            float weight = e[2] * WeightScale;
            bool electrical = Roles[pre] == "lc4" || Roles[pre] == "lplc2"
                || (Roles[pre] == "other" && Types[pre] == "sensory");
            if (electrical && Roles[post] == "gf")
            {
                weight *= gapJunctionBoost;
            }
            int idx = fill[pre];
            _colIdx[idx] = post;
            _w[idx] = weight;
            fill[pre]++;
        }
    }

    public bool ConsumeGF()
    {
        bool s = _gfLatch;
        _gfLatch = false;
        return s;
    }

    public void Step(int ms)
    {
        if (ms <= 0) return;

        lock (_stimLock)
        {
            foreach (var p in _pendingStims)
            {
                _activeStims.Add(p with { UntilMs = SimMs + p.DurationMs });
            }
            _pendingStims.Clear();
        }
        _activeStims.RemoveAll(s => SimMs >= s.UntilMs);

        var spikedNow = SpikeBus != null ? new List<(int Neuron, bool IsGF)>() : null;

        for (int step = 0; step < ms; step++)
        {
            SimMs++;
            if (SimMs >= _burstNext)
            {
                _burstUntil = SimMs + 400;
                _burstNext = SimMs + _rng.Next(15_000, 40_001);
            }
            float p = (SimMs < _burstUntil ? PNoise * 6 : PNoise) * ActivityScale;

            for (int i = 0; i < N; i++)
            {
                if (_refr[i] > 0)
                {
                    _refr[i] -= 1;
                    _v[i] *= Decay;
                    continue;
                }
                float vi = _v[i] * Decay + _baseline[i] * ActivityScale;
                if ((float)_rng.NextDouble() < p) vi += NoiseKick;
                _v[i] = vi;
            }

            if (LoomL > 0.001f)
            {
                float add = LoomL * LoomGain * SensoryGate;
                foreach (int i in LoomLeft) _v[i] += add;
            }
            if (LoomR > 0.001f)
            {
                float add = LoomR * LoomGain * SensoryGate;
                foreach (int i in LoomRight) _v[i] += add;
            }

            if (GaitDrive > 0.001f)
            {
                float ph = (float)(GaitPhase * 2 * Math.PI);
                for (int k = 0; k < Ascend.Count; k++)
                {
                    int i = Ascend[k];
                    _v[i] += (float)(GaitDrive * 0.09f * (0.5f + 0.5f * Math.Sin(ph + _ascendPhase[k])));
                }
            }

            if (AirPuff > 0.001f)
            {
                float add = AirPuff * 0.12f * SensoryGate;
                foreach (int i in Sens) _v[i] += add;
            }

            foreach (var s in _activeStims)
            {
                if (SimMs < s.UntilMs)
                {
                    foreach (int i in s.Idx) _v[i] += s.Strength;
                }
            }

            // deliver delayed inhibition
            var curInh = _inhQueue[_qHead];
            for (int j = 0; j < N; j++)
            {
                if (curInh[j] != 0)
                {
                    _v[j] = Math.Max(-2f, _v[j] + curInh[j]);
                    curInh[j] = 0;
                }
            }

            // find spiked neurons
            var spiked = new List<int>();
            for (int i = 0; i < N; i++)
            {
                if (_refr[i] <= 0 && _v[i] >= Threshold)
                {
                    _v[i] = 0;
                    _refr[i] = RefractoryMs;
                    spiked.Add(i);
                }
            }
            TotalSpikes += spiked.Count;

            int inhSlot = (_qHead + InhDelayMs) % _inhQueue.Length;
            var targetInh = _inhQueue[inhSlot];

            foreach (int i in spiked)
            {
                int end = _rowStart[i + 1];
                for (int k = _rowStart[i]; k < end; k++)
                {
                    int j = _colIdx[k];
                    float weight = _w[k];
                    if (weight >= 0)
                    {
                        _v[j] = Math.Max(-2f, _v[j] + weight);
                    }
                    else
                    {
                        targetInh[j] += weight;
                    }
                }
            }
            _qHead = (_qHead + 1) % _inhQueue.Length;

            // group rates (Hz per neuron, EMA)
            int cLoom = 0, cDL = 0, cDR = 0, cM = 0, cF = 0, cG = 0, cW = 0;
            foreach (int i in spiked)
            {
                switch (Roles[i])
                {
                    case "lc4" or "lplc2": cLoom++; break;
                    case "dna01" or "dna02":
                        if (DNaL.Contains(i)) cDL++; else cDR++;
                        break;
                    case "mdn": cM++; break;
                    case "dnp09": cF++; break;
                    case "dng11": cG++; break;
                    case "escw": cW++; break;
                    case "gf": _gfLatch = true; break;
                }
            }

            float nLoom = Math.Max(1, LoomLeft.Count + LoomRight.Count);
            RateLoom += (cLoom * 1000f / nLoom - RateLoom) * RateAlpha;
            RateDNaL += (cDL * 1000f / Math.Max(1, DNaL.Count) - RateDNaL) * RateAlpha;
            RateDNaR += (cDR * 1000f / Math.Max(1, DNaR.Count) - RateDNaR) * RateAlpha;
            RateMDN  += (cM * 1000f / Math.Max(1, MDN.Count) - RateMDN) * RateAlpha;
            RateFwd  += (cF * 1000f / Math.Max(1, Fwd.Count) - RateFwd) * RateAlpha;
            RateGroom += (cG * 1000f / Math.Max(1, Groom.Count) - RateGroom) * RateAlpha;
            RateEscW += (cW * 1000f / Math.Max(1, EscW.Count) - RateEscW) * RateAlpha;
            RatePop  += (spiked.Count * 1000f / Math.Max(1, N) - RatePop) * RateAlpha;

            if (spikedNow != null && spiked.Count > 0)
            {
                int stride = Math.Max(1, spiked.Count / 12);
                for (int i = 0; i < spiked.Count; i += stride)
                {
                    spikedNow.Add((spiked[i], Roles[spiked[i]] == "gf"));
                }
            }
        }

        if (spikedNow != null && spikedNow.Count > 0)
        {
            SpikeBus?.Push(spikedNow);
        }
    }
}
