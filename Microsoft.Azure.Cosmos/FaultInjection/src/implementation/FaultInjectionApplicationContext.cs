//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.FaultInjection
{
    using System;
    using System.Collections.Concurrent;

    /// <summary>
    /// Provides tracking and observability for fault injection rule executions, 
    /// including lookups by rule ID and activity ID.
    /// </summary>
    public class FaultInjectionApplicationContext
    { 

        private readonly ConcurrentDictionary<string, List<(DateTime, Guid)>> executionsByRuleId;
        private readonly ConcurrentDictionary<Guid, List<(DateTime, string)>> executionsByActivityId;
        private readonly BlockingCollection<(DateTime, string, Guid)> values;
        
        public FaultInjectionApplicationContext()
        {
            this.executionsByActivityId = new ConcurrentDictionary<Guid, List<(DateTime, string)>>();
            this.executionsByRuleId = new ConcurrentDictionary<string, List<(DateTime, Guid)>>();
            this.values = new BlockingCollection<(DateTime, string, Guid)>();
        }      
        
        internal void AddRuleExecution(string ruleId, Guid activityId)
        {
            if(!this.executionsByRuleId.TryAdd(ruleId, new List<(DateTime, Guid)>() { (DateTime.UtcNow, activityId) }))
            {
                this.executionsByRuleId[ruleId].Add((DateTime.UtcNow, activityId));
            }

            if (!this.executionsByActivityId.TryAdd(activityId, new List<(DateTime, string)>() { (DateTime.UtcNow, ruleId) }))
            {
                this.executionsByActivityId[activityId].Add((DateTime.UtcNow, ruleId));
            }

            this.values.Add((DateTime.UtcNow, ruleId, activityId));
        }

        /// <summary>
        /// Gets all execution of fault injection rules by DateTime, RuleId, ActivityId
        /// </summary>
        /// <returns><see cref="BlockingCollection{T}"/> of Execution Time, RuleId, ActivityId</returns>
        public BlockingCollection<(DateTime, string, Guid)> GetAllRuleExecutions()
        {
            return this.values;
        }

        /// <summary>
        /// Gets all rule executions indexed by rule ID.
        /// </summary>
        /// <returns>A <see cref="ConcurrentDictionary{TKey, TValue}"/> mapping rule IDs to their executions.</returns>
        public ConcurrentDictionary<string, List<(DateTime, Guid)>> GetAllRuleExecutionsByRuleId()
        {
            return this.executionsByRuleId;
        }

        /// <summary>
        /// Gets all rule executions indexed by activity ID.
        /// </summary>
        /// <returns>A <see cref="ConcurrentDictionary{TKey, TValue}"/> mapping activity IDs to their executions.</returns>
        public ConcurrentDictionary<Guid, List<(DateTime, string)>> GetAllRuleExecutionsByActivityId()
        {
            return this.executionsByActivityId;
        }

        /// <summary>
        /// Tries to get rule executions for the given rule ID.
        /// </summary>
        /// <param name="ruleId">The rule ID to look up.</param>
        /// <param name="execution">The list of executions for the rule, or an empty list if not found.</param>
        /// <returns>True if executions were found, false otherwise.</returns>
        public bool TryGetRuleExecutionsByRuleId(string ruleId, out List<(DateTime, Guid)> execution)
        {
            if (this.executionsByRuleId.TryGetValue(ruleId, out List<(DateTime, Guid)>? ruleExecutions))
            {
                execution = ruleExecutions;
                return true;
            }

            execution = new List<(DateTime, Guid)>();
            return false;
        }

        /// <summary>
        /// Gets the fault injection rule id for the given activity id
        /// If multible FaultInjectionRules are applied to the same activity, the first rule applied will be returned
        /// </summary>
        /// <returns>the fault injection rule id</returns>
        public bool TryGetRuleExecutionByActivityId(Guid activityId, out (DateTime, string) execution)
        {
            if (this.executionsByActivityId.TryGetValue(activityId, out List<(DateTime, string)>? ruleExecutions))
            {
                execution = ruleExecutions[0];
                return true;
            }

            execution = (DateTime.MinValue, string.Empty);
            return false;
        }
    }
}
