//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos
{
    using System;
    using System.Collections.Generic;

    internal class CosmosPage<T> : Page<T>
    {
        internal delegate bool TryGetContinuationToken(out string state);
        private readonly IReadOnlyList<T> values;
        private readonly Response response;
        private readonly TryGetContinuationToken tryGetContinuationToken;

        public CosmosPage(
            IReadOnlyList<T> values,
            Response response,
            TryGetContinuationToken tryGetContinuationToken)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            if (response == null)
            {
                throw new ArgumentNullException(nameof(response));
            }

            if (tryGetContinuationToken == null)
            {
                throw new ArgumentNullException(nameof(tryGetContinuationToken));
            }

            this.values = values;
            this.response = response;
            this.tryGetContinuationToken = tryGetContinuationToken;
        }

        public override IReadOnlyList<T> Values => this.values;

        public override string ContinuationToken
        {
            get
            {
                if (this.tryGetContinuationToken(out string continuationToken))
                {
                    return continuationToken;
                }

                return null;
            }
        }

        public override Response GetRawResponse() => this.response;
    }
}
