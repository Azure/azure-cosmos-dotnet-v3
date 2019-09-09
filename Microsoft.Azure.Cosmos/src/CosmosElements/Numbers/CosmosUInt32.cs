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
    abstract partial class CosmosUInt32 : CosmosNumber
    {
        protected CosmosUInt32()
            : base(CosmosNumberType.UInt32)
        {
        }

        public override bool IsFloatingPoint => false;

        public override bool IsInteger => true;

        public static CosmosUInt32 Create(
            IJsonNavigator jsonNavigator,
            IJsonNavigatorNode jsonNavigatorNode)
        {
            return new LazyCosmosUInt32(jsonNavigator, jsonNavigatorNode);
        }

        public static CosmosUInt32 Create(uint number)
        {
            return new EagerCosmosUInt32(number);
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

            jsonWriter.WriteUInt32Value(this.GetValue());
        }

        protected abstract uint GetValue();
    }
#if INTERNAL
#pragma warning restore SA1601 // Partial elements should be documented
#pragma warning restore SA1600 // Elements should be documented
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#endif
}
