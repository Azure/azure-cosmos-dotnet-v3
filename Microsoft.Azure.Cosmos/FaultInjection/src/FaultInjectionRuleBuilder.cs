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
        private readonly IFaultInjectionResult result;
        private readonly FaultInjectionCondition condition;
        private TimeSpan duration = TimeSpan.MaxValue;
        private TimeSpan startDelay;
        private int hitLimit;
        private bool enabled = true;

        /// <summary>
        /// Initializes a new instance of the <see cref="FaultInjectionRuleBuilder"/> class.
        /// Sets the id of the rule.
        /// </summary>
        /// <param name="id">The id of the rule. Cannot be null or empty</param>
        /// <param name="condition">the <see cref="FaultInjectionCondition"/></param>
        /// <param name="result">the <see cref="IFaultInjectionResult"/> Cannot be null</param>
        public FaultInjectionRuleBuilder(string id, FaultInjectionCondition condition, IFaultInjectionResult result)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id), "Argument 'id' cannot be null or empty.");
            }

            this.id = id;
            this.condition = condition ?? throw new ArgumentNullException(nameof(condition), "Argument 'condition' cannot be null.");
            this.result = result ?? throw new ArgumentNullException(nameof(result), "Argument 'result' cannot be null.");
        }

        /// <summary>
        /// Set the effective duration of the rule. The rule will not be applied after the duration has elapsed.
        /// By default, the duration will be until the end of the application.
        /// The duration starts at the time of the rule creation, not the time of when the rule is enabled (rule is enabled by default).
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
            if (this.condition.GetConnectionType() == FaultInjectionConnectionType.Gateway)
            {
                this.ValidateGatewayConnection();
            }

            if (this.condition.GetConnectionType() == FaultInjectionConnectionType.Direct)
            {
                this.ValidateDirectConnection();
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

        private void ValidateDirectConnection()
        {
            if (this.result == null)
            {
                throw new ArgumentNullException(nameof(this.result), "Argument 'result' cannot be null.");
            }

            FaultInjectionServerErrorResult? serverErrorResult = this.result as FaultInjectionServerErrorResult;

            if (serverErrorResult?.GetServerErrorType() == FaultInjectionServerErrorType.DatabaseAccountNotFound)
            {
                throw new ArgumentException("DatabaseAccountNotFound error type is not supported for Direct connection type.");
            }
        }

        private void ValidateGatewayConnection()
        {
            if (this.result == null)
            {
                throw new ArgumentNullException(nameof(this.result), "Argument 'result' cannot be null.");
            }

            FaultInjectionServerErrorResult? serverErrorResult = this.result as FaultInjectionServerErrorResult;

            if (serverErrorResult?.GetServerErrorType() == FaultInjectionServerErrorType.Gone)
            {
                throw new ArgumentException("Gone error type is not supported for Gateway connection type.");
            }

            if (this.condition.IsMetadataOperationType())
            {
                if (serverErrorResult?.GetServerErrorType() != FaultInjectionServerErrorType.TooManyRequests
                    && serverErrorResult?.GetServerErrorType() != FaultInjectionServerErrorType.ResponseDelay
                    && serverErrorResult?.GetServerErrorType() != FaultInjectionServerErrorType.SendDelay)
                {
                    throw new ArgumentException($"{serverErrorResult?.GetServerErrorType()} is not supported for metadata requests.");
                }
            }
        }
    }
}
