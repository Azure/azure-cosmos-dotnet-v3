namespace Microsoft.Azure.Cosmos.FaultInjection.implementataion
{
    using System;
    using System.Collections.Concurrent;

    internal class FaultInjectionApplicationContext
    { 

        private readonly ConcurrentDictionary<string, List<(DateTime, Guid)>> applicationsByRuleId;
        private readonly ConcurrentDictionary<Guid, (DateTime, string)> applicationsByActivityId;
        private readonly BlockingCollection<(DateTime, string, Guid)> values;
        
        public FaultInjectionApplicationContext()
        {
            this.applicationsByActivityId = new ConcurrentDictionary<Guid, (DateTime, string)>();
            this.applicationsByRuleId = new ConcurrentDictionary<string, List<(DateTime, Guid)>>();
            this.values = new BlockingCollection<(DateTime, string, Guid)>();
        }      
        
        public void AddRuleApplication(string ruleId, Guid activityId)
        {
            if (!this.applicationsByRuleId.ContainsKey(ruleId))
            {
                this.applicationsByRuleId.TryAdd(ruleId, new List<(DateTime, Guid)>() { (DateTime.UtcNow, activityId)});
            }
            else
            {
                this.applicationsByRuleId[ruleId].Add((DateTime.UtcNow, activityId));
            }

            this.applicationsByActivityId.TryAdd(activityId, (DateTime.UtcNow, ruleId));
            this.values.Add((DateTime.UtcNow, ruleId, activityId));
        }

        /// <summary>
        /// Gets all application of fault injection rules by DateTime, RuleId, ActivityId
        /// </summary>
        /// <returns><see cref="BlockingCollection{T}"/> of Application Time, RuleId, ActivityId</returns>
        public BlockingCollection<(DateTime, string, Guid)> GetAllApplications()
        {
            return this.values;
        }

        public ConcurrentDictionary<string, List<(DateTime, Guid)>> GetApplicationsByRuleId()
        {
            return this.applicationsByRuleId;
        }

        public ConcurrentDictionary<Guid, (DateTime, string)> GetApplicationsByActivityId()
        {
            return this.applicationsByActivityId;
        }

        public List<(DateTime, Guid)>? GetApplicationByRuleId(string ruleId)
        {
            if (this.applicationsByRuleId.TryGetValue(ruleId, out List<(DateTime, Guid)>? application))
            {
                return application;
            }

            return null;
        }

        public (DateTime, string)? GetApplicationByActivityId(Guid activityId)
        {
            if (this.applicationsByActivityId.TryGetValue(activityId, out (DateTime, string) application))
            {
                return application;
            }

            return null;
        }
    }
}
