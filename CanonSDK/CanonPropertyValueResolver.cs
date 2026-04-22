using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CanonControl.CanonSDK;

public class CanonPropertyValueResolver
{
    private static readonly Dictionary<uint, double> TvSeconds = new()
    {
        [0x10] = 30.0,
        [0x13] = 25.0,
        [0x14] = 20.0,
        [0x15] = 20.0,
        [0x18] = 15.0,
        [0x1B] = 13.0,
        [0x1C] = 10.0,
        [0x1D] = 10.0,
        [0x20] = 8.0,
        [0x23] = 6.0,
        [0x24] = 6.0,
        [0x25] = 5.0,
        [0x28] = 4.0,
        [0x2B] = 3.2,
        [0x2C] = 3.0,
        [0x2D] = 2.5,
        [0x30] = 2.0,
        [0x33] = 1.6,
        [0x34] = 1.5,
        [0x35] = 1.3,
        [0x38] = 1.0,
        [0x3B] = 0.8,
        [0x3C] = 0.7,
        [0x3D] = 0.6,
        [0x40] = 0.5,
        [0x43] = 0.4,
        [0x44] = 0.3,
        [0x45] = 0.3,
        [0x48] = 1d / 4d,
        [0x4B] = 1d / 5d,
        [0x4C] = 1d / 6d,
        [0x4D] = 1d / 6d,
        [0x50] = 1d / 8d,
        [0x53] = 1d / 10d,
        [0x54] = 1d / 10d,
        [0x55] = 1d / 13d,
        [0x58] = 1d / 15d,
        [0x5B] = 1d / 20d,
        [0x5C] = 1d / 20d,
        [0x5D] = 1d / 25d,
        [0x60] = 1d / 30d,
        [0x63] = 1d / 40d,
        [0x64] = 1d / 45d,
        [0x65] = 1d / 50d,
        [0x68] = 1d / 60d,
        [0x6B] = 1d / 80d,
        [0x6C] = 1d / 90d,
        [0x6D] = 1d / 100d,
        [0x70] = 1d / 125d,
        [0x73] = 1d / 160d,
        [0x74] = 1d / 180d,
        [0x75] = 1d / 200d,
        [0x78] = 1d / 250d,
        [0x7B] = 1d / 320d,
        [0x7C] = 1d / 350d,
        [0x7D] = 1d / 400d,
        [0x80] = 1d / 500d,
        [0x83] = 1d / 640d,
        [0x84] = 1d / 750d,
        [0x85] = 1d / 800d,
        [0x88] = 1d / 1000d,
        [0x8B] = 1d / 1250d,
        [0x8C] = 1d / 1500d,
        [0x8D] = 1d / 1600d,
        [0x90] = 1d / 2000d,
        [0x93] = 1d / 2500d,
        [0x94] = 1d / 3000d,
        [0x95] = 1d / 3200d,
        [0x98] = 1d / 4000d,
        [0x9B] = 1d / 5000d,
        [0x9C] = 1d / 6000d,
        [0x9D] = 1d / 6400d,
        [0xA0] = 1d / 8000d,
    };

    private static readonly Dictionary<uint, double> IsoValues = new()
    {
        [0x28] = 6,
        [0x30] = 12,
        [0x38] = 25,
        [0x40] = 50,
        [0x48] = 100,
        [0x4B] = 125,
        [0x4D] = 160,
        [0x50] = 200,
        [0x53] = 250,
        [0x55] = 320,
        [0x58] = 400,
        [0x5B] = 500,
        [0x5D] = 640,
        [0x60] = 800,
        [0x63] = 1000,
        [0x65] = 1250,
        [0x68] = 1600,
        [0x6B] = 2000,
        [0x6D] = 2500,
        [0x70] = 3200,
        [0x73] = 4000,
        [0x75] = 5000,
        [0x78] = 6400,
        [0x7B] = 8000,
        [0x7D] = 10000,
        [0x80] = 12800,
        [0x83] = 16000,
        [0x85] = 20000,
        [0x88] = 25600,
        [0x8B] = 32000,
        [0x8D] = 40000,
        [0x90] = 51200,
        [0x93] = 64000,
        [0x95] = 80000,
        [0x98] = 102400,
    };

    private static readonly Dictionary<uint, double> AvFStops = new()
    {
        [0x08] = 1.0,
        [0x0B] = 1.1,
        [0x0C] = 1.2,
        [0x0D] = 1.2,
        [0x10] = 1.4,
        [0x13] = 1.6,
        [0x14] = 1.8,
        [0x15] = 1.8,
        [0x18] = 2.0,
        [0x1B] = 2.2,
        [0x1C] = 2.5,
        [0x1D] = 2.5,
        [0x20] = 2.8,
        [0x23] = 3.2,
        [0x24] = 3.5,
        [0x25] = 3.5,
        [0x28] = 4.0,
        [0x2B] = 4.5,
        [0x2C] = 4.5,
        [0x2D] = 5.0,
        [0x30] = 5.6,
        [0x33] = 6.3,
        [0x34] = 6.7,
        [0x35] = 7.1,
        [0x38] = 8.0,
        [0x3B] = 9.0,
        [0x3C] = 9.5,
        [0x3D] = 10.0,
        [0x40] = 11.0,
        [0x43] = 13.0,
        [0x44] = 13.0,
        [0x45] = 14.0,
        [0x48] = 16.0,
        [0x4B] = 18.0,
        [0x4C] = 19.0,
        [0x4D] = 20.0,
        [0x50] = 22.0,
        [0x53] = 25.0,
        [0x54] = 27.0,
        [0x55] = 29.0,
        [0x58] = 32.0,
        [0x5B] = 36.0,
        [0x5C] = 38.0,
        [0x5D] = 40.0,
        [0x60] = 45.0,
        [0x63] = 51.0,
        [0x64] = 54.0,
        [0x65] = 57.0,
        [0x68] = 64.0,
        [0x6B] = 72.0,
        [0x6C] = 76.0,
        [0x6D] = 80.0,
        [0x70] = 91.0,
    };

