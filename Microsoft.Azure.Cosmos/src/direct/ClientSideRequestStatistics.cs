//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Documents.Rntbd;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Net.Http;
    using System.Text;

    internal sealed class ClientSideRequestStatistics : IClientSideRequestStatistics
    {
        private static readonly SystemUsageMonitor systemUsageMonitor;
        private static readonly SystemUsageRecorder systemRecorder;
        private static readonly TimeSpan SystemUsageRecordInterval = TimeSpan.FromSeconds(10);
        private const string EnableCpuMonitorConfig = "CosmosDbEnableCpuMonitor";
        private const int MaxSupplementalRequestsForToString = 10;
        private static bool enableCpuMonitorFlag;

        private DateTime requestStartTime;
        private DateTime? requestEndTime;

        private object lockObject = new object();
        private object requestEndTimeLock = new object();

        private List<StoreResponseStatistics> responseStatisticsList;
        private List<StoreResponseStatistics> supplementalResponseStatisticsList;
        private Dictionary<string, AddressResolutionStatistics> addressResolutionStatistics;
        private Lazy<List<HttpResponseStatistics>> httpResponseStatisticsList;
        private SystemUsageHistory systemUsageHistory;

        static ClientSideRequestStatistics()
        {
            ClientSideRequestStatistics.enableCpuMonitorFlag = true;
#if !(NETSTANDARD15 || NETSTANDARD16)
            string enableCpuMonitorString = System.Configuration.ConfigurationManager.AppSettings[ClientSideRequestStatistics.EnableCpuMonitorConfig];
            if (!string.IsNullOrEmpty(enableCpuMonitorString))
            {

                if (!bool.TryParse(enableCpuMonitorString, out ClientSideRequestStatistics.enableCpuMonitorFlag))
                {
                    ClientSideRequestStatistics.enableCpuMonitorFlag = true;
                }
            }
#endif

            if (ClientSideRequestStatistics.enableCpuMonitorFlag)
            {
                // Have a history up to 1 minute with recording every 10 seconds
                ClientSideRequestStatistics.systemRecorder = new SystemUsageRecorder(
                    identifier: nameof(ClientSideRequestStatistics),
                    historyLength: 6,
                    refreshInterval: ClientSideRequestStatistics.SystemUsageRecordInterval);
                
                ClientSideRequestStatistics.systemUsageMonitor = SystemUsageMonitor.CreateAndStart(new List<SystemUsageRecorder>()
                {
                    ClientSideRequestStatistics.systemRecorder,
                });
            }
        }


        public ClientSideRequestStatistics()
        {
            this.requestStartTime = DateTime.UtcNow;
            this.requestEndTime = null;
            this.responseStatisticsList = new List<StoreResponseStatistics>();
            this.supplementalResponseStatisticsList = new List<StoreResponseStatistics>();
            this.addressResolutionStatistics = new Dictionary<string, AddressResolutionStatistics>();
            this.ContactedReplicas = new List<TransportAddressUri>();
            this.FailedReplicas = new HashSet<TransportAddressUri>();
            this.RegionsContacted = new HashSet<(string, Uri)>();
            this.httpResponseStatisticsList = new Lazy<List<HttpResponseStatistics>>();
        }

        public List<TransportAddressUri> ContactedReplicas { get; set; }

        public HashSet<TransportAddressUri> FailedReplicas { get; private set; }

        public HashSet<(string, Uri)> RegionsContacted { get; private set; }


        public TimeSpan? RequestLatency
        {
            get
            {
                if (!requestEndTime.HasValue)
                {
                    return null;
                }

                return requestEndTime.Value - requestStartTime;
            }
        }

        public bool? IsCpuHigh
        {
            get
            {
                return this.systemUsageHistory?.IsCpuHigh;
            }
        }

        public bool? IsCpuThreadStarvation
        {
            get
            {
                return this.systemUsageHistory?.IsCpuThreadStarvation;
            }
        }

        internal static void DisableCpuMonitor()
        {
            // CPU monitor is already disabled
            if (!ClientSideRequestStatistics.enableCpuMonitorFlag)
            {
                return;
            }

            ClientSideRequestStatistics.enableCpuMonitorFlag = false;
            if (ClientSideRequestStatistics.systemRecorder != null)
            {
                ClientSideRequestStatistics.systemUsageMonitor.Stop();
                ClientSideRequestStatistics.systemUsageMonitor.Dispose();
            }
        }

        public void RecordRequest(DocumentServiceRequest request)
        {
        }

        public void RecordResponse(
            DocumentServiceRequest request,
            StoreResult storeResult,
            DateTime startTimeUtc,
            DateTime endTimeUtc)
        {
            this.UpdateRequestEndTime(endTimeUtc);

            StoreResponseStatistics responseStatistics;
            responseStatistics.RequestStartTime = startTimeUtc;
            responseStatistics.RequestResponseTime = endTimeUtc;
            responseStatistics.StoreResult = storeResult;
            responseStatistics.RequestOperationType = request.OperationType;
            responseStatistics.RequestResourceType = request.ResourceType;

            Uri locationEndpoint = request.RequestContext.LocationEndpointToRoute;
            string regionName = request.RequestContext.RegionName ?? string.Empty;

            lock (this.lockObject)
            {
                if (locationEndpoint != null)
                {
                    this.RegionsContacted.Add((regionName, locationEndpoint));
                }

                if (responseStatistics.RequestOperationType == OperationType.Head || responseStatistics.RequestOperationType == OperationType.HeadFeed)
                {
                    this.supplementalResponseStatisticsList.Add(responseStatistics);
                }
                else
                {
                    this.responseStatisticsList.Add(responseStatistics);
                }
            }
        }

        public void RecordException(
            DocumentServiceRequest request,
            Exception exception,
            DateTime startTime,
            DateTime endTimeUtc)
        {
            this.UpdateRequestEndTime(endTimeUtc);
        }

        public string RecordAddressResolutionStart(Uri targetEndpoint)
        {
            string identifier = Guid.NewGuid().ToString();
            AddressResolutionStatistics resolutionStats = new AddressResolutionStatistics
            {
                StartTime = DateTime.UtcNow,
                EndTime = DateTime.MaxValue,
                TargetEndpoint = targetEndpoint == null ? "<NULL>" : targetEndpoint.ToString()
            };

            lock (this.lockObject)
            {
                this.addressResolutionStatistics.Add(identifier, resolutionStats);
            }

            return identifier;
        }

        public void RecordAddressResolutionEnd(string identifier)
        {
            if (string.IsNullOrEmpty(identifier))
            {
                return;
            }

            DateTime responseTime = DateTime.UtcNow;
            this.UpdateRequestEndTime(DateTime.UtcNow);
            lock (this.lockObject)
            {
                if (!this.addressResolutionStatistics.ContainsKey(identifier))
                {
                    throw new ArgumentException("Identifier {0} does not exist. Please call start before calling end.", identifier);
                }

                this.addressResolutionStatistics[identifier].EndTime = responseTime;
            }
        }

        public void RecordHttpResponse(HttpRequestMessage request,
                               HttpResponseMessage response,
                               ResourceType resourceType,
                               DateTime requestStartTimeUtc)
        {
            DateTime requestEndTimeUtc = DateTime.UtcNow;
            this.UpdateRequestEndTime(requestEndTimeUtc);
            lock (this.httpResponseStatisticsList)
            {
                this.httpResponseStatisticsList.Value.Add(new HttpResponseStatistics(requestStartTimeUtc,
                                                                           requestEndTimeUtc,
                                                                           request.RequestUri,
                                                                           request.Method,
                                                                           resourceType,
                                                                           response,
                                                                           exception: null));
            }
        }

        public void RecordHttpException(HttpRequestMessage request,
                                       Exception exception,
                                       ResourceType resourceType,
                                       DateTime requestStartTimeUtc)
        {
            DateTime requestEndTimeUtc = DateTime.UtcNow;
            this.UpdateRequestEndTime(requestEndTimeUtc);
            lock (this.httpResponseStatisticsList)
            {
                this.httpResponseStatisticsList.Value.Add(new HttpResponseStatistics(requestStartTimeUtc,
                                                                           requestEndTimeUtc,
                                                                           request.RequestUri,
                                                                           request.Method,
                                                                           resourceType,
                                                                           responseMessage: null,
                                                                           exception: exception));
            }
        }

        private void UpdateRequestEndTime(DateTime requestEndTimeUtc)
        {
            lock (this.requestEndTimeLock)
            {
                if (!this.requestEndTime.HasValue || requestEndTimeUtc > this.requestEndTime)
                {
                    this.UpdateSystemUsageHistory();
                    this.requestEndTime = requestEndTimeUtc;
                }
            }
        }

        private void UpdateSystemUsageHistory()
        {
            // Only update the CPU history if it more than 10 seconds has passed since it was originally collected
            if (ClientSideRequestStatistics.enableCpuMonitorFlag &&
                ClientSideRequestStatistics.systemRecorder != null &&
                (this.systemUsageHistory == null || this.systemUsageHistory.LastTimestamp + ClientSideRequestStatistics.SystemUsageRecordInterval < DateTime.UtcNow))
            {
                try
                {
                    this.systemUsageHistory = ClientSideRequestStatistics.systemRecorder.Data;
                }
                catch (Exception ex)
                {
                    DefaultTrace.TraceCritical(
                        "System usage monitor failed with an unexpected exception: {0}",
                        ex);
                }
            }
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            this.AppendToBuilder(sb);
            return sb.ToString();
        }

        public void AppendToBuilder(StringBuilder stringBuilder)
        {
            if (stringBuilder == null)
            {
                throw new ArgumentNullException(nameof(stringBuilder));
            }

            //need to lock in case of concurrent operations. this should be extremely rare since ToString()
            //should only be called at the end of request.
            lock (this.lockObject)
            {
                stringBuilder.AppendLine();

                string endtime;
                if (this.requestEndTime.HasValue)
                {
                    endtime = this.requestEndTime.Value.ToString("o", CultureInfo.InvariantCulture);
                }
                else
                {
                    endtime = $"No response recorded; Current Time: {DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)}";
                }

                //first trace request start time, as well as total non-head/headfeed requests made.
                stringBuilder.AppendFormat(
                   CultureInfo.InvariantCulture,
                   "RequestStartTime: {0}, RequestEndTime: {1},  Number of regions attempted:{2}",
                   this.requestStartTime.ToString("o", CultureInfo.InvariantCulture),
                   endtime,
                   this.RegionsContacted.Count == 0 ? 1 : this.RegionsContacted.Count);
                stringBuilder.AppendLine();

                // This is needed for scenarios where the request was not sent
                // so there is no response which triggers saving the system usage
                if(this.systemUsageHistory == null)
                {
                    this.UpdateSystemUsageHistory();
                }

                if (this.systemUsageHistory != null &&
                    this.systemUsageHistory.Values.Count > 0)
                {
                    this.systemUsageHistory.AppendJsonString(stringBuilder);
                    stringBuilder.AppendLine();
                }
                else
                {
                    stringBuilder.AppendLine("System history not available.");
                }

                //take all responses here - this should be limited in number and each one contains relevant information.
                foreach (StoreResponseStatistics item in this.responseStatisticsList)
                {
                    item.AppendToBuilder(stringBuilder);
                    stringBuilder.AppendLine();
                }

                //take all responses here - this should be limited in number and each one is important.
                foreach (AddressResolutionStatistics item in this.addressResolutionStatistics.Values)
                {
                    item.AppendToBuilder(stringBuilder);
                    stringBuilder.AppendLine();
                }

                //take all responses here - this should be limited in number and each one is important.
                lock (this.httpResponseStatisticsList)
                {
                    if (this.httpResponseStatisticsList.IsValueCreated)
                    {
                        foreach (HttpResponseStatistics item in this.httpResponseStatisticsList.Value)
                        {
                            item.AppendToBuilder(stringBuilder);
                            stringBuilder.AppendLine();
                        }
                    }
                }

                //only take last 10 responses from this list - this has potential of having large number of entries. 
                //since this is for establishing consistency, we can make do with the last responses to paint a meaningful picture.
                int supplementalResponseStatisticsListCount = this.supplementalResponseStatisticsList.Count;
                int initialIndex = Math.Max(supplementalResponseStatisticsListCount - ClientSideRequestStatistics.MaxSupplementalRequestsForToString, 0);

                if (initialIndex != 0)
                {
                    stringBuilder.AppendFormat(
                        CultureInfo.InvariantCulture,
                        "  -- Displaying only the last {0} head/headfeed requests. Total head/headfeed requests: {1}",
                        ClientSideRequestStatistics.MaxSupplementalRequestsForToString,
                        supplementalResponseStatisticsListCount);
                    stringBuilder.AppendLine();
                }

                for (int i = initialIndex; i < supplementalResponseStatisticsListCount; i++)
                {
                    this.supplementalResponseStatisticsList[i].AppendToBuilder(stringBuilder);
                    stringBuilder.AppendLine();
                }
            }
        }

        private struct StoreResponseStatistics
        {
            public DateTime RequestStartTime;
            public DateTime RequestResponseTime;
            public StoreResult StoreResult;
            public ResourceType RequestResourceType;
            public OperationType RequestOperationType;

            public override string ToString()
            {
                StringBuilder stringBuilder = new StringBuilder();
                this.AppendToBuilder(stringBuilder);
                return stringBuilder.ToString();
            }

            public void AppendToBuilder(StringBuilder stringBuilder)
            {
                if (stringBuilder == null)
                {
                    throw new ArgumentNullException(nameof(stringBuilder));
                }

                stringBuilder.Append("RequestStart: ");
                stringBuilder.Append(this.RequestStartTime.ToString("o", CultureInfo.InvariantCulture));
                stringBuilder.Append("; ResponseTime: ");
                stringBuilder.Append(this.RequestResponseTime.ToString("o", CultureInfo.InvariantCulture));

                stringBuilder.Append("; StoreResult: ");
                if (this.StoreResult != null)
                {
                    this.StoreResult.AppendToBuilder(stringBuilder);
                }

                stringBuilder.AppendLine();
                stringBuilder.AppendFormat(
                    CultureInfo.InvariantCulture,
                    " ResourceType: {0}, OperationType: {1}",
                    this.RequestResourceType,
                    this.RequestOperationType);
            }
        }


        private class AddressResolutionStatistics
        {
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
            public string TargetEndpoint { get; set; }

            public override string ToString()
            {
                StringBuilder stringBuilder = new StringBuilder();
                this.AppendToBuilder(stringBuilder);
                return stringBuilder.ToString();
            }

            public void AppendToBuilder(StringBuilder stringBuilder)
            {
                if (stringBuilder == null)
                {
                    throw new ArgumentNullException(nameof(stringBuilder));
                }

                stringBuilder
                    .Append($"AddressResolution - StartTime: {this.StartTime.ToString("o", CultureInfo.InvariantCulture)}, ")
                    .Append($"EndTime: {this.EndTime.ToString("o", CultureInfo.InvariantCulture)}, ")
                    .Append("TargetEndpoint: ")
                    .Append(this.TargetEndpoint);

            }
        }

        public readonly struct HttpResponseStatistics
        {
            public HttpResponseStatistics(
                DateTime requestStartTime,
                DateTime requestEndTime,
                Uri requestUri,
                HttpMethod httpMethod,
                ResourceType resourceType,
                HttpResponseMessage responseMessage,
                Exception exception)
            {
                this.RequestStartTime = requestStartTime;
                this.Duration = requestEndTime - requestStartTime;
                this.HttpResponseMessage = responseMessage;
                this.Exception = exception;
                this.ResourceType = resourceType;
                this.HttpMethod = httpMethod;
                this.RequestUri = requestUri;
                this.ActivityId = Trace.CorrelationManager.ActivityId.ToString();
            }

            public DateTime RequestStartTime { get; }
            public TimeSpan Duration { get; }
            public HttpResponseMessage HttpResponseMessage { get; }
            public Exception Exception { get; }
            public ResourceType ResourceType { get; }
            public HttpMethod HttpMethod { get; }
            public Uri RequestUri { get; }
            public string ActivityId { get; }

            public void AppendToBuilder(StringBuilder stringBuilder)
            {
                if (stringBuilder == null)
                {
                    throw new ArgumentNullException(nameof(stringBuilder));
                }

                stringBuilder
                    .Append("HttpResponseStatistics - ")
                    .Append("RequestStartTime: ")
                    .Append(this.RequestStartTime.ToString("o", CultureInfo.InvariantCulture))
                    .Append(", DurationInMs: ")
                    .Append(this.Duration.TotalMilliseconds)
                    .Append(", RequestUri: ")
                    .Append(this.RequestUri)
                    .Append(", ResourceType: ")
                    .Append(this.ResourceType)
                    .Append(", HttpMethod: ")
                    .Append(this.HttpMethod);

                if (this.Exception != null)
                {
                    stringBuilder.Append(", ExceptionType: ")
                                 .Append(this.Exception.GetType())
                                 .Append(", ExceptionMessage: ")
                                 .Append(this.Exception.Message);
                }

                if (this.HttpResponseMessage != null)
                {
                    stringBuilder.Append(", StatusCode: ")
                                 .Append(this.HttpResponseMessage.StatusCode);
                    if (!this.HttpResponseMessage.IsSuccessStatusCode)
                    {
                        stringBuilder.Append(", ReasonPhrase: ")
                                     .Append(this.HttpResponseMessage.ReasonPhrase);
                    }
                }
            }
        }
    }
}

