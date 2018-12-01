//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using System.Linq;

    internal static class FeedResponseBinder
    {
        //Helper to materialize Any IResourceFeed<T> from IResourceFeed<dynamic> as long as source
        //conversion from dynamic to T.

        //This method is invoked via expression as part of dynamic binding of cast operator.
        public static FeedResponse<T> Convert<T>(FeedResponse<dynamic> dynamicFeed)
        {
            if (typeof(T) == typeof(object))
            {
                return (FeedResponse<T>)(object)dynamicFeed;
            }
            IList<T> result = new List<T>();

            foreach (T item in dynamicFeed)
            {
                result.Add(item);
            }

            return new FeedResponse<T>(
                result,
                dynamicFeed.Count,
                dynamicFeed.Headers,
                dynamicFeed.UseETagAsContinuation,
                dynamicFeed.QueryMetrics,
                dynamicFeed.RequestStatistics,
                responseLengthBytes: dynamicFeed.ResponseLengthBytes);
        }

        public static IQueryable<T> AsQueryable<T>(FeedResponse<dynamic> dynamicFeed)
        {
            FeedResponse<T> response = FeedResponseBinder.Convert<T>(dynamicFeed);
            return response.AsQueryable<T>();
        }
    }
}
