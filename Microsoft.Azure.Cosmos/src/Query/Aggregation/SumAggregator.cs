//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Aggregation
{
    using System;
    using System.Globalization;
    using Microsoft.Azure.Cosmos.Internal;

    internal sealed class SumAggregator : IAggregator
    {
        private double? value;
        private bool initialized;

        public SumAggregator()
        {
            this.value = 0;
        }

        public void Aggregate(object item)
        {
            if (Undefined.Value.Equals(item))
            {
                this.value = null;
                return;
            }

            if (this.value.HasValue)
            {
                this.value += Convert.ToDouble(item, CultureInfo.InvariantCulture);
            }

            this.initialized = true;
        }

        public object GetResult()
        {
            if (!this.initialized || !this.value.HasValue)
            {
                return Undefined.Value;
            }

            return this.value.Value;
        }
    }
}
