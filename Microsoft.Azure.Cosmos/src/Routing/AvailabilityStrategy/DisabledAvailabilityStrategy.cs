//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

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

#if PREVIEW
        public
#else
        internal
#endif
        override Task<ResponseMessage> ExecuteAvailablityStrategyAsync(Func<RequestMessage, CancellationToken, Task<ResponseMessage>> sender, CosmosClient client, RequestMessage requestMessage, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}