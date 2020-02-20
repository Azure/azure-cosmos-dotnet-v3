//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;
    using System.Xml;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Test.BaselineTest;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class EffectivePartitionKeyBaselineTest : BaselineTests<EffectivePartitionKeyBaselineTest.Input, EffectivePartitionKeyBaselineTest.Output>
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

        public override Output ExecuteTest(Input input)
        {
            CosmosElement value = input.PartitionKeyValue;

            EffectivePartitionKey effectivePartitionKeyV1;
            EffectivePartitionKey effectivePartitionKeyV2;

            switch (value)
            {
                case null:
                    effectivePartitionKeyV1 = EffectivePartitionKey.HashUndefinedV1();
                    effectivePartitionKeyV2 = EffectivePartitionKey.HashUndefinedV2();
                    break;

                case CosmosNull cosmosNull:
                    effectivePartitionKeyV1 = EffectivePartitionKey.HashNullV1();
                    effectivePartitionKeyV2 = EffectivePartitionKey.HashNullV2();
                    break;

                case CosmosBoolean cosmosBoolean:
                    effectivePartitionKeyV1 = EffectivePartitionKey.HashV1(cosmosBoolean.Value);
                    effectivePartitionKeyV2 = EffectivePartitionKey.HashV2(cosmosBoolean.Value);
                    break;

                case CosmosString cosmosString:
                    effectivePartitionKeyV1 = EffectivePartitionKey.HashV1(cosmosString.Value);
                    effectivePartitionKeyV2 = EffectivePartitionKey.HashV2(cosmosString.Value);
                    break;

                case CosmosNumber cosmosNumber:
                    effectivePartitionKeyV1 = EffectivePartitionKey.HashV1(Number64.ToDouble(cosmosNumber.Value));
                    effectivePartitionKeyV2 = EffectivePartitionKey.HashV2(Number64.ToDouble(cosmosNumber.Value));
                    break;

                default:
                    throw new ArgumentOutOfRangeException($"Unknown {nameof(CosmosElement)} type: {value.GetType()}.");
            }

            return new Output(effectivePartitionKeyV1, effectivePartitionKeyV2);
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
            internal Output(EffectivePartitionKey effectivePartitionKeyV1, EffectivePartitionKey effectivePartitionKeyV2)
            {
                this.EffectivePartitionKeyV1 = effectivePartitionKeyV1;
                this.EffectivePartitionKeyV2 = effectivePartitionKeyV2;
            }

            internal EffectivePartitionKey EffectivePartitionKeyV1 { get; }
            internal EffectivePartitionKey EffectivePartitionKeyV2 { get; }

            public override void SerializeAsXml(XmlWriter xmlWriter)
            {
                xmlWriter.WriteElementString(nameof(this.EffectivePartitionKeyV1), this.EffectivePartitionKeyV1.Value.ToString());
                xmlWriter.WriteElementString(nameof(this.EffectivePartitionKeyV2), this.EffectivePartitionKeyV2.Value.ToString());
            }
        }
    }
}
