//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedProcessing;
    using Moq;
    using Newtonsoft.Json.Linq;

    static class ChangeFeedObserverExtensions
    {
        private static readonly CosmosJsonDotNetSerializer serializer = new CosmosJsonDotNetSerializer();

        public static Task ProcessChangesAsync<T>(this ChangeFeedObserver observer, ChangeFeedObserverContext context, IReadOnlyList<T> documents, CancellationToken cancellationToken)
        {
            try
            {
                documents = documents.Cast<T>().ToList();
            }
            catch (NullReferenceException)
            {
                documents = new T[documents.Count];
            }
            return observer.ProcessChangesAsync(context, serializer.ToStream(new { Documents = documents.Cast<object>() }), cancellationToken);
        }
    }
}
