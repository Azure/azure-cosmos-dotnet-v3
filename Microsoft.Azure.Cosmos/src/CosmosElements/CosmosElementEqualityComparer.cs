//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
    public
#else
    internal
#endif
    sealed class CosmosElementEqualityComparer : IEqualityComparer<CosmosElement>
    {
        public static CosmosElementEqualityComparer Value = new CosmosElementEqualityComparer();

        private CosmosElementEqualityComparer()
        {
        }

        public bool Equals(CosmosNumber number1, CosmosNumber number2)
        {
            if (number1.NumberType != number2.NumberType)
            {
                return false;
            }
            else
            {
                return number1.Value == number2.Value;
            }
        }

        public bool Equals(CosmosString string1, CosmosString string2)
        {
            return string1.Value.Equals(string2.Value);
        }

        public bool Equals(CosmosGuid guid1, CosmosGuid guid2)
        {
            return guid1.Value.Equals(guid2.Value);
        }

        public bool Equals(CosmosBinary binary1, CosmosBinary binary2)
        {
            return binary1.Value.Span.SequenceEqual(binary2.Value.Span);
        }

        public bool Equals(CosmosBoolean bool1, CosmosBoolean bool2)
        {
            return bool1.Value == bool2.Value;
        }

        public bool Equals(CosmosArray cosmosArray1, CosmosArray cosmosArray2)
        {
            if (cosmosArray1.Count != cosmosArray2.Count)
            {
                return false;
            }

            IEnumerable<Tuple<CosmosElement, CosmosElement>> pairwiseElements = cosmosArray1
                .Zip(cosmosArray2, (first, second) => new Tuple<CosmosElement, CosmosElement>(first, second));
            bool deepEquals = true;
            foreach (Tuple<CosmosElement, CosmosElement> pairwiseElement in pairwiseElements)
            {
                deepEquals &= this.Equals(pairwiseElement.Item1, pairwiseElement.Item2);
            }

            return deepEquals;
        }

        public bool Equals(CosmosObject cosmosObject1, CosmosObject cosmosObject2)
        {
            if (cosmosObject1.Count != cosmosObject2.Count)
            {
                return false;
            }

            bool deepEquals = true;
            foreach (KeyValuePair<string, CosmosElement> kvp in cosmosObject1)
            {
                string name = kvp.Key;
                CosmosElement value1 = kvp.Value;

                if (cosmosObject2.TryGetValue(name, out CosmosElement value2))
                {
                    deepEquals &= this.Equals(value1, value2);
                }
                else
                {
                    return false;
                }
            }

            return deepEquals;
        }

        public bool Equals(CosmosElement cosmosElement1, CosmosElement cosmosElement2)
        {
            if (Object.ReferenceEquals(cosmosElement1, cosmosElement2))
            {
                return true;
            }

            if (cosmosElement1 == null || cosmosElement2 == null)
            {
                return false;
            }

            CosmosElementType type1 = cosmosElement1.Type;
            CosmosElementType type2 = cosmosElement2.Type;

            // If the types don't match
            if (type1 != type2)
            {
                return false;
            }

            switch (type1)
            {
                case CosmosElementType.Array:
                    return this.Equals(
                        cosmosElement1 as CosmosArray,
                        cosmosElement2 as CosmosArray);

                case CosmosElementType.Boolean:
                    return this.Equals(
                        cosmosElement1 as CosmosBoolean,
                        cosmosElement2 as CosmosBoolean);

                case CosmosElementType.Null:
                    return true;

                case CosmosElementType.Number:
                    return this.Equals(
                        cosmosElement1 as CosmosNumber,
                        cosmosElement2 as CosmosNumber);

                case CosmosElementType.Object:
                    return this.Equals(
                        cosmosElement1 as CosmosObject,
                        cosmosElement2 as CosmosObject);

                case CosmosElementType.String:
                    return this.Equals(
                        cosmosElement1 as CosmosString,
                        cosmosElement2 as CosmosString);

                case CosmosElementType.Guid:
                    return this.Equals(
                        cosmosElement1 as CosmosGuid,
                        cosmosElement2 as CosmosGuid);

                case CosmosElementType.Binary:
                    return this.Equals(
                        cosmosElement1 as CosmosBinary,
                        cosmosElement2 as CosmosBinary);

                default:
                    throw new ArgumentException();
            }
        }

        public int GetHashCode(CosmosElement cosmosElement)
        {
            return 0;
        }
    }
}
