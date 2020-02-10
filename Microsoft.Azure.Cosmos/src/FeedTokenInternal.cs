// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Threading.Tasks;

    internal abstract class FeedTokenInternal : FeedToken
    {
        public abstract void FillHeaders(RequestMessage request);

        public abstract string GetContinuation();

        public static bool TryParse(
            string toStringValue,
            out FeedToken parsedToken)
        {
            parsedToken = null;
            return false;
        }

        public virtual Task SplitAsync() => Task.CompletedTask;
    }
}
