//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Net.Http;
    using System.Text;

    internal sealed class ClientSideRequestStatistics : IClientSideRequestStatistics
    {
        private const int MaxSupplementalRequestsForToString = 10;

        private DateTime requestStartTime;
        private DateTime requestEndTime;

        private object lockObject = new object();
        private object requestEndTimeLock = new object();

        private List<StoreResponseStatistics> responseStatisticsList;
        private List<StoreResponseStatistics> supplementalResponseStatisticsList;
        private Dictionary<string, AddressResolutionStatistics> addressResolutionStatistics;
        private Lazy<List<HttpResponseStatistics>> httpResponseStatisticsList;

        public ClientSideRequestStatistics()
        {
            this.requestStartTime = DateTime.UtcNow;
            this.requestEndTime = DateTime.UtcNow;
            this.responseStatisticsList = new List<StoreResponseStatistics>();
            this.supplementalResponseStatisticsList = new List<StoreResponseStatistics>();
            this.addressResolutionStatistics = new Dictionary<string, AddressResolutionStatistics>();
            this.ContactedReplicas = new List<Uri>();
            this.FailedReplicas = new HashSet<Uri>();
            this.RegionsContacted = new HashSet<Uri>();
            this.httpResponseStatisticsList = new Lazy<List<HttpResponseStatistics>>();
        }

        public List<Uri> ContactedReplicas { get; set; }

        public HashSet<Uri> FailedReplicas { get; private set; }

        public HashSet<Uri> RegionsContacted { get; private set; }


        public TimeSpan RequestLatency
        {
            get
            {
                return requestEndTime - requestStartTime;
            }
        }

        public bool IsCpuOverloaded
        {
            get
            {
                foreach (StoreResponseStatistics responseStatistics in this.responseStatisticsList)
                {
                    if (responseStatistics.StoreResult.IsClientCpuOverloaded)
                    {
                        return true;
                    }
                }
                foreach (StoreResponseStatistics responseStatistics in this.supplementalResponseStatisticsList)
                {
                    if (responseStatistics.StoreResult.IsClientCpuOverloaded)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        public void RecordRequest(DocumentServiceRequest request)
        {
        }

        public void RecordResponse(DocumentServiceRequest request, StoreResult storeResult)
        {
            DateTime responseTime = this.GetAndUpdateRequestEndTime();

            StoreResponseStatistics responseStatistics;
            responseStatistics.RequestResponseTime = responseTime;
            responseStatistics.StoreResult = storeResult;
            responseStatistics.RequestOperationType = request.OperationType;
            responseStatistics.RequestResourceType = request.ResourceType;

            Uri locationEndpoint = request.RequestContext.LocationEndpointToRoute;

            lock (this.lockObject)
            {
                if (locationEndpoint != null)
                {
                    this.RegionsContacted.Add(locationEndpoint);
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

            DateTime responseTime = this.GetAndUpdateRequestEndTime();
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
            lock (this.httpResponseStatisticsList)
            {
                DateTime requestEndTimeUtc = this.GetAndUpdateRequestEndTime();
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
            lock (this.httpResponseStatisticsList)
            {
                DateTime requestEndTimeUtc = this.GetAndUpdateRequestEndTime();
                this.httpResponseStatisticsList.Value.Add(new HttpResponseStatistics(requestStartTimeUtc,
                                                                           requestEndTimeUtc,
                                                                           request.RequestUri,
                                                                           request.Method,
                                                                           resourceType,
                                                                           responseMessage: null,
                                                                           exception: exception));
            }
        }

        private DateTime GetAndUpdateRequestEndTime()
        {
            DateTime requestEndTimeUtc = DateTime.UtcNow;
            lock (this.requestEndTimeLock)
            {
                if (requestEndTimeUtc > this.requestEndTime)
                {
                    this.requestEndTime = requestEndTimeUtc;
                }
            }

            return requestEndTimeUtc;
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

                //first trace request start time, as well as total non-head/headfeed requests made.
                stringBuilder.AppendFormat(
                   CultureInfo.InvariantCulture,
                   "RequestStartTime: {0}, RequestEndTime: {1},  Number of regions attempted:{2}",
                   this.requestStartTime.ToString("o", CultureInfo.InvariantCulture),
                   this.requestEndTime.ToString("o", CultureInfo.InvariantCulture),
                   this.RegionsContacted.Count == 0 ? 1 : this.RegionsContacted.Count);
                stringBuilder.AppendLine();


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

                stringBuilder.Append($"ResponseTime: {this.RequestResponseTime.ToString("o", CultureInfo.InvariantCulture)}, ");

                stringBuilder.Append("StoreResult: ");
                if (this.StoreResult != null)
                {
                    this.StoreResult.AppendToBuilder(stringBuilder);
                }

                stringBuilder.AppendFormat(
                    CultureInfo.InvariantCulture,
                    ", ResourceType: {0}, OperationType: {1}",
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

