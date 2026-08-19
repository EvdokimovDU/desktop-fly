using DesktopFly.Core.Data;
using Xunit;

namespace DesktopFly.Tests;

public class DataLoaderTests
{
    [Fact]
    public void LoadBrainData_FindsAndLoadsFiles()
    {
        var data = DataLoader.LoadBrainData();
        Assert.NotNull(data);
        var (points, circuit) = data.Value;

        Assert.NotEmpty(points.Points);
        Assert.Equal(23210, points.Points.Length);
        Assert.Equal(9, points.Classes.Length);

        Assert.NotEmpty(circuit.Neurons);
        Assert.Equal(668, circuit.Neurons.Length);
        Assert.True(circuit.Edges.Length > 15000, $"Expected >15000 edges, got {circuit.Edges.Length}");
    }
}
