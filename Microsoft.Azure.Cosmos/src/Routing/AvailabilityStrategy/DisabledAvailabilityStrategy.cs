//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    /// <summary>
    /// A Disabled availability strategy that does not do anything. Used for overriding the default global availability strategy.
    /// </summary>
#if PREVIEW
    public 
#else
    internal
#endif
    class DisabledAvailabilityStrategy : AvailabilityStrategy
    {
        internal override bool Enabled()
        {
            return false;
        }
    }
}