    public static bool TryResolveShiftedValue(
        uint propertyId,
        uint baseValue,
        double stopOffset,
        uint[] availableValues,
        out uint targetValue
    )
    {
        targetValue = baseValue;

        if (availableValues.Length == 0)
        {
            return false;
        }

        if (Math.Abs(stopOffset) < double.Epsilon)
        {
            return TryResolveNearestAvailable(baseValue, availableValues, out targetValue);
        }

        return propertyId switch
        {
            EdsPropertyID.PropID_Tv => TryResolveScaledMetric(
                baseValue,
                stopOffset,
                availableValues,
                TvSeconds,
                brighterMeansHigherMetric: true,
                out targetValue
            ),
            EdsPropertyID.PropID_ISOSpeed => TryResolveScaledMetric(
                baseValue,
                stopOffset,
                availableValues,
                IsoValues,
                brighterMeansHigherMetric: true,
                out targetValue
            ),
            EdsPropertyID.PropID_Av => TryResolveAperture(
                baseValue,
                stopOffset,
                availableValues,
                out targetValue
            ),
            _ => false,
        };
    }

    private static bool TryResolveNearestAvailable(
        uint baseValue,
        uint[] availableValues,
        out uint targetValue
    )
    {
        targetValue = baseValue;
        if (Array.IndexOf(availableValues, baseValue) >= 0)
        {
            return true;
        }

        targetValue = availableValues[0];
        return true;
    }

    private static bool TryResolveScaledMetric(
        uint baseValue,
        double stopOffset,
        uint[] availableValues,
        Dictionary<uint, double> metricMap,
        bool brighterMeansHigherMetric,
        out uint targetValue
    )
    {
        targetValue = baseValue;

        if (!metricMap.TryGetValue(baseValue, out var baseMetric))
        {
            return false;
        }

        var targetMetric = baseMetric * Math.Pow(2d, stopOffset);
        return TryResolveByTargetMetric(
            targetMetric,
            stopOffset,
            availableValues,
            metricMap,
            brighterMeansHigherMetric,
            out targetValue
        );
    }

    private static bool TryResolveAperture(
        uint baseValue,
        double stopOffset,
        uint[] availableValues,
        out uint targetValue
    )
    {
        targetValue = baseValue;

        if (!AvFStops.TryGetValue(baseValue, out var baseFNumber))
        {
            return false;
        }

        // Positive stop offset means brighter image, which requires smaller f-number.
        var targetFNumber = baseFNumber / Math.Pow(2d, stopOffset / 2d);
        return TryResolveByTargetMetric(
            targetFNumber,
            stopOffset,
            availableValues,
            AvFStops,
            brighterMeansHigherMetric: false,
            out targetValue
        );
    }

    private static bool TryResolveByTargetMetric(
        double targetMetric,
        double stopOffset,
        uint[] availableValues,
        Dictionary<uint, double> metricMap,
        bool brighterMeansHigherMetric,
        out uint targetValue
    )
    {
        targetValue = availableValues[0];

        (uint Value, double Metric)? best = null;
        double bestDistance = double.MaxValue;

        var preferredDirection = Math.Sign(stopOffset);

        foreach (var value in availableValues)
        {
            if (!metricMap.TryGetValue(value, out var metric))
            {
                continue;
            }

            var distance = Math.Abs(Math.Log(metric / targetMetric, 2));
            if (distance < bestDistance)
            {
                best = (value, metric);
                bestDistance = distance;
                continue;
            }

            if (Math.Abs(distance - bestDistance) > double.Epsilon || best is null)
            {
                continue;
            }

            if (preferredDirection == 0)
            {
                continue;
            }

            var currentDelta = metric - targetMetric;
            var bestDelta = best.Value.Metric - targetMetric;

            bool currentIsBrighter = brighterMeansHigherMetric
                ? currentDelta > 0
                : currentDelta < 0;
            bool bestIsBrighter = brighterMeansHigherMetric ? bestDelta > 0 : bestDelta < 0;

            if (preferredDirection > 0)
            {
                if (currentIsBrighter && !bestIsBrighter)
                {
                    best = (value, metric);
                }
            }
            else
            {
                if (!currentIsBrighter && bestIsBrighter)
                {
                    best = (value, metric);
                }
            }
        }

        if (best is null)
        {
            return false;
        }

        targetValue = best.Value.Value;
        return true;
    }
}
