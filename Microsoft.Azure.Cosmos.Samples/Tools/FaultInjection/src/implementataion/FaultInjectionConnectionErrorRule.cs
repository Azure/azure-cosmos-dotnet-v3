//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.FaultInjection
{
    using System;
    using System.Collections.Generic;

    public class FaultInjectionConnectionErrorRule : IFaultInjectionRuleInternal
    {
        private readonly string id;
        private readonly DateTime startTime;
        private readonly DateTime expireTime;
        private readonly List<Uri> regionEndpoints;
        private readonly List<Uri> addresses;
        private readonly FaultInjectionConnectionType connectionType;
        private readonly FaultInjectionConnectionErrorResult result;
        
        private long hitCount;
        private bool enabled;

        public FaultInjectionConnectionErrorRule(
            String id, 
            bool enabled,
            TimeSpan delay,
            TimeSpan duration,
            List<Uri> regionEndpoints,
            List<Uri> addresses,
            FaultInjectionConnectionType connectionType,
            FaultInjectionConnectionErrorResult result)
        {
            this.id = string.IsNullOrEmpty(id) ? throw new ArgumentException("Argument {nameof(id)} cannot be null or empty") : id;
            this.enabled = enabled;
            this.startTime = DateTime.UtcNow + delay;
            this.expireTime = this.startTime + duration;
            this.regionEndpoints = regionEndpoints;
            this.addresses = addresses;
            this.connectionType = connectionType;
            this.result = result ?? throw new ArgumentNullException(nameof(result));
            this.hitCount = 0;
        }
        public void Disable()
        {
            this.enabled = false;
        }

        public void Enable()
        {
            this.enabled = true;
        }

        public List<Uri> GetAddresses()
        {
            return this.addresses;
        }

        public FaultInjectionConnectionType GetConnectionType()
        {
            return this.connectionType;
        }

        public long GetHitCount()
        {
            return this.hitCount;
        }

        public string GetId()
        {
            return this.id;
        }

        public List<Uri> GetRegionEndpoints()
        {
            return this.regionEndpoints;
        }

        public FaultInjectionConnectionErrorResult GetResult()
        {
            return this.result;
        }

        public bool IsValid()
        {
            return this.enabled && DateTime.UtcNow >= this.startTime && DateTime.UtcNow <= this.expireTime;
        }

        public void ApplyRule()
        {
            Interlocked.Increment(ref this.hitCount);
        }
    }
}
