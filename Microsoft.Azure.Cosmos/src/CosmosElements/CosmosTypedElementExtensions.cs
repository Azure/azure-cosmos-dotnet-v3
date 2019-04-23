// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;

    internal static class CosmosTypedElementExtensions
    {
        public static CosmosNumber AsCosmosNumber(this CosmosTypedElement typedElement)
        {
            Number64 number64;
            switch (typedElement.Type)
            {
                case CosmosElementType.Int8:
                    number64 = ((CosmosTypedElement<sbyte>)typedElement).Value;
                    break;

                case CosmosElementType.Int16:
                    number64 = ((CosmosTypedElement<short>)typedElement).Value;
                    break;

                case CosmosElementType.Int32:
                    number64 = ((CosmosTypedElement<int>)typedElement).Value;
                    break;

                case CosmosElementType.Int64:
                    number64 = ((CosmosTypedElement<long>)typedElement).Value;
                    break;

                case CosmosElementType.UInt32:
                    number64 = ((CosmosTypedElement<long>)typedElement).Value;
                    break;

                case CosmosElementType.Float32:
                    number64 = ((CosmosTypedElement<float>)typedElement).Value;
                    break;

                case CosmosElementType.Float64:
                    number64 = ((CosmosTypedElement<double>)typedElement).Value;
                    break;
                default:
                    throw new ArgumentException("Creating CosmosNumber requires numeric type", nameof(typedElement));
            }

            return CosmosNumber.Create(number64);
        }
    }
}