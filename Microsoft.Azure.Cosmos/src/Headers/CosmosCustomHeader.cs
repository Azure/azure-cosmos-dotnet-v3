//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;

    internal sealed class CosmosCustomHeader
    {
        private readonly Func<string> valueGetter;
        private readonly Action<string> valueSetter;
        public CosmosCustomHeader(Func<string> getter, Action<string> setter)
        {
            this.valueGetter = getter ?? throw new ArgumentNullException(nameof(getter));
            this.valueSetter = setter ?? throw new ArgumentNullException(nameof(setter));
        }

        public void Set(string value)
        {
            this.valueSetter(value);
        }

        public string Get()
        {
            return this.valueGetter();
        }
    }
}