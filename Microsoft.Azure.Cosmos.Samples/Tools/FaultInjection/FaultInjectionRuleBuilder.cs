//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.FaultInjection
{
    using System;

    /// <summary>
    /// Builds a <see cref="FaultInjectionRule"/>.
    /// </summary>
    public sealed class FaultInjectionRuleBuilder
    {
        private readonly string id;
        private IFaultInjectionResult? result;
        private FaultInjectionCondition? condition;
        private TimeSpan duration;
        private TimeSpan startDelay;
        private int hitLimit;
        private bool enabled = true;

        /// <summary>
        /// Initializes a new instance of the <see cref="FaultInjectionRuleBuilder"/> class.
        /// Sets the id of the rule.
        /// </summary>
        /// <param name="id">The id of the rule. Cannot be null or empty</param>
        public FaultInjectionRuleBuilder(string id)
        {
            if (id == null || id == string.Empty)
            {
                throw new ArgumentNullException(nameof(id), "Argument 'id' cannot be null or empty.");
            }
            this.id = id;
        }

        /// <summary>
        /// Sets the result of the rule.
        /// </summary>
        /// <param name="result">the <see cref="IFaultInjectionResult"/></param>
        /// <returns>the <see cref="FaultInjectionRuleBuilder"/>.</returns>
        public FaultInjectionRuleBuilder WithResult(IFaultInjectionResult result)
        {
            this.result = result;
            return this;
        }

        /// <summary>
        /// Sets the condition of the rule. Rule will only be applied if the condition is met.
        /// </summary>
        /// <param name="condition">the <see cref="FaultInjectionCondition"/></param>
        /// <returns>the <see cref="FaultInjectionRuleBuilder"/>.</returns>
        public FaultInjectionRuleBuilder WithCondition(FaultInjectionCondition condition)
        {          
            this.condition = condition;
            return this;
        }

        /// <summary>
        /// Set the effective duration of the rule. The rule will not be applied after the duration has elapsed.
        /// By default, the duration will be until the end of the application.
        /// </summary>
        /// <param name="duration">the effective duration.</param>
        /// <returns>the <see cref="FaultInjectionRuleBuilder"/>.</returns>
        public FaultInjectionRuleBuilder WithDuration(TimeSpan duration)
        {
            this.duration = duration;
            return this;
        }

        /// <summary>
        /// Sets the start delay of the rule.
        /// </summary>
        /// <param name="startDelay">the time before the rule will become effective.</param>
        /// <returns>the <see cref="FaultInjectionRuleBuilder"/>.</returns>
        public FaultInjectionRuleBuilder WithStartDelay(TimeSpan startDelay)
        {
            this.startDelay = startDelay;
            return this;
        }

        /// <summary>
        /// Sets the maximum number of times the rule can be applied.
        /// </summary>
        /// <param name="hitLimit">the hit limit.</param>
        /// <returns>the <see cref="FaultInjectionRuleBuilder"/>.</returns>
        public FaultInjectionRuleBuilder WithHitLimit(int hitLimit)
        {
            if (hitLimit <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(hitLimit), "Argument 'hitLimit' must be greater than 0.");
            }

            this.hitLimit = hitLimit;
            return this;
        }

        /// <summary>
        /// Flag to indicate whether the rule is enabled. The rule will not be applied if it is disabled.
        /// A rule can be enabled or disabled multiple times.
        /// The default value is true.
        /// </summary>
        /// <param name="enabled">flag to indicate whether the rule is enabled.</param>
        /// <returns>the <see cref="FaultInjectionRuleBuilder"/>.</returns>
        public FaultInjectionRuleBuilder IsEnabled(bool enabled)
        {
            this.enabled = enabled;
            return this;
        }

        /// <summary>
        /// Creates a new <see cref="FaultInjectionRule"/>.
        /// </summary>
        /// <returns>the <see cref="FaultInjectionRule"/>.</returns>
        public FaultInjectionRule Build()
        {
            if (this.result == null)
            {
                throw new ArgumentNullException(nameof(this.result), "Argument 'result' cannot be null.");
            }

            if (this.condition == null)
            {
                throw new ArgumentNullException(nameof(this.condition), "Argument 'condition' cannot be null.");
            }

            return new FaultInjectionRule(
                this.result,
                this.condition,
                this.id,
                this.duration,
                this.startDelay,
                this.hitLimit,
                this.enabled);
        }

    }
}
