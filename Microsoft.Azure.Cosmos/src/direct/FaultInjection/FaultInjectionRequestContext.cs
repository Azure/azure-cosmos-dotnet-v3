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
        //private readonly ConcurrentDictionary<uint, string> requestIdByRuleId;
        private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, uint>> actvityIdByRuleIdCount;
        private readonly ConcurrentDictionary<Guid, List<string>> activityIdByRuleEvaluation;

        private Uri locationEndpointToRoute;

        /// <summary>
        /// Creates a new instance of the <see cref="FaultInjectionRequestContext"/> class.
        /// </summary>
        public FaultInjectionRequestContext()
        {
            this.hitCountByRule = new ConcurrentDictionary<string, int>();
            this.actvityIdByRuleIdCount = new ConcurrentDictionary<Guid, ConcurrentDictionary<string, uint>>();
            this.activityIdByRuleEvaluation = new ConcurrentDictionary<Guid, List<string>>();
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
            this.actvityIdByRuleIdCount = new ConcurrentDictionary<Guid, ConcurrentDictionary<string, uint>>();
            //this.requestIdByRuleId = new ConcurrentDictionary<uint, string>();
            this.activityIdByRuleEvaluation = new ConcurrentDictionary<Guid, List<string>>();
        }

        /// <summary>
        /// Applies a fault injection rule to the request context. If a rule has not been applied yet, it will be added to the context. If it has the hitcount will be incremented.
        /// </summary>
        /// <param name="activityId"></param>
        /// <param name="ruleId"></param>
        public void ApplyFaultInjectionRule(Guid activityId, string ruleId)
        {
            this.hitCountByRule.AddOrUpdate(
                ruleId,
                1,
                (id, hitcount) => hitcount + 1);

            this.actvityIdByRuleIdCount.AddOrUpdate(
                activityId,
                new ConcurrentDictionary<string, uint>(new List<KeyValuePair<string, uint>> {new KeyValuePair<string, uint>(ruleId, 1) }),
                (id, ruleIdCount) =>
                {
                    ruleIdCount.AddOrUpdate(
                        ruleId,
                        1,
                        (rule, count) => count + 1);
                    return ruleIdCount;
                });
            //this.requestIdByRuleId.TryAdd(requestId, ruleId);
        }

        /// <summary>
        /// Records the result of a rule evaluation. If a rule has not been evaluated, it will add it to the context. If it has, it will add the result to the list of results.
        /// </summary>
        /// <param name="activityId"></param>
        /// <param name="ruleEvaluationResult"></param>
        public void RecordFaultInjectionRuleEvaluation(Guid activityId, string ruleEvaluationResult)
        {
            this.activityIdByRuleEvaluation.AddOrUpdate(
                activityId,
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
        /// <returns>the hit count.</returns>
        public int GetFaultInjectionRuleHitCount(string ruleId)
        {
            if (this.hitCountByRule.TryGetValue(ruleId, out int hitCount))
            {
                return hitCount;
            }
            return 0;
        }

        /// <summary>
        /// Gets the rule id that was applied to a request.
        /// </summary>
        /// <param name="activityId"></param>
        /// <returns>the rule id.</returns>
        public string GetFaultInjectionRuleId(Guid activityId)
        {
            if (this.actvityIdByRuleIdCount.TryGetValue(activityId, out ConcurrentDictionary<string, uint> ruleIdCount))
            {
                return ruleIdCount.Keys.GetEnumerator().Current;
            }

            //if (this.requestIdByRuleId.TryGetValue(requestId, out string ruleId))
            //{
            //    return ruleId;
            //}
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
        /// Given a activity id, returns the list of rule evaluation results.
        /// </summary>
        /// <param name="activityId"></param>
        /// <returns>a list of the evaulation results</returns>
        public List<string> GetFaultInjectionRuleEvaluationResults(Guid activityId)
        {
            if (this.activityIdByRuleEvaluation.TryGetValue(activityId, out List<string> ruleEvaluationResults))
            {
                return ruleEvaluationResults;
            }
            return null;
        }
    }
}
