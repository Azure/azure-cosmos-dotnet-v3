namespace Microsoft.Azure.Cosmos.FaultInjection
{
    using System;
    using System.Collections.Concurrent;

    public class FaultInjectionApplicationContext
    { 

        private readonly ConcurrentDictionary<string, List<(DateTime, Guid)>> executionsByRuleId;
        private readonly ConcurrentDictionary<Guid, (DateTime, string)> executionsByActivityId;
        private readonly BlockingCollection<(DateTime, string, Guid)> values;
        
        public FaultInjectionApplicationContext()
        {
            this.executionsByActivityId = new ConcurrentDictionary<Guid, (DateTime, string)>();
            this.executionsByRuleId = new ConcurrentDictionary<string, List<(DateTime, Guid)>>();
            this.values = new BlockingCollection<(DateTime, string, Guid)>();
        }      
        
        public void AddRuleExecution(string ruleId, Guid activityId)
        {
            if (!this.executionsByRuleId.ContainsKey(ruleId))
            {
                this.executionsByRuleId.TryAdd(ruleId, new List<(DateTime, Guid)>() { (DateTime.UtcNow, activityId)});
            }
            else
            {
                this.executionsByRuleId[ruleId].Add((DateTime.UtcNow, activityId));
            }

            this.executionsByActivityId.TryAdd(activityId, (DateTime.UtcNow, ruleId));
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

        public ConcurrentDictionary<string, List<(DateTime, Guid)>> GetAllRuleExecutionsByRuleId()
        {
            return this.executionsByRuleId;
        }

        public ConcurrentDictionary<Guid, (DateTime, string)> GetAllRuleExecutionsByActivityId()
        {
            return this.executionsByActivityId;
        }

        public List<(DateTime, Guid)>? GetRuleExecutionsByRuleId(string ruleId)
        {
            if (this.executionsByRuleId.TryGetValue(ruleId, out List<(DateTime, Guid)>? execution))
            {
                return execution;
            }

            return null;
        }

        public (DateTime, string)? GetRuleExecutionsByActivityId(Guid activityId)
        {
            if (this.executionsByActivityId.TryGetValue(activityId, out (DateTime, string) execution))
            {
                return execution;
            }

            return null;
        }
    }
}
