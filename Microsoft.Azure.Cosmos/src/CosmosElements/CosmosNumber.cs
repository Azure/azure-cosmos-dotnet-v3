// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.CosmosElements
{
#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
    public
#else
    internal
#endif
    abstract class CosmosNumber : CosmosElement
    {
        protected CosmosNumber(CosmosNumberType cosmosNumberType)
            : base(CosmosElementType.Number)
        {
            this.NumberType = cosmosNumberType;
        }

        public CosmosNumberType NumberType { get; }

        public abstract bool IsInteger
        {
            get;
        }

        public abstract bool IsFloatingPoint
        {
            get;
        }

        public abstract double? AsFloatingPoint();

        public abstract long? AsInteger();
    }
#if INTERNAL
#pragma warning restore SA1600 // Elements should be documented
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#endif
}