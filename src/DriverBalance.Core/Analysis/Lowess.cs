namespace DriverBalance.Core;

/// <summary>Weighted LOWESS (local linear, tricube kernel) that never throws or yields NaN on degenerate input.</summary>
public static class Lowess
{
    public static double[] Smooth(double[] x, double[] y, double[] w, double span)
    {
        int n = x.Length;
        if (n < 3) return (double[])y.Clone();

        int k = (int)(span * n);
        k = Math.Clamp(k, 2, n - 1);

        var result = new double[n];
        var dist = new double[n];
        var sorted = new double[n];
        for (int i = 0; i < n; i++)
        {
            double xi = x[i];
            for (int j = 0; j < n; j++) dist[j] = Math.Abs(x[j] - xi);
            Array.Copy(dist, sorted, n);
            Array.Sort(sorted);
            double maxDist = sorted[k];

            double sumW = 0, sumWX = 0, sumWY = 0, sumWXX = 0, sumWXY = 0;
            double allW = 0, allWY = 0;
            for (int j = 0; j < n; j++)
            {
                allW += w[j];
                allWY += w[j] * y[j];
                if (maxDist <= 0) continue;
                double d = dist[j] / maxDist;
                if (d >= 1) continue;
                double t = 1 - d * d * d;
                double wt = t * t * t * w[j];
                double dx = x[j] - xi; // centred on xi for numerical stability
                sumW += wt;
                sumWX += wt * dx;
                sumWY += wt * y[j];
                sumWXX += wt * dx * dx;
                sumWXY += wt * dx * y[j];
            }

            double value;
            if (maxDist <= 0 || sumW <= 0)
            {
                value = Mean(allW, allWY, y);
            }
            else
            {
                double denom = sumW * sumWXX - sumWX * sumWX;
                if (denom <= 1e-12 * sumW * sumWXX)
                {
                    value = sumWY / sumW; // weighted mean
                }
                else
                {
                    double slope = (sumW * sumWXY - sumWX * sumWY) / denom;
                    value = (sumWY - slope * sumWX) / sumW; // fit evaluated at xi (dx = 0)
                }
            }
            result[i] = double.IsFinite(value) ? value : y[i];
        }
        return result;
    }

    private static double Mean(double sumW, double sumWY, double[] y) =>
        sumW > 0 ? sumWY / sumW : y.Average();
}
