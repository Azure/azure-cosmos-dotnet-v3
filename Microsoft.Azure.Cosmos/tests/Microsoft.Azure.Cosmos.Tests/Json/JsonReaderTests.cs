//-----------------------------------------------------------------------
// <copyright file="JsonReaderTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Tests.Json
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.Cosmos.Core.Utf8;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests for JsonReader.
    /// </summary>
    [TestClass]
    public class JsonReaderTests
    {
        /// <summary>
        /// The byte that goes in front of all binary formatted jsons.
        /// </summary>
        private const byte BinaryFormat = 128;

        [ClassInitialize]
        public static void Initialize(TestContext textContext)
        {
            // put class init code here
        }

        [TestInitialize]
        public void TestInitialize()
        {
            // Put test init code here
        }

        #region Literals
        [TestMethod]
        [Owner("mayapainter")]
        public void TrueTest()
        {
            string input = "true";
            JsonToken[] expectedTokens =
            {
                JsonToken.Boolean(true)
            };

            byte[] binaryInput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.True
            };

            this.VerifyReader(input, expectedTokens);
            this.VerifyReader(binaryInput, expectedTokens);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void FalseTest()
        {
            string input = "false";
            JsonToken[] expectedTokens =
            {
                JsonToken.Boolean(false)
            };

            byte[] binaryInput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.False
            };

            this.VerifyReader(input, expectedTokens);
            this.VerifyReader(binaryInput, expectedTokens);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void NullTest()
        {
            string input = "null";
            byte[] binaryInput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.Null
            };

            JsonToken[] expectedTokens =
            {
                JsonToken.Null()
            };

            this.VerifyReader(input, expectedTokens);
            this.VerifyReader(binaryInput, expectedTokens);
        }
        #endregion
        #region Numbers
        [TestMethod]
        [Owner("mayapainter")]
        public void IntegerTest()
        {
            string input = "1337";
            byte[] binaryInput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.NumberInt16,
                // 1337 in litte endian hex,
                0x39, 0x05,
            };

            JsonToken[] expectedTokens =
            {
                JsonToken.Number(1337)
            };

            this.VerifyReader(input, expectedTokens);
            this.VerifyReader(binaryInput, expectedTokens);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void DoubleTest()
        {
            string input = "1337.0";
            byte[] binaryInput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.NumberDouble,
                // 1337 in litte endian hex for a double
                0x00, 0x00, 0x00, 0x00, 0x00, 0xE4, 0x94, 0x40,
            };

            JsonToken[] expectedTokens =
            {
                JsonToken.Number(1337.0)
            };

            this.VerifyReader(input, expectedTokens);
            this.VerifyReader(binaryInput, expectedTokens);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void NegativeNumberTest()
        {
            string input = "-1337.0";
            byte[] binaryInput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.NumberDouble,
                // -1337 in litte endian hex for a double
                0x00, 0x00, 0x00, 0x00, 0x00, 0xE4, 0x94, 0xC0,
            };

            JsonToken[] expectedTokens =
            {
                JsonToken.Number(-1337.0)
            };

            this.VerifyReader(input, expectedTokens);
            this.VerifyReader(binaryInput, expectedTokens);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void NumberWithPlusSignTest()
        {
            string input = "+1337.0";

            JsonToken[] expectedTokens =
            {
            };

            this.VerifyReader(input, expectedTokens, new JsonUnexpectedTokenException());
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void NumberWithLeadingZeros()
        {
            string input = "01";

            JsonToken[] expectedTokens =
            {
            };

            this.VerifyReader(input, expectedTokens, new JsonInvalidNumberException());
            this.VerifyReader("0" + input, expectedTokens, new JsonInvalidNumberException());
            this.VerifyReader("00" + input, expectedTokens, new JsonInvalidNumberException());
            this.VerifyReader("000" + input, expectedTokens, new JsonInvalidNumberException());

            // But 0 should still pass
            string zeroString = "0";

            JsonToken[] zeroToken =
            {
                JsonToken.Number(0)
            };
            this.VerifyReader(zeroString, zeroToken);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void NumberWithScientificNotationTest()
        {
            string input = "6.02252e23";
            string input2 = "6.02252E23";
            byte[] binaryInput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.NumberDouble,
                // 6.02252e23 in litte endian hex for a double
                0x93, 0x09, 0x9F, 0x5D, 0x09, 0xE2, 0xDF, 0x44
            };

            JsonToken[] expectedTokens =
            {
                JsonToken.Number(6.02252e23)
            };

            this.VerifyReader(input, expectedTokens);
            this.VerifyReader(input2, expectedTokens);
            this.VerifyReader(binaryInput, expectedTokens);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void NumberRegressionTest()
        {
            // regression test - the value 0.00085647800000000004 was being incorrectly rejected
            string numberValueString = "0.00085647800000000004";
            double numberValue = double.Parse(numberValueString);
            byte[] binaryInput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.NumberDouble,
                // 0.00085647800000000004 in litte endian hex for a double
                0x39, 0x98, 0xF7, 0x7F, 0xA8, 0x10, 0x4C, 0x3F
            };

            JsonToken[] expectedTokens =
            {
                JsonToken.Number(numberValue)
            };

            this.VerifyReader(numberValueString, expectedTokens);
            this.VerifyReader(binaryInput, expectedTokens);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void AllNumberRepresentationsTest()
        {
            // trying to read 4 from all possible representations
            int number = 4;
            byte[] binaryLiteralInput =
            {
                BinaryFormat,
                (byte)(JsonBinaryEncoding.TypeMarker.LiteralIntMin + number),
            };

            byte[] binaryUInt8Input =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.NumberUInt8,
                (byte)number,
            };

            byte[] binaryInt16Input =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.NumberInt16,
                0x04, 0x00,
            };

            byte[] binaryInt32Input =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.NumberInt32,
                0x04, 0x00, 0x00, 0x00,
            };

            byte[] binaryInt64Input =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.NumberInt64,
                0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            };

            byte[] binaryDoubleInput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.NumberDouble,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x10, 0x40,
            };

            JsonToken[] expectedTokens =
            {
                JsonToken.Number(number)
            };

            this.VerifyReader("4", expectedTokens);
            this.VerifyReader(binaryLiteralInput, expectedTokens);
            this.VerifyReader(binaryUInt8Input, expectedTokens);
            this.VerifyReader(binaryInt16Input, expectedTokens);
            this.VerifyReader(binaryInt32Input, expectedTokens);
            this.VerifyReader(binaryInt64Input, expectedTokens);
            this.VerifyReader(binaryDoubleInput, expectedTokens);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void NumberLimitsTest()
        {
            // min byte
            JsonToken[] minByteTokens =
            {
                JsonToken.Number(byte.MinValue)
            };

            byte[] minByteInput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.NumberUInt8,
                byte.MinValue
            };

            this.VerifyReader(byte.MinValue.ToString(), minByteTokens);
            this.VerifyReader(minByteInput, minByteTokens);

            // max byte
            JsonToken[] maxByteTokens =
            {
                JsonToken.Number(byte.MaxValue)
            };

            byte[] maxByteInput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.NumberUInt8,
                byte.MaxValue
            };

            this.VerifyReader(byte.MaxValue.ToString(), maxByteTokens);
            this.VerifyReader(maxByteInput, maxByteTokens);

            // min short
            JsonToken[] minShortTokens =
            {
                JsonToken.Number(short.MinValue)
            };

            List<byte> minShortInput = new List<byte>
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.NumberInt16
            };
            minShortInput.AddRange(BitConverter.GetBytes(short.MinValue));

            this.VerifyReader(short.MinValue.ToString(), minShortTokens);
            this.VerifyReader(minShortInput.ToArray(), minShortTokens);

            // max short
            JsonToken[] maxShortTokens =
            {
                JsonToken.Number(short.MaxValue)
            };

            List<byte> maxShortInput = new List<byte>
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.NumberInt16
            };
            maxShortInput.AddRange(BitConverter.GetBytes(short.MaxValue));

            this.VerifyReader(short.MaxValue.ToString(), maxShortTokens);
            this.VerifyReader(maxShortInput.ToArray(), maxShortTokens);

            // min int
            JsonToken[] minIntTokens =
            {
                JsonToken.Number(int.MinValue)
            };

            List<byte> minIntInput = new List<byte>
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.NumberInt32
            };
            minIntInput.AddRange(BitConverter.GetBytes(int.MinValue));

            this.VerifyReader(int.MinValue.ToString(), minIntTokens);
            this.VerifyReader(minIntInput.ToArray(), minIntTokens);

            // max int
            JsonToken[] maxIntTokens =
            {
                JsonToken.Number(int.MaxValue)
            };

            List<byte> maxIntInput = new List<byte>
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.NumberInt32
            };
            maxIntInput.AddRange(BitConverter.GetBytes(int.MaxValue));

            this.VerifyReader(int.MaxValue.ToString(), maxIntTokens);
            this.VerifyReader(maxIntInput.ToArray(), maxIntTokens);

            // min long
            JsonToken[] minLongTokens =
            {
                JsonToken.Number(long.MinValue)
            };

            List<byte> minLongInput = new List<byte>
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.NumberInt64
            };
            minLongInput.AddRange(BitConverter.GetBytes(long.MinValue));

            this.VerifyReader(long.MinValue.ToString(), minLongTokens);
            this.VerifyReader(minLongInput.ToArray(), minLongTokens);

            // max long
            JsonToken[] maxLongTokens =
            {
                JsonToken.Number(long.MaxValue)
            };

            List<byte> maxLongInput = new List<byte>
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.NumberInt64
            };
            maxLongInput.AddRange(BitConverter.GetBytes(long.MaxValue));

            this.VerifyReader(long.MaxValue.ToString(), maxLongTokens);
            this.VerifyReader(maxLongInput.ToArray(), maxLongTokens);

            // min double
            JsonToken[] minDoubleTokens =
            {
                JsonToken.Number(double.MinValue)
            };

            List<byte> minDoubleInput = new List<byte>
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.NumberDouble
            };
            minDoubleInput.AddRange(BitConverter.GetBytes(double.MinValue));

            this.VerifyReader(double.MinValue.ToString("G17"), minDoubleTokens);
            this.VerifyReader(minDoubleInput.ToArray(), minDoubleTokens);

            // max double
            JsonToken[] maxDoubleTokens =
            {
                JsonToken.Number(double.MaxValue)
            };

            List<byte> maxDoubleInput = new List<byte>
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.NumberDouble
            };
            maxDoubleInput.AddRange(BitConverter.GetBytes(double.MaxValue));

            this.VerifyReader(double.MaxValue.ToString("G17"), maxDoubleTokens);
            this.VerifyReader(maxDoubleInput.ToArray(), maxDoubleTokens);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void NumberStartingWithDotTest()
        {
            string input = ".001";

            JsonToken[] expectedTokens =
            {
            };

            this.VerifyReader(input, expectedTokens, new JsonUnexpectedTokenException());
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void ScientificWithNoExponent()
        {
            string input = "1e";
            string input2 = "1E";

            JsonToken[] expectedTokens =
            {
            };

            this.VerifyReader(input, expectedTokens, new JsonInvalidNumberException());
            this.VerifyReader(input2, expectedTokens, new JsonInvalidNumberException());
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void ScientificWithPostitiveExponent()
        {
            string input = "6.02252e+23";
            string input2 = "6.02252E+23";

            JsonToken[] expectedTokens =
            {
                JsonToken.Number(6.02252e+23)
            };

            this.VerifyReader(input, expectedTokens);
            this.VerifyReader(input2, expectedTokens);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void ScientificWithNegativeExponent()
        {
            string input = "6.02252e-23";
            string input2 = "6.02252E-23";

            JsonToken[] expectedTokens =
            {
                JsonToken.Number(6.02252e-23)
            };

            this.VerifyReader(input, expectedTokens);
            this.VerifyReader(input2, expectedTokens);
        }
        #endregion
        #region Strings
        [TestMethod]
        [Owner("mayapainter")]
        public void EmptyStringTest()
        {
            string input = "\"\"";
            JsonToken[] expectedTokens =
            {
                JsonToken.String(string.Empty)
            };

            this.VerifyReader(input, expectedTokens);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void StringTest()
        {
            string input = "\"Hello World\"";

            byte[] binary1ByteLengthInput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.String1ByteLength,
                (byte)"Hello World".Length,
                // Hello World as a utf8 string
                72, 101, 108, 108, 111, 32, 87, 111, 114, 108, 100
            };

            byte[] binary2ByteLengthInput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.String2ByteLength,
                // (ushort)"Hello World".Length,
                0x0B, 0x00,
                // Hello World as a utf8 string
                72, 101, 108, 108, 111, 32, 87, 111, 114, 108, 100
            };

            byte[] binary4ByteLengthInput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.String4ByteLength,
                // (uint)"Hello World".Length,
                0x0B, 0x00, 0x00, 0x00,
                // Hello World as a utf8 string
                72, 101, 108, 108, 111, 32, 87, 111, 114, 108, 100
            };

            JsonToken[] expectedTokens =
            {
                JsonToken.String("Hello World")
            };

            this.VerifyReader(input, expectedTokens);
            this.VerifyReader(binary1ByteLengthInput, expectedTokens);
            this.VerifyReader(binary2ByteLengthInput, expectedTokens);
            this.VerifyReader(binary4ByteLengthInput, expectedTokens);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void SystemStringTest()
        {
            int systemStringId = 0;
            while (JsonBinaryEncoding.SystemStrings.TryGetSystemStringById(systemStringId, out UtfAllString systemString))
            {
                string input = "\"" + systemString.Utf16String + "\"";
                byte[] binaryInput =
                {
                    BinaryFormat,
                    (byte)(JsonBinaryEncoding.TypeMarker.SystemString1ByteLengthMin + systemStringId++),
                };

                JsonToken[] expectedTokens =
                {
                    JsonToken.String(systemString.Utf16String)
                };

                this.VerifyReader(input, expectedTokens);
                this.VerifyReader(binaryInput, expectedTokens);
            }
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void NumberAsStringTest()
        {
            string input = "\"42\"";
            JsonToken[] expectedTokens =
            {
                JsonToken.String("42")
            };

            this.VerifyReader(input, expectedTokens);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void BoolAsStringTest()
        {
            string input = "\"true\"";
            JsonToken[] expectedTokens =
            {
                JsonToken.String("true")
            };

            this.VerifyReader(input, expectedTokens);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void NullAsStringTest()
        {
            string input = "\"null\"";
            JsonToken[] expectedTokens =
            {
                JsonToken.String("null")
            };

            this.VerifyReader(input, expectedTokens);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void ArrayAsStringTest()
        {
            string input = "\"[]\"";
            JsonToken[] expectedTokens =
            {
                JsonToken.String("[]")
            };

            this.VerifyReader(input, expectedTokens);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void ObjectAsStringTest()
        {
            string input = "\"{}\"";
            JsonToken[] expectedTokens =
            {
                JsonToken.String("{}")
            };

            this.VerifyReader(input, expectedTokens);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void GuidStringsTest()
        {
            {
                // Empty Guid
                string guidString = "00000000-0000-0000-0000-000000000000";
                string stringPayload = $"\"{guidString}\"";
                JsonToken[] expectedTokens =
                {
                    JsonToken.String(guidString)
                };

                byte[] binaryPayload =
                {
                    BinaryFormat,
                    JsonBinaryEncoding.TypeMarker.String1ByteLength,
                    (byte)guidString.Length,
                };
                binaryPayload = binaryPayload.Concat(Encoding.UTF8.GetBytes(guidString)).ToArray();

                byte[] compressedBinaryPayload =
                {
                    BinaryFormat,
                    JsonBinaryEncoding.TypeMarker.LowercaseGuidString,
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                };

                this.VerifyReader(stringPayload, expectedTokens);
                this.VerifyReader(binaryPayload, expectedTokens);
                this.VerifyReader(compressedBinaryPayload, expectedTokens);
            }

            {
                // All numbers
                string guidString = "11111111-2222-3333-4444-555555555555";
                string stringPayload = $"\"{guidString}\"";
                JsonToken[] expectedTokens =
                {
                    JsonToken.String(guidString)
                };

                byte[] binaryPayload =
                {
                    BinaryFormat,
                    JsonBinaryEncoding.TypeMarker.String1ByteLength,
                    (byte)guidString.Length,
                };
                binaryPayload = binaryPayload.Concat(Encoding.UTF8.GetBytes(guidString)).ToArray();

                byte[] compressedBinaryPayload =
                {
                    BinaryFormat,
                    JsonBinaryEncoding.TypeMarker.LowercaseGuidString,
                    0x11, 0x11, 0x11, 0x11, 0x22, 0x22, 0x33, 0x33,
                    0x44, 0x44, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55,
                };

                this.VerifyReader(stringPayload, expectedTokens);
                this.VerifyReader(binaryPayload, expectedTokens);
                this.VerifyReader(compressedBinaryPayload, expectedTokens);
            }

            {
                // All lower-case letters
                string guidString = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";
                string stringPayload = $"\"{guidString}\"";
                JsonToken[] expectedTokens =
                {
                    JsonToken.String(guidString)
                };

                byte[] binaryPayload =
                {
                    BinaryFormat,
                    JsonBinaryEncoding.TypeMarker.String1ByteLength,
                    (byte)guidString.Length,
                };
                binaryPayload = binaryPayload.Concat(Encoding.UTF8.GetBytes(guidString)).ToArray();

                byte[] compressedBinaryPayload =
                {
                    BinaryFormat,
                    JsonBinaryEncoding.TypeMarker.LowercaseGuidString,
                    0xAA, 0xAA, 0xAA, 0xAA, 0xBB, 0xBB, 0xCC, 0xCC,
                    0xDD, 0xDD, 0xEE, 0xEE, 0xEE, 0xEE, 0xEE, 0xEE,
                };

                this.VerifyReader(stringPayload, expectedTokens);
                this.VerifyReader(binaryPayload, expectedTokens);
                this.VerifyReader(compressedBinaryPayload, expectedTokens);
            }

            {
                // All upper-case letters
                string guidString = "AAAAAAAA-BBBB-CCCC-DDDD-EEEEEEEEEEEE";
                string stringPayload = $"\"{guidString}\"";
                JsonToken[] expectedTokens =
                {
                    JsonToken.String(guidString)
                };

                byte[] binaryPayload =
                {
                    BinaryFormat,
                    JsonBinaryEncoding.TypeMarker.String1ByteLength,
                    (byte)guidString.Length,
                };
                binaryPayload = binaryPayload.Concat(Encoding.UTF8.GetBytes(guidString)).ToArray();

                byte[] compressedBinaryPayload =
                {
                    BinaryFormat,
                    JsonBinaryEncoding.TypeMarker.UppercaseGuidString,
                    0xAA, 0xAA, 0xAA, 0xAA, 0xBB, 0xBB, 0xCC, 0xCC,
                    0xDD, 0xDD, 0xEE, 0xEE, 0xEE, 0xEE, 0xEE, 0xEE,
                };

                this.VerifyReader(stringPayload, expectedTokens);
                this.VerifyReader(binaryPayload, expectedTokens);
                this.VerifyReader(compressedBinaryPayload, expectedTokens);
            }

            {
                // Lower-case GUID
                string guidString = "ed7e38aa-074e-4a74-bab0-2a4f41079baa";
                string stringPayload = $"\"{guidString}\"";
                JsonToken[] expectedTokens =
                {
                    JsonToken.String(guidString)
                };

                byte[] binaryPayload =
                {
                    BinaryFormat,
                    JsonBinaryEncoding.TypeMarker.String1ByteLength,
                    (byte)guidString.Length,
                };
                binaryPayload = binaryPayload.Concat(Encoding.UTF8.GetBytes(guidString)).ToArray();

                byte[] compressedBinaryPayload =
                {
                    BinaryFormat,
                    JsonBinaryEncoding.TypeMarker.LowercaseGuidString,
                    0xDE, 0xE7, 0x83, 0xAA, 0x70, 0xE4, 0xA4, 0x47,
                    0xAB, 0x0B, 0xA2, 0xF4, 0x14, 0x70, 0xB9, 0xAA,
                };

                this.VerifyReader(stringPayload, expectedTokens);
                this.VerifyReader(binaryPayload, expectedTokens);
                this.VerifyReader(compressedBinaryPayload, expectedTokens);
            }

            {
                // Upper-case GUID
                string guidString = "ED7E38AA-074E-4A74-BAB0-2A4F41079BAA";
                string stringPayload = $"\"{guidString}\"";
                JsonToken[] expectedTokens =
                {
                    JsonToken.String(guidString)
                };

                byte[] binaryPayload =
                {
                    BinaryFormat,
                    JsonBinaryEncoding.TypeMarker.String1ByteLength,
                    (byte)guidString.Length,
                };
                binaryPayload = binaryPayload.Concat(Encoding.UTF8.GetBytes(guidString)).ToArray();

                byte[] compressedBinaryPayload =
                {
                    BinaryFormat,
                    JsonBinaryEncoding.TypeMarker.UppercaseGuidString,
                    0xDE, 0xE7, 0x83, 0xAA, 0x70, 0xE4, 0xA4, 0x47,
                    0xAB, 0x0B, 0xA2, 0xF4, 0x14, 0x70, 0xB9, 0xAA,
                };

                this.VerifyReader(stringPayload, expectedTokens);
                this.VerifyReader(binaryPayload, expectedTokens);
                this.VerifyReader(compressedBinaryPayload, expectedTokens);
            }

            {
                // Upper-case GUID
                string guidString = "ED7E38AA-074E-4A74-BAB0-2A4F41079BAA";
                string stringPayload = $"\"{guidString}\"";
                JsonToken[] expectedTokens =
                {
                    JsonToken.String(guidString)
                };

                byte[] binaryPayload =
                {
                    BinaryFormat,
                    JsonBinaryEncoding.TypeMarker.String1ByteLength,
                    (byte)guidString.Length,
                };
                binaryPayload = binaryPayload.Concat(Encoding.UTF8.GetBytes(guidString)).ToArray();

                byte[] compressedBinaryPayload =
                {
                    BinaryFormat,
                    JsonBinaryEncoding.TypeMarker.UppercaseGuidString,
                    0xDE, 0xE7, 0x83, 0xAA, 0x70, 0xE4, 0xA4, 0x47,
                    0xAB, 0x0B, 0xA2, 0xF4, 0x14, 0x70, 0xB9, 0xAA,
                };

                this.VerifyReader(stringPayload, expectedTokens);
                this.VerifyReader(binaryPayload, expectedTokens);
                this.VerifyReader(compressedBinaryPayload, expectedTokens);
            }

            {
                // Mixed-case GUID (just a regular string)
                string guidString = "412D5baf-acf2-4c43-9ccb-c80a9d6f267D";
                string stringPayload = $"\"{guidString}\"";
                JsonToken[] expectedTokens =
                {
                    JsonToken.String(guidString)
                };

                byte[] binaryPayload =
                {
                    BinaryFormat,
                    JsonBinaryEncoding.TypeMarker.String1ByteLength,
                    (byte)guidString.Length,
                };
                binaryPayload = binaryPayload.Concat(Encoding.UTF8.GetBytes(guidString)).ToArray();

                this.VerifyReader(stringPayload, expectedTokens);
                this.VerifyReader(binaryPayload, expectedTokens);
            }

            {
                // Max-value GUID
                string guidString = "ffffffff-ffff-ffff-ffff-ffffffffffff";
                string stringPayload = $"\"{guidString}\"";
                JsonToken[] expectedTokens =
                {
                    JsonToken.String(guidString)
                };

                byte[] binaryPayload =
                {
                    BinaryFormat,
                    JsonBinaryEncoding.TypeMarker.String1ByteLength,
                    (byte)guidString.Length,
                };
                binaryPayload = binaryPayload.Concat(Encoding.UTF8.GetBytes(guidString)).ToArray();

                byte[] compressedBinaryPayload =
                {
                    BinaryFormat,
                    JsonBinaryEncoding.TypeMarker.LowercaseGuidString,
                    0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
                    0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
                };

                this.VerifyReader(stringPayload, expectedTokens);
                this.VerifyReader(binaryPayload, expectedTokens);
                this.VerifyReader(compressedBinaryPayload, expectedTokens);
            }

            {
                // Lowercase quoted guid
                string guidString = "\"ffffffff-ffff-ffff-ffff-ffffffffffff\"";
                string stringPayload = $"\"\\\"ffffffff-ffff-ffff-ffff-ffffffffffff\\\"\"";
                JsonToken[] expectedTokens =
                {
                    JsonToken.String(guidString)
                };

                byte[] binaryPayload =
                {
                    BinaryFormat,
                    JsonBinaryEncoding.TypeMarker.String1ByteLength,
                    (byte)guidString.Length,
                };
                binaryPayload = binaryPayload.Concat(Encoding.UTF8.GetBytes(guidString)).ToArray();

                byte[] compressedBinaryPayload =
                {
                    BinaryFormat,
                    JsonBinaryEncoding.TypeMarker.DoubleQuotedLowercaseGuidString,
                    0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
                    0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
                };

                this.VerifyReader(stringPayload, expectedTokens);
                this.VerifyReader(binaryPayload, expectedTokens);
                this.VerifyReader(compressedBinaryPayload, expectedTokens);
            }
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void DateTimeStringsTest()
        {
            {
                string dateTimeString = "2015-06-30 23:45:13";
                string stringPayload = $"\"{dateTimeString}\"";
                JsonToken[] expectedTokens =
                {
                    JsonToken.String(dateTimeString)
                };

                byte[] binaryPayload =
                {
                    BinaryFormat,
                    JsonBinaryEncoding.TypeMarker.String1ByteLength,
                    (byte)dateTimeString.Length,
                };
                binaryPayload = binaryPayload.Concat(Encoding.UTF8.GetBytes(dateTimeString)).ToArray();

                byte[] compressedBinaryPayload =
                {
                    BinaryFormat,
                    JsonBinaryEncoding.TypeMarker.CompressedDateTimeString,
                    0x13, 0x13, 0x62, 0x1C, 0xC7, 0x14, 0x30, 0xB4, 0x65, 0x2B, 0x04
                };

                this.VerifyReader(stringPayload, expectedTokens);
                this.VerifyReader(binaryPayload, expectedTokens);
                this.VerifyReader(compressedBinaryPayload, expectedTokens);
            }

            {
                string dateTimeString = "2010-08-02T14:27:44-07:00";
                string stringPayload = $"\"{dateTimeString}\"";
                JsonToken[] expectedTokens =
                {
                    JsonToken.String(dateTimeString)
                };

                byte[] binaryPayload =
                {
                    BinaryFormat,
                    JsonBinaryEncoding.TypeMarker.String1ByteLength,
                    (byte)dateTimeString.Length,
                };
                binaryPayload = binaryPayload.Concat(Encoding.UTF8.GetBytes(dateTimeString)).ToArray();

                byte[] compressedBinaryPayload =
                {
                    BinaryFormat,
                    JsonBinaryEncoding.TypeMarker.CompressedDateTimeString,
                    0x19, 0x13, 0x12, 0x1C, 0xC9, 0x31, 0x2E, 0xB5, 0x83, 0x5B, 0xC5, 0x81, 0x1B, 0x01
                };

                this.VerifyReader(stringPayload, expectedTokens);
                this.VerifyReader(binaryPayload, expectedTokens);
                this.VerifyReader(compressedBinaryPayload, expectedTokens);
            }

            {
                string dateTimeString = "2007-03-01T13:00:00Z";
                string stringPayload = $"\"{dateTimeString}\"";
                JsonToken[] expectedTokens =
                {
                    JsonToken.String(dateTimeString)
                };

                byte[] binaryPayload =
                {
                    BinaryFormat,
                    JsonBinaryEncoding.TypeMarker.String1ByteLength,
                    (byte)dateTimeString.Length,
                };
                binaryPayload = binaryPayload.Concat(Encoding.UTF8.GetBytes(dateTimeString)).ToArray();

                byte[] compressedBinaryPayload =
                {
                    BinaryFormat,
                    JsonBinaryEncoding.TypeMarker.CompressedDateTimeString,
                    0x14, 0x13, 0x81, 0x1C, 0xC4, 0x21, 0x2E, 0xB4, 0x11, 0x1B, 0xF1
                };

                this.VerifyReader(stringPayload, expectedTokens);
                this.VerifyReader(binaryPayload, expectedTokens);
                this.VerifyReader(compressedBinaryPayload, expectedTokens);
            }

            {
                string dateTimeString = "2007-03-01T13:00:00Z";
                string stringPayload = $"\"{dateTimeString}\"";
                JsonToken[] expectedTokens =
                {
                    JsonToken.String(dateTimeString)
                };

                byte[] binaryPayload =
                {
                    BinaryFormat,
                    JsonBinaryEncoding.TypeMarker.String1ByteLength,
                    (byte)dateTimeString.Length,
                };
                binaryPayload = binaryPayload.Concat(Encoding.UTF8.GetBytes(dateTimeString)).ToArray();

                byte[] compressedBinaryPayload =
                {
                    BinaryFormat,
                    JsonBinaryEncoding.TypeMarker.CompressedDateTimeString,
                    0x14, 0x13, 0x81, 0x1C, 0xC4, 0x21, 0x2E, 0xB4, 0x11, 0x1B, 0xF1
                };

                this.VerifyReader(stringPayload, expectedTokens);
                this.VerifyReader(binaryPayload, expectedTokens);
                this.VerifyReader(compressedBinaryPayload, expectedTokens);
            }

            {
                string dateTimeString = "2014-10-18T14:18:17.5337932-07:00";
                string stringPayload = $"\"{dateTimeString}\"";
                JsonToken[] expectedTokens =
                {
                    JsonToken.String(dateTimeString)
                };

                byte[] binaryPayload =
                {
                    BinaryFormat,
                    JsonBinaryEncoding.TypeMarker.String1ByteLength,
                    (byte)dateTimeString.Length,
                };
                binaryPayload = binaryPayload.Concat(Encoding.UTF8.GetBytes(dateTimeString)).ToArray();

                byte[] compressedBinaryPayload =
                {
                    BinaryFormat,
                    JsonBinaryEncoding.TypeMarker.CompressedDateTimeString,
                    0x21, 0x13, 0x52, 0x2C, 0xC1, 0x92, 0x2E, 0xB5, 0x92, 0x2B, 0xD8, 0x46, 0x84, 0x4A, 0xC3, 0x81, 0x1B, 0x01
                };

                this.VerifyReader(stringPayload, expectedTokens);
                this.VerifyReader(binaryPayload, expectedTokens);
                this.VerifyReader(compressedBinaryPayload, expectedTokens);
            }

            {
                string dateTimeString = "2014-10-18T14:18:17.5337932-07:00";
                string stringPayload = $"\"{dateTimeString}\"";
                JsonToken[] expectedTokens =
                {
                    JsonToken.String(dateTimeString)
                };

                byte[] binaryPayload =
                {
                    BinaryFormat,
                    JsonBinaryEncoding.TypeMarker.String1ByteLength,
                    (byte)dateTimeString.Length,
                };
                binaryPayload = binaryPayload.Concat(Encoding.UTF8.GetBytes(dateTimeString)).ToArray();

                byte[] compressedBinaryPayload =
                {
                    BinaryFormat,
                    JsonBinaryEncoding.TypeMarker.CompressedDateTimeString,
                    0x21, 0x13, 0x52, 0x2C, 0xC1, 0x92, 0x2E, 0xB5, 0x92, 0x2B, 0xD8, 0x46, 0x84, 0x4A, 0xC3, 0x81, 0x1B, 0x01
                };

                this.VerifyReader(stringPayload, expectedTokens);
                this.VerifyReader(binaryPayload, expectedTokens);
                this.VerifyReader(compressedBinaryPayload, expectedTokens);
            }

            {
                string dateTimeString = "0123456789:.-TZ ";
                string stringPayload = $"\"{dateTimeString}\"";
                JsonToken[] expectedTokens =
                {
                    JsonToken.String(dateTimeString)
                };

                byte[] binaryPayload =
                {
                    BinaryFormat,
                    JsonBinaryEncoding.TypeMarker.String1ByteLength,
                    (byte)dateTimeString.Length,
                };
                binaryPayload = binaryPayload.Concat(Encoding.UTF8.GetBytes(dateTimeString)).ToArray();

                byte[] compressedBinaryPayload =
                {
                    BinaryFormat,
                    JsonBinaryEncoding.TypeMarker.CompressedDateTimeString,
                    0x10, 0x21, 0x43, 0x65, 0x87, 0xA9, 0xDB, 0xEC, 0x0F
                };

                this.VerifyReader(stringPayload, expectedTokens);
                this.VerifyReader(binaryPayload, expectedTokens);
                this.VerifyReader(compressedBinaryPayload, expectedTokens);
            }
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void HexStringsTest()
        {
            {
                string hexString = "eccab3900d55b946";
                string stringPayload = $"\"{hexString}\"";
                JsonToken[] expectedTokens =
                {
                    JsonToken.String(hexString)
                };

                byte[] binaryPayload =
                {
                    BinaryFormat,
                    JsonBinaryEncoding.TypeMarker.String1ByteLength,
                    (byte)hexString.Length,
                };
                binaryPayload = binaryPayload.Concat(Encoding.UTF8.GetBytes(hexString)).ToArray();

                byte[] compressedBinaryPayload =
                {
                    BinaryFormat,
                    JsonBinaryEncoding.TypeMarker.CompressedLowercaseHexString,
                    0x10, 0xCE, 0xAC, 0x3B, 0x09, 0xD0, 0x55, 0x9B, 0x64
                };

                this.VerifyReader(stringPayload, expectedTokens);
                this.VerifyReader(binaryPayload, expectedTokens);
                this.VerifyReader(compressedBinaryPayload, expectedTokens);
            }

            {
                string hexString = "ECCAB3900D55B946";
                string stringPayload = $"\"{hexString}\"";
                JsonToken[] expectedTokens =
                {
                    JsonToken.String(hexString)
                };

                byte[] binaryPayload =
                {
                    BinaryFormat,
                    JsonBinaryEncoding.TypeMarker.String1ByteLength,
                    (byte)hexString.Length,
                };
                binaryPayload = binaryPayload.Concat(Encoding.UTF8.GetBytes(hexString)).ToArray();

                byte[] compressedBinaryPayload =
                {
                    BinaryFormat,
                    JsonBinaryEncoding.TypeMarker.CompressedUppercaseHexString,
                    0x10, 0xCE, 0xAC, 0x3B, 0x09, 0xD0, 0x55, 0x9B, 0x64
                };

                this.VerifyReader(stringPayload, expectedTokens);
                this.VerifyReader(binaryPayload, expectedTokens);
                this.VerifyReader(compressedBinaryPayload, expectedTokens);
            }
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void EncodedStringLengthTests()
        {
            for (int i = JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin; i < JsonBinaryEncoding.TypeMarker.EncodedStringLengthMax; i++)
            {
                string stringValue = new string('=', count: i - JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin);
                string stringPayload = $"\"{stringValue}\"";
                JsonToken[] expectedTokens =
                {
                    JsonToken.String(stringValue)
                };

                byte[] binaryPayload =
                {
                    BinaryFormat,
                    (byte)i,
                };
                binaryPayload = binaryPayload.Concat(Encoding.UTF8.GetBytes(stringValue)).ToArray();

                this.VerifyReader(stringPayload, expectedTokens);
                this.VerifyReader(binaryPayload, expectedTokens);
            }
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void ReferenceStringTests()
        {
            string stringPayload = "[\"hello\", \"hello\"]";
            JsonToken[] expectedTokens =
            {
                    JsonToken.ArrayStart(),
                    JsonToken.String("hello"),
                    JsonToken.String("hello"),
                    JsonToken.ArrayEnd()
            };

            this.VerifyReader(stringPayload, expectedTokens);

            {
                // 1 byte reference string
                byte[] binaryPayload =
                {
                        BinaryFormat,
                        JsonBinaryEncoding.TypeMarker.ArrL1,
                        9,
                        JsonBinaryEncoding.TypeMarker.String1ByteLength,
                        (byte)"hello".Length,
                        (byte)'h', (byte)'e', (byte)'l', (byte)'l', (byte)'o',

                        JsonBinaryEncoding.TypeMarker.ReferenceString1ByteOffset,
                        3,
                };

                this.VerifyReader(binaryPayload, expectedTokens);
            }

            {
                // 2 byte reference string
                byte[] binaryPayload =
                {
                        BinaryFormat,
                        JsonBinaryEncoding.TypeMarker.ArrL1,
                        10,
                        JsonBinaryEncoding.TypeMarker.String1ByteLength,
                        (byte)"hello".Length,
                        (byte)'h', (byte)'e', (byte)'l', (byte)'l', (byte)'o',

                        JsonBinaryEncoding.TypeMarker.ReferenceString2ByteOffset,
                        3, 0,
                };

                this.VerifyReader(binaryPayload, expectedTokens);
            }

            {
                // 3 byte reference string
                byte[] binaryPayload =
                {
                        BinaryFormat,
                        JsonBinaryEncoding.TypeMarker.ArrL1,
                        11,
                        JsonBinaryEncoding.TypeMarker.String1ByteLength,
                        (byte)"hello".Length,
                        (byte)'h', (byte)'e', (byte)'l', (byte)'l', (byte)'o',

                        JsonBinaryEncoding.TypeMarker.ReferenceString3ByteOffset,
                        3, 0, 0
                };

                this.VerifyReader(binaryPayload, expectedTokens);
            }

            {
                // 4 byte reference string
                byte[] binaryPayload =
                {
                        BinaryFormat,
                        JsonBinaryEncoding.TypeMarker.ArrL1,
                        12,
                        JsonBinaryEncoding.TypeMarker.String1ByteLength,
                        (byte)"hello".Length,
                        (byte)'h', (byte)'e', (byte)'l', (byte)'l', (byte)'o',

                        JsonBinaryEncoding.TypeMarker.ReferenceString4ByteOffset,
                        3, 0, 0, 0
                };

                this.VerifyReader(binaryPayload, expectedTokens);
            }
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void PackedStringsTest()
        {
            {
                // 4 bit packed string
                string compressedString = "ababababababababababababababababg";
                string stringPayload = $"\"{compressedString}\"";
                JsonToken[] expectedTokens =
                {
                    JsonToken.String(compressedString)
                };

                byte[] compressedBinaryPayload =
                {
                    BinaryFormat,
                    JsonBinaryEncoding.TypeMarker.Packed4BitString,
                    0x21, 0x61, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10,
                    0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10,
                    0x10, 0x10, 0xF6
                };

                this.VerifyReader(stringPayload, expectedTokens);
                this.VerifyReader(compressedBinaryPayload, expectedTokens);
            }

            {
                // 5 bit packed string
                string compressedString = "thequickbrownfoxjumpedoverthelazydog";
                string stringPayload = $"\"{compressedString}\"";
                JsonToken[] expectedTokens =
                {
                    JsonToken.String(compressedString)
                };

                byte[] compressedBinaryPayload =
                {
                    BinaryFormat,
                    JsonBinaryEncoding.TypeMarker.Packed5BitString,
                    0x24, 0x61, 0xF3, 0x10, 0x48, 0x91, 0x50, 0x21,
                    0x3A, 0xDB, 0x8A, 0xBB, 0x89, 0xB2, 0x47, 0x86,
                    0xAB, 0x24, 0xCE, 0x43, 0x16, 0xC8, 0x78, 0x38,
                    0xF3
                };

                this.VerifyReader(stringPayload, expectedTokens);
                this.VerifyReader(compressedBinaryPayload, expectedTokens);
            }

            {
                // 6 bit packed string
                string compressedString = "thequickbrownfoxjumpedoverthelazydogTHEQUICKBROWNFOXJUMPEDOVERTHELAZYDOG";
                string stringPayload = $"\"{compressedString}\"";
                JsonToken[] expectedTokens =
                {
                    JsonToken.String(compressedString)
                };

                byte[] compressedBinaryPayload =
                {
                    BinaryFormat,
                    JsonBinaryEncoding.TypeMarker.Packed6BitString,
                    0x48, 0x41, 0xF3, 0x49, 0xC2, 0x34, 0x2A, 0xAA,
                    0x61, 0xEC, 0xDA, 0x6D, 0xE9, 0xDE, 0x29, 0xCD,
                    0xBE, 0xE4, 0xE8, 0xD6, 0x64, 0x3C, 0x9F, 0xE4,
                    0x0A, 0xE6, 0xF8, 0xE8, 0x9A, 0xD3, 0x41, 0x40,
                    0x14, 0x22, 0x28, 0x41, 0xE4, 0x58, 0x4D, 0xE1,
                    0x5C, 0x09, 0xC5, 0x3C, 0xC4, 0xE0, 0x54, 0x44,
                    0x34, 0x1D, 0xC4, 0x02, 0x64, 0xD8, 0xE0, 0x18
                };

                this.VerifyReader(stringPayload, expectedTokens);
                this.VerifyReader(compressedBinaryPayload, expectedTokens);
            }

            {
                // 7 bit packed string length 1
                string compressedString = "thequickbrownfoxjumpedoverthelazydogTHEQUICKBROWNFOXJUMPEDOVERTHELAZYDOG0123456789!@#$%^&*";
                string stringPayload = $"\"{compressedString}\"";
                JsonToken[] expectedTokens =
                {
                    JsonToken.String(compressedString)
                };

                byte[] compressedBinaryPayload =
                {
                    BinaryFormat,
                    JsonBinaryEncoding.TypeMarker.Packed7BitStringLength1,
                    0x5A, 0x74, 0x74, 0x39, 0x5E, 0x4F, 0x8F, 0xD7,
                    0x62, 0xF9, 0xFB, 0xEE, 0x36, 0xBF, 0xF1, 0xEA,
                    0x7A, 0x1B, 0x5E, 0x26, 0xBF, 0xED, 0x65, 0x39,
                    0x1D, 0x5D, 0x66, 0x87, 0xF5, 0x79, 0xF2, 0xFB,
                    0x4C, 0x45, 0x16, 0xA3, 0xD5, 0xE4, 0x70, 0x29,
                    0x94, 0x3E, 0xAF, 0x4E, 0xE3, 0x13, 0xAB, 0xAC,
                    0x36, 0xA1, 0x45, 0xE2, 0xD3, 0x5A, 0x94, 0x52,
                    0x91, 0x45, 0x66, 0x50, 0x9B, 0x25, 0x3E, 0x8F,
                    0xB0, 0x98, 0x6C, 0x46, 0xAB, 0xD9, 0x6E, 0xB8,
                    0x5C, 0x08, 0x38, 0x22, 0x95, 0xBC, 0x26, 0x15
                };

                this.VerifyReader(stringPayload, expectedTokens);
                this.VerifyReader(compressedBinaryPayload, expectedTokens);
            }

            {
                // 7 bit packed string length 2
                string compressedString = "thequickbrownfoxjumpedoverthelazydogTHEQUICKBROWNFOXJUMPEDOVERTHELAZYDOG0123456789!@#$%^&*";
                compressedString = compressedString + compressedString + compressedString;
                string stringPayload = $"\"{compressedString}\"";
                JsonToken[] expectedTokens =
                {
                    JsonToken.String(compressedString)
                };

                byte[] compressedBinaryPayload =
                {
                    BinaryFormat,
                    JsonBinaryEncoding.TypeMarker.Packed7BitStringLength2,
                    0x0E, 0x01, 0x74, 0x74, 0x39, 0x5E, 0x4F, 0x8F,
                    0xD7, 0x62, 0xF9, 0xFB, 0xEE, 0x36, 0xBF, 0xF1,
                    0xEA, 0x7A, 0x1B, 0x5E, 0x26, 0xBF, 0xED, 0x65,
                    0x39, 0x1D, 0x5D, 0x66, 0x87, 0xF5, 0x79, 0xF2,
                    0xFB, 0x4C, 0x45, 0x16, 0xA3, 0xD5, 0xE4, 0x70,
                    0x29, 0x94, 0x3E, 0xAF, 0x4E, 0xE3, 0x13, 0xAB,
                    0xAC, 0x36, 0xA1, 0x45, 0xE2, 0xD3, 0x5A, 0x94,
                    0x52, 0x91, 0x45, 0x66, 0x50, 0x9B, 0x25, 0x3E,
                    0x8F, 0xB0, 0x98, 0x6C, 0x46, 0xAB, 0xD9, 0x6E,
                    0xB8, 0x5C, 0x08, 0x38, 0x22, 0x95, 0xBC, 0x26,
                    0x15, 0x1D, 0x5D, 0x8E, 0xD7, 0xD3, 0xE3, 0xB5,
                    0x58, 0xFE, 0xBE, 0xBB, 0xCD, 0x6F, 0xBC, 0xBA,
                    0xDE, 0x86, 0x97, 0xC9, 0x6F, 0x7B, 0x59, 0x4E,
                    0x47, 0x97, 0xD9, 0x61, 0x7D, 0x9E, 0xFC, 0x3E,
                    0x53, 0x91, 0xC5, 0x68, 0x35, 0x39, 0x5C, 0x0A,
                    0xA5, 0xCF, 0xAB, 0xD3, 0xF8, 0xC4, 0x2A, 0xAB,
                    0x4D, 0x68, 0x91, 0xF8, 0xB4, 0x16, 0xA5, 0x54,
                    0x64, 0x91, 0x19, 0xD4, 0x66, 0x89, 0xCF, 0x23,
                    0x2C, 0x26, 0x9B, 0xD1, 0x6A, 0xB6, 0x1B, 0x2E,
                    0x17, 0x02, 0x8E, 0x48, 0x25, 0xAF, 0x49, 0x45,
                    0x47, 0x97, 0xE3, 0xF5, 0xF4, 0x78, 0x2D, 0x96,
                    0xBF, 0xEF, 0x6E, 0xF3, 0x1B, 0xAF, 0xAE, 0xB7,
                    0xE1, 0x65, 0xF2, 0xDB, 0x5E, 0x96, 0xD3, 0xD1,
                    0x65, 0x76, 0x58, 0x9F, 0x27, 0xBF, 0xCF, 0x54,
                    0x64, 0x31, 0x5A, 0x4D, 0x0E, 0x97, 0x42, 0xE9,
                    0xF3, 0xEA, 0x34, 0x3E, 0xB1, 0xCA, 0x6A, 0x13,
                    0x5A, 0x24, 0x3E, 0xAD, 0x45, 0x29, 0x15, 0x59,
                    0x64, 0x06, 0xB5, 0x59, 0xE2, 0xF3, 0x08, 0x8B,
                    0xC9, 0x66, 0xB4, 0x9A, 0xED, 0x86, 0xCB, 0x85,
                    0x80, 0x23, 0x52, 0xC9, 0x6B, 0x52, 0x01
                };

                this.VerifyReader(stringPayload, expectedTokens);
                this.VerifyReader(compressedBinaryPayload, expectedTokens);
            }
        }
        #endregion
        #region Arrays
        [TestMethod]
        [Owner("mayapainter")]
        public void ArrayRepresentationTest()
        {
            string input = "[true, false]";
            byte[] binary1ByteLengthInput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.ArrL1,
                // length
                2,
                JsonBinaryEncoding.TypeMarker.True,
                JsonBinaryEncoding.TypeMarker.False,
            };

            byte[] binary1ByteLengthAndCountInput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.ArrLC1,
                // length
                2,
                // count
                2,
                JsonBinaryEncoding.TypeMarker.True,
                JsonBinaryEncoding.TypeMarker.False,
            };

            byte[] binary2ByteLengthInput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.ArrL2,
                // length
                2, 0x00,
                JsonBinaryEncoding.TypeMarker.True,
                JsonBinaryEncoding.TypeMarker.False,
            };

            byte[] binary2ByteLengthAndCountInput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.ArrLC2,
                // length
                2, 0x00,
                // count
                2, 0x00,
                JsonBinaryEncoding.TypeMarker.True,
                JsonBinaryEncoding.TypeMarker.False,
            };

            byte[] binary4ByteLengthInput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.ArrL4,
                // length
                2, 0x00, 0x00, 0x00,
                JsonBinaryEncoding.TypeMarker.True,
                JsonBinaryEncoding.TypeMarker.False,
            };

            byte[] binary4ByteLengthAndCountInput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.ArrLC4,
                // length
                2, 0x00, 0x00, 0x00,
                // count
                2, 0x00, 0x00, 0x00,
                JsonBinaryEncoding.TypeMarker.True,
                JsonBinaryEncoding.TypeMarker.False,
            };

            JsonToken[] expectedTokens =
            {
                JsonToken.ArrayStart(),
                JsonToken.Boolean(true),
                JsonToken.Boolean(false),
                JsonToken.ArrayEnd(),
            };

            this.VerifyReader(input, expectedTokens);
            this.VerifyReader(binary1ByteLengthInput, expectedTokens);
            this.VerifyReader(binary1ByteLengthAndCountInput, expectedTokens);
            this.VerifyReader(binary2ByteLengthInput, expectedTokens);
            this.VerifyReader(binary2ByteLengthAndCountInput, expectedTokens);
            this.VerifyReader(binary4ByteLengthInput, expectedTokens);
            this.VerifyReader(binary4ByteLengthAndCountInput, expectedTokens);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void EmptyArrayTest()
        {
            string input = "[  ]  ";
            byte[] binaryInput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.Arr0
            };

            JsonToken[] expectedTokens =
            {
                JsonToken.ArrayStart(),
                JsonToken.ArrayEnd(),
            };

            this.VerifyReader(input, expectedTokens);
            this.VerifyReader(binaryInput, expectedTokens);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void SingleItemArrayTest()
        {
            string input = "[ true ]  ";
            byte[] binaryInput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.Arr1,
                JsonBinaryEncoding.TypeMarker.True
            };

            JsonToken[] expectedTokens =
            {
                JsonToken.ArrayStart(),
                JsonToken.Boolean(true),
                JsonToken.ArrayEnd(),
            };

            this.VerifyReader(input, expectedTokens);
            this.VerifyReader(binaryInput, expectedTokens);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void IntArrayTest()
        {
            string input = "[ -2, -1, 0, 1, 2]  ";
            List<byte[]> binaryInputBuilder = new List<byte[]>
            {
                new byte[] { BinaryFormat, JsonBinaryEncoding.TypeMarker.ArrL1 }
            };

            List<byte[]> numbers = new List<byte[]>
            {
                new byte[] { JsonBinaryEncoding.TypeMarker.NumberInt16, 0xFE, 0xFF },
                new byte[] { JsonBinaryEncoding.TypeMarker.NumberInt16, 0xFF, 0xFF },
                new byte[] { JsonBinaryEncoding.TypeMarker.LiteralIntMin },
                new byte[] { JsonBinaryEncoding.TypeMarker.LiteralIntMin + 1 },
                new byte[] { JsonBinaryEncoding.TypeMarker.LiteralIntMin + 2 }
            };
            byte[] numbersBytes = numbers.SelectMany(x => x).ToArray();

            binaryInputBuilder.Add(new byte[] { (byte)numbersBytes.Length });
            binaryInputBuilder.Add(numbersBytes);
            byte[] binaryInput = binaryInputBuilder.SelectMany(x => x).ToArray();

            JsonToken[] expectedTokens =
            {
                JsonToken.ArrayStart(),
                JsonToken.Number(-2),
                JsonToken.Number(-1),
                JsonToken.Number(0),
                JsonToken.Number(1),
                JsonToken.Number(2),
                JsonToken.ArrayEnd(),
            };

            this.VerifyReader(input, expectedTokens);
            this.VerifyReader(binaryInput, expectedTokens);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void NumberArrayTest()
        {
            string input = "[15,  22, 0.1, -7.3e-2, 77.0001e90 ]  ";

            List<byte[]> binaryInputBuilder = new List<byte[]>
            {
                new byte[] { BinaryFormat, JsonBinaryEncoding.TypeMarker.ArrL1 }
            };

            List<byte[]> numbers = new List<byte[]>
            {
                new byte[] { JsonBinaryEncoding.TypeMarker.LiteralIntMin + 15 },
                new byte[] { JsonBinaryEncoding.TypeMarker.LiteralIntMin + 22 },
                new byte[] { JsonBinaryEncoding.TypeMarker.NumberDouble, 0x9A, 0x99, 0x99, 0x99, 0x99, 0x99, 0xB9, 0x3F },
                new byte[] { JsonBinaryEncoding.TypeMarker.NumberDouble, 0xE3, 0xA5, 0x9B, 0xC4, 0x20, 0xB0, 0xB2, 0xBF },
                new byte[] { JsonBinaryEncoding.TypeMarker.NumberDouble, 0xBE, 0xDA, 0x50, 0xA7, 0x68, 0xE6, 0x02, 0x53 }
            };
            byte[] numbersBytes = numbers.SelectMany(x => x).ToArray();

            binaryInputBuilder.Add(new byte[] { (byte)numbersBytes.Length });
            binaryInputBuilder.Add(numbersBytes);
            byte[] binaryInput = binaryInputBuilder.SelectMany(x => x).ToArray();

            JsonToken[] expectedTokens =
            {
                JsonToken.ArrayStart(),
                JsonToken.Number(15),
                JsonToken.Number(22),
                JsonToken.Number(0.1),
                JsonToken.Number(-7.3e-2),
                JsonToken.Number(77.0001e90),
                JsonToken.ArrayEnd(),
            };

            this.VerifyReader(input, expectedTokens);
            this.VerifyReader(binaryInput, expectedTokens);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void StringArrayTest()
        {
            string input = @"[""Hello"", ""World"", ""Bye""]";

            List<byte[]> binaryInputBuilder = new List<byte[]>
            {
                new byte[] { BinaryFormat, JsonBinaryEncoding.TypeMarker.ArrL1 }
            };

            List<byte[]> strings = new List<byte[]>
            {
                new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + "Hello".Length) },
                Encoding.UTF8.GetBytes("Hello"),
                new byte[] { JsonBinaryEncoding.TypeMarker.String1ByteLength },
                new byte[] { (byte)"World".Length },
                Encoding.UTF8.GetBytes("World"),
                new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + "Bye".Length) },
                Encoding.UTF8.GetBytes("Bye")
            };
            byte[] stringBytes = strings.SelectMany(x => x).ToArray();

            binaryInputBuilder.Add(new byte[] { (byte)stringBytes.Length });
            binaryInputBuilder.Add(stringBytes);
            byte[] binaryInput = binaryInputBuilder.SelectMany(x => x).ToArray();

            JsonToken[] expectedTokens =
            {
                JsonToken.ArrayStart(),
                JsonToken.String("Hello"),
                JsonToken.String("World"),
                JsonToken.String("Bye"),
                JsonToken.ArrayEnd(),
            };

            this.VerifyReader(input, expectedTokens);
            this.VerifyReader(binaryInput, expectedTokens);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void BooleanArrayTest()
        {
            string input = "[ true, false]  ";
            byte[] binaryInput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.ArrL1,
                // length
                2,
                JsonBinaryEncoding.TypeMarker.True,
                JsonBinaryEncoding.TypeMarker.False,
            };

            JsonToken[] expectedTokens =
            {
                JsonToken.ArrayStart(),
                JsonToken.Boolean(true),
                JsonToken.Boolean(false),
                JsonToken.ArrayEnd(),
            };

            this.VerifyReader(input, expectedTokens);
            this.VerifyReader(binaryInput, expectedTokens);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void NullArrayTest()
        {
            string input = "[ null, null, null]  ";
            byte[] binaryInput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.ArrL1,
                // length
                3,
                JsonBinaryEncoding.TypeMarker.Null,
                JsonBinaryEncoding.TypeMarker.Null,
                JsonBinaryEncoding.TypeMarker.Null,
            };

            JsonToken[] expectedTokens =
            {
                JsonToken.ArrayStart(),
                JsonToken.Null(),
                JsonToken.Null(),
                JsonToken.Null(),
                JsonToken.ArrayEnd(),
            };

            this.VerifyReader(input, expectedTokens);
            this.VerifyReader(binaryInput, expectedTokens);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void ObjectArrayTest()
        {
            string input = "[{}, {}]  ";
            byte[] binaryInput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.ArrL1,
                // length
                2,
                JsonBinaryEncoding.TypeMarker.Obj0,
                JsonBinaryEncoding.TypeMarker.Obj0,
            };

            JsonToken[] expectedTokens =
            {
                JsonToken.ArrayStart(),
                JsonToken.ObjectStart(),
                JsonToken.ObjectEnd(),
                JsonToken.ObjectStart(),
                JsonToken.ObjectEnd(),
                JsonToken.ArrayEnd(),
            };

            this.VerifyReader(input, expectedTokens);
            this.VerifyReader(binaryInput, expectedTokens);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void AllPrimitiveArrayTest()
        {
            string input = "[0, 0.0, -1, -1.0, 1, 2, \"hello\", null, true, false]  ";
            List<byte[]> binaryInputBuilder = new List<byte[]>
            {
                new byte[] { BinaryFormat, JsonBinaryEncoding.TypeMarker.ArrL1 }
            };

            List<byte[]> elements = new List<byte[]>
            {
                new byte[] { JsonBinaryEncoding.TypeMarker.LiteralIntMin },
                new byte[] { JsonBinaryEncoding.TypeMarker.NumberDouble, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 },
                new byte[] { JsonBinaryEncoding.TypeMarker.NumberInt16, 0xFF, 0xFF },
                new byte[] { JsonBinaryEncoding.TypeMarker.NumberDouble, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xF0, 0xBF },
                new byte[] { JsonBinaryEncoding.TypeMarker.LiteralIntMin + 1 },
                new byte[] { JsonBinaryEncoding.TypeMarker.LiteralIntMin + 2 },
                new byte[] { JsonBinaryEncoding.TypeMarker.String1ByteLength, (byte)"hello".Length, 104, 101, 108, 108, 111 },
                new byte[] { JsonBinaryEncoding.TypeMarker.Null },
                new byte[] { JsonBinaryEncoding.TypeMarker.True },
                new byte[] { JsonBinaryEncoding.TypeMarker.False }
            };
            byte[] elementsBytes = elements.SelectMany(x => x).ToArray();

            binaryInputBuilder.Add(new byte[] { (byte)elementsBytes.Length });
            binaryInputBuilder.Add(elementsBytes);
            byte[] binaryInput = binaryInputBuilder.SelectMany(x => x).ToArray();

            JsonToken[] expectedTokens =
            {
                JsonToken.ArrayStart(),
                JsonToken.Number(0),
                JsonToken.Number(0.0),
                JsonToken.Number(-1),
                JsonToken.Number(-1.0),
                JsonToken.Number(1),
                JsonToken.Number(2),
                JsonToken.String("hello"),
                JsonToken.Null(),
                JsonToken.Boolean(true),
                JsonToken.Boolean(false),
                JsonToken.ArrayEnd(),
            };

            this.VerifyReader(input, expectedTokens);
            this.VerifyReader(binaryInput, expectedTokens);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void NestedArrayTest()
        {
            string input = "[[], []]  ";
            byte[] binaryInput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.ArrL1,
                // length
                2,
                JsonBinaryEncoding.TypeMarker.Arr0,
                JsonBinaryEncoding.TypeMarker.Arr0,
            };

            JsonToken[] expectedTokens =
            {
                JsonToken.ArrayStart(),
                JsonToken.ArrayStart(),
                JsonToken.ArrayEnd(),
                JsonToken.ArrayStart(),
                JsonToken.ArrayEnd(),
                JsonToken.ArrayEnd(),
            };

            this.VerifyReader(input, expectedTokens);
            this.VerifyReader(binaryInput, expectedTokens);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void StrangeNumberArrayTest()
        {
            string input = @"[
        1111111110111111111011111111101111111110,
        1111111110111111111011111111101111111110111111111011111111101111111110,
       11111111101111111110111111111011111111101111111110111111111011111111101111111110111111111011111111101111111110111111111011111111101111111110,
        1111111110111111111011111111101111111110111111111011111111101111111110111111111011111111101111111110111111111011111111101111111110111111111011111111101111111110111111111011111111101111111110111111111011111111101111111110111111111011111111101111111110111111111011111111101111111110
            ]";

            List<byte[]> binaryInputBuilder = new List<byte[]>
            {
                new byte[] { BinaryFormat, JsonBinaryEncoding.TypeMarker.ArrL1 }
            };

            List<byte[]> elements = new List<byte[]>
            {
                new byte[] { JsonBinaryEncoding.TypeMarker.NumberDouble, 0xBC, 0xCA, 0x0F, 0xBA, 0x41, 0x1F, 0x0A, 0x48 },
                new byte[] { JsonBinaryEncoding.TypeMarker.NumberDouble, 0xDB, 0x5E, 0xAE, 0xBE, 0x50, 0x9B, 0x44, 0x4E },
                new byte[] { JsonBinaryEncoding.TypeMarker.NumberDouble, 0x32, 0x80, 0x84, 0x3C, 0x73, 0xDB, 0xCD, 0x5C },
                new byte[] { JsonBinaryEncoding.TypeMarker.NumberDouble, 0x8D, 0x0D, 0x28, 0x0B, 0x16, 0x57, 0xDF, 0x79 }
            };
            byte[] elementsBytes = elements.SelectMany(x => x).ToArray();

            binaryInputBuilder.Add(new byte[] { (byte)elementsBytes.Length });
            binaryInputBuilder.Add(elementsBytes);
            byte[] binaryInput = binaryInputBuilder.SelectMany(x => x).ToArray();

            JsonToken[] expectedTokens =
            {
                JsonToken.ArrayStart(),
                JsonToken.Number(1111111110111111111011111111101111111110.0),
                JsonToken.Number(1111111110111111111011111111101111111110111111111011111111101111111110.0),
                JsonToken.Number(1.1111111101111111e+139),
                JsonToken.Number(1.1111111101111111e+279),
                JsonToken.ArrayEnd(),
            };

            this.VerifyReader(input, expectedTokens);
            this.VerifyReader(binaryInput, expectedTokens);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void ExtendedTypeArrayTest()
        {
            string input = @"[I1,H2,L3,LL4,UL5,S6,D7,BbilGgyjThtuSzs5Md/5Dew==,G00000000-0000-0000-0000-000000000000]";

            List<byte> bytes = new List<byte>
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.ArrL1,
                JsonBinaryEncoding.TypeMarker.Int8,
                (byte)1,
                JsonBinaryEncoding.TypeMarker.Int16
            };
            bytes.AddRange(BitConverter.GetBytes((short)2));
            bytes.Add(JsonBinaryEncoding.TypeMarker.Int32);
            bytes.AddRange(BitConverter.GetBytes((int)3));
            bytes.Add(JsonBinaryEncoding.TypeMarker.Int64);
            bytes.AddRange(BitConverter.GetBytes((long)4));
            bytes.Add(JsonBinaryEncoding.TypeMarker.UInt32);
            bytes.AddRange(BitConverter.GetBytes((uint)5));
            bytes.Add(JsonBinaryEncoding.TypeMarker.Float32);
            bytes.AddRange(BitConverter.GetBytes((float)6));
            bytes.Add(JsonBinaryEncoding.TypeMarker.Float64);
            bytes.AddRange(BitConverter.GetBytes((double)7));
            bytes.Add(JsonBinaryEncoding.TypeMarker.Binary1ByteLength);
            bytes.Add((byte)Convert.FromBase64String("bilGgyjThtuSzs5Md/5Dew==").Length);
            bytes.AddRange(Convert.FromBase64String("bilGgyjThtuSzs5Md/5Dew=="));
            bytes.Add(JsonBinaryEncoding.TypeMarker.Guid);
            bytes.AddRange(Guid.Empty.ToByteArray());
            bytes.Insert(2, (byte)(bytes.Count - 2));

            JsonToken[] expectedTokens =
            {
                JsonToken.ArrayStart(),
                JsonToken.Int8(1),
                JsonToken.Int16(2),
                JsonToken.Int32(3),
                JsonToken.Int64(4),
                JsonToken.UInt32(5),
                JsonToken.Float32(6),
                JsonToken.Float64(7),
                JsonToken.Binary(Convert.FromBase64String("bilGgyjThtuSzs5Md/5Dew==")),
                JsonToken.Guid(Guid.Empty),
                JsonToken.ArrayEnd(),
            };

            this.VerifyReader(input, expectedTokens);
            this.VerifyReader(bytes.ToArray(), expectedTokens);
        }
        #endregion
        #region Escaping
        [TestMethod]
        [Owner("mayapainter")]
        public void EscapeCharacterTest()
        {
            // Set of all escape characters in JSON.
            Tuple<string, string>[] escapeCharacters = new Tuple<string, string>[]
            {
                new Tuple<string, string>(@"\b", "\b"),
                new Tuple<string, string>(@"\f", "\f"),
                new Tuple<string, string>(@"\n", "\n"),
                new Tuple<string, string>(@"\r", "\r"),
                new Tuple<string, string>(@"\t", "\t"),
                new Tuple<string, string>(@"\""", "\""),
                new Tuple<string, string>(@"\\", @"\"),
                new Tuple<string, string>(@"\/", "/"),
            };

            foreach (Tuple<string, string> escapeCharacter in escapeCharacters)
            {
                string input = "\"" + escapeCharacter.Item1 + "\"";
                JsonToken[] expectedTokens =
                {
                     JsonToken.String(escapeCharacter.Item2),
                };

                this.VerifyReader(input, expectedTokens);
                // Binary does not test this since you would just put the literal character if you wanted it.
            }
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void WhitespaceCharacterTest()
        {
            // http://www.ietf.org/rfc/rfc4627.txt for JSON whitespace definition (Section 2).
            string input = "[" + " " + "\"hello\"" + "," + "\t" + "\"my\"" + "\r" + "," + "\"name\"" + "\n" + "," + "\"is\"" + "]";

            JsonToken[] expectedTokens =
            {
                JsonToken.ArrayStart(),
                JsonToken.String("hello"),
                JsonToken.String("my"),
                JsonToken.String("name"),
                JsonToken.String("is"),
                JsonToken.ArrayEnd(),
            };

            this.VerifyReader(input, expectedTokens);
            // Binary does not test this since you would just put the literal character if you wanted it.
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void UnicodeEscapeTest()
        {
            {
                // unicode characters are utf-16 when unescaped by default
                string unicodeEscapedString = @"""\u20AC""";
                // This is the 2 byte escaped equivalent.
                string expectedString = "\x20AC";

                JsonToken[] expectedTokens =
                {
                    JsonToken.String(expectedString),
                };

                this.VerifyReader(unicodeEscapedString, expectedTokens);
                // Binary does not test this since you would just put the literal character if you wanted it.
            }

            {
                // Surrogate pair testing
                string unicodeEscapedString = @"""\uD801\uDC37""";
                // This is the 2 byte escaped equivalent.
                string expectedString = "𐐷";

                JsonToken[] expectedTokens =
                {
                    JsonToken.String(expectedString),
                };

                this.VerifyReader(unicodeEscapedString, expectedTokens);
                // Binary does not test this since you would just put the literal character if you wanted it.
            }
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void TwoAdjacentUnicodeCharactersTest()
        {
            // 2 unicode escape characters that are not surrogate pairs
            string unicodeEscapedString = @"""\u20AC\u20AC""";
            // This is the escaped equivalent.
            string expectedString = "\x20AC\x20AC";

            JsonToken[] expectedTokens =
            {
                 JsonToken.String(expectedString),
            };

            this.VerifyReader(unicodeEscapedString, expectedTokens);
            // Binary does not test this since you would just put the literal character if you wanted it.
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void UnicodeTest()
        {
            // the user might literally paste a unicode character into the json.
            string unicodeString = "\"€\"";
            // This is the 2 byte equivalent.
            string expectedString = "€";
            byte[] binaryInput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + 3,
                // € in utf8 hex
                0xE2, 0x82, 0xAC
            };

            JsonToken[] expectedTokens =
            {
                 JsonToken.String(expectedString),
            };

            this.VerifyReader(unicodeString, expectedTokens);
            this.VerifyReader(binaryInput, expectedTokens);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void EmojiUTF32Test()
        {
            // the user might literally paste a utf 32 character (like the poop emoji).
            string unicodeString = "\"💩\"";
            // This is the 4 byte equivalent.
            string expectedString = "💩";
            byte[] binaryInput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + 4,
                // 💩 in utf8 hex
                0xF0, 0x9F, 0x92, 0xA9
            };

            JsonToken[] expectedTokens =
            {
                 JsonToken.String(expectedString),
            };

            this.VerifyReader(unicodeString, expectedTokens);
            this.VerifyReader(binaryInput, expectedTokens);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void EscapedEmojiUTF32Test()
        {
            // the user might want to encode the utf32 character as an escape utf32 character.
            // older javascript only supports 16-bit Unicode escape sequences with four hex characters in string literals,
            // so there's no other way than to use UTF-16 surrogates (high surrogate and low surrogate) in escape sequences for code points above 0xFFFF 

            // basically its two utf 16 escaped characters
            string unicodeString = "\"\\uD83D\\uDCA9\"";
            // This is the 4 byte equivalent.
            string expectedString = "💩";
            byte[] binaryInput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + 4,
                // 💩 in utf8 hex
                0xF0, 0x9F, 0x92, 0xA9
            };

            JsonToken[] expectedTokens =
            {
                 JsonToken.String(expectedString),
            };

            this.VerifyReader(unicodeString, expectedTokens);
            this.VerifyReader(binaryInput, expectedTokens);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void ControlCharacterTests()
        {
            // control characters (U+0000 through U+001F)
            for (byte controlCharacter = 0; controlCharacter <= 0x1F; controlCharacter++)
            {
                string unicodeString = "\"" + "\\u" + "00" + controlCharacter.ToString("X2") + "\"";

                JsonToken[] expectedTokens =
                {
                    JsonToken.String(string.Empty + (char)controlCharacter)
                };

                this.VerifyReader(unicodeString, expectedTokens);
            }
        }
        #endregion
        #region Objects
        [TestMethod]
        [Owner("mayapainter")]
        public void EmptyObjectTest()
        {
            string input = "{}";
            byte[] binaryInput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.Obj0,
            };

            JsonToken[] expectedTokens =
            {
                 JsonToken.ObjectStart(),
                 JsonToken.ObjectEnd(),
            };

            this.VerifyReader(input, expectedTokens);
            this.VerifyReader(binaryInput, expectedTokens);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void SimpleObjectTest()
        {
            string input = "{\"GlossDiv\":10,\"title\": \"example glossary\" }";

            byte[] binaryInput;
            {
                List<byte[]> binaryInputBuilder = new List<byte[]>
                {
                    new byte[] { BinaryFormat, JsonBinaryEncoding.TypeMarker.ObjL1 }
                };

                List<byte[]> elements = new List<byte[]>
                {
                    new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + "GlossDiv".Length), 71, 108, 111, 115, 115, 68, 105, 118 },
                    new byte[] { JsonBinaryEncoding.TypeMarker.LiteralIntMin + 10 },
                    new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + "title".Length), 116, 105, 116, 108, 101 },
                    new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + "example glossary".Length), 101, 120, 97, 109, 112, 108, 101, 32, 103, 108, 111, 115, 115, 97, 114, 121 }
                };
                byte[] elementsBytes = elements.SelectMany(x => x).ToArray();

                binaryInputBuilder.Add(new byte[] { (byte)elementsBytes.Length });
                binaryInputBuilder.Add(elementsBytes);
                binaryInput = binaryInputBuilder.SelectMany(x => x).ToArray();
            }

            byte[] binaryInputWithEncoding;
            {
                List<byte[]> binaryInputBuilder = new List<byte[]>
                {
                    new byte[] { BinaryFormat, JsonBinaryEncoding.TypeMarker.ObjL1 }
                };

                List<byte[]> elements = new List<byte[]>
                {
                    new byte[] { (byte)JsonBinaryEncoding.TypeMarker.UserString1ByteLengthMin },
                    new byte[] { JsonBinaryEncoding.TypeMarker.LiteralIntMin + 10 },
                    new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.UserString1ByteLengthMin + 1) },
                    new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + "example glossary".Length), 101, 120, 97, 109, 112, 108, 101, 32, 103, 108, 111, 115, 115, 97, 114, 121 }
                };
                byte[] elementsBytes = elements.SelectMany(x => x).ToArray();

                binaryInputBuilder.Add(new byte[] { (byte)elementsBytes.Length });
                binaryInputBuilder.Add(elementsBytes);
                binaryInputWithEncoding = binaryInputBuilder.SelectMany(x => x).ToArray();
            }

            JsonToken[] expectedTokens =
            {
                JsonToken.ObjectStart(),
                JsonToken.FieldName("GlossDiv"),
                JsonToken.Number(10),
                JsonToken.FieldName("title"),
                JsonToken.String("example glossary"),
                JsonToken.ObjectEnd(),
            };

            this.VerifyReader(input, expectedTokens);
            this.VerifyReader(binaryInput, expectedTokens);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void AllPrimitivesObjectTest()
        {
            string input = @"{
                ""id"": ""7029d079-4016-4436-b7da-36c0bae54ff6"",
                ""double"": 0.18963001816981939,
                ""int"": -1330192615,
                ""string"": ""XCPCFXPHHF"",
                ""boolean"": true,
                ""null"": null,
                ""datetime"": ""2526-07-11T18:18:16.4520716"",
                ""spatialPoint"": {
                    ""type"": ""Point"",
                    ""coordinates"": [
                        118.9897,
                        -46.6781
                    ]
                },
                ""text"": ""tiger diamond newbrunswick snowleopard chocolate dog snowleopard turtle cat sapphire peach sapphire vancouver white chocolate horse diamond lion superlongcolourname ruby""
            }";

            byte[] binaryInput;
            {
                List<byte[]> binaryInputBuilder = new List<byte[]>
                {
                    new byte[] { BinaryFormat, JsonBinaryEncoding.TypeMarker.ObjL2 }
                };

                List<byte[]> elements = new List<byte[]>
                {
                    new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.SystemString1ByteLengthMin + 12) },
                    new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + "7029d079-4016-4436-b7da-36c0bae54ff6".Length), 55, 48, 50, 57, 100, 48, 55, 57, 45, 52, 48, 49, 54, 45, 52, 52, 51, 54, 45, 98, 55, 100, 97, 45, 51, 54, 99, 48, 98, 97, 101, 53, 52, 102, 102, 54 },

                    new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + "double".Length), 100, 111, 117, 98, 108, 101 },
                    new byte[] { JsonBinaryEncoding.TypeMarker.NumberDouble, 0x98, 0x8B, 0x30, 0xE3, 0xCB, 0x45, 0xC8, 0x3F },

                    new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + "int".Length), 105, 110, 116 },
                    new byte[] { JsonBinaryEncoding.TypeMarker.NumberInt32, 0x19, 0xDF, 0xB6, 0xB0 },

                    new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + "string".Length), 115, 116, 114, 105, 110, 103 },
                    new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + "XCPCFXPHHF".Length), 88, 67, 80, 67, 70, 88, 80, 72, 72, 70 },

                    new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + "boolean".Length), 98, 111, 111, 108, 101, 97, 110 },
                    new byte[] { JsonBinaryEncoding.TypeMarker.True },

                    new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + "null".Length), 110, 117, 108, 108 },
                    new byte[] { JsonBinaryEncoding.TypeMarker.Null },

                    new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + "datetime".Length), 100, 97, 116, 101, 116, 105, 109, 101 },
                    new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + "2526-07-11T18:18:16.4520716".Length), 50, 53, 50, 54, 45, 48, 55, 45, 49, 49, 84, 49, 56, 58, 49, 56, 58, 49, 54, 46, 52, 53, 50, 48, 55, 49, 54 },

                    new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + "spatialPoint".Length), 115, 112, 97, 116, 105, 97, 108, 80, 111, 105, 110, 116 }
                };

                List<byte[]> innerObjectElements = new List<byte[]>
                {
                    new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.SystemString1ByteLengthMin + 27) },
                    new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.SystemString1ByteLengthMin + 24) },

                    new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.SystemString1ByteLengthMin + 09) }
                };
                List<byte[]> innerArrayElements = new List<byte[]>
                {
                    new byte[] { JsonBinaryEncoding.TypeMarker.NumberDouble, 0x7A, 0x36, 0xAB, 0x3E, 0x57, 0xBF, 0x5D, 0x40 },
                    new byte[] { JsonBinaryEncoding.TypeMarker.NumberDouble, 0x74, 0xB5, 0x15, 0xFB, 0xCB, 0x56, 0x47, 0xC0 }
                };
                byte[] innerArrayElementsBytes = innerArrayElements.SelectMany(x => x).ToArray();

                innerObjectElements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.ArrL1, (byte)innerArrayElementsBytes.Length });
                innerObjectElements.Add(innerArrayElementsBytes);

                byte[] innerObjectElementsBytes = innerObjectElements.SelectMany(x => x).ToArray();
                elements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.ObjL1, (byte)innerObjectElementsBytes.Length });
                elements.Add(innerObjectElementsBytes);

                elements.Add(new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + "text".Length), 116, 101, 120, 116 });
                elements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.String1ByteLength, (byte)"tiger diamond newbrunswick snowleopard chocolate dog snowleopard turtle cat sapphire peach sapphire vancouver white chocolate horse diamond lion superlongcolourname ruby".Length, 116, 105, 103, 101, 114, 32, 100, 105, 97, 109, 111, 110, 100, 32, 110, 101, 119, 98, 114, 117, 110, 115, 119, 105, 99, 107, 32, 115, 110, 111, 119, 108, 101, 111, 112, 97, 114, 100, 32, 99, 104, 111, 99, 111, 108, 97, 116, 101, 32, 100, 111, 103, 32, 115, 110, 111, 119, 108, 101, 111, 112, 97, 114, 100, 32, 116, 117, 114, 116, 108, 101, 32, 99, 97, 116, 32, 115, 97, 112, 112, 104, 105, 114, 101, 32, 112, 101, 97, 99, 104, 32, 115, 97, 112, 112, 104, 105, 114, 101, 32, 118, 97, 110, 99, 111, 117, 118, 101, 114, 32, 119, 104, 105, 116, 101, 32, 99, 104, 111, 99, 111, 108, 97, 116, 101, 32, 104, 111, 114, 115, 101, 32, 100, 105, 97, 109, 111, 110, 100, 32, 108, 105, 111, 110, 32, 115, 117, 112, 101, 114, 108, 111, 110, 103, 99, 111, 108, 111, 117, 114, 110, 97, 109, 101, 32, 114, 117, 98, 121 });

                byte[] elementsBytes = elements.SelectMany(x => x).ToArray();

                binaryInputBuilder.Add(BitConverter.GetBytes((short)elementsBytes.Length));
                binaryInputBuilder.Add(elementsBytes);
                binaryInput = binaryInputBuilder.SelectMany(x => x).ToArray();
            }

            byte[] binaryInputWithEncoding;
            {
                List<byte[]> binaryInputBuilder = new List<byte[]>
                {
                    new byte[] { BinaryFormat, JsonBinaryEncoding.TypeMarker.ObjL2 }
                };

                List<byte[]> elements = new List<byte[]>
                {
                    new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.SystemString1ByteLengthMin + 12) },
                    new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + "7029d079-4016-4436-b7da-36c0bae54ff6".Length), 55, 48, 50, 57, 100, 48, 55, 57, 45, 52, 48, 49, 54, 45, 52, 52, 51, 54, 45, 98, 55, 100, 97, 45, 51, 54, 99, 48, 98, 97, 101, 53, 52, 102, 102, 54 },

                    new byte[] { (byte)JsonBinaryEncoding.TypeMarker.UserString1ByteLengthMin },
                    new byte[] { JsonBinaryEncoding.TypeMarker.NumberDouble, 0x98, 0x8B, 0x30, 0xE3, 0xCB, 0x45, 0xC8, 0x3F },

                    new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.UserString1ByteLengthMin + 1) },
                    new byte[] { JsonBinaryEncoding.TypeMarker.NumberInt32, 0x19, 0xDF, 0xB6, 0xB0 },

                    new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.UserString1ByteLengthMin + 2) },
                    new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + "XCPCFXPHHF".Length), 88, 67, 80, 67, 70, 88, 80, 72, 72, 70 },

                    new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.UserString1ByteLengthMin + 3) },
                    new byte[] { JsonBinaryEncoding.TypeMarker.True },

                    new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.UserString1ByteLengthMin + 4) },
                    new byte[] { JsonBinaryEncoding.TypeMarker.Null },

                    new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.UserString1ByteLengthMin + 5) },
                    new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + "2526-07-11T18:18:16.4520716".Length), 50, 53, 50, 54, 45, 48, 55, 45, 49, 49, 84, 49, 56, 58, 49, 56, 58, 49, 54, 46, 52, 53, 50, 48, 55, 49, 54 },

                    new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.UserString1ByteLengthMin + 6) }
                };

                List<byte[]> innerObjectElements = new List<byte[]>
                {
                    new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.SystemString1ByteLengthMin + 27) },
                    new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.SystemString1ByteLengthMin + 24) },

                    new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.SystemString1ByteLengthMin + 09) }
                };
                List<byte[]> innerArrayElements = new List<byte[]>
                {
                    new byte[] { JsonBinaryEncoding.TypeMarker.NumberDouble, 0x7A, 0x36, 0xAB, 0x3E, 0x57, 0xBF, 0x5D, 0x40 },
                    new byte[] { JsonBinaryEncoding.TypeMarker.NumberDouble, 0x74, 0xB5, 0x15, 0xFB, 0xCB, 0x56, 0x47, 0xC0 }
                };
                byte[] innerArrayElementsBytes = innerArrayElements.SelectMany(x => x).ToArray();

                innerObjectElements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.ArrL1, (byte)innerArrayElementsBytes.Length });
                innerObjectElements.Add(innerArrayElementsBytes);

                byte[] innerObjectElementsBytes = innerObjectElements.SelectMany(x => x).ToArray();
                elements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.ObjL1, (byte)innerObjectElementsBytes.Length });
                elements.Add(innerObjectElementsBytes);

                elements.Add(new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.UserString1ByteLengthMin + 7) });
                elements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.String1ByteLength, (byte)"tiger diamond newbrunswick snowleopard chocolate dog snowleopard turtle cat sapphire peach sapphire vancouver white chocolate horse diamond lion superlongcolourname ruby".Length, 116, 105, 103, 101, 114, 32, 100, 105, 97, 109, 111, 110, 100, 32, 110, 101, 119, 98, 114, 117, 110, 115, 119, 105, 99, 107, 32, 115, 110, 111, 119, 108, 101, 111, 112, 97, 114, 100, 32, 99, 104, 111, 99, 111, 108, 97, 116, 101, 32, 100, 111, 103, 32, 115, 110, 111, 119, 108, 101, 111, 112, 97, 114, 100, 32, 116, 117, 114, 116, 108, 101, 32, 99, 97, 116, 32, 115, 97, 112, 112, 104, 105, 114, 101, 32, 112, 101, 97, 99, 104, 32, 115, 97, 112, 112, 104, 105, 114, 101, 32, 118, 97, 110, 99, 111, 117, 118, 101, 114, 32, 119, 104, 105, 116, 101, 32, 99, 104, 111, 99, 111, 108, 97, 116, 101, 32, 104, 111, 114, 115, 101, 32, 100, 105, 97, 109, 111, 110, 100, 32, 108, 105, 111, 110, 32, 115, 117, 112, 101, 114, 108, 111, 110, 103, 99, 111, 108, 111, 117, 114, 110, 97, 109, 101, 32, 114, 117, 98, 121 });

                byte[] elementsBytes = elements.SelectMany(x => x).ToArray();

                binaryInputBuilder.Add(BitConverter.GetBytes((short)elementsBytes.Length));
                binaryInputBuilder.Add(elementsBytes);
                binaryInputWithEncoding = binaryInputBuilder.SelectMany(x => x).ToArray();
            }

            JsonToken[] expectedTokens =
            {
                JsonToken.ObjectStart(),

                    JsonToken.FieldName("id"),
                    JsonToken.String("7029d079-4016-4436-b7da-36c0bae54ff6"),

                    JsonToken.FieldName("double"),
                    JsonToken.Number(0.18963001816981939),

                    JsonToken.FieldName("int"),
                    JsonToken.Number(-1330192615),

                    JsonToken.FieldName("string"),
                    JsonToken.String("XCPCFXPHHF"),

                    JsonToken.FieldName("boolean"),
                    JsonToken.Boolean(true),

                    JsonToken.FieldName("null"),
                    JsonToken.Null(),

                    JsonToken.FieldName("datetime"),
                    JsonToken.String("2526-07-11T18:18:16.4520716"),

                    JsonToken.FieldName("spatialPoint"),
                    JsonToken.ObjectStart(),
                        JsonToken.FieldName("type"),
                        JsonToken.String("Point"),

                        JsonToken.FieldName("coordinates"),
                        JsonToken.ArrayStart(),
                            JsonToken.Number(118.9897),
                            JsonToken.Number(-46.6781),
                        JsonToken.ArrayEnd(),
                    JsonToken.ObjectEnd(),

                    JsonToken.FieldName("text"),
                    JsonToken.String("tiger diamond newbrunswick snowleopard chocolate dog snowleopard turtle cat sapphire peach sapphire vancouver white chocolate horse diamond lion superlongcolourname ruby"),
                JsonToken.ObjectEnd(),
            };

            this.VerifyReader(input, expectedTokens);
            this.VerifyReader(binaryInput, expectedTokens);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void TrailingGarbageTest()
        {
            string input = "{\"name\":\"477cecf7-5547-4f87-81c2-72ee2c7d6179\",\"permissionMode\":\"Read\",\"resource\":\"-iQET8M3A0c=\"}..garbage..";

            List<byte[]> binaryInputBuilder = new List<byte[]>
            {
                new byte[] { BinaryFormat, JsonBinaryEncoding.TypeMarker.ObjL4 }
            };

            List<byte[]> elements = new List<byte[]>
            {
                new byte[] { JsonBinaryEncoding.TypeMarker.String1ByteLength, (byte)"name".Length, 110, 97, 109, 101 },
                new byte[] { JsonBinaryEncoding.TypeMarker.String1ByteLength, (byte)"477cecf7-5547-4f87-81c2-72ee2c7d6179".Length, 52, 55, 55, 99, 101, 99, 102, 55, 45, 53, 53, 52, 55, 45, 52, 102, 56, 55, 45, 56, 49, 99, 50, 45, 55, 50, 101, 101, 50, 99, 55, 100, 54, 49, 55, 57 },

                new byte[] { JsonBinaryEncoding.TypeMarker.String1ByteLength, (byte)"permissionMode".Length, 112, 101, 114, 109, 105, 115, 115, 105, 111, 110, 77, 111, 100, 101 },
                new byte[] { JsonBinaryEncoding.TypeMarker.String1ByteLength, (byte)"Read".Length, 82, 101, 97, 100 },

                new byte[] { JsonBinaryEncoding.TypeMarker.String1ByteLength, (byte)"resource".Length, 114, 101, 115, 111, 117, 114, 99, 101 },
                new byte[] { JsonBinaryEncoding.TypeMarker.String1ByteLength, (byte)"-iQET8M3A0c=".Length, 45, 105, 81, 69, 84, 56, 77, 51, 65, 48, 99, 61 }
            };
            byte[] elementsBytes = elements.SelectMany(x => x).ToArray();

            binaryInputBuilder.Add(BitConverter.GetBytes(elementsBytes.Length));
            binaryInputBuilder.Add(elementsBytes);
            binaryInputBuilder.Add(new byte[] { JsonBinaryEncoding.TypeMarker.String1ByteLength, (byte)"..garbage..".Length, 46, 46, 103, 97, 114, 98, 97, 103, 101, 46, 46 });
            byte[] binaryInput = binaryInputBuilder.SelectMany(x => x).ToArray();

            JsonToken[] expectedTokens =
            {
                JsonToken.ObjectStart(),
                JsonToken.FieldName("name"),
                JsonToken.String("477cecf7-5547-4f87-81c2-72ee2c7d6179"),
                JsonToken.FieldName("permissionMode"),
                JsonToken.String("Read"),
                JsonToken.FieldName("resource"),
                JsonToken.String("-iQET8M3A0c="),
                JsonToken.ObjectEnd(),
            };

            this.VerifyReader(input, expectedTokens, new JsonUnexpectedTokenException());
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void InvalidIntTest()
        {
            string invalidIntString = "{\"type\": 1??? }";

            JsonToken[] invalidIntStringTokens =
            {
                JsonToken.ObjectStart(),
                JsonToken.FieldName("type"),
            };

            this.VerifyReader(invalidIntString, invalidIntStringTokens, new JsonInvalidNumberException());
            // There is no way to test this in a binary reader, since "???" would just convert to a valid binary integer.
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void InvalidExponentTest()
        {
            string invalidExponent = "{\"type\": 1.0e-??? }";
            string invalidExponent2 = "{\"type\": 1.0E-??? }";

            JsonToken[] invalidExponentTokens =
            {
                JsonToken.ObjectStart(),
                JsonToken.FieldName("type"),
            };

            this.VerifyReader(invalidExponent, invalidExponentTokens, new JsonInvalidNumberException());
            this.VerifyReader(invalidExponent2, invalidExponentTokens, new JsonInvalidNumberException());
            // There is no way to test this in a binary reader, since "1.0e-???" would just convert to a valid binary number.
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void InvalidExponentTest2()
        {
            string invalidExponent = "{\"type\": 1e+1e1 }";
            string invalidExponent2 = "{\"type\": 1E+1E1 }";

            JsonToken[] invalidExponentTokens =
            {
                JsonToken.ObjectStart(),
                JsonToken.FieldName("type"),
            };

            this.VerifyReader(invalidExponent, invalidExponentTokens, new JsonInvalidNumberException());
            this.VerifyReader(invalidExponent2, invalidExponentTokens, new JsonInvalidNumberException());
            // There is no way to test this in a binary reader, since "1e+1e1" would just convert to a valid binary number.
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void InvalidNumberTest()
        {
            string input = "{\"type\": 1.e5 }";
            string input2 = "{\"type\": 1.e5 }";

            JsonToken[] exponentTokens =
            {
                JsonToken.ObjectStart(),
                JsonToken.FieldName("type"),
            };

            this.VerifyReader(input, exponentTokens, new JsonInvalidNumberException());
            this.VerifyReader(input2, exponentTokens, new JsonInvalidNumberException());
            // There is no way to test this in a binary reader, since "1.e5" is not possible in binary.
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void InvalidNumberWithoutExponentTest()
        {
            string input = "{\"type\": 1Garbage }";

            JsonToken[] exponentTokens =
            {
                JsonToken.ObjectStart(),
                JsonToken.FieldName("type"),
            };

            this.VerifyReader(input, exponentTokens, new JsonInvalidNumberException());
            // There is no way to test this in a binary reader, since "1Garbage" is not possible in binary.
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void MissingClosingQuoteTest()
        {
            string missingQuote = "{\"type\": \"unfinished }";

            JsonToken[] missingQuoteTokens =
            {
                JsonToken.ObjectStart(),
                JsonToken.FieldName("type"),
            };

            this.VerifyReader(missingQuote, missingQuoteTokens, new JsonMissingClosingQuoteException());
            // Binary does not test this.
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void MissingPropertyTest()
        {
            string input = "[{{";

            JsonToken[] expectedTokens =
            {
                JsonToken.ArrayStart(),
                JsonToken.ObjectStart(),
            };

            this.VerifyReader(input, expectedTokens, new JsonMissingPropertyException());
            // Binary does not test this.
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void MissingPropertyTest2()
        {
            string input = "{true: false}";

            JsonToken[] expectedTokens =
            {
                JsonToken.ObjectStart(),
            };

            this.VerifyReader(input, expectedTokens, new JsonMissingPropertyException());
            // Binary does not test this.
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void MissingNameSeperatorTest()
        {
            string input = "{\"prop\"\"value\"}";

            JsonToken[] expectedTokens =
            {
                JsonToken.ObjectStart(),
                JsonToken.FieldName("prop"),
            };

            this.VerifyReader(input, expectedTokens, new JsonMissingNameSeparatorException());
            // Binary does not test this.
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void MissingValueSeperatorTest()
        {
            string input = "[true false]";

            JsonToken[] expectedTokens =
            {
                JsonToken.ArrayStart(),
                JsonToken.Boolean(true),
            };

            this.VerifyReader(input, expectedTokens, new JsonUnexpectedTokenException());
            // Binary does not test this.
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void UnexpectedNameSeperatorTest()
        {
            string input = "[true: false]";

            JsonToken[] expectedTokens =
            {
                JsonToken.ArrayStart(),
                JsonToken.Boolean(true),
            };

            this.VerifyReader(input, expectedTokens, new JsonUnexpectedNameSeparatorException());
            // Binary does not test this.
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void UnexpectedEndObjectTest()
        {
            string input = "[true,}";

            JsonToken[] expectedTokens =
            {
                JsonToken.ArrayStart(),
                JsonToken.Boolean(true),
            };

            this.VerifyReader(input, expectedTokens, new JsonUnexpectedEndObjectException());
            // Binary does not test this.
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void TrailingCommaUnexpectedEndObjectTest()
        {
            string input = "{\"prop\": false, }";

            JsonToken[] expectedTokens =
            {
                JsonToken.ObjectStart(),
                JsonToken.FieldName("prop"),
                JsonToken.Boolean(false),
            };

            this.VerifyReader(input, expectedTokens, new JsonUnexpectedEndObjectException());
            // Binary does not test this.
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void UnexpectedEndArrayTest()
        {
            string input = "{\"prop\": false, ]";

            JsonToken[] expectedTokens =
            {
                JsonToken.ObjectStart(),
                JsonToken.FieldName("prop"),
                JsonToken.Boolean(false),
            };

            this.VerifyReader(input, expectedTokens, new JsonUnexpectedEndArrayException());
            // Binary does not test this.
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void TrailingCommaUnexpectedEndArrayTest()
        {
            string input = "[true, ]";

            JsonToken[] expectedTokens =
            {
                JsonToken.ArrayStart(),
                JsonToken.Boolean(true),
            };

            this.VerifyReader(input, expectedTokens, new JsonUnexpectedEndArrayException());
            // Binary does not test this.
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void MissingEndObjectTest()
        {
            string input = "{";

            JsonToken[] expectedTokens =
            {
                JsonToken.ObjectStart()
            };

            this.VerifyReader(input, expectedTokens, new JsonMissingEndObjectException());
            // Binary does not test this.
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void MissingEndArrayTest()
        {
            string input = "[";

            JsonToken[] expectedTokens =
            {
                JsonToken.ArrayStart()
            };

            this.VerifyReader(input, expectedTokens, new JsonMissingEndArrayException());
            // Binary does not test this.
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void InvalidEscapeCharacterTest()
        {
            JsonToken[] expectedTokens =
            {
            };

            this.VerifyReader("\"\\p\"", expectedTokens, new JsonInvalidEscapedCharacterException());
            this.VerifyReader("\"\\\\,\\.\"", expectedTokens, new JsonInvalidEscapedCharacterException());
            this.VerifyReader("\"\\\xC2\xA2\"", expectedTokens, new JsonInvalidEscapedCharacterException());
            // Binary does not test this.
        }
        #endregion
        #region ExtendedTypes
        [TestMethod]
        [Owner("mayapainter")]
        public void Int8Test()
        {
            sbyte[] values = new sbyte[] { sbyte.MinValue, sbyte.MinValue + 1, -1, 0, 1, sbyte.MaxValue, sbyte.MaxValue - 1 };
            foreach (sbyte value in values)
            {
                string input = $"I{value}";
                byte[] binaryInput;
                unchecked
                {
                    binaryInput = new byte[]
                    {
                        BinaryFormat,
                        JsonBinaryEncoding.TypeMarker.Int8,
                        (byte)value
                    };
                }

                JsonToken[] expectedTokens =
                {
                    JsonToken.Int8(value)
                };

                this.VerifyReader(input, expectedTokens);
                this.VerifyReader(binaryInput, expectedTokens);
            }
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void Int16Test()
        {
            short[] values = new short[] { short.MinValue, short.MinValue + 1, -1, 0, 1, short.MaxValue, short.MaxValue - 1 };
            foreach (short value in values)
            {
                string input = $"H{value}";
                byte[] binaryInput;
                unchecked
                {
                    binaryInput = new byte[]
                    {
                        BinaryFormat,
                        JsonBinaryEncoding.TypeMarker.Int16,
                    };
                    binaryInput = binaryInput.Concat(BitConverter.GetBytes(value)).ToArray();
                }

                JsonToken[] expectedTokens =
                {
                    JsonToken.Int16(value)
                };

                this.VerifyReader(input, expectedTokens);
                this.VerifyReader(binaryInput, expectedTokens);
            }
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void Int32Test()
        {
            int[] values = new int[] { int.MinValue, int.MinValue + 1, -1, 0, 1, int.MaxValue, int.MaxValue - 1 };
            foreach (int value in values)
            {
                string input = $"L{value}";
                byte[] binaryInput;
                unchecked
                {
                    binaryInput = new byte[]
                    {
                        BinaryFormat,
                        JsonBinaryEncoding.TypeMarker.Int32,
                    };
                    binaryInput = binaryInput.Concat(BitConverter.GetBytes(value)).ToArray();
                }

                JsonToken[] expectedTokens =
                {
                    JsonToken.Int32(value)
                };

                this.VerifyReader(input, expectedTokens);
                this.VerifyReader(binaryInput, expectedTokens);
            }
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void Int64Test()
        {
            long[] values = new long[] { long.MinValue, long.MinValue + 1, -1, 0, 1, long.MaxValue, long.MaxValue - 1 };
            foreach (long value in values)
            {
                string input = $"LL{value}";
                byte[] binaryInput;
                unchecked
                {
                    binaryInput = new byte[]
                    {
                        BinaryFormat,
                        JsonBinaryEncoding.TypeMarker.Int64,
                    };
                    binaryInput = binaryInput.Concat(BitConverter.GetBytes(value)).ToArray();
                }

                JsonToken[] expectedTokens =
                {
                    JsonToken.Int64(value)
                };

                this.VerifyReader(input, expectedTokens);
                this.VerifyReader(binaryInput, expectedTokens);
            }
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void UInt32Test()
        {
            uint[] values = new uint[] { uint.MinValue, uint.MinValue + 1, 0, 1, uint.MaxValue, uint.MaxValue - 1 };
            foreach (uint value in values)
            {
                string input = $"UL{value}";
                byte[] binaryInput;
                unchecked
                {
                    binaryInput = new byte[]
                    {
                        BinaryFormat,
                        JsonBinaryEncoding.TypeMarker.UInt32,
                    };
                    binaryInput = binaryInput.Concat(BitConverter.GetBytes(value)).ToArray();
                }

                JsonToken[] expectedTokens =
                {
                    JsonToken.UInt32(value)
                };

                this.VerifyReader(input, expectedTokens);
                this.VerifyReader(binaryInput, expectedTokens);
            }
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void Float32Test()
        {
            float[] values = new float[] { float.MinValue, float.MinValue + 1, 0, 1, float.MaxValue, float.MaxValue - 1 };
            foreach (float value in values)
            {
                string input = $"S{value.ToString("G9", CultureInfo.InvariantCulture)}";
                byte[] binaryInput;
                unchecked
                {
                    binaryInput = new byte[]
                    {
                        BinaryFormat,
                        JsonBinaryEncoding.TypeMarker.Float32,
                    };
                    binaryInput = binaryInput.Concat(BitConverter.GetBytes(value)).ToArray();
                }

                JsonToken[] expectedTokens =
                {
                    JsonToken.Float32(value)
                };

                this.VerifyReader(input, expectedTokens);
                this.VerifyReader(binaryInput, expectedTokens);
            }
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void Float64Test()
        {
            double[] values = new double[] { double.MinValue, double.MinValue + 1, 0, 1, double.MaxValue, double.MaxValue - 1 };
            foreach (double value in values)
            {
                string input = $"D{value.ToString("G17", CultureInfo.InvariantCulture)}";
                byte[] binaryInput;
                unchecked
                {
                    binaryInput = new byte[]
                    {
                        BinaryFormat,
                        JsonBinaryEncoding.TypeMarker.Float64,
                    };
                    binaryInput = binaryInput.Concat(BitConverter.GetBytes(value)).ToArray();
                }

                JsonToken[] expectedTokens =
                {
                    JsonToken.Float64(value)
                };

                this.VerifyReader(input, expectedTokens);
                this.VerifyReader(binaryInput, expectedTokens);
            }
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void GuidTest()
        {
            Guid[] values = new Guid[] { Guid.Empty, Guid.NewGuid() };
            foreach (Guid value in values)
            {
                string input = $"G{value}";
                byte[] binaryInput;
                unchecked
                {
                    binaryInput = new byte[]
                    {
                        BinaryFormat,
                        JsonBinaryEncoding.TypeMarker.Guid,
                    };
                    binaryInput = binaryInput.Concat(value.ToByteArray()).ToArray();
                }

                JsonToken[] expectedTokens =
                {
                    JsonToken.Guid(value)
                };

                this.VerifyReader(input, expectedTokens);
                this.VerifyReader(binaryInput, expectedTokens);
            }
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void BinaryTest()
        {
            {
                // Empty Binary
                string input = $"B";
                byte[] binaryInput;
                unchecked
                {
                    binaryInput = new byte[]
                    {
                        BinaryFormat,
                        JsonBinaryEncoding.TypeMarker.Binary1ByteLength,
                        0,
                    };
                }

                JsonToken[] expectedTokens =
                {
                    JsonToken.Binary(new byte[]{ })
                };

                this.VerifyReader(input, expectedTokens);
                this.VerifyReader(binaryInput, expectedTokens);
            }

            {
                // Binary 1 Byte Length
                byte[] binary = Enumerable.Range(0, 25).Select(x => (byte)x).ToArray();
                string input = $"B{Convert.ToBase64String(binary.ToArray())}";
                byte[] binaryInput;
                unchecked
                {
                    binaryInput = new byte[]
                    {
                        BinaryFormat,
                        JsonBinaryEncoding.TypeMarker.Binary1ByteLength,
                        (byte)binary.Length,
                    };
                    binaryInput = binaryInput.Concat(binary).ToArray();
                }

                JsonToken[] expectedTokens =
                {
                    JsonToken.Binary(binary)
                };

                this.VerifyReader(input, expectedTokens);
                this.VerifyReader(binaryInput, expectedTokens);
            }
        }
        #endregion
        private void VerifyReader(string input, JsonToken[] expectedTokens)
        {
            this.VerifyReader(input, expectedTokens, null);
        }

        /// <summary>
        /// Tries to read with the text reader using all the supported encodings.
        /// </summary>
        private void VerifyReader(string input, JsonToken[] expectedTokens, Exception expectedException)
        {
            byte[] utf8ByteArray = Encoding.UTF8.GetBytes(input);
            // Test readers created with the array API
            this.VerifyReader(
                () => JsonReader.Create(utf8ByteArray),
                expectedTokens,
                expectedException);
        }

        private void VerifyReader(ReadOnlyMemory<byte> input, JsonToken[] expectedTokens, Exception expectedException = null)
        {
            // Test binary reader created with the array API
            this.VerifyReader(() => JsonReader.Create(input), expectedTokens, expectedException);
        }

        /// <summary>
        /// Verifies the reader by constructing a JsonReader from the memorystream with the specified encoding and then reads tokens from to see if they match the expected tokens. If there is an exception provided it also tries to read until it hits that exception.
        /// </summary>
        private void VerifyReader(Func<IJsonReader> createJsonReader, JsonToken[] expectedTokens, Exception expectedException)
        {
            CultureInfo defaultCultureInfo = System.Threading.Thread.CurrentThread.CurrentCulture;

            CultureInfo[] cultureInfoList = new CultureInfo[]
            {
                defaultCultureInfo,
                System.Globalization.CultureInfo.GetCultureInfo("fr-FR")
            };

            try
            {
                foreach (CultureInfo cultureInfo in cultureInfoList)
                {
                    System.Threading.Thread.CurrentThread.CurrentCulture = cultureInfo;

                    IJsonReader jsonReader = createJsonReader();
                    JsonTokenType jsonTokenType = jsonReader.CurrentTokenType;
                    Assert.AreEqual(JsonTokenType.NotStarted, jsonTokenType);

                    foreach (JsonToken expectedToken in expectedTokens)
                    {
                        jsonReader.Read();

                        switch (expectedToken.JsonTokenType)
                        {
                            case JsonTokenType.BeginArray:
                                this.VerifyBeginArray(jsonReader);
                                break;

                            case JsonTokenType.EndArray:
                                this.VerifyEndArray(jsonReader);
                                break;

                            case JsonTokenType.BeginObject:
                                this.VerifyBeginObject(jsonReader);
                                break;

                            case JsonTokenType.EndObject:
                                this.VerifyEndObject(jsonReader);
                                break;

                            case JsonTokenType.String:
                                this.VerifyString(jsonReader, ((JsonStringToken)expectedToken).Value);
                                break;

                            case JsonTokenType.Number:
                                this.VerifyNumber(jsonReader, ((JsonNumberToken)expectedToken).Value);
                                break;

                            case JsonTokenType.True:
                                this.VerifyTrue(jsonReader);
                                break;

                            case JsonTokenType.False:
                                this.VerifyFalse(jsonReader);
                                break;

                            case JsonTokenType.Null:
                                this.VerifyNull(jsonReader);
                                break;

                            case JsonTokenType.FieldName:
                                this.VerifyFieldName(jsonReader, ((JsonFieldNameToken)expectedToken).Value);
                                break;

                            case JsonTokenType.Int8:
                                this.VerifyInt8(jsonReader, ((JsonInt8Token)expectedToken).Value);
                                break;

                            case JsonTokenType.Int16:
                                this.VerifyInt16(jsonReader, ((JsonInt16Token)expectedToken).Value);
                                break;

                            case JsonTokenType.Int32:
                                this.VerifyInt32(jsonReader, ((JsonInt32Token)expectedToken).Value);
                                break;

                            case JsonTokenType.Int64:
                                this.VerifyInt64(jsonReader, ((JsonInt64Token)expectedToken).Value);
                                break;

                            case JsonTokenType.UInt32:
                                this.VerifyUInt32(jsonReader, ((JsonUInt32Token)expectedToken).Value);
                                break;

                            case JsonTokenType.Float32:
                                this.VerifyFloat32(jsonReader, ((JsonFloat32Token)expectedToken).Value);
                                break;

                            case JsonTokenType.Float64:
                                this.VerifyFloat64(jsonReader, ((JsonFloat64Token)expectedToken).Value);
                                break;

                            case JsonTokenType.Guid:
                                this.VerifyGuid(jsonReader, ((JsonGuidToken)expectedToken).Value);
                                break;

                            case JsonTokenType.Binary:
                                this.VerifyBinary(jsonReader, ((JsonBinaryToken)expectedToken).Value);
                                break;

                            case JsonTokenType.NotStarted:
                            default:
                                Assert.Fail($"Got an unexpected JsonTokenType: {expectedToken.JsonTokenType} as an expected token type");
                                break;
                        }
                    }

                    if (expectedException != null)
                    {
                        try
                        {
                            jsonReader.Read();
                            Assert.Fail(string.Format("Expected to receive {0} but didn't", expectedException.Message));
                        }
                        catch (Exception exception)
                        {
                            Assert.AreEqual(expectedException.GetType(), exception.GetType());
                        }
                    }
                }
            }
            finally
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = defaultCultureInfo;
            }
        }

        private void VerifyBeginArray(IJsonReader jsonReader)
        {
            this.VerifyToken(jsonReader, JsonTokenType.BeginArray, "[");
        }

        private void VerifyEndArray(IJsonReader jsonReader)
        {
            this.VerifyToken(jsonReader, JsonTokenType.EndArray, "]");
        }

        private void VerifyBeginObject(IJsonReader jsonReader)
        {
            this.VerifyToken(jsonReader, JsonTokenType.BeginObject, "{");
        }

        private void VerifyEndObject(IJsonReader jsonReader)
        {
            this.VerifyToken(jsonReader, JsonTokenType.EndObject, "}");
        }

        private void VerifyString(IJsonReader jsonReader, string expectedString)
        {
            JsonTokenType jsonTokenType = jsonReader.CurrentTokenType;
            Assert.AreEqual(JsonTokenType.String, jsonTokenType);

            this.VerifyStringOrFieldNameHelper(jsonReader, expectedString);
        }

        private void VerifyFieldName(IJsonReader jsonReader, string expectedString)
        {
            JsonTokenType jsonTokenType = jsonReader.CurrentTokenType;
            Assert.AreEqual(JsonTokenType.FieldName, jsonTokenType);

            this.VerifyStringOrFieldNameHelper(jsonReader, expectedString);
        }

        private void VerifyStringOrFieldNameHelper(IJsonReader jsonReader, string expectedString)
        {
            string actualString = jsonReader.GetStringValue();
            Assert.AreEqual(expectedString, actualString);
            if (jsonReader is ITypedJsonReader typedJsonReader)
            {
                Utf8Span actualUtf8SpanValue = typedJsonReader.GetUtf8SpanValue();
                Assert.AreEqual(expectedString, actualUtf8SpanValue.ToString());
            }
        }

        private void VerifyNumber(IJsonReader jsonReader, Number64 expectedNumberValue)
        {
            JsonTokenType jsonTokenType = jsonReader.CurrentTokenType;
            Assert.AreEqual(JsonTokenType.Number, jsonTokenType);

            Number64 actualNumberValue = jsonReader.GetNumberValue();
            Assert.IsTrue(expectedNumberValue == actualNumberValue);

            // Additionally check if the text is correct
            if (jsonReader.SerializationFormat == JsonSerializationFormat.Text)
            {
                IJsonTextReaderPrivateImplementation textReader = (IJsonTextReaderPrivateImplementation)jsonReader;
                string stringRawJsonToken = Encoding.UTF8.GetString(textReader.GetBufferedJsonToken().Memory.ToArray());
                double valueFromString = double.Parse(stringRawJsonToken, CultureInfo.InvariantCulture);
                Assert.AreEqual(expectedNumberValue, valueFromString);
            }
        }

        private void VerifyInt8(IJsonReader jsonReader, sbyte expected)
        {
            JsonTokenType jsonTokenType = jsonReader.CurrentTokenType;
            Assert.AreEqual(JsonTokenType.Int8, jsonTokenType);

            sbyte actual = jsonReader.GetInt8Value();
            Assert.AreEqual(expected, actual);

            // Additionally check if the text is correct
            if (jsonReader.SerializationFormat == JsonSerializationFormat.Text)
            {
                IJsonTextReaderPrivateImplementation textReader = (IJsonTextReaderPrivateImplementation)jsonReader;
                string stringRawJsonToken = Encoding.UTF8.GetString(textReader.GetBufferedJsonToken().Memory.ToArray());
                Assert.AreEqual($"I{expected}", stringRawJsonToken);
            }
        }

        private void VerifyInt16(IJsonReader jsonReader, short expected)
        {
            JsonTokenType jsonTokenType = jsonReader.CurrentTokenType;
            Assert.AreEqual(JsonTokenType.Int16, jsonTokenType);

            short actual = jsonReader.GetInt16Value();
            Assert.AreEqual(expected, actual);

            // Additionally check if the text is correct
            if (jsonReader.SerializationFormat == JsonSerializationFormat.Text)
            {
                IJsonTextReaderPrivateImplementation textReader = (IJsonTextReaderPrivateImplementation)jsonReader;
                string stringRawJsonToken = Encoding.UTF8.GetString(textReader.GetBufferedJsonToken().Memory.ToArray());
                Assert.AreEqual($"H{expected}", stringRawJsonToken);
            }
        }

        private void VerifyInt32(IJsonReader jsonReader, int expected)
        {
            JsonTokenType jsonTokenType = jsonReader.CurrentTokenType;
            Assert.AreEqual(JsonTokenType.Int32, jsonTokenType);

            int actual = jsonReader.GetInt32Value();
            Assert.AreEqual(expected, actual);

            // Additionally check if the text is correct
            if (jsonReader.SerializationFormat == JsonSerializationFormat.Text)
            {
                IJsonTextReaderPrivateImplementation textReader = (IJsonTextReaderPrivateImplementation)jsonReader;
                string stringRawJsonToken = Encoding.UTF8.GetString(textReader.GetBufferedJsonToken().Memory.ToArray());
                Assert.AreEqual($"L{expected}", stringRawJsonToken);
            }
        }

        private void VerifyInt64(IJsonReader jsonReader, long expected)
        {
            JsonTokenType jsonTokenType = jsonReader.CurrentTokenType;
            Assert.AreEqual(JsonTokenType.Int64, jsonTokenType);

            long actual = jsonReader.GetInt64Value();
            Assert.AreEqual(expected, actual);

            // Additionally check if the text is correct
            if (jsonReader.SerializationFormat == JsonSerializationFormat.Text)
            {
                IJsonTextReaderPrivateImplementation textReader = (IJsonTextReaderPrivateImplementation)jsonReader;
                string stringRawJsonToken = Encoding.UTF8.GetString(textReader.GetBufferedJsonToken().Memory.ToArray());
                Assert.AreEqual($"LL{expected}", stringRawJsonToken);
            }
        }

        private void VerifyUInt32(IJsonReader jsonReader, uint expected)
        {
            JsonTokenType jsonTokenType = jsonReader.CurrentTokenType;
            Assert.AreEqual(JsonTokenType.UInt32, jsonTokenType);

            uint actual = jsonReader.GetUInt32Value();
            Assert.AreEqual(expected, actual);

            // Additionally check if the text is correct
            if (jsonReader.SerializationFormat == JsonSerializationFormat.Text)
            {
                IJsonTextReaderPrivateImplementation textReader = (IJsonTextReaderPrivateImplementation)jsonReader;
                string stringRawJsonToken = Encoding.UTF8.GetString(textReader.GetBufferedJsonToken().Memory.ToArray());
                Assert.AreEqual($"UL{expected}", stringRawJsonToken);
            }
        }

        private void VerifyFloat32(IJsonReader jsonReader, float expected)
        {
            JsonTokenType jsonTokenType = jsonReader.CurrentTokenType;
            Assert.AreEqual(JsonTokenType.Float32, jsonTokenType);

            float actual = jsonReader.GetFloat32Value();
            Assert.AreEqual(expected, actual, double.Epsilon);

            // Additionally check if the text is correct
            if (jsonReader.SerializationFormat == JsonSerializationFormat.Text)
            {
                IJsonTextReaderPrivateImplementation textReader = (IJsonTextReaderPrivateImplementation)jsonReader;
                string stringRawJsonToken = Encoding.UTF8.GetString(textReader.GetBufferedJsonToken().Memory.ToArray());
                Assert.AreEqual($"S{expected.ToString("G9", CultureInfo.InvariantCulture)}", stringRawJsonToken);
            }
        }

        private void VerifyFloat64(IJsonReader jsonReader, double expected)
        {
            JsonTokenType jsonTokenType = jsonReader.CurrentTokenType;
            Assert.AreEqual(JsonTokenType.Float64, jsonTokenType);

            double actual = jsonReader.GetFloat64Value();
            Assert.AreEqual(expected, actual);

            // Additionally check if the text is correct
            if (jsonReader.SerializationFormat == JsonSerializationFormat.Text)
            {
                IJsonTextReaderPrivateImplementation textReader = (IJsonTextReaderPrivateImplementation)jsonReader;
                string stringRawJsonToken = Encoding.UTF8.GetString(textReader.GetBufferedJsonToken().Memory.ToArray());
                Assert.AreEqual($"D{expected.ToString("G17", CultureInfo.InvariantCulture)}", stringRawJsonToken);
            }
        }

        private void VerifyGuid(IJsonReader jsonReader, Guid expected)
        {
            JsonTokenType jsonTokenType = jsonReader.CurrentTokenType;
            Assert.AreEqual(JsonTokenType.Guid, jsonTokenType);

            Guid actual = jsonReader.GetGuidValue();
            Assert.AreEqual(expected, actual);

            // Additionally check if the text is correct
            if (jsonReader.SerializationFormat == JsonSerializationFormat.Text)
            {
                IJsonTextReaderPrivateImplementation textReader = (IJsonTextReaderPrivateImplementation)jsonReader;
                string stringRawJsonToken = Encoding.UTF8.GetString(textReader.GetBufferedJsonToken().Memory.ToArray());
                Assert.AreEqual($"G{expected}", stringRawJsonToken);
            }
        }

        private void VerifyBinary(IJsonReader jsonReader, ReadOnlyMemory<byte> expected)
        {
            JsonTokenType jsonTokenType = jsonReader.CurrentTokenType;
            Assert.AreEqual(JsonTokenType.Binary, jsonTokenType);

            ReadOnlyMemory<byte> actual = jsonReader.GetBinaryValue();
            Assert.IsTrue(expected.Span.SequenceEqual(actual.Span));

            // Additionally check if the text is correct
            if (jsonReader.SerializationFormat == JsonSerializationFormat.Text)
            {
                IJsonTextReaderPrivateImplementation textReader = (IJsonTextReaderPrivateImplementation)jsonReader;
                string stringRawJsonToken = Encoding.UTF8.GetString(textReader.GetBufferedJsonToken().Memory.ToArray());
                Assert.AreEqual($"B{Convert.ToBase64String(expected.ToArray())}", stringRawJsonToken);
            }
        }

        private void VerifyTrue(IJsonReader jsonReader)
        {
            this.VerifyToken(jsonReader, JsonTokenType.True, "true");
        }

        private void VerifyFalse(IJsonReader jsonReader)
        {
            this.VerifyToken(jsonReader, JsonTokenType.False, "false");
        }

        private void VerifyNull(IJsonReader jsonReader)
        {
            this.VerifyToken(jsonReader, JsonTokenType.Null, "null");
        }

        private void VerifyToken(IJsonReader jsonReader, JsonTokenType expectedJsonTokenType, string fragmentString)
        {
            JsonTokenType jsonTokenType = jsonReader.CurrentTokenType;

            Assert.AreEqual(expectedJsonTokenType, jsonTokenType);

            if (jsonReader.SerializationFormat == JsonSerializationFormat.Text)
            {
                this.VerifyFragment(jsonReader, fragmentString);
            }
        }

        private void VerifyFragment(IJsonReader jsonReader, string fragment)
        {
            IJsonTextReaderPrivateImplementation textReader = (IJsonTextReaderPrivateImplementation)jsonReader;
            string stringRawJsonToken = Encoding.UTF8.GetString(textReader.GetBufferedJsonToken().Memory.ToArray());
            Assert.AreEqual(fragment, stringRawJsonToken);
        }
    }
}