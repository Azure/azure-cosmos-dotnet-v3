//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Bootstrapping
{
    using System.Threading.Tasks;

    internal abstract class Bootstrapper
    {
        public abstract Task InitializeAsync();
    }
}