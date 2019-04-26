//-----------------------------------------------------------------------
// <copyright file="CosmosInt64.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;
    using Microsoft.Azure.Cosmos.Json;

    internal abstract partial class CosmosInt64 : CosmosNumber
    {
        protected CosmosInt64()
            : base(CosmosNumberType.Int64)
        {
        }

        public override bool IsFloatingPoint => false;

        public override bool IsInteger => true;

        public static CosmosNumber Create(
            IJsonNavigator jsonNavigator,
            IJsonNavigatorNode jsonNavigatorNode)
        {
            return new LazyCosmosInt64(jsonNavigator, jsonNavigatorNode);
        }

        public static CosmosNumber Create(long number)
        {
            return new EagerCosmosInt64(number);
        }

        public override double? AsFloatingPoint()
        {
            return (double)this.GetValue();
        }

        public override long? AsInteger()
        {
            return this.GetValue();
        }

        public override void WriteTo(IJsonWriter jsonWriter)
        {
            if (jsonWriter == null)
            {
                throw new ArgumentNullException($"{nameof(jsonWriter)}");
            }

            jsonWriter.WriteInt64Value(this.GetValue());
        }

        protected abstract long GetValue();
    }
}
