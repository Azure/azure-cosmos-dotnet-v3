// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.CosmosElements
{
    internal abstract class CosmosNumber : CosmosElement
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
}