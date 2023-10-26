namespace Microsoft.Azure.Cosmos.FaultInjection.implementataion
{
    using System;
    using System.Collections.Concurrent;

    internal class FaultInjectionApplicationContext
    { 

        private readonly ConcurrentDictionary<Guid, (DateTime, Guid)> applicationsByRuleId;
        private readonly ConcurrentDictionary<Guid, (DateTime, Guid)> applicationsByActivityId;
        private readonly BlockingCollection<(DateTime, Guid, Guid)> values;
        
        public FaultInjectionApplicationContext()
        {
            this.applicationsByActivityId = new ConcurrentDictionary<Guid, (DateTime, Guid)>();
            this.applicationsByRuleId = new ConcurrentDictionary<Guid, (DateTime, Guid)>();
            this.values = new BlockingCollection<(DateTime, Guid, Guid)>();
        }      
        
        public void AddApplication(Guid ruleId, Guid activityId)
        {
            this.applicationsByRuleId.TryAdd(ruleId, (DateTime.UtcNow, activityId));
            this.applicationsByActivityId.TryAdd(activityId, (DateTime.UtcNow, ruleId));
            this.values.Add((DateTime.UtcNow, ruleId, activityId));
        }

        /// <summary>
        /// Gets all application of fault injection rules by DateTime, RuleId, ActivityId
        /// </summary>
        /// <returns><see cref="BlockingCollection{T}"/> of Application Time, RuleId, ActivityId</returns>
        public BlockingCollection<(DateTime, Guid, Guid)> GetValues()
        {
            return this.values;
        }

        public ConcurrentDictionary<Guid, (DateTime, Guid)> GetApplicationsByRuleId()
        {
            return this.applicationsByRuleId;
        }

        public ConcurrentDictionary<Guid, (DateTime, Guid)> GetApplicationsByActivityId()
        {
            return this.applicationsByActivityId;
        }
    }
}
