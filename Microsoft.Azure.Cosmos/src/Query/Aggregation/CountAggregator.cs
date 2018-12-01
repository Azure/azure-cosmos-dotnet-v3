//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Aggregation
{
    using System;
    using System.Globalization;

    internal sealed class CountAggregator : IAggregator
    {
        private long value;

        public void Aggregate(object item)
        {
            value += Convert.ToInt64(item, CultureInfo.InvariantCulture);
        }

        public object GetResult()
        {
            return value;
        }
    }
}