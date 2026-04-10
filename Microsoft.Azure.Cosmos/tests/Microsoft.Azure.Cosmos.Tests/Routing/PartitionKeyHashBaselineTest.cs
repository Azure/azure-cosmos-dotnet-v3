//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using System.Xml;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Test.BaselineTest;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class PartitionKeyHashBaselineTest : BaselineTests<PartitionKeyHashBaselineTest.Input, PartitionKeyHashBaselineTest.Output>
    {
        private static readonly long MaxSafeInteger = (long)BigInteger.Pow(2, 53) - 1;
        [TestMethod]
        public void Singletons()
        {
            List<Input> inputs = new List<Input>()
            {
                new Input(
                    description: "Undefined",
                    partitionKeyValue: null),
                new Input(
                    description: "null",
                    partitionKeyValue: CosmosNull.Create()),
                new Input(
                    description: "true",
                    partitionKeyValue: CosmosBoolean.Create(true)),
                new Input(
                    description: "false",
                    partitionKeyValue: CosmosBoolean.Create(false)),
            };

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void Strings()
        {
            List<Input> inputs = new List<Input>()
            {
                new Input(
                    description: "Empty String",
                    partitionKeyValue: CosmosString.Create(string.Empty)),
                new Input(
                    description: "short string",
                    partitionKeyValue: CosmosString.Create("asdf")),
                new Input(
                    description: "99 byte string",
                    partitionKeyValue: CosmosString.Create(new string('a', 99))),
                new Input(
                    description: "100 byte string",
                    partitionKeyValue: CosmosString.Create(new string('a', 100))),
                new Input(
                    description: "101 byte string",
                    partitionKeyValue: CosmosString.Create(new string('a', 101))),
                new Input(
                    description: "2kb byte string",
                    partitionKeyValue: CosmosString.Create(new string('a', 2 * 1024))),
            };

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void Numbers()
        {
            List<Input> inputs = new List<Input>()
            {
                new Input(
                    description: "positive zero",
                    partitionKeyValue: CosmosNumber64.Create(0.0)),
                new Input(
                    description: "negative zero",
                    partitionKeyValue: CosmosNumber64.Create(-0.0)),
                new Input(
                    description: "positive number",
                    partitionKeyValue: CosmosNumber64.Create(1)),
                new Input(
                    description: "negative number",
                    partitionKeyValue: CosmosNumber64.Create(-1)),
                new Input(
                    description: nameof(double.Epsilon),
                    partitionKeyValue: CosmosNumber64.Create(double.Epsilon)),
                new Input(
                    description: nameof(double.MaxValue),
                    partitionKeyValue: CosmosNumber64.Create(double.MaxValue)),
                new Input(
                    description: nameof(double.MinValue),
                    partitionKeyValue: CosmosNumber64.Create(double.MinValue)),
                new Input(
                    description: nameof(double.NaN),
                    partitionKeyValue: CosmosNumber64.Create(double.NaN)),
                new Input(
                    description: "long " + nameof(double.NegativeInfinity),
                    partitionKeyValue: CosmosNumber64.Create(double.NegativeInfinity)),
                new Input(
                    description: "long " + nameof(double.PositiveInfinity),
                    partitionKeyValue: CosmosNumber64.Create(double.PositiveInfinity)),
                new Input(
                    description: "long " + nameof(long.MaxValue),
                    partitionKeyValue: CosmosNumber64.Create(long.MaxValue)),
                new Input(
                    description: "long " + nameof(long.MaxValue) + " minus 1",
                    partitionKeyValue: CosmosNumber64.Create(long.MaxValue - 1)),
                new Input(
                    description: "long " + nameof(long.MinValue),
                    partitionKeyValue: CosmosNumber64.Create(long.MinValue)),
                new Input(
                    description: nameof(MaxSafeInteger),
                    partitionKeyValue: CosmosNumber64.Create(MaxSafeInteger)),
                new Input(
                    description: nameof(MaxSafeInteger) + " Minus 1",
                    partitionKeyValue: CosmosNumber64.Create(MaxSafeInteger - 1)),
                new Input(
                    description: nameof(MaxSafeInteger) + " Plus 1",
                    partitionKeyValue: CosmosNumber64.Create(MaxSafeInteger + 1)),
            };

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void Lists()
        {
            List<Input> inputs = new List<Input>()
            {
                new Input(
                    description: "1 Path List",
                    partitionKeyValue: CosmosArray.Create(new List<CosmosElement>()
                    {
                        CosmosString.Create("/path1")
                    })),
                new Input(
                    description: "2 Path List",
                    partitionKeyValue: CosmosArray.Create(new List<CosmosElement>()
                    {
                        CosmosString.Create("/path1"),
                        CosmosString.Create("/path2")
                    })),
                new Input(
                    description: "3 Path List",
                    partitionKeyValue: CosmosArray.Create(new List<CosmosElement>()
                    {
                        CosmosString.Create("/path1"),
                        CosmosString.Create("/path2"),
                        CosmosString.Create("/path3")
                    })),
            };

            this.ExecuteTestSuite(inputs);
        }

        public override Output ExecuteTest(Input input)
        {
            CosmosElement value = input.PartitionKeyValue;

            PartitionKeyHash partitionKeyHashV1;
            PartitionKeyHash partitionKeyHashV2;

            switch (value)
            {
                case null:
                    partitionKeyHashV1 = PartitionKeyHash.V1.HashUndefined();
                    partitionKeyHashV2 = PartitionKeyHash.V2.HashUndefined();
                    break;

                case CosmosNull cosmosNull:
                    partitionKeyHashV1 = PartitionKeyHash.V1.HashNull();
                    partitionKeyHashV2 = PartitionKeyHash.V2.HashNull();
                    break;

                case CosmosBoolean cosmosBoolean:
                    partitionKeyHashV1 = PartitionKeyHash.V1.Hash(cosmosBoolean.Value);
                    partitionKeyHashV2 = PartitionKeyHash.V2.Hash(cosmosBoolean.Value);
                    break;

                case CosmosString cosmosString:
                    partitionKeyHashV1 = PartitionKeyHash.V1.Hash(cosmosString.Value);
                    partitionKeyHashV2 = PartitionKeyHash.V2.Hash(cosmosString.Value);
                    break;

                case CosmosNumber cosmosNumber:
                    partitionKeyHashV1 = PartitionKeyHash.V1.Hash(Number64.ToDouble(cosmosNumber.Value));
                    partitionKeyHashV2 = PartitionKeyHash.V2.Hash(Number64.ToDouble(cosmosNumber.Value));
                    break;
                case CosmosArray cosmosArray:
                    IList<UInt128> partitionKeyHashValuesV1 = new List<UInt128>();
                    IList<UInt128> partitionKeyHashValuesV2 = new List<UInt128>();

                    foreach (CosmosElement element in cosmosArray)
                    {
                        PartitionKeyHash elementHashV1 = element switch
                        {
                            null => PartitionKeyHash.V2.HashUndefined(),
                            CosmosString stringPartitionKey => PartitionKeyHash.V1.Hash(stringPartitionKey.Value),
                            CosmosNumber numberPartitionKey => PartitionKeyHash.V1.Hash(Number64.ToDouble(numberPartitionKey.Value)),
                            CosmosBoolean cosmosBoolean => PartitionKeyHash.V1.Hash(cosmosBoolean.Value),
                            CosmosNull _ => PartitionKeyHash.V1.HashNull(),
                            _ => throw new ArgumentOutOfRangeException(),
                        };
                        partitionKeyHashValuesV1.Add(elementHashV1.HashValues[0]);

                        PartitionKeyHash elementHashV2 = element switch
                        {
                            null => PartitionKeyHash.V2.HashUndefined(),
                            CosmosString stringPartitionKey => PartitionKeyHash.V2.Hash(stringPartitionKey.Value),
                            CosmosNumber numberPartitionKey => PartitionKeyHash.V2.Hash(Number64.ToDouble(numberPartitionKey.Value)),
                            CosmosBoolean cosmosBoolean => PartitionKeyHash.V2.Hash(cosmosBoolean.Value),
                            CosmosNull _ => PartitionKeyHash.V2.HashNull(),
                            _ => throw new ArgumentOutOfRangeException(),
                        };
                        partitionKeyHashValuesV2.Add(elementHashV2.HashValues[0]);
                    }

                    partitionKeyHashV1 = new PartitionKeyHash(partitionKeyHashValuesV1.ToArray());
                    partitionKeyHashV2 = new PartitionKeyHash(partitionKeyHashValuesV2.ToArray());
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"Unknown {nameof(CosmosElement)} type: {value.GetType()}.");
            }

            return new Output(partitionKeyHashV1, partitionKeyHashV2);
        }

        public sealed class Input : BaselineTestInput
        {
            internal Input(string description, CosmosElement partitionKeyValue)
                : base(description)
            {
                this.PartitionKeyValue = partitionKeyValue;
            }

            internal CosmosElement PartitionKeyValue { get; }

            public override void SerializeAsXml(XmlWriter xmlWriter)
            {
                xmlWriter.WriteElementString(nameof(this.Description), this.Description);
                xmlWriter.WriteElementString(nameof(this.PartitionKeyValue), this.PartitionKeyValue == null ? "UNDEFINED" : this.PartitionKeyValue.ToString());
            }
        }

        public sealed class Output : BaselineTestOutput
        {
            internal Output(PartitionKeyHash partitionKeyHashV1, PartitionKeyHash partitionKeyHashV2)
            {
                this.PartitionKeyHashV1 = partitionKeyHashV1;
                this.PartitionKeyHashV2 = partitionKeyHashV2;
            }

            internal PartitionKeyHash PartitionKeyHashV1 { get; }
            internal PartitionKeyHash PartitionKeyHashV2 { get; }

            public override void SerializeAsXml(XmlWriter xmlWriter)
            {
                xmlWriter.WriteElementString(nameof(this.PartitionKeyHashV1), this.PartitionKeyHashV1.Value.ToString());
                xmlWriter.WriteElementString(nameof(this.PartitionKeyHashV2), this.PartitionKeyHashV2.Value.ToString());
            }
        }
    }
}