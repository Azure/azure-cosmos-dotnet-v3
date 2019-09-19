//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    internal static class QueryMetricsUtils
    {
        public static Dictionary<string, double> ParseDelimitedString(string delimitedString)
        {
            if (delimitedString == null)
            {
                throw new ArgumentNullException("delimitedString");
            }

            Dictionary<string, double> metrics = new Dictionary<string, double>();

            const int Key = 0;
            const int Value = 1;
            string[] headerAttributes = delimitedString.Split(';');

            foreach (string attribute in headerAttributes)
            {
                string[] attributeKeyValue = attribute.Split('=');

                if (attributeKeyValue.Length != 2)
                {
                    throw new ArgumentException("recieved a malformed delimited string");
                }

                string attributeKey = attributeKeyValue[Key];
                double attributeValue = double.Parse(attributeKeyValue[Value], CultureInfo.InvariantCulture);
                metrics[attributeKey] = attributeValue;
            }

            return metrics;
        }

        public static TimeSpan DoubleMillisecondsToTimeSpan(double milliseconds)
        {
            return TimeSpan.FromTicks((long)(TimeSpan.TicksPerMillisecond * milliseconds));
        }

        public static TimeSpan TimeSpanFromMetrics(Dictionary<string, double> metrics, string key)
        {
            double timeSpanInMilliseconds;
            TimeSpan timeSpanFromMetrics;
            if (metrics.TryGetValue(key, out timeSpanInMilliseconds))
            {
                // Can not use TimeSpan.FromMilliseconds since double has a loss of precision
                timeSpanFromMetrics = QueryMetricsUtils.DoubleMillisecondsToTimeSpan(timeSpanInMilliseconds);
            }
            else
            {
                timeSpanFromMetrics = default(TimeSpan);
            }

            return timeSpanFromMetrics;
        }
    }
}