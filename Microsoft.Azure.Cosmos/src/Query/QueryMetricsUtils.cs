//-----------------------------------------------------------------------
// <copyright file="QueryMetricsUtils.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

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

        public static void AppendToStringBuilder(StringBuilder stringBuilder, string property, string value, string units, int indentLevel)
        {
            const string Indent = "  ";
            const string FormatString = "{0,-40} : {1,15} {2,-12}{3}";

            stringBuilder.Append(string.Format(
                CultureInfo.InvariantCulture,
                FormatString,
                string.Concat(Enumerable.Repeat(Indent, indentLevel)) + property,
                value,
                units,
                Environment.NewLine));
        }

        public static void AppendBytesToStringBuilder(StringBuilder stringBuilder, string property, long bytes, int indentLevel)
        {
            const string BytesFormatString = "{0:n0}";
            const string BytesUnitString = "bytes";

            QueryMetricsUtils.AppendToStringBuilder(
                stringBuilder,
                property,
                string.Format(CultureInfo.InvariantCulture, BytesFormatString, bytes),
                BytesUnitString,
                indentLevel);
        }

        public static void AppendMillisecondsToStringBuilder(StringBuilder stringBuilder, string property, double milliseconds, int indentLevel)
        {
            const string MillisecondsFormatString = "{0:n2}";
            const string MillisecondsUnitString = "milliseconds";

            QueryMetricsUtils.AppendToStringBuilder(
                stringBuilder,
                property,
                string.Format(CultureInfo.InvariantCulture, MillisecondsFormatString, milliseconds),
                MillisecondsUnitString,
                indentLevel);
        }

        public static void AppendCountToStringBuilder(StringBuilder stringBuilder, string property, long count, int indentLevel)
        {
            const string CountFormatString = "{0:n0}";
            const string CountUnitString = "";

            QueryMetricsUtils.AppendToStringBuilder(
                stringBuilder,
                property,
                string.Format(CultureInfo.InvariantCulture, CountFormatString, count),
                CountUnitString,
                indentLevel);
        }

        public static void AppendPercentageToStringBuilder(StringBuilder stringBuilder, string property, double percentage, int indentLevel)
        {
            const string PercentageFormatString = "{0:n2}";
            const string PercentageUnitString = "%";

            QueryMetricsUtils.AppendToStringBuilder(
                stringBuilder,
                property,
                string.Format(CultureInfo.InvariantCulture, PercentageFormatString, percentage * 100),
                PercentageUnitString,
                indentLevel);
        }

        public static void AppendHeaderToStringBuilder(StringBuilder stringBuilder, string headerTitle, int indentLevel)
        {
            const string Indent = "  ";
            const string FormatString = "{0}{1}";

            stringBuilder.Append(string.Format(
                CultureInfo.InvariantCulture,
                FormatString,
                string.Concat(Enumerable.Repeat(Indent, indentLevel)) + headerTitle,
                Environment.NewLine));
        }

        public static void AppendRUToStringBuilder(StringBuilder stringBuilder, string property, double requestCharge, int indentLevel)
        {
            const string RequestChargeFormatString = "{0:n2}";
            const string RequestChargeUnitString = "RUs";

            QueryMetricsUtils.AppendToStringBuilder(
                stringBuilder,
                property,
                string.Format(CultureInfo.InvariantCulture, RequestChargeFormatString, requestCharge),
                RequestChargeUnitString,
                indentLevel);
        }

        public static void AppendActivityIdsToStringBuilder(StringBuilder stringBuilder, string activityIdsLabel, IReadOnlyList<Guid> activityIds, int indentLevel)
        {
            const string Indent = "  ";
            stringBuilder.Append(activityIdsLabel);
            stringBuilder.AppendLine();
            foreach (Guid activityId in activityIds)
            {
                stringBuilder.Append(Indent);
                stringBuilder.Append(activityId);
                stringBuilder.AppendLine();
            }
        }

        public static void AppendNewlineToStringBuilder(StringBuilder stringBuilder)
        {
            QueryMetricsUtils.AppendHeaderToStringBuilder(stringBuilder, string.Empty, 0);
        }
    }
}