// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Pagination
{
    internal interface IChangeFeedStateVisitor<TInput, TOutput>
    {
        TOutput Visit(ChangeFeedStateBeginning changeFeedStateBeginning, TInput input);
        TOutput Visit(ChangeFeedStateTime changeFeedStateTime, TInput input);
        TOutput Visit(ChangeFeedStateContinuation changeFeedStateContinuation, TInput input);
        TOutput Visit(ChangeFeedStateNow changeFeedStateNow, TInput input);
    }
}
