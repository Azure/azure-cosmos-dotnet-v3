namespace Microsoft.Azure.Cosmos.Pagination
{
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

    internal sealed class CrossPartitionAsyncPaginator : IAsyncEnumerator<TryCatch<Page>>
    {
    }
}
