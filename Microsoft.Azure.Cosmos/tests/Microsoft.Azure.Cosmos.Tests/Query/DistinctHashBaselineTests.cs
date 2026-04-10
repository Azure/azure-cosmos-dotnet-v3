namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Xml;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Distinct;
    using Microsoft.Azure.Cosmos.Test.BaselineTest;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class DistinctHashBaselineTests : BaselineTests<DistinctHashBaselineTests.Input, DistinctHashBaselineTests.Output>
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
            public static readonly CosmosElement Guid = CosmosGuid.Create(System.Guid.Empty);
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
        public void ElementsHash()
        {
            this.ExecuteTestSuite(GenerateInputsFromClass(typeof(Elements)));
        }

        [TestMethod]
        public void NumbersHash()
        {
            this.ExecuteTestSuite(GenerateInputsFromClass(typeof(Numbers)));
        }

        [TestMethod]
        public void ExtendedTypesHash()
        {
            this.ExecuteTestSuite(GenerateInputsFromClass(typeof(ExtendedTypes)));
        }

        [TestMethod]
        public void WrappedElementsHash()
        {
            this.ExecuteTestSuite(GenerateWrappedInputs(typeof(Elements), typeof(Numbers), typeof(ExtendedTypes)));
        }

        private static IEnumerable<Input> GenerateInputsFromClass(Type type)
        {
            foreach (FieldInfo p in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                object v = p.GetValue(null);
                yield return new Input((CosmosElement)v);
            }
        }

        private static IEnumerable<Input> GenerateWrappedInputs(params Type[] types)
        {
            foreach (Type type in types)
            {
                foreach (FieldInfo p in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    object v = p.GetValue(null);
                    yield return new Input((CosmosElement)v);
                    yield return new Input(CosmosArray.Create(new List<CosmosElement>() { (CosmosElement)v }));
                    yield return new Input(CosmosObject.Create(new Dictionary<string, CosmosElement>() { { "prop", (CosmosElement)v } }));
                }
            }
        }

        public override Output ExecuteTest(Input input)
        {
            UInt128 hash = DistinctHash.GetHash(input.CosmosElement);
            return new Output(hash);
        }

        public sealed class Input : BaselineTestInput
        {
            internal Input(CosmosElement cosmosElement)
                : base(description: cosmosElement.ToString())
            {
                this.CosmosElement = cosmosElement;
            }

            public override void SerializeAsXml(XmlWriter xmlWriter)
            {
                xmlWriter.WriteStartElement($"{nameof(this.CosmosElement)}");
                xmlWriter.WriteCData(this.CosmosElement.ToString());
                xmlWriter.WriteEndElement();
            }

            internal CosmosElement CosmosElement { get; }
        }

        public sealed class Output : BaselineTestOutput
        {
            internal Output(UInt128 hash)
            {
                this.Hash = hash;
            }

            internal UInt128 Hash { get; }

            public override void SerializeAsXml(XmlWriter xmlWriter)
            {
                xmlWriter.WriteStartElement($"{nameof(this.Hash)}");
                xmlWriter.WriteCData(this.Hash.ToString());
                xmlWriter.WriteEndElement();
            }
        }
    }
}