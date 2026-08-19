using System.Text.Json;
using DesktopFly.Core.Models;

namespace DesktopFly.Core.Data;

public static class DataLoader
{
    public static string? FindDataDir()
    {
        var candidates = new List<string>
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data"),
            Path.Combine(Directory.GetCurrentDirectory(), "data"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "data"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "data"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "data")
        };

        foreach (var dir in candidates)
        {
            var fullPath = Path.GetFullPath(dir);
            if (Directory.Exists(fullPath) && File.Exists(Path.Combine(fullPath, "circuit.json")))
            {
                return fullPath;
            }
        }
        return null;
    }

    public static (BrainPointsFile Points, CircuitFile Circuit)? LoadBrainData(string? dataDir = null)
    {
        var dir = dataDir ?? FindDataDir();
        if (dir == null) return null;

        var pointsPath = Path.Combine(dir, "brain_points.json");
        var circuitPath = Path.Combine(dir, "circuit.json");

        if (!File.Exists(pointsPath) || !File.Exists(circuitPath))
            return null;

        try
        {
            var pointsJson = File.ReadAllText(pointsPath);
            var circuitJson = File.ReadAllText(circuitPath);

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var points = JsonSerializer.Deserialize<BrainPointsFile>(pointsJson, options);
            var circuit = JsonSerializer.Deserialize<CircuitFile>(circuitJson, options);

            if (points == null || circuit == null) return null;
            return (points, circuit);
        }
        catch
        {
            return null;
        }
    }
}
