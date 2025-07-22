// ----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Performance.Tests.Benchmarks
{
    using System;
    using System.Collections.Generic;

    internal static class TimerUtilities
    {
#pragma warning disable IDE0044 // Add readonly modifier
        private static Random random = new Random();
#pragma warning restore IDE0044 // Add readonly modifier
        public static IReadOnlyList<int> GenerateTimeoutList(
            int count,
            int maxTimeoutValue,
            int resolution)
        {
            IReadOnlyList<int> possibleValues = TimerUtilities.GeneratePossibleValuesList(maxTimeoutValue, resolution);
            List<int> timeouts = new List<int>(count);
            for (int i = 0; i < count; i++)
            {
                timeouts.Add(possibleValues[TimerUtilities.random.Next(0, possibleValues.Count)]);
            }
            return timeouts;
        }

        private static IReadOnlyList<int> GeneratePossibleValuesList(
            int maxTimeoutValue,
            int resolution)
        {
            int possibleValuesCount = maxTimeoutValue / resolution;
            List<int> possibleValues = new List<int>(possibleValuesCount);
            for (int i = 0; i < possibleValuesCount; i++)
            {
                int value = (i + 1) * resolution;
                if (value > maxTimeoutValue)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                possibleValues.Add(value);
            }

            return possibleValues;
        }
    }
}