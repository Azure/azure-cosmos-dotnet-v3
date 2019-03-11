//-----------------------------------------------------------------------
// <copyright file="CosmosNumber.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using Microsoft.Azure.Cosmos.Json;

    internal abstract partial class CosmosNumber : CosmosElement
    {
        protected CosmosNumber()
            : base(CosmosElementType.Number)
        {
        }

        public abstract bool IsInteger
        {
            get;
        }

        public abstract bool IsFloatingPoint
        {
            get;
        }

        public static CosmosNumber Create(
            IJsonNavigator jsonNavigator,
            IJsonNavigatorNode jsonNavigatorNode)
        {
            return new LazyCosmosNumber(jsonNavigator, jsonNavigatorNode);
        }

        public static CosmosNumber Create(Number64 number)
        {
            return CosmosNumber.Create(number);
        }

        public abstract double? AsFloatingPoint();

        public abstract long? AsInteger();
    }
}
