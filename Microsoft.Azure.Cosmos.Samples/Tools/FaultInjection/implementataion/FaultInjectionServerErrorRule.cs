//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.FaultInjection
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Concurrent;
    using System.Text;
    using Microsoft.Azure.Documents.FaultInjection;
    using Microsoft.Azure.Documents.Rntbd;
    using Microsoft.Azure.Documents;
    using System.Diagnostics.Metrics;

    public class FaultInjectionServerErrorRule : IFaultInjectionRuleInternal
    {
        private readonly string id;
        private readonly DateTime startTime;
        private readonly DateTime expireTime;
        private readonly int hitLimit;
        private readonly ConcurrentDictionary<string, long> hitCountDetails;
        private readonly FaultInjectionConnectionType connectionType; 
        private readonly FaultInjectionConditionInternal condition;
        private readonly FaultInjectionServerErrorResultInternal result;

        private long hitCount;
        private long evaluationCount;
        private bool enabled;

        public FaultInjectionServerErrorRule(
            string id,
            bool enabled,
            TimeSpan delay,
            TimeSpan duration,
            int hitLimit,
            FaultInjectionConnectionType connectionType,
            FaultInjectionConditionInternal condition,
            FaultInjectionServerErrorResultInternal result)
        {
            if (id == null || string.Equals(id, string.Empty))
            {
                throw new ArgumentException("$ Argument {nameof(id)} cannot be null or empty");
            }
            this.id = id;
            this.enabled = enabled;
            this.hitLimit = hitLimit;
            this.startTime = DateTime.UtcNow + delay;
            this.expireTime = duration == TimeSpan.MaxValue ? DateTime.MaxValue : this.startTime + duration;
            this.hitCount = 0;
            this.hitCountDetails = new ConcurrentDictionary<string, long>();
            this.evaluationCount = 0;
            this.condition = condition ?? throw new ArgumentNullException(nameof(condition));
            this.result = result ?? throw new ArgumentNullException(nameof(result));
            this.connectionType = connectionType;
        }

        public bool IsApplicable(ChannelCallArguments args)
        {
            if (!this.IsValid())
            {
                args.FaultInjectionRequestContext.RecordFaultInjectionRuleEvaluation(args.CommonArguments.ActivityId,
                    String.Format(
                        "{0}{Disable or duration reached. StartTime: {1}, ExpireTime: {2}]",
                        this.id,
                        this.startTime,
                        this.expireTime));

                return false;
            }

            // the failure reason will be populated during condition evaluation
            if (!this.condition.IsApplicable(this.id, args))
            {
                return false;
            }

            if (!this.result.IsApplicable(this.id, args))
            {
                args.FaultInjectionRequestContext.RecordFaultInjectionRuleEvaluation(
                    args.CommonArguments.ActivityId,
                    this.id + "[Per operation apply limit reached]");
                return false;
            }

            long evaluationCount = this.evaluationCount + 1;
            Interlocked.Increment(ref this.evaluationCount);
            bool withinHitLimit = this.hitLimit == 0 || evaluationCount <= this.hitLimit;
            if (!withinHitLimit)
            {
                args.FaultInjectionRequestContext.RecordFaultInjectionRuleEvaluation(
                    args.CommonArguments.ActivityId,
                    this.id + "[Hit limit reached]");
                return false;
            }
            else
            {
                Interlocked.Increment(ref this.hitCount);

                // track hit count details, keay is operationType-ResourceType
                String key = args.OperationType.ToString() + "-" + args.ResourceType.ToString();
                this.hitCountDetails.AddOrUpdate(
                    key, 
                    1L, 
                    (k, v) => v ++);

                return true;
            }
        }

        //Used for Connection Delay
        public bool IsApplicable(
            Guid activityId, 
            string callUri, 
            DocumentServiceRequest request)
        {
            if (!this.IsValid())
            {
                request.FaultInjectionRequestContext.RecordFaultInjectionRuleEvaluation(activityId,
                    String.Format(
                        "{0}{Disable or duration reached. StartTime: {1}, ExpireTime: {2}]",
                        this.id,
                        this.startTime,
                        this.expireTime));

                return false;
            }

            // the failure reason will be populated during condition evaluation
            if (!this.condition.IsApplicable(this.id, activityId, callUri, request))
            {
                return false;
            }

            if (!this.result.IsApplicable(this.id, request))
            {
                request.FaultInjectionRequestContext.RecordFaultInjectionRuleEvaluation(
                    activityId,
                    this.id + "[Per operation apply limit reached]");
                return false;
            }

            long evaluationCount = this.evaluationCount + 1;
            Interlocked.Increment(ref this.evaluationCount);
            bool withinHitLimit = this.hitLimit == 0 || evaluationCount <= this.hitLimit;
            if (!withinHitLimit)
            {
                request.FaultInjectionRequestContext.RecordFaultInjectionRuleEvaluation(
                    activityId,
                    this.id + "[Hit limit reached]");
                return false;
            }
            else
            {
                Interlocked.Increment(ref this.hitCount);

                // track hit count details, keay is operationType-ResourceType
                String key = request.OperationType.ToString() + "-" + request.ResourceType.ToString();
                this.hitCountDetails.AddOrUpdate(
                    key,
                    1L,
                    (k, v) => v++);

                return true;
            }
        }

        public void SetInjectedServerError(ChannelCallArguments args, TransportRequestStats transportRequestStats)
        {
            this.result.SetInjectedServerError(args, transportRequestStats);
        }

        public string GetId()
        {
            return this.id;
        }

        public long GetHitCount()
        {
            return this.hitCount;
        }

        public ConcurrentDictionary<string, long> GetHitCountDetails()
        {
            return this.hitCountDetails;
        }

        public FaultInjectionConnectionType GetConnectionType()
        {
            return this.connectionType;
        }

        public FaultInjectionConditionInternal GetCondition()
        {
            return this.condition;
        }

        public FaultInjectionServerErrorResultInternal GetResult()
        {
            return this.result;
        }

        public TimeSpan GetDelay()
        {
            return this.result.GetDelay();
        }

        public bool IsValid()
        {
            DateTime now = DateTime.UtcNow;
            return this.enabled && now >= this.startTime && now <= this.expireTime;
        }

        public void Disable()
        {
            this.enabled = false;
        }

        public List<Uri> GetAddresses()
        {
            return this.condition.GetPhysicalAddresses();
        }        

        public List<Uri> GetRegionEndpoints()
        {
            return this.condition.GetRegionEndpoints();
        }

        
    }
}
