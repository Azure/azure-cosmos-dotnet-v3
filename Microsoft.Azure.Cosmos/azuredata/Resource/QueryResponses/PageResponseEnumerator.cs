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
        public static FuncPageable<T> CreateEnumerable<T>(Func<string, Page<T>> pageFunc)
        {
            return new FuncPageable<T>((continuationToken, pageSizeHint) => pageFunc(continuationToken));
        }

        public static FuncPageable<T> CreateEnumerable<T>(Func<string, int?, Page<T>> pageFunc)
        {
            return new FuncPageable<T>(pageFunc);
        }

        public static AsyncPageable<T> CreateAsyncEnumerable<T>(Func<string, Task<Page<T>>> pageFunc)
        {
            return new FuncAsyncPageable<T>((continuationToken, pageSizeHint) => pageFunc(continuationToken));
        }

        public static AsyncPageable<T> CreateAsyncEnumerable<T>(Func<string, int?, Task<Page<T>>> pageFunc)
        {
            return new FuncAsyncPageable<T>(pageFunc);
        }

        internal class FuncAsyncPageable<T> : AsyncPageable<T>
        {
            private readonly Func<string, int?, Task<Page<T>>> pageFunc;

            public FuncAsyncPageable(Func<string, int?, Task<Page<T>>> pageFunc)
            {
                this.pageFunc = pageFunc;
            }

            public override async IAsyncEnumerable<Page<T>> AsPages(string continuationToken = default, int? pageSizeHint = default)
            {
                do
                {
                    Page<T> pageResponse = await this.pageFunc(continuationToken, pageSizeHint).ConfigureAwait(false);
                    yield return pageResponse;
                    continuationToken = pageResponse.ContinuationToken;
                }
                while (continuationToken != null);
            }
        }

        internal class FuncPageable<T> : Pageable<T>
        {
            private readonly Func<string, int?, Page<T>> pageFunc;

            public FuncPageable(Func<string, int?, Page<T>> pageFunc)
            {
                this.pageFunc = pageFunc;
            }

            public override IEnumerable<Page<T>> AsPages(string continuationToken = default, int? pageSizeHint = default)
            {
                do
                {
                    Page<T> pageResponse = this.pageFunc(continuationToken, pageSizeHint);
                    yield return pageResponse;
                    continuationToken = pageResponse.ContinuationToken;
                }
                while (continuationToken != null);
            }
        }
    }
}