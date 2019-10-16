//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedProcessing;
    using Newtonsoft.Json;

    static class ChangeFeedObserverExtensions
    {
        class DocumentsWrapper<T>
        {
            private DocumentsWrapper(IEnumerable<T> documents)
            {
                this.Documents = documents;
            }
            public IEnumerable<T> Documents { get; }

            public static Stream ToStream(IEnumerable<T> documents)
            {
                return new CosmosJsonDotNetSerializer().ToStream(new DocumentsWrapper<T>(documents));
            }
        }

        public static Task ProcessChangesAsync<T>(this ChangeFeedObserver observer, ChangeFeedObserverContext context, IEnumerable<T> documents, CancellationToken cancellationToken)
        {
            return observer.ProcessChangesAsync(context, DocumentsWrapper<T>.ToStream(documents), cancellationToken);
        }
    }
}
