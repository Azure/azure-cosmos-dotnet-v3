namespace Microsoft.Azure.Cosmos.Tests.CosmosElements
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class EquatableTests
    {
        private static class Numbers
        {
            public static readonly CosmosElement Float32 = CosmosFloat32.Create(1337.42f);
            public static readonly CosmosElement Float64 = CosmosFloat64.Create(1337.42);
            public static readonly CosmosElement Int8 = CosmosInt8.Create(42);
            public static readonly CosmosElement Int16 = CosmosInt16.Create(42);
            public static readonly CosmosElement Int32 = CosmosInt32.Create(42);
            public static readonly CosmosElement Int64 = CosmosInt64.Create(42);
            public static readonly CosmosElement Number64 = CosmosNumber64.Create(1234);
            public static readonly CosmosElement UInt32 = CosmosUInt32.Create(1234);
        }

        private static class ExtendedTypes
        {
            public static readonly CosmosElement EmptyBinary = CosmosBinary.Create(ReadOnlyMemory<byte>.Empty);
            public static readonly CosmosElement Binary = CosmosBinary.Create(new byte[] { 1, 2, 3 });
            public static readonly CosmosElement EmptyGuid = CosmosGuid.Create(System.Guid.Empty);
            public static readonly CosmosElement Guid = CosmosGuid.Create(System.Guid.NewGuid());
        }

        private static class Elements
        {
            public static readonly CosmosElement Null = CosmosNull.Create();
            public static readonly CosmosElement False = CosmosBoolean.Create(false);
            public static readonly CosmosElement True = CosmosBoolean.Create(true);
            public static readonly CosmosElement EmptyString = CosmosString.Create(string.Empty);
            public static readonly CosmosElement String = CosmosString.Create("asdfasdfasdfasdfasdfasdf");
            public static readonly CosmosElement EmptyArray = CosmosArray.Create(
                new List<CosmosElement>()
                {
                });
            public static readonly CosmosElement ArrayWithItems = CosmosArray.Create(
                new List<CosmosElement>()
                {
                    Null,
                    False,
                    True,
                    Numbers.Number64,
                    String
                });
            public static readonly CosmosElement EmptyObject = CosmosObject.Create(new Dictionary<string, CosmosElement>());
            public static readonly CosmosElement ObjectWithItems = CosmosObject.Create(new Dictionary<string, CosmosElement>()
            {
                { "null", Null },
                { "false", False },
                { "true", True },
                { "cosmosNumber", Numbers.Number64 },
                { "cosmosString", String },
            });
        }

        [TestMethod]
        public void SanityTest()
        {
            List<CosmosElement> allElements = GenerateInputsFromClass(typeof(Elements))
                .Concat(GenerateInputsFromClass(typeof(ExtendedTypes)))
                .Concat(GenerateInputsFromClass(typeof(Numbers)))
                .ToList();

            foreach (CosmosElement first in allElements)
            {
                foreach (CosmosElement second in allElements)
                {
                    if (object.ReferenceEquals(first, second))
                    {
                        // They should be equals semantically too
                        Assert.AreEqual(first, second);

                        // Try with a deep copy to make sure it's not just asserting reference eqaulity.
                        Assert.AreEqual(first, CosmosElement.Parse(second.ToString()));

                        // If two objects are equal, then so are their hashes (consistent hashing).
                        Assert.AreEqual(first.GetHashCode(), second.GetHashCode());
                    }
                    else
                    {
                        Assert.AreNotEqual(first, second);
                    }
                }
            }
        }

        [TestMethod]
        public void ArrayOrderMatters()
        {
            CosmosArray cosmosArray = (CosmosArray)Elements.ArrayWithItems;
            CosmosArray cosmosArrayReversed = CosmosArray.Create(cosmosArray.Reverse());

            Assert.AreNotEqual(cosmosArray, cosmosArrayReversed);
        }

        [TestMethod]
        public void ObjectPropertyOrderDoesNotMatter()
        {
            CosmosObject cosmosObject = (CosmosObject)Elements.ObjectWithItems;
            CosmosObject cosmosObjectReversed = CosmosObject.Create(cosmosObject.Reverse().ToList().ToDictionary(kvp => kvp.Key, kvp => kvp.Value));

            Assert.AreEqual(cosmosObject, cosmosObjectReversed);
            Assert.AreEqual(cosmosObject.GetHashCode(), cosmosObjectReversed.GetHashCode());
        }

        [TestMethod]
        public void NumberTypeDoesMatter()
        {
            CosmosElement int8 = CosmosInt8.Create(42);
            CosmosElement int16 = CosmosInt16.Create(42);

            Assert.AreNotEqual(int8, int16);
        }

        private static IEnumerable<CosmosElement> GenerateInputsFromClass(Type type)
        {
            foreach (FieldInfo p in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                object v = p.GetValue(null);
                yield return (CosmosElement)v;
            }
        }
    }
}