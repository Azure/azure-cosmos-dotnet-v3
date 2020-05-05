//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
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

        private Dictionary<DocumentServiceRequest, DateTime> RecordRequestStartTime = new Dictionary<DocumentServiceRequest, DateTime>();

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
            this.RecordRequestStartTime[request] = DateTime.UtcNow;
        }

        public void RecordResponse(DocumentServiceRequest request, StoreResult storeResult)
        {
            // One DocumentServiceRequest can map to multiple store results, and the DocumentServiceRequest
            // is reliably cleaned up else where in the code so no need to clean up the dictionary.
            DateTime? startDateTime = null;
            if (this.RecordRequestStartTime.TryGetValue(request, out DateTime startRequestTime))
            {
                startDateTime = startRequestTime;
            }
            else
            {
                Debug.Fail("DocumentServiceRequest start time not recorded");
            }

            DateTime responseTime = DateTime.UtcNow;
            Uri locationEndpoint = request.RequestContext.LocationEndpointToRoute;
            StoreResponseStatistics responseStatistics = new StoreResponseStatistics(
                startDateTime,
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
            // This is required for the older IClientSideRequestStatistics
            // Capture the entire diagnostic context in the toString to avoid losing any information
            // for any APIs using the older interface.
            return this.DiagnosticsContext.ToString();
        }

        public void AppendToBuilder(StringBuilder stringBuilder)
        {
            // This is required for the older IClientSideRequestStatistics
            // Capture the entire diagnostic context in the toString to avoid losing any information
            // for any APIs using the older interface.
            stringBuilder.Append(this.DiagnosticsContext.ToString());
        }

        public override void Accept(CosmosDiagnosticsInternalVisitor visitor)
        {
            visitor.Visit(this);
        }

        public override TResult Accept<TResult>(CosmosDiagnosticsInternalVisitor<TResult> visitor)
        {
            return visitor.Visit(this);
        }
    }
}