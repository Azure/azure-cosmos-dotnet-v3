// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    internal static class PageResponseEnumerator
    {
        public static FuncPageable<T> CreateEnumerable<T>(Func<string, (Page<T>, bool)> pageFunc)
        {
            return new FuncPageable<T>((continuationToken, pageSizeHint) => pageFunc(continuationToken));
        }

        public static FuncPageable<T> CreateEnumerable<T>(Func<string, int?, (Page<T>, bool)> pageFunc)
        {
            return new FuncPageable<T>(pageFunc);
        }

        public static AsyncPageable<T> CreateAsyncPageable<T>(Func<string, Task<(Page<T>, bool)>> pageCreator)
        {
            return new FuncAsyncPageable<T>((continuationToken, pageSizeHint) => pageCreator(continuationToken));
        }

        public static AsyncPageable<T> CreateAsyncPageable<T>(Func<string, int?, Task<(Page<T>, bool)>> pageCreator)
        {
            return new FuncAsyncPageable<T>(pageCreator);
        }

        internal class FuncAsyncPageable<T> : AsyncPageable<T>
        {
            private readonly Func<string, int?, Task<(Page<T>, bool)>> pageFunc;

            public FuncAsyncPageable(Func<string, int?, Task<(Page<T>, bool)>> pageFunc)
            {
                this.pageFunc = pageFunc;
            }

            public override async IAsyncEnumerable<Page<T>> AsPages(string continuationToken = default, int? pageSizeHint = default)
            {
                bool hasMoreResults = true;
                do
                {
                    (Page<T> pageResponse, bool pageHasMoreResults) = await this.pageFunc(continuationToken, pageSizeHint).ConfigureAwait(false);
                    yield return pageResponse;
                    hasMoreResults = pageHasMoreResults;
                }
                while (hasMoreResults);
            }
        }

        internal class FuncPageable<T> : Pageable<T>
        {
            private readonly Func<string, int?, (Page<T>, bool)> pageFunc;

            public FuncPageable(Func<string, int?, (Page<T>, bool)> pageFunc)
            {
                this.pageFunc = pageFunc;
            }

            public override IEnumerable<Page<T>> AsPages(string continuationToken = default, int? pageSizeHint = default)
            {
                bool hasMoreResults = true;
                do
                {
                    (Page<T> pageResponse, bool pageHasMoreResults) = this.pageFunc(continuationToken, pageSizeHint);
                    yield return pageResponse;
                    hasMoreResults = pageHasMoreResults;
                }
                while (hasMoreResults);
            }
        }
    }
}