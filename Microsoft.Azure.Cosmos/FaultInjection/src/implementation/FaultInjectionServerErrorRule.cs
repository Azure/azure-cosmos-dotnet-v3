//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.FaultInjection
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using Antlr4.Runtime.Misc;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Rntbd;

    internal class FaultInjectionServerErrorRule : IFaultInjectionRuleInternal
    {
        private const string FautInjecitonId = "FaultInjectionId";

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
                return false;
            }

            // the failure reason will be populated during condition evaluation
            if (!this.condition.IsApplicable(this.id, args))
            {
                return false;
            }

            if (!this.result.IsApplicable(this.id, args.CommonArguments.ActivityId))
            {
                return false;
            }

            long evaluationCount = this.evaluationCount + 1;
            Interlocked.Increment(ref this.evaluationCount);
            bool withinHitLimit = this.hitLimit == 0 || evaluationCount <= this.hitLimit;
            if (!withinHitLimit)
            {
                return false;
            }
            else if (Random.Shared.NextDouble() > this.result.GetInjectionRate())
            {
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

        //Used for Gateway requests
        public bool IsApplicable(DocumentServiceRequest dsr)
        {
            if(!this.IsValid())
            {
                return false;
            }

            // the failure reason will be populated during condition evaluation
            if (!this.condition.IsApplicable(this.id, dsr))
            {
                return false;
            }

            if (!this.result.IsApplicable(
                this.id, 
                new Guid(dsr.Headers.Get(FaultInjectionServerErrorRule.FautInjecitonId))))
            {
                return false;
            }

            long evaluationCount = this.evaluationCount + 1;
            Interlocked.Increment(ref this.evaluationCount);
            bool withinHitLimit = this.hitLimit == 0 || evaluationCount <= this.hitLimit;
            if (!withinHitLimit)
            {
                return false;
            }
            else if (Random.Shared.NextDouble() > this.result.GetInjectionRate())
            {
                return false;
            }
            else
            {
                Interlocked.Increment(ref this.hitCount);

                // track hit count details, keay is operationType-ResourceType
                String key = dsr.OperationType.ToString() + "-" + dsr.ResourceType.ToString();
                this.hitCountDetails.AddOrUpdate(
                    key,
                    1L,
                    (k, v) => v++);

                return true;
            }
        }

        //Used for Connection Delay
        public bool IsApplicable(
            Uri callUri, 
            DocumentServiceRequest request,
            Guid activityId)
        {
            if (!this.IsValid())
            {
                return false;
            }

            // the failure reason will be populated during condition evaluation
            if (!this.condition.IsApplicable(this.id, callUri, request))
            {
                return false;
            }

            if (!this.result.IsApplicable(this.id, activityId))
            {
                return false;
            }

            long evaluationCount = this.evaluationCount + 1;
            Interlocked.Increment(ref this.evaluationCount);
            bool withinHitLimit = this.hitLimit == 0 || evaluationCount <= this.hitLimit;
            if (!withinHitLimit)
            {
                return false;
            }
            else
            {
                Interlocked.Increment(ref this.hitCount);

                // track hit count details, key is operationType-ResourceType
                String key = request.OperationType.ToString() + "-" + request.ResourceType.ToString();
                this.hitCountDetails.AddOrUpdate(
                    key,
                    1L,
                    (k, v) => v++);

                return true;
            }
        }

        public StoreResponse GetInjectedServerError(ChannelCallArguments args)
        {
            return this.result.GetInjectedServerError(args, this.id);
        }

        public HttpResponseMessage GetInjectedServerError(DocumentServiceRequest request)
        {

           return this.result.GetInjectedServerError(request, this.id);
        }

        public FaultInjectionServerErrorType GetInjectedServerErrorType()
        {
            return this.result.GetInjectedServerErrorType();
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

        public void Enable()
        {
            this.enabled = true;
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
