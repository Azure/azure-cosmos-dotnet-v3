//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class EncryptionProcessorTypeMarkerTests
    {
        private static (object typeMarker, byte[] bytes) CallSerialize(JToken token)
        {
            var (marker, bytes) = EncryptionProcessor.Serialize(token);
            return (marker, bytes);
        }

        private static JToken CallDeserialize(byte[] bytes, object typeMarker)
        {
            return EncryptionProcessor.DeserializeAndAddProperty(bytes, (EncryptionProcessor.TypeMarker)typeMarker);
        }

        [TestMethod]
        public void RoundTrip_Bool_True_False()
        {
            foreach (bool value in new[] { true, false })
            {
                JToken token = new JValue(value);
                var (marker, bytes) = CallSerialize(token);
                JToken roundTripped = CallDeserialize(bytes, marker);
                Assert.AreEqual(JTokenType.Boolean, roundTripped.Type);
                Assert.AreEqual(value, roundTripped.Value<bool>());
            }
        }

        [TestMethod]
        public void RoundTrip_Long_Min_Max()
        {
            foreach (long value in new[] { long.MinValue, long.MaxValue, 0L, -1L, 1L })
            {
                JToken token = new JValue(value);
                var (marker, bytes) = CallSerialize(token);
                JToken roundTripped = CallDeserialize(bytes, marker);
                Assert.AreEqual(JTokenType.Integer, roundTripped.Type);
                Assert.AreEqual(value, roundTripped.Value<long>());
            }
        }

        [TestMethod]
        public void RoundTrip_Double_Normal()
        {
            double[] values = new[] { 0.0, -0.0, 1.2345, -98765.4321, Math.PI, Math.E };
            foreach (double value in values)
            {
                JToken token = new JValue(value);
                var (marker, bytes) = CallSerialize(token);
                JToken roundTripped = CallDeserialize(bytes, marker);
                Assert.AreEqual(JTokenType.Float, roundTripped.Type);
                Assert.AreEqual(value, roundTripped.Value<double>(), 0.0, $"Mismatch for {value}");
            }
        }

        [TestMethod]
        public void Serialize_Double_NaN_Infinity_Throws()
        {
            // If serializers allow these values in the future, adjust expectations accordingly.
            double[] special = new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity };
            foreach (double value in special)
            {
                JToken token = new JValue(value);
                bool threw = false;
                try
                {
                    _ = CallSerialize(token);
                }
                catch (Exception)
                {
                    threw = true;
                }

                if (!threw)
                {
                    Assert.Fail($"Serialize should throw for {value}");
                }
            }
        }

        [TestMethod]
        public void RoundTrip_String_VeryLong()
        {
            // 1 MiB string to validate serializer handles long inputs (UTF-8 VarChar serializer with size -1).
            string s = new string('a', 1024 * 1024);
            JToken token = new JValue(s);
            var (marker, bytes) = CallSerialize(token);
            JToken roundTripped = CallDeserialize(bytes, marker);
            Assert.AreEqual(JTokenType.String, roundTripped.Type);
            Assert.AreEqual(s.Length, roundTripped.Value<string>()!.Length);
            Assert.AreEqual(s, roundTripped.Value<string>());
        }

        [TestMethod]
        public void Decimal_IsCoerced_To_Double_With_Expected_Rounding()
        {
            // Newtonsoft JValue(decimal) has JTokenType.Float; Serialize coerces to double via ToObject<double>().
            decimal d = 1234567890.123456789m;
            JToken token = new JValue(d);
            var (marker, bytes) = CallSerialize(token);
            JToken roundTripped = CallDeserialize(bytes, marker);

            Assert.AreEqual(JTokenType.Float, roundTripped.Type, "Decimal should be serialized as Double");

            double expected = Convert.ToDouble(d);
            double actual = roundTripped.Value<double>();

            // Assert exact equality to the Convert.ToDouble result (same conversion path used internally).
            Assert.AreEqual(expected, actual, 0.0, "Expected exact double produced by Convert.ToDouble(decimal)");

            // And confirm precision loss compared to original decimal for typical values with > double precision.
            bool exact = (decimal)actual == d;
            Assert.IsFalse(exact, "Decimal with high precision should lose precision when coerced to double");
        }
    }
}
