//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using Microsoft.Azure.Cosmos.Json;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable SA1601 // Partial elements should be documented
    public
#else
    internal
#endif
    abstract partial class CosmosNumber64 : CosmosNumber
    {
        protected CosmosNumber64()
            : base(CosmosNumberType.Number64)
        {
        }

        public override bool IsFloatingPoint
        {
            get
            {
                return this.GetValue().IsDouble;
            }
        }

        public override bool IsInteger
        {
            get
            {
                return this.GetValue().IsInteger;
            }
        }

        public override double? AsFloatingPoint()
        {
            double? value;
            if (this.IsFloatingPoint)
            {
                value = Number64.ToDouble(this.GetValue());
            }
            else
            {
                value = null;
            }

            return value;
        }

        public override long? AsInteger()
        {
            long? value;
            if (this.IsInteger)
            {
                value = Number64.ToLong(this.GetValue());
            }
            else
            {
                value = null;
            }

            return value;
        }

        public static CosmosNumber64 Create(
            IJsonNavigator jsonNavigator,
            IJsonNavigatorNode jsonNavigatorNode)
        {
            return new LazyCosmosNumber64(jsonNavigator, jsonNavigatorNode);
        }

        public static CosmosNumber64 Create(Number64 number)
        {
            return new EagerCosmosNumber64(number);
        }

        public abstract Number64 GetValue();
    }
#if INTERNAL
#pragma warning restore SA1601 // Partial elements should be documented
#pragma warning restore SA1600 // Elements should be documented
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#endif
}
