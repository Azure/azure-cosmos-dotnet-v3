//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.Text;

    internal sealed class TransportRequestStats
    {
        private const string RequestStageCreated = "Created";
        private const string RequestStageChannelAcquisitionStarted = "ChannelAcquisitionStarted";
        private const string RequestStagePipelined = "Pipelined";
        private const string RequestStageSent = "Transit Time";
        private const string RequestStageReceived = "Received";
        private const string RequestStageCompleted = "Completed";
        private const string RequestStageFailed = "Failed";

        private readonly Stopwatch stopwatch;
        private readonly DateTime requestCreatedTime;

        // measured in TimeSpan from start time
        private TimeSpan? channelAcquisitionStartedTime;
        private TimeSpan? requestPipelinedTime;
        private TimeSpan? requestSentTime;
        private TimeSpan? requestReceivedTime;
        private TimeSpan? requestCompletedTime;
        private TimeSpan? requestFailedTime;

        // New Object created everytime in TransportClient and thus not worrying about thread safety.
        public TransportRequestStats()
        {
            this.CurrentStage = RequestStage.Created;
            this.requestCreatedTime = DateTime.UtcNow;
            this.stopwatch = Stopwatch.StartNew();
        }

        public RequestStage CurrentStage { get; private set; }

        public long? RequestSizeInBytes { get; set; }
        public long? RequestBodySizeInBytes { get; set; }
        public long? ResponseMetadataSizeInBytes { get; set; }
        public long? ResponseBodySizeInBytes { get; set; }

        public int? NumberOfInflightRequestsToEndpoint { get; set; }
        public int? NumberOfOpenConnectionsToEndpoint { get; set; } // after this request is assigned to a connection

        // Connection Stats (the connection the request is assigned to)
        public bool? RequestWaitingForConnectionInitialization { get; set; }
        public int? NumberOfInflightRequestsInConnection { get; set; }
        public DateTime? ConnectionLastSendAttemptTime { get; set; }
        public DateTime? ConnectionLastSendTime { get; set; }
        public DateTime? ConnectionLastReceiveTime { get; set; }

        public void RecordState(RequestStage requestStage)
        {
            TimeSpan elapsedTime = this.stopwatch.Elapsed;
            switch (requestStage)
            {
                case RequestStage.ChannelAcquisitionStarted:
                    Debug.Assert(this.CurrentStage == RequestStage.Created,
                        $"Expected Transition from CREATED to CHANNEL_ACQUISITION_STARTED and not from {this.CurrentStage}");
                    this.channelAcquisitionStartedTime = elapsedTime;
                    this.CurrentStage = RequestStage.ChannelAcquisitionStarted;
                    break;
                case RequestStage.Pipelined:
                    Debug.Assert(this.CurrentStage == RequestStage.ChannelAcquisitionStarted,
                        $"Expected Transition from CHANNELACQUISITIONSTARTED to PIPELINED and not from {this.CurrentStage}");
                    this.requestPipelinedTime = elapsedTime;
                    this.CurrentStage = RequestStage.Pipelined;
                    break;
                case RequestStage.Sent:
                    this.requestSentTime = elapsedTime;
                    this.CurrentStage = RequestStage.Sent;
                    break;
                case RequestStage.Received:
                    this.requestReceivedTime = elapsedTime;
                    this.CurrentStage = RequestStage.Received;
                    break;
                case RequestStage.Completed:
                    this.requestCompletedTime = elapsedTime;
                    this.CurrentStage = RequestStage.Completed;
                    break;
                case RequestStage.Failed:
                    this.requestFailedTime = elapsedTime;
                    this.CurrentStage = RequestStage.Failed;
                    break;
                default:
                    throw new InvalidOperationException($"No transition to {requestStage}");
            }
        }

        public override string ToString()
        {
            StringBuilder stringBuilder = new StringBuilder();
            this.AppendJsonString(stringBuilder);
            return stringBuilder.ToString();
        }

        public void AppendJsonString(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"requestTimeline\":[");
            TransportRequestStats.AppendRequestStats(
                stringBuilder,
                TransportRequestStats.RequestStageCreated,
                this.requestCreatedTime,
                TimeSpan.Zero,
                this.channelAcquisitionStartedTime,
                this.requestFailedTime);

            if (this.channelAcquisitionStartedTime.HasValue)
            {
                stringBuilder.Append(",");
                TransportRequestStats.AppendRequestStats(
                    stringBuilder,
                    TransportRequestStats.RequestStageChannelAcquisitionStarted,
                    this.requestCreatedTime,
                    this.channelAcquisitionStartedTime.Value,
                    this.requestPipelinedTime,
                    this.requestFailedTime);
            }

            if (this.requestPipelinedTime.HasValue)
            {
                stringBuilder.Append(",");
                TransportRequestStats.AppendRequestStats(
                    stringBuilder,
                    TransportRequestStats.RequestStagePipelined,
                    this.requestCreatedTime,
                    this.requestPipelinedTime.Value,
                    this.requestSentTime,
                    this.requestFailedTime);
            }

            if (this.requestSentTime.HasValue)
            {
                stringBuilder.Append(",");
                TransportRequestStats.AppendRequestStats(
                    stringBuilder,
                    TransportRequestStats.RequestStageSent,
                    this.requestCreatedTime,
                    this.requestSentTime.Value,
                    this.requestReceivedTime,
                    this.requestFailedTime);
            }

            if (this.requestReceivedTime.HasValue)
            {
                stringBuilder.Append(",");
                TransportRequestStats.AppendRequestStats(
                    stringBuilder,
                    TransportRequestStats.RequestStageReceived,
                    this.requestCreatedTime,
                    this.requestReceivedTime.Value,
                    this.requestCompletedTime,
                    this.requestFailedTime);
            }

            if (this.requestCompletedTime.HasValue)
            {
                stringBuilder.Append(",");
                TransportRequestStats.AppendRequestStats(
                    stringBuilder,
                    TransportRequestStats.RequestStageCompleted,
                    this.requestCreatedTime,
                    this.requestCompletedTime.Value,
                    this.requestCompletedTime,
                    this.requestFailedTime);
            }

            if (this.requestFailedTime.HasValue)
            {
                stringBuilder.Append(",");
                TransportRequestStats.AppendRequestStats(
                    stringBuilder,
                    TransportRequestStats.RequestStageFailed,
                    this.requestCreatedTime,
                    this.requestFailedTime.Value,
                    this.requestFailedTime,
                    this.requestFailedTime);
            }

            stringBuilder.Append("]");

            if (this.RequestSizeInBytes.HasValue)
            {
                stringBuilder.Append(",\"requestSizeInBytes\":");
                stringBuilder.Append(this.RequestSizeInBytes.Value.ToString(CultureInfo.InvariantCulture));
            }

            if (this.RequestBodySizeInBytes.HasValue)
            {
                stringBuilder.Append(",\"requestBodySizeInBytes\":");
                stringBuilder.Append(this.RequestBodySizeInBytes.Value.ToString(CultureInfo.InvariantCulture));
            }

            if (this.ResponseMetadataSizeInBytes.HasValue)
            {
                stringBuilder.Append(",\"responseMetadataSizeInBytes\":");
                stringBuilder.Append(this.ResponseMetadataSizeInBytes.Value.ToString(CultureInfo.InvariantCulture));
            }

            if (this.ResponseBodySizeInBytes.HasValue)
            {
                stringBuilder.Append(",\"responseBodySizeInBytes\":");
                stringBuilder.Append(this.ResponseBodySizeInBytes.Value.ToString(CultureInfo.InvariantCulture));
            }

            this.AppendServiceEndpointStats(stringBuilder);
            this.AppendConnectionStats(stringBuilder);

            stringBuilder.Append("}");
        }

        private void AppendServiceEndpointStats(StringBuilder stringBuilder)
        {
            stringBuilder.Append($",\"serviceEndpointStats\":");
            stringBuilder.Append("{");
            if (this.NumberOfInflightRequestsToEndpoint.HasValue)
            {
                stringBuilder.Append($"\"inflightRequests\":");
                stringBuilder.Append(this.NumberOfInflightRequestsToEndpoint.Value.ToString(CultureInfo.InvariantCulture));
            }
            if (this.NumberOfOpenConnectionsToEndpoint.HasValue)
            {
                stringBuilder.Append($", \"openConnections\":");
                stringBuilder.Append(this.NumberOfOpenConnectionsToEndpoint.Value.ToString(CultureInfo.InvariantCulture));
            }
            stringBuilder.Append("}");
        }

        private void AppendConnectionStats(StringBuilder stringBuilder)
        {
            stringBuilder.Append($",\"connectionStats\":");
            stringBuilder.Append("{");
            if (this.RequestWaitingForConnectionInitialization.HasValue)
            {
                stringBuilder.Append("\"waitforConnectionInit\":\"");
                stringBuilder.Append(this.RequestWaitingForConnectionInitialization.Value.ToString());
                stringBuilder.Append("\"");
            }
            if (this.NumberOfInflightRequestsInConnection.HasValue)
            {
                stringBuilder.Append(",\"callsPendingReceive\":");
                stringBuilder.Append(this.NumberOfInflightRequestsInConnection.Value.ToString(CultureInfo.InvariantCulture));
            }
            if (this.ConnectionLastSendAttemptTime.HasValue)
            {
                stringBuilder.Append(",\"lastSendAttempt\":\"");
                stringBuilder.Append(this.ConnectionLastSendAttemptTime.Value.ToString("o", CultureInfo.InvariantCulture));
                stringBuilder.Append("\"");
            }
            if (this.ConnectionLastSendTime.HasValue)
            {
                stringBuilder.Append(",\"lastSend\":\"");
                stringBuilder.Append(this.ConnectionLastSendTime.Value.ToString("o", CultureInfo.InvariantCulture));
                stringBuilder.Append("\"");
            }
            if (this.ConnectionLastReceiveTime.HasValue)
            {
                stringBuilder.Append(",\"lastReceive\":\"");
                stringBuilder.Append(this.ConnectionLastReceiveTime.Value.ToString("o", CultureInfo.InvariantCulture));
                stringBuilder.Append("\"");
            }

            stringBuilder.Append("}");
        }

        private static void AppendRequestStats(
            StringBuilder stringBuilder,
            string eventName,
            DateTime requestStartTime,
            TimeSpan startTime,
            TimeSpan? endTime,
            TimeSpan? failedTime)
        {
            stringBuilder.Append("{\"event\": \"");
            stringBuilder.Append(eventName);
            stringBuilder.Append("\", \"startTimeUtc\": \"");
            stringBuilder.Append((requestStartTime + startTime).ToString("o", CultureInfo.InvariantCulture));
            stringBuilder.Append("\", \"durationInMs\": ");
            TimeSpan? duration = endTime ?? failedTime;
            if (duration.HasValue)
            {
                stringBuilder.Append((duration.Value - startTime).TotalMilliseconds.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                stringBuilder.Append("\"Not Set\"");
            }

            stringBuilder.Append("}");
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
