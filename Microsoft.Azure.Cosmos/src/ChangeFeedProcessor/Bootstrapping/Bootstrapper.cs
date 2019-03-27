//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  Licensed under the MIT license.
//----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeedProcessor.Bootstrapping
{
    using System.Threading.Tasks;

    internal abstract class Bootstrapper
    {
        public abstract Task InitializeAsync();
    }
}