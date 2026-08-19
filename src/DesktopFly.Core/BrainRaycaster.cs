using System.Numerics;
using DesktopFly.Core.Sim;

namespace DesktopFly.Core;

public static class BrainRaycaster
{
    public static (int[] Picked, Vector3 Anchor, string RegionName)? Pick(
        Vector2 mousePixel, float viewWidth, float viewHeight, Matrix4x4 modelMatrix, LIFSim sim)
    {
        var cameraPos = new Vector3(0f, 0.6f, 29f);
        var viewMatrix = Matrix4x4.CreateLookAt(cameraPos, new Vector3(0, 0.6f, 0), Vector3.UnitY);
        var projMatrix = Matrix4x4.CreatePerspectiveFieldOfView(46f * MathF.PI / 180f, viewWidth / viewHeight, 1f, 120f);

        var viewProj = viewMatrix * projMatrix;
        if (!Matrix4x4.Invert(viewProj, out var invViewProj))
            return null;

        // Normalized Device Coordinates (NDC)
        float ndcX = (mousePixel.X / viewWidth) * 2f - 1f;
        float ndcY = 1f - (mousePixel.Y / viewHeight) * 2f; // Top-left Y to OpenGL Y

        var nearNdc = new Vector4(ndcX, ndcY, -1f, 1f);
        var farNdc = new Vector4(ndcX, ndcY, 1f, 1f);

        var nearWorld4 = Vector4.Transform(nearNdc, invViewProj);
        var farWorld4 = Vector4.Transform(farNdc, invViewProj);

        var nearWorld = new Vector3(nearWorld4.X, nearWorld4.Y, nearWorld4.Z) / nearWorld4.W;
        var farWorld = new Vector3(farWorld4.X, farWorld4.Y, farWorld4.Z) / farWorld4.W;

        if (!Matrix4x4.Invert(modelMatrix, out var invModel))
            return null;

        var a = Vector3.Transform(nearWorld, invModel);
        var b = Vector3.Transform(farWorld, invModel);
        var d = Vector3.Normalize(b - a);

        int best = -1;
        float bestPerp = float.MaxValue;

        for (int i = 0; i < sim.N; i++)
        {
            var ap = sim.Positions[i] - a;
            float proj = Vector3.Dot(ap, d);
            var perpVec = ap - proj * d;
            float perp = perpVec.Length();
            if (perp < bestPerp)
            {
                bestPerp = perp;
                best = i;
            }
        }

        if (best < 0) return null;
        var anchor = sim.Positions[best];

        var list = new List<int>();
        for (int i = 0; i < sim.N; i++)
        {
            if (Vector3.Distance(sim.Positions[i], anchor) < 2.2f)
                list.Add(i);
        }

        if (list.Count < 4)
        {
            list = Enumerable.Range(0, sim.N)
                .OrderBy(i => Vector3.Distance(sim.Positions[i], anchor))
                .Take(6)
                .ToList();
        }
        else if (list.Count > 60)
        {
            list = list
                .OrderBy(i => Vector3.Distance(sim.Positions[i], anchor))
                .Take(60)
                .ToList();
        }

        string region = GetRegionName(list, sim);
        return (list.ToArray(), anchor, region);
    }

    public static string GetRegionName(IReadOnlyList<int> picked, LIFSim sim)
    {
        var counts = new Dictionary<string, int>();
        foreach (int i in picked)
        {
            string role = sim.Roles[i];
            counts[role] = counts.GetValueOrDefault(role, 0) + 1;
        }

        string major = counts.OrderByDescending(kv => kv.Value).First().Key;
        string SideSuffix(string role)
        {
            int l = picked.Count(idx => sim.Roles[idx] == role && sim.Positions[idx].X < 0);
            int r = picked.Count(idx => sim.Roles[idx] == role) - l;
            return l == r ? "" : (l > r ? " · left" : " · right");
        }

        return major switch
        {
            "lc4" or "lplc2" => $"⚡ Looming detectors (LC4/LPLC2){SideSuffix(major)}",
            "gf" => "⚡ Giant Fiber (DNp01) — escape!",
            "dna01" or "dna02" => $"⚡ Steering neurons (DNa01/02){SideSuffix(major)}",
            "dnp09" => "⚡ Walking command (DNp09)",
            "dng11" => "⚡ Grooming command (DNg11)",
            "escw" => "⚡ Escape-wing DNs (DNp02/04/11)",
            "mdn" => "⚡ Moonwalker neurons (MDN)",
            _ => $"⚡ {(sim.Types.Length > picked[0] ? sim.Types[picked[0]] : "central")} neurons"
        };
    }
}
