namespace Swarm.Server.Diagnostics;

// Pure calculations: no input/output, shared mutable state, or changes to the input array.
public static class Statistics
{
    // An empty array has no average, so return null.
    public static double? Average(double[] measurements)
    {
        ArgumentNullException.ThrowIfNull(measurements);

        if (measurements.Length == 0)
        {
            return null;
        }

        double sum = 0;
        foreach (double measurement in measurements)
        {
            sum += measurement;
        }

        return sum / measurements.Length;
    }

    // An empty array has no maximum, so return null.
    public static double? Maximum(double[] measurements)
    {
        ArgumentNullException.ThrowIfNull(measurements);

        if (measurements.Length == 0)
        {
            return null;
        }

        double maximum = measurements[0];
        for (int index = 1; index < measurements.Length; index++)
        {
            if (measurements[index] > maximum)
            {
                maximum = measurements[index];
            }
        }

        return maximum;
    }

    // Strictly above the threshold; an empty array produces zero.
    public static int CountAbove(double[] measurements, double threshold)
    {
        ArgumentNullException.ThrowIfNull(measurements);

        int count = 0;
        foreach (double measurement in measurements)
        {
            if (measurement > threshold)
            {
                count++;
            }
        }

        return count;
    }
}
