namespace DesktopFly.Core.Sim;

public class SpikeBus
{
    private readonly object _lock = new();
    private readonly List<(int Neuron, bool IsGF)> _events = new(256);

    public void Push(IReadOnlyList<(int Neuron, bool IsGF)> e)
    {
        if (e.Count == 0) return;
        lock (_lock)
        {
            _events.AddRange(e);
            if (_events.Count > 256)
            {
                _events.RemoveRange(0, _events.Count - 256);
            }
        }
    }

    public List<(int Neuron, bool IsGF)> PopAll()
    {
        lock (_lock)
        {
            if (_events.Count == 0) return new List<(int Neuron, bool IsGF)>();
            var result = new List<(int Neuron, bool IsGF)>(_events);
            _events.Clear();
            return result;
        }
    }
}
