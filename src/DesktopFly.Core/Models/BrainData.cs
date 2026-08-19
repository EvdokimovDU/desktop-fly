using System.Text.Json.Serialization;

namespace DesktopFly.Core.Models;

public class BrainPointsFile
{
    [JsonPropertyName("classes")]
    public string[] Classes { get; set; } = Array.Empty<string>();

    [JsonPropertyName("points")]
    public float[][] Points { get; set; } = Array.Empty<float[]>();
}

public class CircuitNeuronFile
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("role")]
    public string Role { get; set; } = "";

    [JsonPropertyName("side")]
    public string Side { get; set; } = "";

    [JsonPropertyName("pos")]
    public float[] Pos { get; set; } = Array.Empty<float>();
}

public class CircuitFile
{
    [JsonPropertyName("neurons")]
    public CircuitNeuronFile[] Neurons { get; set; } = Array.Empty<CircuitNeuronFile>();

    [JsonPropertyName("edges")]
    public float[][] Edges { get; set; } = Array.Empty<float[]>();
}
