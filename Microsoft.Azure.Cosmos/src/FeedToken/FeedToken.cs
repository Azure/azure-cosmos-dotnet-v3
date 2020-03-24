// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    internal abstract class FeedToken
    {
        private static IReadOnlyList<FeedTokenEPKRange> EmptyScale = new List<FeedTokenEPKRange>();

        public readonly string ContainerRid;

        protected FeedToken()
        {
        }

        public FeedToken(string containerRid)
        {
            this.ContainerRid = containerRid;
        }

        public virtual Task<bool> ShouldRetryAsync(
            ContainerCore containerCore,
            ResponseMessage responseMessage,
            CancellationToken cancellationToken) => Task.FromResult(false);

        public virtual IReadOnlyList<FeedTokenEPKRange> Scale() => FeedToken.EmptyScale;
    }
}
