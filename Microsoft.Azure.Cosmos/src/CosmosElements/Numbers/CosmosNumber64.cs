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
    }
#if INTERNAL
#pragma warning restore SA1601 // Partial elements should be documented
#pragma warning restore SA1600 // Elements should be documented
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#endif
}
