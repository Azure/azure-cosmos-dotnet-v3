namespace Azure.Cosmos.Tests
{
    using System.Collections.Generic;
    using System.Net;
    using Microsoft.Azure.Cosmos;

    public class MockAsyncPageable<T> : AsyncPageable<T>
    {
        private IReadOnlyList<Page<T>> pages;
        public MockAsyncPageable(IReadOnlyList<Page<T>> pages)
        {
            this.pages = pages;
        }

        public MockAsyncPageable(IReadOnlyList<T> items)
        {
            this.pages = new List<Page<T>>() { new CosmosPage<T>(items, new ResponseMessage(HttpStatusCode.OK), this.TryGetContinuationToken) };
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public override async IAsyncEnumerable<Page<T>> AsPages(string continuationToken = null, int? pageSizeHint = null)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            foreach(Page<T> page in this.pages)
            {
                yield return page;
            }
        }

        private bool TryGetContinuationToken(out string state)
        {
            state = null;
            return false;
        }
    }
}
