namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    internal sealed class CosmosElementEqualityComparer : IEqualityComparer<CosmosElement>
    {
        public static CosmosElementEqualityComparer Value = new CosmosElementEqualityComparer();

        private CosmosElementEqualityComparer() { }

        public bool Equals(CosmosNumber number1, CosmosNumber number2)
        {
            double double1;
            if (number1.IsFloatingPoint)
            {
                double1 = number1.AsFloatingPoint().Value;
            }
            else
            {
                double1 = number1.AsInteger().Value;
            }

            double double2;
            if (number2.IsFloatingPoint)
            {
                double2 = number2.AsFloatingPoint().Value;
            }
            else
            {
                double2 = number2.AsInteger().Value;
            }

            return double1 == double2;
        }

        public bool Equals(CosmosTypedElement typedElement, CosmosTypedElement typedElement2)
        {
            return typedElement.Equals(typedElement2);
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

                CosmosElement value2;
                if (cosmosObject2.TryGetValue(name, out value2))
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
                        (cosmosElement1 as CosmosBoolean),
                        (cosmosElement2 as CosmosBoolean));

                case CosmosElementType.Null:
                    return true;

                case CosmosElementType.Number:
                    return this.Equals(
                        (cosmosElement1 as CosmosNumber),
                        (cosmosElement2 as CosmosNumber));

                case CosmosElementType.Object:
                    return this.Equals(
                        cosmosElement1 as CosmosObject,
                        cosmosElement2 as CosmosObject);

                case CosmosElementType.String:
                case CosmosElementType.Int8:
                case CosmosElementType.Int16:
                case CosmosElementType.Int32:
                case CosmosElementType.Int64:
                case CosmosElementType.UInt32:
                case CosmosElementType.Float32:
                case CosmosElementType.Float64:
                    return this.Equals(
                        (cosmosElement1 as CosmosTypedElement),
                        (cosmosElement2 as CosmosTypedElement));

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
