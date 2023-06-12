//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.FaultInjection
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;

    /// <summary>
    /// Fault Injection Request Context
    /// This is used to track the number of times a rule has been applied to an operation.
    /// This is also use to track what rule was applied to each network request.
    /// </summary>
    public class FaultInjectionRequestContext
    {
        private readonly ConcurrentDictionary<string, int> hitCountByRule;
        private readonly ConcurrentDictionary<uint, string> requestIdByRuleId;
        private readonly ConcurrentDictionary<uint, List<string>> requestIdByRuleEvaluation;

        private Uri locationEndpointToRoute;

        /// <summary>
        /// Creates a new instance of the <see cref="FaultInjectionRequestContext"/> class.
        /// </summary>
        public FaultInjectionRequestContext()
        {
            this.hitCountByRule = new ConcurrentDictionary<string, int>();
            //this.transportRequestIdByRuleId = new ConcurrentDictionary<long, string>();
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
            this.requestIdByRuleId = new ConcurrentDictionary<uint, string>();
            this.requestIdByRuleEvaluation = new ConcurrentDictionary<uint, List<string>>();
        }

        /// <summary>
        /// Applies a fault injection rule to the request context. If a rule has not been applied yet, it will be added to the context. If it has the hitcount will be incremented.
        /// </summary>
        /// <param name="requestId"></param>
        /// <param name="ruleId"></param>
        public void ApplyFaultInjectionRule(uint requestId, string ruleId)
        {
            this.hitCountByRule.AddOrUpdate(
                ruleId,
                1,
                (id, hitcount) => hitcount + 1);

            this.requestIdByRuleId.TryAdd(requestId, ruleId);
        }

        /// <summary>
        /// Records the result of a rule evaluation. If a rule has not been evaluated, it will add it to the context. If it has, it will add the result to the list of results.
        /// </summary>
        /// <param name="requestId"></param>
        /// <param name="ruleEvaluationResult"></param>
        public void RecordFaultInjectionRuleEvaluation(uint requestId, string ruleEvaluationResult)
        {
            this.requestIdByRuleEvaluation.AddOrUpdate(
                requestId,
                new List<string> { ruleEvaluationResult },
                (id, ruleEvaluationResults) =>
                {
                    ruleEvaluationResults.Add(ruleEvaluationResult);
                    return ruleEvaluationResults;
                });
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
        /// <param name="requestId"></param>
        /// <returns>the rule id.</returns>
        public string GetFaultInjectionRuleId(uint requestId)
        {
            if (this.requestIdByRuleId.TryGetValue(requestId, out string ruleId))
            {
                return ruleId;
            }
            return null;
        }

        /// <summary>
        /// Sets location endpoint to route.
        /// </summary>
        /// <param name="locationEndpointToRoute"></param>
        public void SetLocationEndpointToRoute(Uri locationEndpointToRoute)
        {
            this.locationEndpointToRoute = locationEndpointToRoute;
        }

        /// <summary>
        /// Gets location endpoint to route.
        /// </summary>
        /// <returns>the location.</returns>
        public Uri GetLocationEndpointToRoute()
        {
            return this.locationEndpointToRoute;
        }

        /// <summary>
        /// Given a request id, returns the list of rule evaluation results.
        /// </summary>
        /// <param name="requestId"></param>
        /// <returns>a list of the evaulation results</returns>
        public List<string> GetFaultInjectionRuleEvaluationResults(uint requestId)
        {
            if (this.requestIdByRuleEvaluation.TryGetValue(requestId, out List<string> ruleEvaluationResults))
            {
                return ruleEvaluationResults;
            }
            return null;
        }
    }
}
