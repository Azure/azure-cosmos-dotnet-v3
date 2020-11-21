// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Pagination
{
    internal interface IChangeFeedStateVisitor<TInput>
    {
        void Visit(ChangeFeedStateBeginning changeFeedStateBeginning, TInput input);
        void Visit(ChangeFeedStateTime changeFeedStateTime, TInput input);
        void Visit(ChangeFeedStateContinuation changeFeedStateContinuation, TInput input);
        void Visit(ChangeFeedStateNow changeFeedStateNow, TInput input);
    }
}
