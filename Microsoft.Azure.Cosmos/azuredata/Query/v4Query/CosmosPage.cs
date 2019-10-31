//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos
{
    using System;
    using System.Collections.Generic;

    internal class CosmosPage<T> : Page<T>
    {
        private readonly IReadOnlyList<T> values;
        private readonly Response response;

        public CosmosPage(
            IReadOnlyList<T> values,
            Response response)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            if (response == null)
            {
                throw new ArgumentNullException(nameof(response));
            }

            this.values = values;
            this.response = response;
        }

        public override IReadOnlyList<T> Values => this.values;

        public override string ContinuationToken => this.response.Headers.GetContinuationToken();

        public override Response GetRawResponse() => this.response;
    }
}
