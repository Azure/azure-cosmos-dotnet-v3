//-----------------------------------------------------------------------
// <copyright file="QueryMetrics.ClientSideMetrics.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Newtonsoft.Json;

    /// <summary>
    /// Stores client side QueryMetrics.
    /// </summary>
    internal sealed class ClientSideMetrics
    {
        public static readonly ClientSideMetrics Zero = new ClientSideMetrics(0, 0, new List<FetchExecutionRange>(), new List<Tuple<string, SchedulingTimeSpan>>());

        // Constants for Partition Execution Timeline Table
        private const string StartTimeHeader = "Start Time (UTC)";
        private const string EndTimeHeader = "End Time (UTC)";
        private const string DurationHeader = "Duration (ms)";
        private const string PartitionKeyRangeIdHeader = "Partition Id";
        private const string NumberOfDocumentsHeader = "Number of Documents";
        private const string RetryCountHeader = "Retry Count";

        // Constants for Scheduling Metrics Table
        private const string PartitionIdHeader = "Partition Id";
        private const string ResponseTimeHeader = "Response Time (ms)";
        private const string RunTimeHeader = "Run Time (ms)";
        private const string WaitTimeHeader = "Wait Time (ms)";
        private const string TurnaroundTimeHeader = "Turnaround Time (ms)";
        private const string NumberOfPreemptionHeader = "Number of Preemptions";

        // Static readonly for Partition Execution Timeline Table
        private static readonly int MaxDateTimeStringLength = DateTime.MaxValue.ToUniversalTime().ToString("hh:mm:ss.ffffff").Length;
        private static readonly int StartTimeHeaderLength = Math.Max(MaxDateTimeStringLength, StartTimeHeader.Length);
        private static readonly int EndTimeHeaderLength = Math.Max(MaxDateTimeStringLength, EndTimeHeader.Length);
        private static readonly int DurationHeaderLength = Math.Max(DurationHeader.Length, TimeSpan.MaxValue.TotalMilliseconds.ToString("0.00").Length);
        private static readonly int PartitionKeyRangeIdHeaderLength = PartitionKeyRangeIdHeader.Length;
        private static readonly int NumberOfDocumentsHeaderLength = NumberOfDocumentsHeader.Length;
        private static readonly int RetryCountHeaderLength = RetryCountHeader.Length;

        private static readonly TextTable.Column[] PartitionExecutionTimelineColumns = new TextTable.Column[]
        {
            new TextTable.Column(PartitionKeyRangeIdHeader, PartitionKeyRangeIdHeaderLength),
            new TextTable.Column(StartTimeHeader, StartTimeHeaderLength),
            new TextTable.Column(EndTimeHeader, EndTimeHeaderLength),
            new TextTable.Column(DurationHeader, DurationHeaderLength),
            new TextTable.Column(NumberOfDocumentsHeader, NumberOfDocumentsHeaderLength),
            new TextTable.Column(RetryCountHeader, RetryCountHeaderLength),
        };

        private static readonly TextTable PartitionExecutionTimelineTable = new TextTable(PartitionExecutionTimelineColumns);

        // Static readonly for Scheduling Metrics Table
        private static readonly int MaxTimeSpanStringLength = Math.Max(TimeSpan.MaxValue.TotalMilliseconds.ToString("G17").Length, TurnaroundTimeHeader.Length);
        private static readonly int PartitionIdHeaderLength = PartitionIdHeader.Length;
        private static readonly int ResponseTimeHeaderLength = MaxTimeSpanStringLength;
        private static readonly int RunTimeHeaderLength = MaxTimeSpanStringLength;
        private static readonly int WaitTimeHeaderLength = MaxTimeSpanStringLength;
        private static readonly int TurnaroundTimeHeaderLength = MaxTimeSpanStringLength;
        private static readonly int NumberOfPreemptionHeaderLength = NumberOfPreemptionHeader.Length;

        private static readonly TextTable.Column[] SchedulingMetricsColumns = new TextTable.Column[]
        {
            new TextTable.Column(PartitionIdHeader, PartitionIdHeaderLength),
            new TextTable.Column(ResponseTimeHeader, ResponseTimeHeaderLength),
            new TextTable.Column(RunTimeHeader, RunTimeHeaderLength),
            new TextTable.Column(WaitTimeHeader, WaitTimeHeaderLength),
            new TextTable.Column(TurnaroundTimeHeader, TurnaroundTimeHeaderLength),
            new TextTable.Column(NumberOfPreemptionHeader, NumberOfPreemptionHeaderLength),
        };

        private static readonly TextTable SchedulingMetricsTable = new TextTable(SchedulingMetricsColumns);

        /// <summary>
        /// Initializes a new instance of the ClientSideMetrics class.
        /// </summary>
        /// <param name="retries">The number of retries required to execute the query.</param>
        /// <param name="requestCharge">The request charge incurred from executing the query.</param>
        /// <param name="fetchExecutionRanges">The fetch execution ranges from executing the query.</param>
        /// <param name="partitionSchedulingTimeSpans">The partition scheduling timespans from the query.</param>
        [JsonConstructor]
        public ClientSideMetrics(long retries, double requestCharge, IEnumerable<FetchExecutionRange> fetchExecutionRanges, IEnumerable<Tuple<string, SchedulingTimeSpan>> partitionSchedulingTimeSpans)
        {
            if (fetchExecutionRanges == null)
            {
                throw new ArgumentNullException("fetchExecutionRanges");
            }

            if (partitionSchedulingTimeSpans == null)
            {
                throw new ArgumentNullException("partitionSchedulingTimeSpans");
            }

            this.Retries = retries;
            this.RequestCharge = requestCharge;
            this.FetchExecutionRanges = fetchExecutionRanges;
            this.PartitionSchedulingTimeSpans = partitionSchedulingTimeSpans;
        }

        /// <summary>
        /// Gets number of retries in the Azure DocumentDB database service (see IRetryPolicy.cs).
        /// </summary>
        public long Retries
        {
            get;
        }

        /// <summary>
        /// Gets the request charge for this continuation of the query.
        /// </summary>
        public double RequestCharge
        {
            get;
        }

        /// <summary>
        /// Gets the Fetch Execution Ranges for this continuation of the query.
        /// </summary>
        public IEnumerable<FetchExecutionRange> FetchExecutionRanges
        {
            get;
        }

        /// <summary>
        /// Gets the Partition Scheduling TimeSpans for this query.
        /// </summary>
        public IEnumerable<Tuple<string, SchedulingTimeSpan>> PartitionSchedulingTimeSpans
        {
            get;
        }

        /// <summary>
        /// Creates a new ClientSideMetrics that is the sum of all elements in an IEnumerable.
        /// </summary>
        /// <param name="clientSideMetricsList">The IEnumerable to aggregate.</param>
        /// <returns>A new ClientSideMetrics that is the sum of all elements in an IEnumerable.</returns>
        public static ClientSideMetrics CreateFromIEnumerable(IEnumerable<ClientSideMetrics> clientSideMetricsList)
        {
            long retries = 0;
            double requestCharge = 0;
            IEnumerable<FetchExecutionRange> fetchExecutionRanges = new List<FetchExecutionRange>();
            IEnumerable<Tuple<string, SchedulingTimeSpan>> schedulingTimeSpans = new List<Tuple<string, SchedulingTimeSpan>>();

            if (clientSideMetricsList == null)
            {
                throw new ArgumentNullException("clientSideQueryMetricsList");
            }

            foreach (ClientSideMetrics clientSideQueryMetrics in clientSideMetricsList)
            {
                retries += clientSideQueryMetrics.Retries;
                requestCharge += clientSideQueryMetrics.RequestCharge;
                fetchExecutionRanges = fetchExecutionRanges.Concat(clientSideQueryMetrics.FetchExecutionRanges);
                schedulingTimeSpans = schedulingTimeSpans.Concat(clientSideQueryMetrics.PartitionSchedulingTimeSpans);
            }

            return new ClientSideMetrics(retries, requestCharge, fetchExecutionRanges, schedulingTimeSpans);
        }

        /// <summary>
        /// Gets a human readable plain text of the ClientSideMetrics (Please use monospace font).
        /// </summary>
        /// <param name="indentLevel">The indent / nesting level of the ClientSideMetrics.</param>
        /// <returns>A human readable plain text of the ClientSideMetrics.</returns>
        public string ToTextString(int indentLevel = 0)
        {
            if (indentLevel == int.MaxValue)
            {
                throw new ArgumentOutOfRangeException("indentLevel", "input must be less than Int32.MaxValue");
            }

            StringBuilder stringBuilder = new StringBuilder();
            checked
            {
                // Properties
                QueryMetricsUtils.AppendHeaderToStringBuilder(
                    stringBuilder,
                    QueryMetricsConstants.ClientSideQueryMetricsText,
                    indentLevel);
                QueryMetricsUtils.AppendCountToStringBuilder(
                    stringBuilder,
                    QueryMetricsConstants.RetriesText,
                    this.Retries,
                    indentLevel + 1);
                QueryMetricsUtils.AppendRUToStringBuilder(
                    stringBuilder,
                    QueryMetricsConstants.RequestChargeText,
                    this.RequestCharge,
                    indentLevel + 1);

                QueryMetricsUtils.AppendNewlineToStringBuilder(stringBuilder);

                // Building the table for fetch execution ranges
                QueryMetricsUtils.AppendHeaderToStringBuilder(stringBuilder, QueryMetricsConstants.FetchExecutionRangesText, 1);
                QueryMetricsUtils.AppendHeaderToStringBuilder(stringBuilder, PartitionExecutionTimelineTable.TopLine, 1);
                QueryMetricsUtils.AppendHeaderToStringBuilder(stringBuilder, PartitionExecutionTimelineTable.Header, 1);
                QueryMetricsUtils.AppendHeaderToStringBuilder(stringBuilder, PartitionExecutionTimelineTable.MiddleLine, 1);
                foreach (FetchExecutionRange fetchExecutionRange in this.FetchExecutionRanges.OrderBy(fetchExecutionRange => fetchExecutionRange.StartTime))
                {
                    QueryMetricsUtils.AppendHeaderToStringBuilder(
                        stringBuilder,
                        PartitionExecutionTimelineTable.GetRow(
                            fetchExecutionRange.PartitionId,
                            fetchExecutionRange.StartTime.ToUniversalTime().ToString("hh:mm:ss.ffffff"),
                            fetchExecutionRange.EndTime.ToUniversalTime().ToString("hh:mm:ss.ffffff"),
                            (fetchExecutionRange.EndTime - fetchExecutionRange.StartTime).TotalMilliseconds.ToString("0.00"),
                            fetchExecutionRange.NumberOfDocuments,
                            fetchExecutionRange.RetryCount),
                        1);
                }

                QueryMetricsUtils.AppendHeaderToStringBuilder(stringBuilder, PartitionExecutionTimelineTable.BottomLine, 1);

                QueryMetricsUtils.AppendNewlineToStringBuilder(stringBuilder);

                // Building the table for scheduling metrics
                QueryMetricsUtils.AppendHeaderToStringBuilder(stringBuilder, QueryMetricsConstants.SchedulingMetricsText, 1);
                QueryMetricsUtils.AppendHeaderToStringBuilder(stringBuilder, SchedulingMetricsTable.TopLine, 1);
                QueryMetricsUtils.AppendHeaderToStringBuilder(stringBuilder, SchedulingMetricsTable.Header, 1);
                QueryMetricsUtils.AppendHeaderToStringBuilder(stringBuilder, SchedulingMetricsTable.MiddleLine, 1);
                foreach (Tuple<string, SchedulingTimeSpan> partitionSchedulingTimeSpan in this.PartitionSchedulingTimeSpans.OrderBy(x => x.Item2.ResponseTime))
                {
                    string partitionId = partitionSchedulingTimeSpan.Item1;
                    SchedulingTimeSpan schedulingTimeSpan = partitionSchedulingTimeSpan.Item2;

                    QueryMetricsUtils.AppendHeaderToStringBuilder(
                        stringBuilder,
                        SchedulingMetricsTable.GetRow(
                            partitionId,
                            schedulingTimeSpan.ResponseTime.TotalMilliseconds.ToString("0.00"),
                            schedulingTimeSpan.RunTime.TotalMilliseconds.ToString("0.00"),
                            schedulingTimeSpan.WaitTime.TotalMilliseconds.ToString("0.00"),
                            schedulingTimeSpan.TurnaroundTime.TotalMilliseconds.ToString("0.00"),
                            schedulingTimeSpan.NumPreemptions),
                        1);
                }

                QueryMetricsUtils.AppendHeaderToStringBuilder(stringBuilder, SchedulingMetricsTable.BottomLine, 1);
            }

            return stringBuilder.ToString();
        }
    }
}
