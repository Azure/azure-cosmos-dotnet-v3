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
        private readonly CosmosDiagnosticsContext diagnosticsContext;

        public CosmosClientSideRequestStatistics(CosmosDiagnosticsContext diagnosticsContext = null)
        {
            this.RequestStartTimeUtc = DateTime.UtcNow;
            this.RequestEndTimeUtc = null;
            this.EndpointToAddressResolutionStatistics = new Dictionary<string, AddressResolutionStatistics>();
            this.ContactedReplicas = new List<Uri>();
            this.FailedReplicas = new HashSet<Uri>();
            this.RegionsContacted = new HashSet<Uri>();
            this.diagnosticsContext = diagnosticsContext;
            diagnosticsContext?.AddDiagnosticsInternal(this);
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

        public bool IsCpuOverloaded { get; private set; }

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

            if (storeResult.IsClientCpuOverloaded)
            {
                this.IsCpuOverloaded = true;
            }

            lock (this.lockObject)
            {
                if (locationEndpoint != null)
                {
                    this.RegionsContacted.Add(locationEndpoint);
                }

                this.diagnosticsContext?.AddDiagnosticsInternal(responseStatistics);
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
                this.diagnosticsContext?.AddDiagnosticsInternal(resolutionStats);
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

        public void AppendToBuilder(StringBuilder stringBuilder)
        {
            using (StringWriter stringWriter = new StringWriter(stringBuilder))
            {
                this.WriteTo(stringWriter);
            }
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
