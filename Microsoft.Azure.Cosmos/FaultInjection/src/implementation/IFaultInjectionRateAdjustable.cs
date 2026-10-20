//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.FaultInjection
{
    /// <summary>
    /// Implemented by effective rules whose injection rate can be changed after the rule has been
    /// registered with a client. Kept separate from <see cref="IFaultInjectionRuleInternal"/> because
    /// that interface is public and cannot take new members without a binary breaking change.
    /// </summary>
    internal interface IFaultInjectionRateAdjustable
    {
        /// <summary>
        /// Sets the injection rate of the rule.
        /// </summary>
        /// <param name="injectionRate">the new injection rate, in the range (0, 1].</param>
        void SetInjectionRate(double injectionRate);
    }
}
