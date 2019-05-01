//-----------------------------------------------------------------------
// <copyright file="CosmosFloat64.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;
    using Microsoft.Azure.Cosmos.Json;

    internal abstract partial class CosmosFloat64 : CosmosNumber
    {
        protected CosmosFloat64()
            : base(CosmosNumberType.Float64)
        {
        }

        public override bool IsFloatingPoint => true;

        public override bool IsInteger => false;

        public static CosmosFloat64 Create(
            IJsonNavigator jsonNavigator,
            IJsonNavigatorNode jsonNavigatorNode)
        {
            return new LazyCosmosFloat64(jsonNavigator, jsonNavigatorNode);
        }

        public static CosmosFloat64 Create(double number)
        {
            return new EagerCosmosFloat64(number);
        }

        public override double? AsFloatingPoint()
        {
            return this.GetValue();
        }

        public override long? AsInteger()
        {
            return null;
        }

        public override void WriteTo(IJsonWriter jsonWriter)
        {
            if (jsonWriter == null)
            {
                throw new ArgumentNullException($"{nameof(jsonWriter)}");
            }

            jsonWriter.WriteFloat64Value(this.GetValue());
        }

        protected abstract double GetValue();
    }
}
