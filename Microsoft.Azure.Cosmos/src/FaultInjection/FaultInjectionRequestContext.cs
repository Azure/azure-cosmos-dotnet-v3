//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Runtime.ConstrainedExecution;
    using System.Text;

    /// <summary>
    /// Fault Injection Request Context
    /// This is used to track the number of times a rule has been applied to an operation.
    /// This is also use to track what rule was applied to each network request.
    /// </summary>
    public class FaultInjectionRequestContext
    {
        private readonly ConcurrentDictionary<string, int> hitCountByRule;
        private readonly ConcurrentDictionary<long, string> transportRequestIdByRuleId;

        /// <summary>
        /// Creates a new instance of the <see cref="FaultInjectionRequestContext"/> class.
        /// </summary>
        public FaultInjectionRequestContext()
        {
            this.hitCountByRule = new ConcurrentDictionary<string, int>();
            this.transportRequestIdByRuleId = new ConcurrentDictionary<long, string>();
        }

        /// <summary>
        /// Creates a new instance of the <see cref="FaultInjectionRequestContext"/> class. This constructior is used during retries.
        /// Hit count must be copied from the previous context for the hit count limit to be honored.
        /// The transport request id to rule id mapping does not need to be copied as the required diagnostics will have already been logged.
        /// </summary>
        /// <param name="cloneContext"> the previous fault injection request context.</param>
        public FaultInjectionRequestContext(FaultInjectionRequestContext cloneContext)
        {
            this.hitCountByRule = cloneContext.hitCountByRule;
            this.transportRequestIdByRuleId = new ConcurrentDictionary<long, string>();
        }

        /// <summary>
        /// Applies a fault injection rule to the request context. If a rule has not been applied yet, it will be added to the context. If it has the hitcount will be incremented.
        /// </summary>
        /// <param name="transportId"></param>
        /// <param name="ruleId"></param>
        public void applyFaultInjectionRule(long transportId, string ruleId)
        {
            this.hitCountByRule.AddOrUpdate(
                ruleId,
                1,
                (id, hitcount) => hitcount + 1);

            this.transportRequestIdByRuleId.TryAdd(transportId, ruleId);
        }

        /// <summary>
        /// Gets the number of times a rule has been applied to an operation.
        /// </summary>
        /// <param name="ruleId">the id of the rule.</param>
        /// <returns>the hit coutn.</returns>
        public int GetFaultInjectionRuleHitCount(string ruleId)
        {
            if (this.hitCountByRule.TryGetValue(ruleId, out int hitCount))
            {
                return hitCount;
            }
            return 0;
        }

        /// <summary>
        /// Gets the rule id that was applied to a network request.
        /// </summary>
        /// <param name="transportId"></param>
        /// <returns>the rule id.</returns>
        public string GetFaultInjectionRuleId(long transportId)
        {
            if (this.transportRequestIdByRuleId.TryGetValue(transportId, out string ruleId))
            {
                return ruleId;
            }
            return null;
        }
    }
}
