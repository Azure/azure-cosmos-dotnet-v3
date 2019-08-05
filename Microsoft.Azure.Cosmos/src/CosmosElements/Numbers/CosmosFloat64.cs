//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;
    using Microsoft.Azure.Cosmos.Json;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable SA1601 // Partial elements should be documented
    public
#else
    internal
#endif
    abstract partial class CosmosFloat64 : CosmosNumber
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
#if INTERNAL
#pragma warning restore SA1601 // Partial elements should be documented
#pragma warning restore SA1600 // Elements should be documented
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#endif
}
