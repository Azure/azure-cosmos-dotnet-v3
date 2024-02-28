//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.FaultInjection
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Interface for a Fault Injection Rule
    /// </summary>
    public interface IFaultInjectionRuleInternal
    {
        /// <summary>
        /// Disables the rule
        /// </summary>
        void Disable();

        /// <summary>
        /// Enables the rule
        /// </summary>
        void Enable();

        /// <summary>
        /// Gets the physical addresses of the rule.
        /// </summary>
        /// <returns>a list of the physical addresses of the rule.</returns>
        List<Uri> GetAddresses();

        /// <summary>
        /// Gets the region endpoints of the rule.
        /// </summary>
        /// <returns>a list of the region endpoints.</returns>
        List<Uri> GetRegionEndpoints();

        /// <summary>
        /// A flag indicating if the rule is valid.
        /// </summary>
        /// <returns>the flag.</returns>
        bool IsValid();

        /// <summary>
        /// Gets the id of the rule.
        /// </summary>
        /// <returns>the id</returns>
        string GetId();

        /// <summary>
        /// Gets tht hit count of the rule.
        /// </summary>
        /// <returns>the hit count.</returns>
        long GetHitCount();

        /// <summary>
        /// Gets the connection type of the rule.
        /// </summary>
        /// <returns>the <see cref="FaultInjectionConnectionType"/>.</returns>
        FaultInjectionConnectionType GetConnectionType();
    }
}
