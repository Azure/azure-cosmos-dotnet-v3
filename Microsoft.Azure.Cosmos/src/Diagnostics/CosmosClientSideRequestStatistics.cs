//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;

    internal sealed class CosmosClientSideRequestStatistics : CosmosDiagnosticsInternal, IClientSideRequestStatistics
    {
        private readonly object lockObject = new object();

        public CosmosClientSideRequestStatistics(CosmosDiagnosticsContext diagnosticsContext = null)
        {
            this.RequestStartTimeUtc = DateTime.UtcNow;
            this.RequestEndTimeUtc = null;
            this.EndpointToAddressResolutionStatistics = new Dictionary<string, AddressResolutionStatistics>();
            this.ContactedReplicas = new List<Uri>();
            this.FailedReplicas = new HashSet<Uri>();
            this.RegionsContacted = new HashSet<Uri>();
            this.DiagnosticsContext = diagnosticsContext ?? new CosmosDiagnosticsContextCore();
            this.DiagnosticsContext.AddDiagnosticsInternal(this);
        }

        private DateTime RequestStartTimeUtc { get; }

        private DateTime? RequestEndTimeUtc { get; set; }

        private Dictionary<string, AddressResolutionStatistics> EndpointToAddressResolutionStatistics { get; }

        public List<Uri> ContactedReplicas { get; set; }

        public HashSet<Uri> FailedReplicas { get; }

        public HashSet<Uri> RegionsContacted { get; }

        public TimeSpan RequestLatency
        {
            get
            {
                if (this.RequestEndTimeUtc.HasValue)
                {
                    return this.RequestEndTimeUtc.Value - this.RequestStartTimeUtc;
                }

                return TimeSpan.MaxValue;
            }
        }

        public bool IsCpuOverloaded { get; private set; } = false;

        public CosmosDiagnosticsContext DiagnosticsContext { get; }

        public void RecordRequest(DocumentServiceRequest request)
        {
        }

        public void RecordResponse(DocumentServiceRequest request, StoreResult storeResult)
        {
            DateTime responseTime = DateTime.UtcNow;
            Uri locationEndpoint = request.RequestContext.LocationEndpointToRoute;
            StoreResponseStatistics responseStatistics = new StoreResponseStatistics(
                responseTime,
                storeResult,
                request.ResourceType,
                request.OperationType,
                locationEndpoint);

            if (storeResult?.IsClientCpuOverloaded ?? false)
            {
                this.IsCpuOverloaded = true;
            }

            lock (this.lockObject)
            {
                if (!this.RequestEndTimeUtc.HasValue || responseTime > this.RequestEndTimeUtc)
                {
                    this.RequestEndTimeUtc = responseTime;
                }

                if (locationEndpoint != null)
                {
                    this.RegionsContacted.Add(locationEndpoint);
                }

                this.DiagnosticsContext.AddDiagnosticsInternal(responseStatistics);
            }
        }

        public string RecordAddressResolutionStart(Uri targetEndpoint)
        {
            string identifier = Guid.NewGuid().ToString();
            AddressResolutionStatistics resolutionStats = new AddressResolutionStatistics(
                startTime: DateTime.UtcNow,
                endTime: DateTime.MaxValue,
                targetEndpoint: targetEndpoint == null ? "<NULL>" : targetEndpoint.ToString());

            lock (this.lockObject)
            {
                this.EndpointToAddressResolutionStatistics.Add(identifier, resolutionStats);
                this.DiagnosticsContext.AddDiagnosticsInternal(resolutionStats);
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
            lock (this.lockObject)
            {
                if (!this.EndpointToAddressResolutionStatistics.ContainsKey(identifier))
                {
                    throw new ArgumentException("Identifier {0} does not exist. Please call start before calling end.", identifier);
                }

                if (!this.RequestEndTimeUtc.HasValue || responseTime > this.RequestEndTimeUtc)
                {
                    this.RequestEndTimeUtc = responseTime;
                }

                this.EndpointToAddressResolutionStatistics[identifier].EndTime = responseTime;
            }
        }

        public override string ToString()
        {
            // This is required for the older IClientSideRequestStatistics.
            // Using the compatibility to avoid issue from the additional size the diagnostic context contains.
            StringBuilder stringBuilder = new StringBuilder();
            V2CompatabilitySerializerVisitor serializerVisitor = new V2CompatabilitySerializerVisitor(stringBuilder);
            serializerVisitor.Visit(this);
            return stringBuilder.ToString();
        }

        public void AppendToBuilder(StringBuilder stringBuilder)
        {
            // This is required for the older IClientSideRequestStatistics.
            // Using the compatibility to avoid issue from the additional size the diagnostic context contains
            V2CompatabilitySerializerVisitor serializerVisitor = new V2CompatabilitySerializerVisitor(stringBuilder);
            serializerVisitor.Visit(this);
        }

        public override void Accept(CosmosDiagnosticsInternalVisitor visitor)
        {
            visitor.Visit(this);
        }

        public override TResult Accept<TResult>(CosmosDiagnosticsInternalVisitor<TResult> visitor)
        {
            return visitor.Visit(this);
        }

        /// <summary>
        /// This visitor formats the content as v2 SDK to avoid breaking backward compatibility
        /// </summary>
        private class V2CompatabilitySerializerVisitor : CosmosDiagnosticsInternalVisitor
        {
            private const int MaxSupplementalRequestsForToString = 10;
            private StringBuilder stringBuilder = null;

            public V2CompatabilitySerializerVisitor(StringBuilder stringBuilder)
            {
                this.stringBuilder = stringBuilder ?? throw new ArgumentNullException(nameof(stringBuilder));
            }

            public override void Visit(PointOperationStatistics pointOperationStatistics)
            {
                throw new NotSupportedException("Only supports CosmosClientSideRequestStatistics for back ToString compatibility.");
            }

            public override void Visit(CosmosDiagnosticsContext cosmosDiagnosticsContext)
            {
                throw new NotSupportedException("Only supports CosmosClientSideRequestStatistics for back ToString compatibility.");
            }

            public override void Visit(CosmosDiagnosticScope cosmosDiagnosticScope)
            {
                throw new NotSupportedException("Only supports CosmosClientSideRequestStatistics for back ToString compatibility.");
            }

            public override void Visit(QueryPageDiagnostics queryPageDiagnostics)
            {
                throw new NotSupportedException("Only supports CosmosClientSideRequestStatistics for back ToString compatibility.");
            }

            public override void Visit(AddressResolutionStatistics addressResolutionStatistics)
            {
                string endTime = addressResolutionStatistics.EndTime.HasValue ?
                    addressResolutionStatistics.EndTime.Value.ToString("o", CultureInfo.InvariantCulture)
                    : "Not set";

                this.stringBuilder
                    .Append($"AddressResolution - StartTime: {addressResolutionStatistics.StartTime.ToString("o", CultureInfo.InvariantCulture)}, ")
                    .Append($"EndTime: {endTime}, ")
                    .Append("TargetEndpoint: ")
                    .Append(addressResolutionStatistics.TargetEndpoint);
            }

            public override void Visit(StoreResponseStatistics storeResponseStatistics)
            {
                this.stringBuilder.Append($"ResponseTime: {storeResponseStatistics.RequestResponseTime.ToString("o", CultureInfo.InvariantCulture)}, ");

                this.stringBuilder.Append("StoreResult: ");
                if (storeResponseStatistics.StoreResult != null)
                {
                    storeResponseStatistics.StoreResult.AppendToBuilder(this.stringBuilder);
                }

                this.stringBuilder.AppendFormat(
                    CultureInfo.InvariantCulture,
                    ", ResourceType: {0}, OperationType: {1}",
                    storeResponseStatistics.RequestResourceType,
                    storeResponseStatistics.RequestOperationType);
            }

            public override void Visit(CosmosClientSideRequestStatistics clientSideRequestStatistics)
            {
                if (this.stringBuilder == null)
                {
                    throw new ArgumentNullException(nameof(this.stringBuilder));
                }

                this.stringBuilder.AppendLine();

                string endtime = clientSideRequestStatistics.RequestEndTimeUtc.HasValue ?
                    clientSideRequestStatistics.RequestEndTimeUtc.Value.ToString("o", CultureInfo.InvariantCulture)
                    : "Not Set";

                //first trace request start time, as well as total non-head/headfeed requests made.
                this.stringBuilder.AppendFormat(
                   CultureInfo.InvariantCulture,
                   "RequestStartTime: {0}, RequestEndTime: {1},  Number of regions attempted:{2}",
                   clientSideRequestStatistics.RequestStartTimeUtc.ToString("o", CultureInfo.InvariantCulture),
                   endtime,
                   clientSideRequestStatistics.RegionsContacted.Count == 0 ? 1 : clientSideRequestStatistics.RegionsContacted.Count);
                this.stringBuilder.AppendLine();

                List<StoreResponseStatistics> storeResponseStatisticsList = new List<StoreResponseStatistics>();
                List<AddressResolutionStatistics> addressResolutionStatisticsList = new List<AddressResolutionStatistics>();

                //only take last 10 responses from this list - this has potential of having large number of entries. 
                //since this is for establishing consistency, we can make do with the last responses to paint a meaningful picture.
                Queue<StoreResponseStatistics> supplementalResponseStatisticsListLimit10 = new Queue<StoreResponseStatistics>(10);
                int totalSupplementalResponseStatisticsCount = 0;

                foreach (CosmosDiagnosticsInternal diagnosticsInternal in clientSideRequestStatistics.DiagnosticsContext)
                {
                    if (diagnosticsInternal is StoreResponseStatistics storeResponseStatistics)
                    {
                        if (storeResponseStatistics.IsSupplementalResponse)
                        {
                            if (supplementalResponseStatisticsListLimit10.Count == 10)
                            {
                                supplementalResponseStatisticsListLimit10.Dequeue();
                            }

                            totalSupplementalResponseStatisticsCount++;
                            supplementalResponseStatisticsListLimit10.Enqueue(storeResponseStatistics);
                        }
                        else
                        {
                            storeResponseStatisticsList.Add(storeResponseStatistics);
                        }

                    }

                    if (diagnosticsInternal is AddressResolutionStatistics addressResolutionStatistics)
                    {
                        addressResolutionStatisticsList.Add(addressResolutionStatistics);
                    }
                }

                //take all responses here - this should be limited in number and each one contains relevant information.
                foreach (StoreResponseStatistics item in storeResponseStatisticsList)
                {
                    item.Accept(this);
                    this.stringBuilder.AppendLine();
                }

                //take all responses here - this should be limited in number and each one is important.
                foreach (AddressResolutionStatistics item in addressResolutionStatisticsList)
                {
                    item.Accept(this);
                    this.stringBuilder.AppendLine();
                }

                if (totalSupplementalResponseStatisticsCount != 0)
                {
                    this.stringBuilder.AppendFormat(
                        CultureInfo.InvariantCulture,
                        "  -- Displaying only the last {0} head/headfeed requests. Total head/headfeed requests: {1}",
                        V2CompatabilitySerializerVisitor.MaxSupplementalRequestsForToString,
                        totalSupplementalResponseStatisticsCount);
                    this.stringBuilder.AppendLine();
                }

                foreach (StoreResponseStatistics item in supplementalResponseStatisticsListLimit10)
                {
                    item.Accept(this);
                    this.stringBuilder.AppendLine();
                }
            }
        }
    }
}