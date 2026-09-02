//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.FaultInjection
{
    using System;
    using System.Collections.Generic;
    using System.Threading;

    /// <summary>
    /// Fault Injector Rule
    /// </summary>
    public sealed class FaultInjectionRule
    {
        private readonly IFaultInjectionResult result;
        private readonly FaultInjectionCondition condition;
        private readonly string id;
        private readonly TimeSpan duration;
        private readonly TimeSpan startDelay;
        private readonly int hitLimit;
        private volatile bool enabled;
        private double injectionRate;
        private volatile IFaultInjectionRuleInternal? effectiveRule;

        /// <summary>
        /// Initializes a new instance of the <see cref="FaultInjectionRule"/> class.
        /// </summary>
        /// <param name="result">the <see cref="IFaultInjectionResult"/> of the rule.</param>
        /// <param name="condition">the <see cref="FaultInjectionCondition"/> of the rule.</param>
        /// <param name="id">the id of the rule</param>
        /// <param name="duration">the lifetime of the rule. The duration starts at the time of the rule creation, not the time of when the rule is enabled (rule is enabled by default).</param>
        /// <param name="startDelay">the start delay of the rule. </param>
        /// <param name="hitLimit">the maximum number of times the rule can be applied.</param>
        /// <param name="enabled">whether the rule is enabled.</param>
        public FaultInjectionRule(
            IFaultInjectionResult result,
            FaultInjectionCondition condition,
            string id,
            TimeSpan duration,
            TimeSpan startDelay,
            int hitLimit,
            bool enabled)
        {         
            this.result = result ?? throw new ArgumentNullException(nameof(result), "Argument 'result' cannot be null.");
            this.condition = condition;
            this.id = id;
            this.duration = duration;
            this.startDelay = startDelay;
            this.hitLimit = hitLimit;
            this.enabled = enabled;
            this.injectionRate = this.result switch
            {
                FaultInjectionServerErrorResult serverErrorResult => serverErrorResult.GetInjectionRate(),
                FaultInjectionCustomServerErrorResult customServerErrorResult => customServerErrorResult.GetInjectionRate(),
                _ => 1,
            };
        }

        /// <summary>
        /// The fault injection result.
        /// </summary>
        /// <remarks>
        /// The returned result carries the values the rule was built with. It is not updated by
        /// <see cref="SetInjectionRate"/>; use <see cref="GetInjectionRate"/> for the rate in effect.
        /// </remarks>
        /// <returns>the <see cref="IFaultInjectionResult"/>.</returns>
        public IFaultInjectionResult GetResult()
        {
            return this.result;
        }

        /// <summary>
        /// Gets the fault injection condition.
        /// </summary>
        /// <returns>the <see cref="FaultInjectionCondition"/>.</returns>
        public FaultInjectionCondition GetCondition()
        {
            return this.condition;
        }

        /// <summary>
        /// Gets the effictive life span of the fault injection rule.
        /// </summary>
        /// <returns>a <see cref="TimeSpan"/> representing the duration.</returns>
        public TimeSpan GetDuration()
        {
            return this.duration;
        }

        /// <summary>
        /// Gets the start delay of the fault injection rule.
        /// </summary>
        /// <returns>a <see cref="TimeSpan"/> representing the start delay.</returns>
        public TimeSpan GetStartDelay()
        {
            return this.startDelay;
        }

        /// <summary>
        /// The hit limit of the fault injection rule.
        /// </summary>
        /// <returns>the hit count.</returns>
        public int GetHitLimit()
        {
            return this.hitLimit;
        }

        /// <summary>
        /// Gets the id of the fault injection rule.
        /// </summary>
        /// <returns>the id.</returns>
        public string GetId()
        {
            return this.id;
        }

        /// <summary>
        /// Gets the flag to indicate whether the rule is enabled.
        /// </summary>
        /// <returns>the flag to indicate whether the rule is enabled.</returns>
        public bool IsEnabled()
        {
            return this.enabled;
        }

        /// <summary>
        /// Disables the fault injection rule.
        /// </summary>
        public void Disable()
        {
            this.enabled = false;

            this.effectiveRule?.Disable();
        }

        /// <summary>
        /// Enables the fault injection rule.
        /// </summary>
        public void Enable()
        {
            this.enabled = true;

            this.effectiveRule?.Enable();
        }

        /// <summary>
        /// Gets the injection rate of the fault injection rule, which is the probability that the
        /// fault is injected into a request that already matches the rule's condition.
        /// Connection error rules have no injection rate and always report 1; they use the connection
        /// threshold configured through <c>WithThreshold</c> instead.
        /// </summary>
        /// <returns>the injection rate, in the range (0, 1].</returns>
        public double GetInjectionRate()
        {
            return Volatile.Read(ref this.injectionRate);
        }

        /// <summary>
        /// Updates the injection rate of the fault injection rule. Like <see cref="Enable"/> and
        /// <see cref="Disable"/>, this can be called at any time, including after the rule has been
        /// registered with a <see cref="CosmosClient"/>. The new rate takes effect on the next
        /// request evaluated by the rule.
        /// Only server error and custom server error rules have an injection rate. Calling this on a
        /// connection error rule throws, because such rules use the connection threshold configured
        /// through <c>WithThreshold</c>, which is fixed when the rule is built.
        /// </summary>
        /// <param name="injectionRate">the new injection rate, in the range (0, 1].</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when injectionRate is not within the valid range, or is not a number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the rule does not have an injection rate.</exception>
        public void SetInjectionRate(double injectionRate)
        {
            if (this.result is not FaultInjectionServerErrorResult
                && this.result is not FaultInjectionCustomServerErrorResult)
            {
                throw new InvalidOperationException(
                    $"'{nameof(SetInjectionRate)}' is only supported for server error and custom server error rules. "
                    + $"Rule '{this.id}' uses a '{this.result.GetType().Name}', which has no injection rate.");
            }

            // Negated so that double.NaN, which compares false against every bound, is rejected.
            if (!(injectionRate > 0 && injectionRate <= 1))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(injectionRate),
                    $"Argument '{nameof(injectionRate)}' must be within the range (0, 1].");
            }

            Volatile.Write(ref this.injectionRate, injectionRate);

            FaultInjectionRule.ApplyInjectionRate(this.effectiveRule, injectionRate);
        }

        /// <summary>
        /// Gets the count of how many times the rule has been applied.
        /// </summary>
        /// <returns>the hit count.</returns>
        public long GetHitCount()
        {
            return this.effectiveRule == null ? 0 : this.effectiveRule.GetHitCount();
        }

        /// <summary>
        /// Get the physical addresses of the fault injection rule.
        /// </summary>
        /// <returns>a List of Uri's of the physical addresses</returns>
        public List<Uri> GetAddresses()
        {
            return this.effectiveRule?.GetAddresses() ?? new List<Uri> { };
        }

        /// <summary>
        /// Gets the region endpoints of the fault injection rule.
        /// </summary>
        /// <returns>a List of Uri's of the region endpoints</returns>
        public List<Uri> GetRegionEndpoints()
        {
            return this.effectiveRule?.GetRegionEndpoints() ?? new List<Uri> { };
        }

        /// <summary>
        /// Sets the effective fault injection rule.
        /// </summary>
        /// <param name="effectiveRule">the effective fault injection rule.</param>
        internal void SetEffectiveFaultInjectionRule(IFaultInjectionRuleInternal effectiveRule)
        {
            this.effectiveRule = effectiveRule;

            // Rule processing is asynchronous, so a rate set between the effective rule being built
            // and it being bound here would otherwise be lost.
            FaultInjectionRule.ApplyInjectionRate(effectiveRule, Volatile.Read(ref this.injectionRate));
        }

        private static void ApplyInjectionRate(IFaultInjectionRuleInternal? effectiveRule, double injectionRate)
        {
            if (effectiveRule is IFaultInjectionRateAdjustable rateAdjustableRule)
            {
                rateAdjustableRule.SetInjectionRate(injectionRate);
            }
        }

        /// <summary>
        /// Represents Fault Injection Rule as a string.
        /// </summary>
        /// <returns>the fault injection rule represented as a string.</returns>
        public override string ToString()
        {
            return string.Format(
                "FaultInjectionRule{{ id: {0}, result: {1}, condition: {2}, duration: {3}, startDelay: {4}, hitlimit: {5}, enabled: {6}, injectionRate: {7}}}",
                this.id,
                this.result,
                this.condition,
                this.duration,
                this.startDelay,
                this.hitLimit,
                this.enabled,
                this.GetInjectionRate());
        }
    }
}
