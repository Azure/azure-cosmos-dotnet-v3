namespace Microsoft.Azure.Cosmos.Tests.CosmosElements
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ComparableTests
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
        public void VerifyTypeOrderSort()
        {
            List<CosmosElement> manuallySortedList = new List<CosmosElement>()
            {
                Elements.Null,
                Elements.False,
                Elements.True,
                Numbers.Number64,
                Numbers.Int8,
                Numbers.Int16,
                Numbers.Int32,
                Numbers.Int64,
                Numbers.UInt32,
                Numbers.Float32,
                Numbers.Float64,
                Elements.EmptyString,
                Elements.String,
                Elements.ArrayWithItems,
                Elements.ObjectWithItems,
                ExtendedTypes.Guid,
                ExtendedTypes.Binary,
            };

            VerifySort(manuallySortedList);
        }

        [TestMethod]
        public void VerifyNumberSort()
        {
            List<CosmosElement> manuallySortedList = new List<CosmosElement>()
            {
                CosmosNumber64.Create(-1),
                CosmosNumber64.Create(0),
                CosmosNumber64.Create(1),

                CosmosInt8.Create(-1),
                CosmosInt8.Create(0),
                CosmosInt8.Create(1),

                CosmosInt16.Create(-1),
                CosmosInt16.Create(0),
                CosmosInt16.Create(1),

                CosmosInt32.Create(-1),
                CosmosInt32.Create(0),
                CosmosInt32.Create(1),


                CosmosInt64.Create(-1),
                CosmosInt64.Create(0),
                CosmosInt64.Create(1),

                CosmosUInt32.Create(0),
                CosmosUInt32.Create(1),

                CosmosFloat32.Create(-1),
                CosmosFloat32.Create(0),
                CosmosFloat32.Create(1),

                CosmosFloat64.Create(-1),
                CosmosFloat64.Create(0),
                CosmosFloat64.Create(1),
            };

            VerifySort(manuallySortedList);
        }

        [TestMethod]
        public void VerifyStringSort()
        {
            List<CosmosElement> manuallySortedList = new List<CosmosElement>()
            {
                CosmosString.Create(string.Empty),
                CosmosString.Create("a"),
                CosmosString.Create("b"),
                CosmosString.Create("c"),
            };

            VerifySort(manuallySortedList);
        }

        private static void VerifySort(List<CosmosElement> manuallySortedList)
        {
            List<CosmosElement> automaticallySortedList = manuallySortedList.OrderBy(x => Guid.NewGuid()).ToList();
            automaticallySortedList.Sort();

            Assert.IsTrue(manuallySortedList.SequenceEqual(automaticallySortedList));
        }
    }
}