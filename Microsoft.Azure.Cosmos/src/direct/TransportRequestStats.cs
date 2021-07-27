//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Diagnostics;
    using System.Globalization;
    using System.Text;

    internal sealed class TransportRequestStats
    {
        private readonly Stopwatch stopwatch;
        private DateTime requestCreatedTime;

        // measured in TimeSpan from start time
        private TimeSpan? channelAcquisitionStartedTime;
        private TimeSpan? requestPipelinedTime;
        private TimeSpan? requestSentTime;
        private TimeSpan? requestReceivedTime;
        private TimeSpan? requestCompletedTime;
        private TimeSpan? requestFailedTime;
        private RequestStage currentStage;

        private static readonly Dictionary<RequestStage, string> RequestStageToString = new Dictionary<RequestStage, string>
        {
            { RequestStage.Created, "Created" },
            { RequestStage.ChannelAcquisitionStarted, "ChannelAcquisitionStarted" },
            { RequestStage.Pipelined, "Pipelined" },
            { RequestStage.Sent, "Transit Time" },
            { RequestStage.Received, "Received" },
            { RequestStage.Completed, "Completed" },
            { RequestStage.Failed, "Failed" },
        };

        // New Object created everytime in TransportClient and thus not worrying about thread safety.
        public TransportRequestStats()
        {
            this.currentStage = RequestStage.Created;
            this.requestCreatedTime = DateTime.UtcNow;
            this.stopwatch = Stopwatch.StartNew();
        }

        public RequestStage CurrentStage => this.currentStage;

        public long? RequestSizeInBytes { get; set; }
        public long? RequestBodySizeInBytes { get; set; }
        public long? ResponseMetadataSizeInBytes { get; set; }
        public long? ResponsetBodySizeInBytes { get; set; }

        public void RecordState(RequestStage requestStage)
        {
            TimeSpan elapsedTime = this.stopwatch.Elapsed;
            switch (requestStage)
            {
                case RequestStage.ChannelAcquisitionStarted:
                    Debug.Assert(this.currentStage == RequestStage.Created,
                        $"Expected Transition from CREATED to CHANNEL_ACQUISITION_STARTED and not from {this.currentStage}");
                    this.channelAcquisitionStartedTime = elapsedTime;
                    this.currentStage = RequestStage.ChannelAcquisitionStarted;
                    break;
                case RequestStage.Pipelined:
                    Debug.Assert(this.currentStage == RequestStage.ChannelAcquisitionStarted,
                        $"Expected Transition from CHANNELACQUISITIONSTARTED to PIPELINED and not from {this.currentStage}");
                    this.requestPipelinedTime = elapsedTime;
                    this.currentStage = RequestStage.Pipelined;
                    break;
                case RequestStage.Sent:
                    this.requestSentTime = elapsedTime;
                    this.currentStage = RequestStage.Sent;
                    break;
                case RequestStage.Received:
                    this.requestReceivedTime = elapsedTime;
                    this.currentStage = RequestStage.Received;
                    break;
                case RequestStage.Completed:
                    this.requestCompletedTime = elapsedTime;
                    this.currentStage = RequestStage.Completed;
                    break;
                case RequestStage.Failed:
                    this.requestFailedTime = elapsedTime;
                    this.currentStage = RequestStage.Failed;
                    break;
                default:
                    throw new InvalidOperationException($"No transition to {requestStage}");
            }
        }

        public IEnumerable<RequestEvent> GetRequestTimeline()
        {
            yield return new RequestEvent(TransportRequestStats.RequestStageToString[RequestStage.Created],
                                                this.requestCreatedTime,
                                                TimeSpan.Zero, 
                                                this.channelAcquisitionStartedTime,
                                                this.requestFailedTime);
            if (this.channelAcquisitionStartedTime.HasValue)
            {
                yield return new RequestEvent(TransportRequestStats.RequestStageToString[RequestStage.ChannelAcquisitionStarted],
                                                      this.requestCreatedTime,
                                                      this.channelAcquisitionStartedTime.Value,
                                                      this.requestPipelinedTime,
                                                      this.requestFailedTime);
            }

            if (this.requestPipelinedTime.HasValue)
            {
                yield return new RequestEvent(TransportRequestStats.RequestStageToString[RequestStage.Pipelined],
                                                      this.requestCreatedTime,
                                                      this.requestPipelinedTime.Value,
                                                      this.requestSentTime,
                                                      this.requestFailedTime);
            }

            if (this.requestSentTime.HasValue)
            {
                yield return new RequestEvent(TransportRequestStats.RequestStageToString[RequestStage.Sent],
                                                      this.requestCreatedTime,
                                                      this.requestSentTime.Value,
                                                      this.requestReceivedTime,
                                                      this.requestFailedTime);
            }

            if (this.requestReceivedTime.HasValue)
            {
                yield return new RequestEvent(TransportRequestStats.RequestStageToString[RequestStage.Received],
                                                      this.requestCreatedTime,
                                                      this.requestReceivedTime.Value,
                                                      this.requestCompletedTime,
                                                      this.requestFailedTime);
            }

            if (this.requestCompletedTime.HasValue)
            {
                yield return new RequestEvent(TransportRequestStats.RequestStageToString[RequestStage.Completed],
                                                      this.requestCreatedTime,
                                                      this.requestCompletedTime.Value,
                                                      this.requestCompletedTime,
                                                      this.requestFailedTime);
            }

            if (this.requestFailedTime.HasValue)
            {
                yield return new RequestEvent(TransportRequestStats.RequestStageToString[RequestStage.Failed],
                                                      this.requestCreatedTime,
                                                      this.requestFailedTime.Value,
                                                      this.requestFailedTime,
                                                      this.requestFailedTime);
            }
        }

        public readonly struct RequestEvent
        {
            public RequestEvent(string eventName,
                                DateTime requestStartTime,
                                TimeSpan startTime,
                                TimeSpan? endTime,
                                TimeSpan? failedTime)
            {
                this.EventName = eventName;
                this.StartTime = requestStartTime + startTime;
                this.DurationInMicroSec = endTime.HasValue ? Math.Max((long)((endTime.Value - startTime).TotalMilliseconds * 1000), 0) :
                                          failedTime.HasValue ? Math.Max((long)((failedTime.Value - startTime).TotalMilliseconds * 1000), 0) : -1;
            }

            public string EventName { get; }
            public DateTime StartTime { get; }
            public long DurationInMicroSec { get; }

            public override string ToString()
            {
                StringBuilder stringBuilder = new StringBuilder();
                this.AppendToBuilder(stringBuilder);
                return stringBuilder.ToString();
            }

            public void AppendToBuilder(StringBuilder stringBuilder)
            {
                stringBuilder.AppendFormat(CultureInfo.InvariantCulture,
                    "Event: {0}, StartTime: {1}, DurationMicroSecs: {2}",
                    this.EventName,
                    this.StartTime.ToString("o", CultureInfo.InvariantCulture),
                    this.DurationInMicroSec.ToString());
            }
        }

        public enum RequestStage
        {
            Created,
            ChannelAcquisitionStarted,
            Pipelined,
            Sent,
            Received,
            Completed,
            Failed
        }
    }
}
