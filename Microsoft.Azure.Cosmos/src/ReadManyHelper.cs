//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    internal abstract class ReadManyHelper : IDisposable
    {
        public abstract Task<ResponseMessage> ExecuteReadManyRequestAsync(CancellationToken cancellationToken = default);

        public abstract Task<FeedResponse<T>> ExecuteReadManyRequestAsync<T>(CancellationToken cancellationToken = default);

        public abstract void Dispose();
    }
}
