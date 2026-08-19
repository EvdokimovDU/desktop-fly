namespace DesktopFly.Core.Models;

public struct BrainSignals
{
    public bool Escape { get; set; } = false;          // giant fiber spiked -> takeoff NOW
    public float Nervous { get; set; } = 0f;          // looming-detector population rate, 0..1
    public float TurnBias { get; set; } = 0f;         // rad/s steering from DNa01/DNa02 left-right rate difference
    public bool Backward { get; set; } = false;        // MDN burst -> backward walking
    public float WalkDrive { get; set; } = 0f;        // DNp09 forward-walking command rate, ~0..1.5
    public float GroomDrive { get; set; } = 0f;       // DNg11 grooming command rate, ~0..1.5
    public float WingDrive { get; set; } = 0f;        // DNp02/04/11 escape-maneuver DN rate, ~0..1.3
    public float Arousal { get; set; } = 0f;          // whole-population activity, ~0..1
    public float Tempo { get; set; } = 1f;            // thermal "temperature" scaling of locomotion
    public bool Sleep { get; set; } = false;           // circadian + idle -> sleep-like state

    public BrainSignals() { }
}
