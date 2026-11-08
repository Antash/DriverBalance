namespace DriverBalance.Core.Tests;

public class LowessTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Lowess_SmallN_NoThrow(int n)
    {
        var x = Enumerable.Range(0, n).Select(i => (double)i).ToArray();
        var y = Enumerable.Range(0, n).Select(i => 3.0 + i).ToArray();
        var w = Enumerable.Repeat(1.0, n).ToArray();
        var r = Lowess.Smooth(x, y, w, 0.25);
        Assert.Equal(y, r);
    }

    [Fact]
    public void Lowess_AllSameX_NoNaN()
    {
        var x = Enumerable.Repeat(5.0, 10).ToArray();
        var y = Enumerable.Range(0, 10).Select(i => (double)i).ToArray();
        var w = Enumerable.Repeat(2.0, 10).ToArray();
        var r = Lowess.Smooth(x, y, w, 0.25);
        Assert.All(r, v => Assert.Equal(4.5, v, 9));
    }

    [Fact]
    public void Lowess_LinearData_ReturnsLine()
    {
        var x = Enumerable.Range(0, 40).Select(i => 45000.0 + i * 1.7).ToArray();
        var y = x.Select(v => 0.3 * (v - 45000) + 6).ToArray();
        var w = Enumerable.Range(0, 40).Select(i => 1.0 + i % 5).ToArray();
        var r = Lowess.Smooth(x, y, w, 0.25);
        for (int i = 0; i < y.Length; i++) Assert.Equal(y[i], r[i], 6);
    }
}
