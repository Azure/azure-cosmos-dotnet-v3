//-----------------------------------------------------------------------
// <copyright file="JsonReaderTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.NetFramework.Tests.Json
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Text.RegularExpressions;
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
        [Owner("brchon")]
        public void TrueTest()
        {
            string input = "true";
            JsonTokenInfo[] expectedTokens = 
            {
                JsonTokenInfo.Boolean(true)
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
        [Owner("brchon")]
        public void FalseTest()
        {
            string input = "false";
            JsonTokenInfo[] expectedTokens = 
            {
                JsonTokenInfo.Boolean(false)
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
        [Owner("brchon")]
        public void NullTest()
        {
            string input = "null";
            byte[] binaryInput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.Null
            };

            JsonTokenInfo[] expectedTokens = 
            {
                JsonTokenInfo.Null()
            };

            this.VerifyReader(input, expectedTokens);
            this.VerifyReader(binaryInput, expectedTokens);
        }
        #endregion
        #region Numbers
        [TestMethod]
        [Owner("brchon")]
        public void IntegerTest()
        {
            string input = "1337";
            byte[] binaryInput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.Int16,
                // 1337 in litte endian hex,
                0x39, 0x05,
            };

            JsonTokenInfo[] expectedTokens = 
            {
                JsonTokenInfo.Number(1337)
            };

            this.VerifyReader(input, expectedTokens);
            this.VerifyReader(binaryInput, expectedTokens);
        }

        [TestMethod]
        [Owner("brchon")]
        public void DoubleTest()
        {
            string input = "1337.0";
            byte[] binaryInput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.Double,
                // 1337 in litte endian hex for a double
                0x00, 0x00, 0x00, 0x00, 0x00, 0xE4, 0x94, 0x40,
            };

            JsonTokenInfo[] expectedTokens = 
            {
                JsonTokenInfo.Number(1337.0)
            };

            this.VerifyReader(input, expectedTokens);
            this.VerifyReader(binaryInput, expectedTokens);
        }

        [TestMethod]
        [Owner("brchon")]
        public void NegativeNumberTest()
        {
            string input = "-1337.0";
            byte[] binaryInput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.Double,
                // -1337 in litte endian hex for a double
                0x00, 0x00, 0x00, 0x00, 0x00, 0xE4, 0x94, 0xC0,
            };

            JsonTokenInfo[] expectedTokens = 
            {
                JsonTokenInfo.Number(-1337.0)
            };

            this.VerifyReader(input, expectedTokens);
            this.VerifyReader(binaryInput, expectedTokens);
        }

        [TestMethod]
        [Owner("brchon")]
        public void NumberWithPlusSignTest()
        {
            string input = "+1337.0";

            JsonTokenInfo[] expectedTokens = 
            {
            };

            this.VerifyReader(input, expectedTokens, new JsonUnexpectedTokenException());
        }

        [TestMethod]
        [Owner("brchon")]
        public void NumberWithLeadingZeros()
        {
            string input = "01";

            JsonTokenInfo[] expectedTokens = 
            {
            };

            this.VerifyReader(input, expectedTokens, new JsonInvalidNumberException());
            this.VerifyReader("0" + input, expectedTokens, new JsonInvalidNumberException());
            this.VerifyReader("00" + input, expectedTokens, new JsonInvalidNumberException());
            this.VerifyReader("000" + input, expectedTokens, new JsonInvalidNumberException());

            // But 0 should still pass
            string zeroString = "0";

            JsonTokenInfo[] zeroToken = 
            {
                JsonTokenInfo.Number(0)
            };
            this.VerifyReader(zeroString, zeroToken);
        }

        [TestMethod]
        [Owner("brchon")]
        public void NumberWithScientificNotationTest()
        {
            string input = "6.02252e23";
            string input2 = "6.02252E23";
            byte[] binaryInput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.Double,
                // 6.02252e23 in litte endian hex for a double
                0x93, 0x09, 0x9F, 0x5D, 0x09, 0xE2, 0xDF, 0x44
            };

            JsonTokenInfo[] expectedTokens = 
            {
                JsonTokenInfo.Number(6.02252e23)
            };

            this.VerifyReader(input, expectedTokens);
            this.VerifyReader(input2, expectedTokens);
            this.VerifyReader(binaryInput, expectedTokens);
        }

        [TestMethod]
        [Owner("brchon")]
        public void NumberRegressionTest()
        {
            // regression test - the value 0.00085647800000000004 was being incorrectly rejected
            string numberValueString = "0.00085647800000000004";
            double numberValue = double.Parse(numberValueString);
            byte[] binaryInput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.Double,
                // 0.00085647800000000004 in litte endian hex for a double
                0x39, 0x98, 0xF7, 0x7F, 0xA8, 0x10, 0x4C, 0x3F
            };

            JsonTokenInfo[] expectedTokens = 
            {
                JsonTokenInfo.Number(numberValue)
            };

            this.VerifyReader(numberValueString, expectedTokens);
            this.VerifyReader(binaryInput, expectedTokens);
        }

        [TestMethod]
        [Owner("brchon")]
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
                JsonBinaryEncoding.TypeMarker.UInt8,
                (byte)number,
            };

            byte[] binaryInt16Input =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.Int16,
                0x04, 0x00,
            };

            byte[] binaryInt32Input =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.Int32,
                0x04, 0x00, 0x00, 0x00,
            };

            byte[] binaryInt64Input =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.Int64,
                0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            };

            byte[] binaryDoubleInput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.Double,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x10, 0x40,
            };

            JsonTokenInfo[] expectedTokens = 
            {
                JsonTokenInfo.Number(number)
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
        [Owner("brchon")]
        public void NumberLimitsTest()
        {
            // min byte
            JsonTokenInfo[] minByteTokens =
            {
                JsonTokenInfo.Number(byte.MinValue)
            };

            byte[] minByteInput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.UInt8,
                byte.MinValue
            };

            this.VerifyReader(byte.MinValue.ToString(), minByteTokens);
            this.VerifyReader(minByteInput, minByteTokens);

            // max byte
            JsonTokenInfo[] maxByteTokens =
            {
                JsonTokenInfo.Number(byte.MaxValue)
            };

            byte[] maxByteInput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.UInt8,
                byte.MaxValue
            };

            this.VerifyReader(byte.MaxValue.ToString(), maxByteTokens);
            this.VerifyReader(maxByteInput, maxByteTokens);

            // min short
            JsonTokenInfo[] minShortTokens =
            {
                JsonTokenInfo.Number(short.MinValue)
            };

            List<byte> minShortInput = new List<byte>();
            minShortInput.Add(BinaryFormat);
            minShortInput.Add(JsonBinaryEncoding.TypeMarker.Int16);
            minShortInput.AddRange(BitConverter.GetBytes(short.MinValue));

            this.VerifyReader(short.MinValue.ToString(), minShortTokens);
            this.VerifyReader(minShortInput.ToArray(), minShortTokens);

            // max short
            JsonTokenInfo[] maxShortTokens =
            {
                JsonTokenInfo.Number(short.MaxValue)
            };

            List<byte> maxShortInput = new List<byte>();
            maxShortInput.Add(BinaryFormat);
            maxShortInput.Add(JsonBinaryEncoding.TypeMarker.Int16);
            maxShortInput.AddRange(BitConverter.GetBytes(short.MaxValue));

            this.VerifyReader(short.MaxValue.ToString(), maxShortTokens);
            this.VerifyReader(maxShortInput.ToArray(), maxShortTokens);

            // min int
            JsonTokenInfo[] minIntTokens =
            {
                JsonTokenInfo.Number(int.MinValue)
            };

            List<byte> minIntInput = new List<byte>();
            minIntInput.Add(BinaryFormat);
            minIntInput.Add(JsonBinaryEncoding.TypeMarker.Int32);
            minIntInput.AddRange(BitConverter.GetBytes(int.MinValue));

            this.VerifyReader(int.MinValue.ToString(), minIntTokens);
            this.VerifyReader(minIntInput.ToArray(), minIntTokens);

            // max int
            JsonTokenInfo[] maxIntTokens =
            {
                JsonTokenInfo.Number(int.MaxValue)
            };

            List<byte> maxIntInput = new List<byte>();
            maxIntInput.Add(BinaryFormat);
            maxIntInput.Add(JsonBinaryEncoding.TypeMarker.Int32);
            maxIntInput.AddRange(BitConverter.GetBytes(int.MaxValue));

            this.VerifyReader(int.MaxValue.ToString(), maxIntTokens);
            this.VerifyReader(maxIntInput.ToArray(), maxIntTokens);

            // min long
            JsonTokenInfo[] minLongTokens =
            {
                JsonTokenInfo.Number(long.MinValue)
            };

            List<byte> minLongInput = new List<byte>();
            minLongInput.Add(BinaryFormat);
            minLongInput.Add(JsonBinaryEncoding.TypeMarker.Int64);
            minLongInput.AddRange(BitConverter.GetBytes(long.MinValue));

            this.VerifyReader(long.MinValue.ToString(), minLongTokens);
            this.VerifyReader(minLongInput.ToArray(), minLongTokens);

            // max long
            JsonTokenInfo[] maxLongTokens =
            {
                JsonTokenInfo.Number(long.MaxValue)
            };

            List<byte> maxLongInput = new List<byte>();
            maxLongInput.Add(BinaryFormat);
            maxLongInput.Add(JsonBinaryEncoding.TypeMarker.Int64);
            maxLongInput.AddRange(BitConverter.GetBytes(long.MaxValue));

            this.VerifyReader(long.MaxValue.ToString(), maxLongTokens);
            this.VerifyReader(maxLongInput.ToArray(), maxLongTokens);

            // min double
            JsonTokenInfo[] minDoubleTokens =
            {
                JsonTokenInfo.Number(double.MinValue)
            };

            List<byte> minDoubleInput = new List<byte>();
            minDoubleInput.Add(BinaryFormat);
            minDoubleInput.Add(JsonBinaryEncoding.TypeMarker.Double);
            minDoubleInput.AddRange(BitConverter.GetBytes(double.MinValue));

            this.VerifyReader(double.MinValue.ToString("G17"), minDoubleTokens);
            this.VerifyReader(minDoubleInput.ToArray(), minDoubleTokens);

            // max double
            JsonTokenInfo[] maxDoubleTokens =
            {
                JsonTokenInfo.Number(double.MaxValue)
            };

            List<byte> maxDoubleInput = new List<byte>();
            maxDoubleInput.Add(BinaryFormat);
            maxDoubleInput.Add(JsonBinaryEncoding.TypeMarker.Double);
            maxDoubleInput.AddRange(BitConverter.GetBytes(double.MaxValue));

            this.VerifyReader(double.MaxValue.ToString("G17"), maxDoubleTokens);
            this.VerifyReader(maxDoubleInput.ToArray(), maxDoubleTokens);
        }

        [TestMethod]
        [Owner("brchon")]
        public void NumberStartingWithDotTest()
        {
            string input = ".001";

            JsonTokenInfo[] expectedTokens = 
            {
            };

            this.VerifyReader(input, expectedTokens, new JsonUnexpectedTokenException());
        }

        [TestMethod]
        [Owner("brchon")]
        public void ScientificWithNoExponent()
        {
            string input = "1e";
            string input2 = "1E";

            JsonTokenInfo[] expectedTokens = 
            {
            };

            this.VerifyReader(input, expectedTokens, new JsonInvalidNumberException());
            this.VerifyReader(input2, expectedTokens, new JsonInvalidNumberException());
        }

        [TestMethod]
        [Owner("brchon")]
        public void ScientificWithPostitiveExponent()
        {
            string input = "6.02252e+23";
            string input2 = "6.02252E+23";

            JsonTokenInfo[] expectedTokens = 
            {
                JsonTokenInfo.Number(6.02252e+23)
            };

            this.VerifyReader(input, expectedTokens);
            this.VerifyReader(input2, expectedTokens);
        }

        [TestMethod]
        [Owner("brchon")]
        public void ScientificWithNegativeExponent()
        {
            string input = "6.02252e-23";
            string input2 = "6.02252E-23";

            JsonTokenInfo[] expectedTokens = 
            {
                JsonTokenInfo.Number(6.02252e-23)
            };

            this.VerifyReader(input, expectedTokens);
            this.VerifyReader(input2, expectedTokens);
        }
        #endregion
        #region Strings
        [TestMethod]
        [Owner("brchon")]
        public void EmptyStringTest()
        {
            string input = "\"\"";
            JsonTokenInfo[] expectedTokens = 
            {
                JsonTokenInfo.String(string.Empty)
            };

            this.VerifyReader(input, expectedTokens);
        }

        [TestMethod]
        [Owner("brchon")]
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

            JsonTokenInfo[] expectedTokens = 
            {
                JsonTokenInfo.String("Hello World")
            };

            this.VerifyReader(input, expectedTokens);
            this.VerifyReader(binary1ByteLengthInput, expectedTokens);
            this.VerifyReader(binary2ByteLengthInput, expectedTokens);
            this.VerifyReader(binary4ByteLengthInput, expectedTokens);
        }

        [TestMethod]
        [Owner("brchon")]
        public void SystemStringTest()
        {
            Type jsonBinaryEncodingType = typeof(JsonBinaryEncoding);
            FieldInfo systemStringsFieldInfo = jsonBinaryEncodingType.GetField("SystemStrings", BindingFlags.NonPublic | BindingFlags.Static);
            string[] systemStrings = (string[])systemStringsFieldInfo.GetValue(null);
            Assert.IsNotNull(systemStrings, "Failed to get system strings using reflection");

            int systemStringId = 0;
            foreach (string systemString in systemStrings)
            {
                string input = "\"" + systemString + "\"";
                byte[] binaryInput = 
                {
                    BinaryFormat,
                    (byte)(JsonBinaryEncoding.TypeMarker.SystemString1ByteLengthMin + systemStringId++),
                };

                JsonTokenInfo[] expectedTokens =
                {
                    JsonTokenInfo.String(systemString)
                };

                this.VerifyReader(input, expectedTokens);
                this.VerifyReader(binaryInput, expectedTokens);
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public void UserStringTest()
        {
            // Object with 33 field names. This creates a user string with 2 byte type marker.

            List<JsonTokenInfo> tokensToWrite = new List<JsonTokenInfo>() { JsonTokenInfo.ObjectStart() };
            StringBuilder textInput = new StringBuilder("{");
            List<byte> binaryInput = new List<byte>() { BinaryFormat, JsonBinaryEncoding.TypeMarker.Object1ByteLength, };
            List<byte> binaryInputWithEncoding = new List<byte>() { BinaryFormat, JsonBinaryEncoding.TypeMarker.Object1ByteLength };

            const byte OneByteCount = JsonBinaryEncoding.TypeMarker.UserString1ByteLengthMax - JsonBinaryEncoding.TypeMarker.UserString1ByteLengthMin;
            for (int i = 0; i < OneByteCount + 1; i++)
            {
                string userEncodedString = "a" + i.ToString();

                tokensToWrite.Add(JsonTokenInfo.FieldName(userEncodedString));
                tokensToWrite.Add(JsonTokenInfo.String(userEncodedString));

                if (i > 0)
                {
                    textInput.Append(",");
                }

                textInput.Append($@"""{userEncodedString}"":""{userEncodedString}""");

                for (int j = 0; j < 2; j++)
                {
                    binaryInput.Add((byte)(JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + userEncodedString.Length));
                    binaryInput.AddRange(Encoding.UTF8.GetBytes(userEncodedString));
                }

                if (i < OneByteCount)
                {
                    binaryInputWithEncoding.Add((byte)(JsonBinaryEncoding.TypeMarker.UserString1ByteLengthMin + i));
                }
                else
                {
                    int twoByteOffset = i - OneByteCount;
                    binaryInputWithEncoding.Add((byte)((twoByteOffset / 0xFF) + JsonBinaryEncoding.TypeMarker.UserString2ByteLengthMin));
                    binaryInputWithEncoding.Add((byte)(twoByteOffset % 0xFF));
                }

                binaryInputWithEncoding.Add((byte)(JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + userEncodedString.Length));
                binaryInputWithEncoding.AddRange(Encoding.UTF8.GetBytes(userEncodedString));
            }

            tokensToWrite.Add(JsonTokenInfo.ObjectEnd());
            textInput.Append("}");
            binaryInput.Insert(2, (byte)(binaryInput.Count() - 2));
            binaryInputWithEncoding.Insert(2, (byte)(binaryInputWithEncoding.Count() - 2));

            this.VerifyReader(textInput.ToString(), tokensToWrite.ToArray());
            this.VerifyReader(binaryInput.ToArray(), tokensToWrite.ToArray());

            JsonStringDictionary jsonStringDictionary = new JsonStringDictionary(capacity: 100);
            for (int i = 0; i < OneByteCount + 1; i++)
            {
                string userEncodedString = "a" + i.ToString();
                Assert.IsTrue(jsonStringDictionary.TryAddString(userEncodedString, out int index));
                Assert.AreEqual(i, index);
            }

            this.VerifyReader(binaryInputWithEncoding.ToArray(), tokensToWrite.ToArray(), jsonStringDictionary);
        }

        [TestMethod]
        [Owner("brchon")]
        public void NumberAsStringTest()
        {
            string input = "\"42\"";
            JsonTokenInfo[] expectedTokens =
            {
                JsonTokenInfo.String("42")
            };

            this.VerifyReader(input, expectedTokens);
        }

        [TestMethod]
        [Owner("brchon")]
        public void BoolAsStringTest()
        {
            string input = "\"true\"";
            JsonTokenInfo[] expectedTokens =
            {
                JsonTokenInfo.String("true")
            };

            this.VerifyReader(input, expectedTokens);
        }

        [TestMethod]
        [Owner("brchon")]
        public void NullAsStringTest()
        {
            string input = "\"null\"";
            JsonTokenInfo[] expectedTokens =
            {
                JsonTokenInfo.String("null")
            };

            this.VerifyReader(input, expectedTokens);
        }

        [TestMethod]
        [Owner("brchon")]
        public void ArrayAsStringTest()
        {
            string input = "\"[]\"";
            JsonTokenInfo[] expectedTokens =
            {
                JsonTokenInfo.String("[]")
            };

            this.VerifyReader(input, expectedTokens);
        }

        [TestMethod]
        [Owner("brchon")]
        public void ObjectAsStringTest()
        {
            string input = "\"{}\"";
            JsonTokenInfo[] expectedTokens =
            {
                JsonTokenInfo.String("{}")
            };

            this.VerifyReader(input, expectedTokens);
        }
        #endregion
        #region Arrays
        [TestMethod]
        [Owner("brchon")]
        public void ArrayRepresentationTest()
        {
            string input = "[true, false]";
            byte[] binary1ByteLengthInput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.Array1ByteLength,
                // length
                2,
                JsonBinaryEncoding.TypeMarker.True,
                JsonBinaryEncoding.TypeMarker.False,
            };

            byte[] binary1ByteLengthAndCountInput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.Array1ByteLengthAndCount,
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
                JsonBinaryEncoding.TypeMarker.Array2ByteLength,
                // length
                2, 0x00,
                JsonBinaryEncoding.TypeMarker.True,
                JsonBinaryEncoding.TypeMarker.False,
            };

            byte[] binary2ByteLengthAndCountInput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.Array2ByteLengthAndCount,
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
                JsonBinaryEncoding.TypeMarker.Array4ByteLength,
                // length
                2, 0x00, 0x00, 0x00,
                JsonBinaryEncoding.TypeMarker.True,
                JsonBinaryEncoding.TypeMarker.False,
            };

            byte[] binary4ByteLengthAndCountInput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.Array4ByteLengthAndCount,
                // length
                2, 0x00, 0x00, 0x00,
                // count
                2, 0x00, 0x00, 0x00,
                JsonBinaryEncoding.TypeMarker.True,
                JsonBinaryEncoding.TypeMarker.False,
            };

            JsonTokenInfo[] expectedTokens =
            {
                JsonTokenInfo.ArrayStart(),
                JsonTokenInfo.Boolean(true),
                JsonTokenInfo.Boolean(false),
                JsonTokenInfo.ArrayEnd(),
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
        [Owner("brchon")]
        public void EmptyArrayTest()
        {
            string input = "[  ]  ";
            byte[] binaryInput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.EmptyArray
            };

            JsonTokenInfo[] expectedTokens =
            {
                JsonTokenInfo.ArrayStart(),
                JsonTokenInfo.ArrayEnd(),
            };

            this.VerifyReader(input, expectedTokens);
            this.VerifyReader(binaryInput, expectedTokens);
        }

        [TestMethod]
        [Owner("brchon")]
        public void SingleItemArrayTest()
        {
            string input = "[ true ]  ";
            byte[] binaryInput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.SingleItemArray,
                JsonBinaryEncoding.TypeMarker.True
            };

            JsonTokenInfo[] expectedTokens =
            {
                JsonTokenInfo.ArrayStart(),
                JsonTokenInfo.Boolean(true),
                JsonTokenInfo.ArrayEnd(),
            };

            this.VerifyReader(input, expectedTokens);
            this.VerifyReader(binaryInput, expectedTokens);
        }

        [TestMethod]
        [Owner("brchon")]
        public void IntArrayTest()
        {
            string input = "[ -2, -1, 0, 1, 2]  ";
            List<byte[]> binaryInputBuilder = new List<byte[]>();
            binaryInputBuilder.Add(new byte[] { BinaryFormat, JsonBinaryEncoding.TypeMarker.Array1ByteLength });

            List<byte[]> numbers = new List<byte[]>();
            numbers.Add(new byte[] { JsonBinaryEncoding.TypeMarker.Int16, 0xFE, 0xFF });
            numbers.Add(new byte[] { JsonBinaryEncoding.TypeMarker.Int16, 0xFF, 0xFF });
            numbers.Add(new byte[] { JsonBinaryEncoding.TypeMarker.LiteralIntMin });
            numbers.Add(new byte[] { JsonBinaryEncoding.TypeMarker.LiteralIntMin + 1 });
            numbers.Add(new byte[] { JsonBinaryEncoding.TypeMarker.LiteralIntMin + 2 });
            byte[] numbersBytes = numbers.SelectMany(x => x).ToArray();

            binaryInputBuilder.Add(new byte[] { (byte)numbersBytes.Length });
            binaryInputBuilder.Add(numbersBytes);
            byte[] binaryInput = binaryInputBuilder.SelectMany(x => x).ToArray();

            JsonTokenInfo[] expectedTokens =
            {
                JsonTokenInfo.ArrayStart(),
                JsonTokenInfo.Number(-2),
                JsonTokenInfo.Number(-1),
                JsonTokenInfo.Number(0),
                JsonTokenInfo.Number(1),
                JsonTokenInfo.Number(2),
                JsonTokenInfo.ArrayEnd(),
            };

            this.VerifyReader(input, expectedTokens);
            this.VerifyReader(binaryInput, expectedTokens);
        }

        [TestMethod]
        [Owner("brchon")]
        public void NumberArrayTest()
        {
            string input = "[15,  22, 0.1, -7.3e-2, 77.0001e90 ]  ";

            List<byte[]> binaryInputBuilder = new List<byte[]>();
            binaryInputBuilder.Add(new byte[] { BinaryFormat, JsonBinaryEncoding.TypeMarker.Array1ByteLength });

            List<byte[]> numbers = new List<byte[]>();
            numbers.Add(new byte[] { JsonBinaryEncoding.TypeMarker.LiteralIntMin + 15 });
            numbers.Add(new byte[] { JsonBinaryEncoding.TypeMarker.LiteralIntMin + 22 });
            numbers.Add(new byte[] { JsonBinaryEncoding.TypeMarker.Double, 0x9A, 0x99, 0x99, 0x99, 0x99, 0x99, 0xB9, 0x3F });
            numbers.Add(new byte[] { JsonBinaryEncoding.TypeMarker.Double, 0xE3, 0xA5, 0x9B, 0xC4, 0x20, 0xB0, 0xB2, 0xBF });
            numbers.Add(new byte[] { JsonBinaryEncoding.TypeMarker.Double, 0xBE, 0xDA, 0x50, 0xA7, 0x68, 0xE6, 0x02, 0x53 });
            byte[] numbersBytes = numbers.SelectMany(x => x).ToArray();

            binaryInputBuilder.Add(new byte[] { (byte)numbersBytes.Length });
            binaryInputBuilder.Add(numbersBytes);
            byte[] binaryInput = binaryInputBuilder.SelectMany(x => x).ToArray();

            JsonTokenInfo[] expectedTokens =
            {
                JsonTokenInfo.ArrayStart(),
                JsonTokenInfo.Number(15),
                JsonTokenInfo.Number(22),
                JsonTokenInfo.Number(0.1),
                JsonTokenInfo.Number(-7.3e-2),
                JsonTokenInfo.Number(77.0001e90),
                JsonTokenInfo.ArrayEnd(),
            };

            this.VerifyReader(input, expectedTokens);
            this.VerifyReader(binaryInput, expectedTokens);
        }

        [TestMethod]
        [Owner("brchon")]
        public void StringArrayTest()
        {
            string input = @"[""Hello"", ""World"", ""Bye""]";

            List<byte[]> binaryInputBuilder = new List<byte[]>();
            binaryInputBuilder.Add(new byte[] { BinaryFormat, JsonBinaryEncoding.TypeMarker.Array1ByteLength });

            List<byte[]> strings = new List<byte[]>();
            strings.Add(new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + "Hello".Length) });
            strings.Add(Encoding.UTF8.GetBytes("Hello"));
            strings.Add(new byte[] { JsonBinaryEncoding.TypeMarker.String1ByteLength });
            strings.Add(new byte[] { (byte)("World".Length) });
            strings.Add(Encoding.UTF8.GetBytes("World"));
            strings.Add(new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + "Bye".Length) });
            strings.Add(Encoding.UTF8.GetBytes("Bye"));
            byte[] stringBytes = strings.SelectMany(x => x).ToArray();

            binaryInputBuilder.Add(new byte[] { (byte)stringBytes.Length });
            binaryInputBuilder.Add(stringBytes);
            byte[] binaryInput = binaryInputBuilder.SelectMany(x => x).ToArray();

            JsonTokenInfo[] expectedTokens =
            {
                JsonTokenInfo.ArrayStart(),
                JsonTokenInfo.String("Hello"),
                JsonTokenInfo.String("World"),
                JsonTokenInfo.String("Bye"),
                JsonTokenInfo.ArrayEnd(),
            };

            this.VerifyReader(input, expectedTokens);
            this.VerifyReader(binaryInput, expectedTokens);
        }

        [TestMethod]
        [Owner("brchon")]
        public void BooleanArrayTest()
        {
            string input = "[ true, false]  ";
            byte[] binaryInput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.Array1ByteLength,
                // length
                2,
                JsonBinaryEncoding.TypeMarker.True,
                JsonBinaryEncoding.TypeMarker.False,
            };

            JsonTokenInfo[] expectedTokens =
            {
                JsonTokenInfo.ArrayStart(),
                JsonTokenInfo.Boolean(true),
                JsonTokenInfo.Boolean(false),
                JsonTokenInfo.ArrayEnd(),
            };

            this.VerifyReader(input, expectedTokens);
            this.VerifyReader(binaryInput, expectedTokens);
        }

        [TestMethod]
        [Owner("brchon")]
        public void NullArrayTest()
        {
            string input = "[ null, null, null]  ";
            byte[] binaryInput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.Array1ByteLength,
                // length
                3,
                JsonBinaryEncoding.TypeMarker.Null,
                JsonBinaryEncoding.TypeMarker.Null,
                JsonBinaryEncoding.TypeMarker.Null,
            };

            JsonTokenInfo[] expectedTokens =
            {
                JsonTokenInfo.ArrayStart(),
                JsonTokenInfo.Null(),
                JsonTokenInfo.Null(),
                JsonTokenInfo.Null(),
                JsonTokenInfo.ArrayEnd(),
            };

            this.VerifyReader(input, expectedTokens);
            this.VerifyReader(binaryInput, expectedTokens);
        }

        [TestMethod]
        [Owner("brchon")]
        public void ObjectArrayTest()
        {
            string input = "[{}, {}]  ";
            byte[] binaryInput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.Array1ByteLength,
                // length
                2,
                JsonBinaryEncoding.TypeMarker.EmptyObject,
                JsonBinaryEncoding.TypeMarker.EmptyObject,
            };

            JsonTokenInfo[] expectedTokens =
            {
                JsonTokenInfo.ArrayStart(),
                JsonTokenInfo.ObjectStart(),
                JsonTokenInfo.ObjectEnd(),
                JsonTokenInfo.ObjectStart(),
                JsonTokenInfo.ObjectEnd(),
                JsonTokenInfo.ArrayEnd(),
            };

            this.VerifyReader(input, expectedTokens);
            this.VerifyReader(binaryInput, expectedTokens);
        }

        [TestMethod]
        [Owner("brchon")]
        public void AllPrimitiveArrayTest()
        {
            string input = "[0, 0.0, -1, -1.0, 1, 2, \"hello\", null, true, false]  ";
            List<byte[]> binaryInputBuilder = new List<byte[]>();
            binaryInputBuilder.Add(new byte[] { BinaryFormat, JsonBinaryEncoding.TypeMarker.Array1ByteLength });

            List<byte[]> elements = new List<byte[]>();
            elements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.LiteralIntMin });
            elements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.Double, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });
            elements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.Int16, 0xFF, 0xFF });
            elements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.Double, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xF0, 0xBF });
            elements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.LiteralIntMin + 1 });
            elements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.LiteralIntMin + 2 });
            elements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.String1ByteLength, (byte)"hello".Length, 104, 101, 108, 108, 111 });
            elements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.Null });
            elements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.True });
            elements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.False });
            byte[] elementsBytes = elements.SelectMany(x => x).ToArray();

            binaryInputBuilder.Add(new byte[] { (byte)elementsBytes.Length });
            binaryInputBuilder.Add(elementsBytes);
            byte[] binaryInput = binaryInputBuilder.SelectMany(x => x).ToArray();

            JsonTokenInfo[] expectedTokens =
            {
                JsonTokenInfo.ArrayStart(),
                JsonTokenInfo.Number(0),
                JsonTokenInfo.Number(0.0),
                JsonTokenInfo.Number(-1),
                JsonTokenInfo.Number(-1.0),
                JsonTokenInfo.Number(1),
                JsonTokenInfo.Number(2),
                JsonTokenInfo.String("hello"),
                JsonTokenInfo.Null(),
                JsonTokenInfo.Boolean(true),
                JsonTokenInfo.Boolean(false),
                JsonTokenInfo.ArrayEnd(),
            };

            this.VerifyReader(input, expectedTokens);
            this.VerifyReader(binaryInput, expectedTokens);
        }

        [TestMethod]
        [Owner("brchon")]
        public void NestedArrayTest()
        {
            string input = "[[], []]  ";
            byte[] binaryInput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.Array1ByteLength,
                // length
                2,
                JsonBinaryEncoding.TypeMarker.EmptyArray,
                JsonBinaryEncoding.TypeMarker.EmptyArray,
            };

            JsonTokenInfo[] expectedTokens =
            {
                JsonTokenInfo.ArrayStart(),
                JsonTokenInfo.ArrayStart(),
                JsonTokenInfo.ArrayEnd(),
                JsonTokenInfo.ArrayStart(),
                JsonTokenInfo.ArrayEnd(),
                JsonTokenInfo.ArrayEnd(),
            };

            this.VerifyReader(input, expectedTokens);
            this.VerifyReader(binaryInput, expectedTokens);
        }

        [TestMethod]
        [Owner("brchon")]
        public void StrangeNumberArrayTest()
        {
            string input = @"[
        1111111110111111111011111111101111111110,
        1111111110111111111011111111101111111110111111111011111111101111111110,
       11111111101111111110111111111011111111101111111110111111111011111111101111111110111111111011111111101111111110111111111011111111101111111110,
        1111111110111111111011111111101111111110111111111011111111101111111110111111111011111111101111111110111111111011111111101111111110111111111011111111101111111110111111111011111111101111111110111111111011111111101111111110111111111011111111101111111110111111111011111111101111111110
            ]";

            List<byte[]> binaryInputBuilder = new List<byte[]>();
            binaryInputBuilder.Add(new byte[] { BinaryFormat, JsonBinaryEncoding.TypeMarker.Array1ByteLength });

            List<byte[]> elements = new List<byte[]>();
            elements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.Double, 0xBC, 0xCA, 0x0F, 0xBA, 0x41, 0x1F, 0x0A, 0x48 });
            elements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.Double, 0xDB, 0x5E, 0xAE, 0xBE, 0x50, 0x9B, 0x44, 0x4E });
            elements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.Double, 0x32, 0x80, 0x84, 0x3C, 0x73, 0xDB, 0xCD, 0x5C });
            elements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.Double, 0x8D, 0x0D, 0x28, 0x0B, 0x16, 0x57, 0xDF, 0x79 });
            byte[] elementsBytes = elements.SelectMany(x => x).ToArray();

            binaryInputBuilder.Add(new byte[] { (byte)elementsBytes.Length });
            binaryInputBuilder.Add(elementsBytes);
            byte[] binaryInput = binaryInputBuilder.SelectMany(x => x).ToArray();

            JsonTokenInfo[] expectedTokens =
            {
                JsonTokenInfo.ArrayStart(),
                JsonTokenInfo.Number(1111111110111111111011111111101111111110.0),
                JsonTokenInfo.Number(1111111110111111111011111111101111111110111111111011111111101111111110.0),
                JsonTokenInfo.Number(1.1111111101111111e+139),
                JsonTokenInfo.Number(1.1111111101111111e+279),
                JsonTokenInfo.ArrayEnd(),
            };

            this.VerifyReader(input, expectedTokens);
            this.VerifyReader(binaryInput, expectedTokens);
        }

        #endregion
        #region Escaping
        [TestMethod]
        [Owner("brchon")]
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
                JsonTokenInfo[] expectedTokens =
                {
                     JsonTokenInfo.String(escapeCharacter.Item2),
                };

                this.VerifyReader(input, expectedTokens);
                // Binary does not test this since you would just put the literal character if you wanted it.
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public void WhitespaceCharacterTest()
        {
            // http://www.ietf.org/rfc/rfc4627.txt for JSON whitespace definition (Section 2).
            char[] whitespaceCharacters = new char[]
            {
                ' ',
                '\t',
                '\r',
                '\n'
            };

            string input = "[" + " " + "\"hello\"" + "," + "\t" + "\"my\"" + "\r" + "," + "\"name\"" + "\n" + "," + "\"is\"" + "]";

            JsonTokenInfo[] expectedTokens =
            {
                JsonTokenInfo.ArrayStart(),
                JsonTokenInfo.String("hello"),
                JsonTokenInfo.String("my"),
                JsonTokenInfo.String("name"),
                JsonTokenInfo.String("is"),
                JsonTokenInfo.ArrayEnd(),
            };

            this.VerifyReader(input, expectedTokens);
            // Binary does not test this since you would just put the literal character if you wanted it.
        }

        [TestMethod]
        [Owner("brchon")]
        public void UnicodeEscapeTest()
        {
            // unicode characters are utf-16 when unescaped by default
            string unicodeEscapedString = @"""\u20AC""";
            // This is the 2 byte escaped equivalent.
            string expectedString = "\x20AC";

            JsonTokenInfo[] expectedTokens =
            {
                 JsonTokenInfo.String(expectedString),
            };

            this.VerifyReader(unicodeEscapedString, expectedTokens);
            // Binary does not test this since you would just put the literal character if you wanted it.
        }

        [TestMethod]
        [Owner("brchon")]
        public void TwoAdjacentUnicodeCharactersTest()
        {
            // 2 unicode escape characters that are not surrogate pairs
            string unicodeEscapedString = @"""\u20AC\u20AC""";
            // This is the escaped equivalent.
            string expectedString = "\x20AC\x20AC";

            JsonTokenInfo[] expectedTokens =
            {
                 JsonTokenInfo.String(expectedString),
            };

            this.VerifyReader(unicodeEscapedString, expectedTokens);
            // Binary does not test this since you would just put the literal character if you wanted it.
        }

        [TestMethod]
        [Owner("brchon")]
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

            JsonTokenInfo[] expectedTokens =
            {
                 JsonTokenInfo.String(expectedString),
            };

            this.VerifyReader(unicodeString, expectedTokens);
            this.VerifyReader(binaryInput, expectedTokens);
        }

        [TestMethod]
        [Owner("brchon")]
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

            JsonTokenInfo[] expectedTokens =
            {
                 JsonTokenInfo.String(expectedString),
            };

            this.VerifyReader(unicodeString, expectedTokens);
            this.VerifyReader(binaryInput, expectedTokens);
        }

        [TestMethod]
        [Owner("brchon")]
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

            JsonTokenInfo[] expectedTokens =
            {
                 JsonTokenInfo.String(expectedString),
            };

            this.VerifyReader(unicodeString, expectedTokens);
            this.VerifyReader(binaryInput, expectedTokens);
        }

        [TestMethod]
        [Owner("brchon")]
        public void ControlCharacterTests()
        {
            // control characters (U+0000 through U+001F)
            for (byte controlCharacter = 0; controlCharacter <= 0x1F; controlCharacter++)
            {
                string unicodeString = "\"" + "\\u" + "00" + controlCharacter.ToString("X2") + "\"";

                JsonTokenInfo[] expectedTokens =
                {
                    JsonTokenInfo.String(string.Empty + (char)controlCharacter)
                };

                this.VerifyReader(unicodeString, expectedTokens);
            }
        }
        #endregion
        #region Objects
        [TestMethod]
        [Owner("brchon")]
        public void EmptyObjectTest()
        {
            string input = "{}";
            byte[] binaryInput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.EmptyObject,
            };

            JsonTokenInfo[] expectedTokens =
            {
                 JsonTokenInfo.ObjectStart(),
                 JsonTokenInfo.ObjectEnd(),
            };

            this.VerifyReader(input, expectedTokens);
            this.VerifyReader(binaryInput, expectedTokens);
        }

        [TestMethod]
        [Owner("brchon")]
        public void SimpleObjectTest()
        {
            string input = "{\"GlossDiv\":10,\"title\": \"example glossary\" }";

            byte[] binaryInput;
            {
                List<byte[]> binaryInputBuilder = new List<byte[]>();
                binaryInputBuilder.Add(new byte[] { BinaryFormat, JsonBinaryEncoding.TypeMarker.Object1ByteLength });

                List<byte[]> elements = new List<byte[]>();
                elements.Add(new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + "GlossDiv".Length), 71, 108, 111, 115, 115, 68, 105, 118 });
                elements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.LiteralIntMin + 10 });
                elements.Add(new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + "title".Length), 116, 105, 116, 108, 101 });
                elements.Add(new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + "example glossary".Length), 101, 120, 97, 109, 112, 108, 101, 32, 103, 108, 111, 115, 115, 97, 114, 121 });
                byte[] elementsBytes = elements.SelectMany(x => x).ToArray();

                binaryInputBuilder.Add(new byte[] { (byte)elementsBytes.Length });
                binaryInputBuilder.Add(elementsBytes);
                binaryInput = binaryInputBuilder.SelectMany(x => x).ToArray();
            }

            byte[] binaryInputWithEncoding;
            {
                List<byte[]> binaryInputBuilder = new List<byte[]>();
                binaryInputBuilder.Add(new byte[] { BinaryFormat, JsonBinaryEncoding.TypeMarker.Object1ByteLength });

                List<byte[]> elements = new List<byte[]>();
                elements.Add(new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.UserString1ByteLengthMin) });
                elements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.LiteralIntMin + 10 });
                elements.Add(new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.UserString1ByteLengthMin + 1) });
                elements.Add(new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + "example glossary".Length), 101, 120, 97, 109, 112, 108, 101, 32, 103, 108, 111, 115, 115, 97, 114, 121 });
                byte[] elementsBytes = elements.SelectMany(x => x).ToArray();

                binaryInputBuilder.Add(new byte[] { (byte)elementsBytes.Length });
                binaryInputBuilder.Add(elementsBytes);
                binaryInputWithEncoding = binaryInputBuilder.SelectMany(x => x).ToArray();
            }

            JsonTokenInfo[] expectedTokens =
            {
                JsonTokenInfo.ObjectStart(),
                JsonTokenInfo.FieldName("GlossDiv"),
                JsonTokenInfo.Number(10),
                JsonTokenInfo.FieldName("title"),
                JsonTokenInfo.String("example glossary"),
                JsonTokenInfo.ObjectEnd(),
            };

            this.VerifyReader(input, expectedTokens);
            this.VerifyReader(binaryInput, expectedTokens);
            JsonStringDictionary jsonStringDictionary = new JsonStringDictionary(capacity: 100);
            Assert.IsTrue(jsonStringDictionary.TryAddString("GlossDiv", out int index1));
            Assert.IsTrue(jsonStringDictionary.TryAddString("title", out int index2));
            this.VerifyReader(binaryInputWithEncoding, expectedTokens, jsonStringDictionary);
        }

        [TestMethod]
        [Owner("brchon")]
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
                List<byte[]> binaryInputBuilder = new List<byte[]>();
                binaryInputBuilder.Add(new byte[] { BinaryFormat, JsonBinaryEncoding.TypeMarker.Object2ByteLength });

                List<byte[]> elements = new List<byte[]>();
                elements.Add(new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.SystemString1ByteLengthMin + 12) });
                elements.Add(new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + "7029d079-4016-4436-b7da-36c0bae54ff6".Length), 55, 48, 50, 57, 100, 48, 55, 57, 45, 52, 48, 49, 54, 45, 52, 52, 51, 54, 45, 98, 55, 100, 97, 45, 51, 54, 99, 48, 98, 97, 101, 53, 52, 102, 102, 54 });

                elements.Add(new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + "double".Length), 100, 111, 117, 98, 108, 101 });
                elements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.Double, 0x98, 0x8B, 0x30, 0xE3, 0xCB, 0x45, 0xC8, 0x3F });

                elements.Add(new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + "int".Length), 105, 110, 116 });
                elements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.Int32, 0x19, 0xDF, 0xB6, 0xB0 });

                elements.Add(new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + "string".Length), 115, 116, 114, 105, 110, 103 });
                elements.Add(new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + "XCPCFXPHHF".Length), 88, 67, 80, 67, 70, 88, 80, 72, 72, 70 });

                elements.Add(new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + "boolean".Length), 98, 111, 111, 108, 101, 97, 110 });
                elements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.True });

                elements.Add(new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + "null".Length), 110, 117, 108, 108 });
                elements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.Null });

                elements.Add(new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + "datetime".Length), 100, 97, 116, 101, 116, 105, 109, 101 });
                elements.Add(new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + "2526-07-11T18:18:16.4520716".Length), 50, 53, 50, 54, 45, 48, 55, 45, 49, 49, 84, 49, 56, 58, 49, 56, 58, 49, 54, 46, 52, 53, 50, 48, 55, 49, 54 });

                elements.Add(new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + "spatialPoint".Length), 115, 112, 97, 116, 105, 97, 108, 80, 111, 105, 110, 116 });

                List<byte[]> innerObjectElements = new List<byte[]>();
                innerObjectElements.Add(new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.SystemString1ByteLengthMin + 27) });
                innerObjectElements.Add(new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.SystemString1ByteLengthMin + 24) });

                innerObjectElements.Add(new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.SystemString1ByteLengthMin + 09) });
                List<byte[]> innerArrayElements = new List<byte[]>();
                innerArrayElements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.Double, 0x7A, 0x36, 0xAB, 0x3E, 0x57, 0xBF, 0x5D, 0x40 });
                innerArrayElements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.Double, 0x74, 0xB5, 0x15, 0xFB, 0xCB, 0x56, 0x47, 0xC0 });
                byte[] innerArrayElementsBytes = innerArrayElements.SelectMany(x => x).ToArray();

                innerObjectElements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.Array1ByteLength, (byte)innerArrayElementsBytes.Length });
                innerObjectElements.Add(innerArrayElementsBytes);

                byte[] innerObjectElementsBytes = innerObjectElements.SelectMany(x => x).ToArray();
                elements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.Object1ByteLength, (byte)innerObjectElementsBytes.Length });
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
                List<byte[]> binaryInputBuilder = new List<byte[]>();
                binaryInputBuilder.Add(new byte[] { BinaryFormat, JsonBinaryEncoding.TypeMarker.Object2ByteLength });

                List<byte[]> elements = new List<byte[]>();
                elements.Add(new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.SystemString1ByteLengthMin + 12) });
                elements.Add(new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + "7029d079-4016-4436-b7da-36c0bae54ff6".Length), 55, 48, 50, 57, 100, 48, 55, 57, 45, 52, 48, 49, 54, 45, 52, 52, 51, 54, 45, 98, 55, 100, 97, 45, 51, 54, 99, 48, 98, 97, 101, 53, 52, 102, 102, 54 });

                elements.Add(new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.UserString1ByteLengthMin) });
                elements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.Double, 0x98, 0x8B, 0x30, 0xE3, 0xCB, 0x45, 0xC8, 0x3F });

                elements.Add(new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.UserString1ByteLengthMin + 1) });
                elements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.Int32, 0x19, 0xDF, 0xB6, 0xB0 });

                elements.Add(new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.UserString1ByteLengthMin + 2) });
                elements.Add(new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + "XCPCFXPHHF".Length), 88, 67, 80, 67, 70, 88, 80, 72, 72, 70 });

                elements.Add(new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.UserString1ByteLengthMin + 3) });
                elements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.True });

                elements.Add(new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.UserString1ByteLengthMin + 4) });
                elements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.Null });

                elements.Add(new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.UserString1ByteLengthMin + 5) });
                elements.Add(new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + "2526-07-11T18:18:16.4520716".Length), 50, 53, 50, 54, 45, 48, 55, 45, 49, 49, 84, 49, 56, 58, 49, 56, 58, 49, 54, 46, 52, 53, 50, 48, 55, 49, 54 });

                elements.Add(new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.UserString1ByteLengthMin + 6) });

                List<byte[]> innerObjectElements = new List<byte[]>();
                innerObjectElements.Add(new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.SystemString1ByteLengthMin + 27) });
                innerObjectElements.Add(new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.SystemString1ByteLengthMin + 24) });

                innerObjectElements.Add(new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.SystemString1ByteLengthMin + 09) });
                List<byte[]> innerArrayElements = new List<byte[]>();
                innerArrayElements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.Double, 0x7A, 0x36, 0xAB, 0x3E, 0x57, 0xBF, 0x5D, 0x40 });
                innerArrayElements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.Double, 0x74, 0xB5, 0x15, 0xFB, 0xCB, 0x56, 0x47, 0xC0 });
                byte[] innerArrayElementsBytes = innerArrayElements.SelectMany(x => x).ToArray();

                innerObjectElements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.Array1ByteLength, (byte)innerArrayElementsBytes.Length });
                innerObjectElements.Add(innerArrayElementsBytes);

                byte[] innerObjectElementsBytes = innerObjectElements.SelectMany(x => x).ToArray();
                elements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.Object1ByteLength, (byte)innerObjectElementsBytes.Length });
                elements.Add(innerObjectElementsBytes);

                elements.Add(new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.UserString1ByteLengthMin + 7) });
                elements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.String1ByteLength, (byte)"tiger diamond newbrunswick snowleopard chocolate dog snowleopard turtle cat sapphire peach sapphire vancouver white chocolate horse diamond lion superlongcolourname ruby".Length, 116, 105, 103, 101, 114, 32, 100, 105, 97, 109, 111, 110, 100, 32, 110, 101, 119, 98, 114, 117, 110, 115, 119, 105, 99, 107, 32, 115, 110, 111, 119, 108, 101, 111, 112, 97, 114, 100, 32, 99, 104, 111, 99, 111, 108, 97, 116, 101, 32, 100, 111, 103, 32, 115, 110, 111, 119, 108, 101, 111, 112, 97, 114, 100, 32, 116, 117, 114, 116, 108, 101, 32, 99, 97, 116, 32, 115, 97, 112, 112, 104, 105, 114, 101, 32, 112, 101, 97, 99, 104, 32, 115, 97, 112, 112, 104, 105, 114, 101, 32, 118, 97, 110, 99, 111, 117, 118, 101, 114, 32, 119, 104, 105, 116, 101, 32, 99, 104, 111, 99, 111, 108, 97, 116, 101, 32, 104, 111, 114, 115, 101, 32, 100, 105, 97, 109, 111, 110, 100, 32, 108, 105, 111, 110, 32, 115, 117, 112, 101, 114, 108, 111, 110, 103, 99, 111, 108, 111, 117, 114, 110, 97, 109, 101, 32, 114, 117, 98, 121 });

                byte[] elementsBytes = elements.SelectMany(x => x).ToArray();

                binaryInputBuilder.Add(BitConverter.GetBytes((short)elementsBytes.Length));
                binaryInputBuilder.Add(elementsBytes);
                binaryInputWithEncoding = binaryInputBuilder.SelectMany(x => x).ToArray();
            }

            JsonTokenInfo[] expectedTokens =
            {
                JsonTokenInfo.ObjectStart(),

                    JsonTokenInfo.FieldName("id"),
                    JsonTokenInfo.String("7029d079-4016-4436-b7da-36c0bae54ff6"),

                    JsonTokenInfo.FieldName("double"),
                    JsonTokenInfo.Number(0.18963001816981939),

                    JsonTokenInfo.FieldName("int"),
                    JsonTokenInfo.Number(-1330192615),

                    JsonTokenInfo.FieldName("string"),
                    JsonTokenInfo.String("XCPCFXPHHF"),

                    JsonTokenInfo.FieldName("boolean"),
                    JsonTokenInfo.Boolean(true),

                    JsonTokenInfo.FieldName("null"),
                    JsonTokenInfo.Null(),

                    JsonTokenInfo.FieldName("datetime"),
                    JsonTokenInfo.String("2526-07-11T18:18:16.4520716"),

                    JsonTokenInfo.FieldName("spatialPoint"),
                    JsonTokenInfo.ObjectStart(),
                        JsonTokenInfo.FieldName("type"),
                        JsonTokenInfo.String("Point"),

                        JsonTokenInfo.FieldName("coordinates"),
                        JsonTokenInfo.ArrayStart(),
                            JsonTokenInfo.Number(118.9897),
                            JsonTokenInfo.Number(-46.6781),
                        JsonTokenInfo.ArrayEnd(),
                    JsonTokenInfo.ObjectEnd(),

                    JsonTokenInfo.FieldName("text"),
                    JsonTokenInfo.String("tiger diamond newbrunswick snowleopard chocolate dog snowleopard turtle cat sapphire peach sapphire vancouver white chocolate horse diamond lion superlongcolourname ruby"),
                JsonTokenInfo.ObjectEnd(),
            };

            this.VerifyReader(input, expectedTokens);
            this.VerifyReader(binaryInput, expectedTokens);

            JsonStringDictionary jsonStringDictionary = new JsonStringDictionary(capacity: 100);
            Assert.IsTrue(jsonStringDictionary.TryAddString("double", out int index1));
            Assert.IsTrue(jsonStringDictionary.TryAddString("int", out int index2));
            Assert.IsTrue(jsonStringDictionary.TryAddString("string", out int index3));
            Assert.IsTrue(jsonStringDictionary.TryAddString("boolean", out int index4));
            Assert.IsTrue(jsonStringDictionary.TryAddString("null", out int index5));
            Assert.IsTrue(jsonStringDictionary.TryAddString("datetime", out int index6));
            Assert.IsTrue(jsonStringDictionary.TryAddString("spatialPoint", out int index7));
            Assert.IsTrue(jsonStringDictionary.TryAddString("text", out int index8));
            this.VerifyReader(binaryInputWithEncoding, expectedTokens, jsonStringDictionary);
        }

        [TestMethod]
        [Owner("brchon")]
        public void TrailingGarbageTest()
        {
            string input = "{\"name\":\"477cecf7-5547-4f87-81c2-72ee2c7d6179\",\"permissionMode\":\"Read\",\"resource\":\"-iQET8M3A0c=\"}..garbage..";

            List<byte[]> binaryInputBuilder = new List<byte[]>();
            binaryInputBuilder.Add(new byte[] { BinaryFormat, JsonBinaryEncoding.TypeMarker.Object4ByteLength });

            List<byte[]> elements = new List<byte[]>();
            elements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.String1ByteLength, (byte)"name".Length, 110, 97, 109, 101 });
            elements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.String1ByteLength, (byte)"477cecf7-5547-4f87-81c2-72ee2c7d6179".Length, 52, 55, 55, 99, 101, 99, 102, 55, 45, 53, 53, 52, 55, 45, 52, 102, 56, 55, 45, 56, 49, 99, 50, 45, 55, 50, 101, 101, 50, 99, 55, 100, 54, 49, 55, 57 });

            elements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.String1ByteLength, (byte)"permissionMode".Length, 112, 101, 114, 109, 105, 115, 115, 105, 111, 110, 77, 111, 100, 101 });
            elements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.String1ByteLength, (byte)"Read".Length, 82, 101, 97, 100 });

            elements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.String1ByteLength, (byte)"resource".Length, 114, 101, 115, 111, 117, 114, 99, 101 });
            elements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.String1ByteLength, (byte)"-iQET8M3A0c=".Length, 45, 105, 81, 69, 84, 56, 77, 51, 65, 48, 99, 61 });
            byte[] elementsBytes = elements.SelectMany(x => x).ToArray();

            binaryInputBuilder.Add(BitConverter.GetBytes(elementsBytes.Length));
            binaryInputBuilder.Add(elementsBytes);
            binaryInputBuilder.Add(new byte[] { JsonBinaryEncoding.TypeMarker.String1ByteLength, (byte)"..garbage..".Length, 46, 46, 103, 97, 114, 98, 97, 103, 101, 46, 46 });
            byte[] binaryInput = binaryInputBuilder.SelectMany(x => x).ToArray();

            JsonTokenInfo[] expectedTokens =
            {
                JsonTokenInfo.ObjectStart(),
                JsonTokenInfo.FieldName("name"),
                JsonTokenInfo.String("477cecf7-5547-4f87-81c2-72ee2c7d6179"),
                JsonTokenInfo.FieldName("permissionMode"),
                JsonTokenInfo.String("Read"),
                JsonTokenInfo.FieldName("resource"),
                JsonTokenInfo.String("-iQET8M3A0c="),
                JsonTokenInfo.ObjectEnd(),
            };

            this.VerifyReader(input, expectedTokens, new JsonUnexpectedTokenException());
            this.VerifyReader(binaryInput, expectedTokens, null, new JsonUnexpectedTokenException());
        }

        [TestMethod]
        [Owner("brchon")]
        public void InvalidIntTest()
        {
            string invalidIntString = "{\"type\": 1??? }";

            JsonTokenInfo[] invalidIntStringTokens =
            {
                JsonTokenInfo.ObjectStart(),
                JsonTokenInfo.FieldName("type"),
            };

            this.VerifyReader(invalidIntString, invalidIntStringTokens, new JsonInvalidNumberException());
            // There is no way to test this in a binary reader, since "???" would just convert to a valid binary integer.
        }

        [TestMethod]
        [Owner("brchon")]
        public void InvalidExponentTest()
        {
            string invalidExponent = "{\"type\": 1.0e-??? }";
            string invalidExponent2 = "{\"type\": 1.0E-??? }";

            JsonTokenInfo[] invalidExponentTokens =
            {
                JsonTokenInfo.ObjectStart(),
                JsonTokenInfo.FieldName("type"),
            };

            this.VerifyReader(invalidExponent, invalidExponentTokens, new JsonInvalidNumberException());
            this.VerifyReader(invalidExponent2, invalidExponentTokens, new JsonInvalidNumberException());
            // There is no way to test this in a binary reader, since "1.0e-???" would just convert to a valid binary number.
        }

        [TestMethod]
        [Owner("brchon")]
        public void InvalidExponentTest2()
        {
            string invalidExponent = "{\"type\": 1e+1e1 }";
            string invalidExponent2 = "{\"type\": 1E+1E1 }";

            JsonTokenInfo[] invalidExponentTokens =
            {
                JsonTokenInfo.ObjectStart(),
                JsonTokenInfo.FieldName("type"),
            };

            this.VerifyReader(invalidExponent, invalidExponentTokens, new JsonInvalidNumberException());
            this.VerifyReader(invalidExponent2, invalidExponentTokens, new JsonInvalidNumberException());
            // There is no way to test this in a binary reader, since "1e+1e1" would just convert to a valid binary number.
        }

        [TestMethod]
        [Owner("brchon")]
        public void InvalidNumberTest()
        {
            string input = "{\"type\": 1.e5 }";
            string input2 = "{\"type\": 1.e5 }";

            JsonTokenInfo[] exponentTokens =
            {
                JsonTokenInfo.ObjectStart(),
                JsonTokenInfo.FieldName("type"),
            };

            this.VerifyReader(input, exponentTokens, new JsonInvalidNumberException());
            this.VerifyReader(input2, exponentTokens, new JsonInvalidNumberException());
            // There is no way to test this in a binary reader, since "1.e5" is not possible in binary.
        }

        [TestMethod]
        [Owner("brchon")]
        public void InvalidNumberWithoutExponentTest()
        {
            string input = "{\"type\": 1Garbage }";

            JsonTokenInfo[] exponentTokens =
            {
                JsonTokenInfo.ObjectStart(),
                JsonTokenInfo.FieldName("type"),
            };

            this.VerifyReader(input, exponentTokens, new JsonInvalidNumberException());
            // There is no way to test this in a binary reader, since "1Garbage" is not possible in binary.
        }

        [TestMethod]
        [Owner("brchon")]
        public void MissingClosingQuoteTest()
        {
            string missingQuote = "{\"type\": \"unfinished }";

            JsonTokenInfo[] missingQuoteTokens =
            {
                JsonTokenInfo.ObjectStart(),
                JsonTokenInfo.FieldName("type"),
            };

            this.VerifyReader(missingQuote, missingQuoteTokens, new JsonMissingClosingQuoteException());
            // Binary does not test this.
        }

        [TestMethod]
        [Owner("brchon")]
        public void MissingPropertyTest()
        {
            string input = "[{{";

            JsonTokenInfo[] expectedTokens =
            {
                JsonTokenInfo.ArrayStart(),
                JsonTokenInfo.ObjectStart(),
            };

            this.VerifyReader(input, expectedTokens, new JsonMissingPropertyException());
            // Binary does not test this.
        }

        [TestMethod]
        [Owner("brchon")]
        public void MissingPropertyTest2()
        {
            string input = "{true: false}";

            JsonTokenInfo[] expectedTokens =
            {
                JsonTokenInfo.ObjectStart(),
            };

            this.VerifyReader(input, expectedTokens, new JsonMissingPropertyException());
            // Binary does not test this.
        }

        [TestMethod]
        [Owner("brchon")]
        public void MissingNameSeperatorTest()
        {
            string input = "{\"prop\"\"value\"}";

            JsonTokenInfo[] expectedTokens =
            {
                JsonTokenInfo.ObjectStart(),
                JsonTokenInfo.FieldName("prop"),
            };

            this.VerifyReader(input, expectedTokens, new JsonMissingNameSeparatorException());
            // Binary does not test this.
        }

        [TestMethod]
        [Owner("brchon")]
        public void MissingValueSeperatorTest()
        {
            string input = "[true false]";

            JsonTokenInfo[] expectedTokens =
            {
                JsonTokenInfo.ArrayStart(),
                JsonTokenInfo.Boolean(true),
            };

            this.VerifyReader(input, expectedTokens, new JsonUnexpectedTokenException());
            // Binary does not test this.
        }

        [TestMethod]
        [Owner("brchon")]
        public void UnexpectedNameSeperatorTest()
        {
            string input = "[true: false]";

            JsonTokenInfo[] expectedTokens =
            {
                JsonTokenInfo.ArrayStart(),
                JsonTokenInfo.Boolean(true),
            };

            this.VerifyReader(input, expectedTokens, new JsonUnexpectedNameSeparatorException());
            // Binary does not test this.
        }

        [TestMethod]
        [Owner("brchon")]
        public void UnexpectedEndObjectTest()
        {
            string input = "[true,}";

            JsonTokenInfo[] expectedTokens =
            {
                JsonTokenInfo.ArrayStart(),
                JsonTokenInfo.Boolean(true),
            };

            this.VerifyReader(input, expectedTokens, new JsonUnexpectedEndObjectException());
            // Binary does not test this.
        }

        [TestMethod]
        [Owner("brchon")]
        public void TrailingCommaUnexpectedEndObjectTest()
        {
            string input = "{\"prop\": false, }";

            JsonTokenInfo[] expectedTokens =
            {
                JsonTokenInfo.ObjectStart(),
                JsonTokenInfo.FieldName("prop"),
                JsonTokenInfo.Boolean(false),
            };

            this.VerifyReader(input, expectedTokens, new JsonUnexpectedEndObjectException());
            // Binary does not test this.
        }

        [TestMethod]
        [Owner("brchon")]
        public void UnexpectedEndArrayTest()
        {
            string input = "{\"prop\": false, ]";

            JsonTokenInfo[] expectedTokens =
            {
                JsonTokenInfo.ObjectStart(),
                JsonTokenInfo.FieldName("prop"),
                JsonTokenInfo.Boolean(false),
            };

            this.VerifyReader(input, expectedTokens, new JsonUnexpectedEndArrayException());
            // Binary does not test this.
        }

        [TestMethod]
        [Owner("brchon")]
        public void TrailingCommaUnexpectedEndArrayTest()
        {
            string input = "[true, ]";

            JsonTokenInfo[] expectedTokens =
            {
                JsonTokenInfo.ArrayStart(),
                JsonTokenInfo.Boolean(true),
            };

            this.VerifyReader(input, expectedTokens, new JsonUnexpectedEndArrayException());
            // Binary does not test this.
        }

        [TestMethod]
        [Owner("brchon")]
        public void MissingEndObjectTest()
        {
            string input = "{";

            JsonTokenInfo[] expectedTokens =
            {
                JsonTokenInfo.ObjectStart()
            };

            this.VerifyReader(input, expectedTokens, new JsonMissingEndObjectException());
            // Binary does not test this.
        }

        [TestMethod]
        [Owner("brchon")]
        public void MissingEndArrayTest()
        {
            string input = "[";

            JsonTokenInfo[] expectedTokens =
            {
                JsonTokenInfo.ArrayStart()
            };

            this.VerifyReader(input, expectedTokens, new JsonMissingEndArrayException());
            // Binary does not test this.
        }

        [TestMethod]
        [Owner("brchon")]
        public void InvalidEscapeCharacterTest()
        {
            JsonTokenInfo[] expectedTokens =
            {
            };

            this.VerifyReader("\"\\p\"", expectedTokens, new JsonInvalidEscapedCharacterException());
            this.VerifyReader("\"\\\\,\\.\"", expectedTokens, new JsonInvalidEscapedCharacterException());
            this.VerifyReader("\"\\\xC2\xA2\"", expectedTokens, new JsonInvalidEscapedCharacterException());
            // Binary does not test this.
        }
        #endregion

        private void VerifyReader(string input, JsonTokenInfo[] expectedTokens)
        {
            this.VerifyReader(input, expectedTokens, null);
        }

        /// <summary>
        /// Tries to read with the text reader using all the supported encodings.
        /// </summary>
        private void VerifyReader(string input, JsonTokenInfo[] expectedTokens, Exception expectedException)
        {
            byte[] utf8ByteArray = Encoding.UTF8.GetBytes(input);
            // Test readers created with the array API
            this.VerifyReader(
                () => JsonReader.Create(utf8ByteArray), 
                expectedTokens, 
                expectedException, 
                Encoding.UTF8);

            // Test readers create from the stream API (without buffering).
            this.VerifyReader(
                () => JsonReader.Create(new MemoryStream(utf8ByteArray)), 
                expectedTokens, 
                expectedException, 
                Encoding.UTF8);

            //// TODO: have a test where you are reading from a file and over the network.

            // Test readers created with the encoding API
            Encoding[] encodings = 
            {
                Encoding.UTF8,
                Encoding.Unicode,
                Encoding.UTF32,
            };

            foreach (Encoding encoding in encodings)
            {
                byte[] encodedBytes = encoding.GetBytes(input);

                this.VerifyReader(
                    () => JsonReader.CreateTextReaderWithEncoding(new MemoryStream(encodedBytes), encoding), 
                    expectedTokens, 
                    expectedException, 
                    encoding);
            }
        }

        private void VerifyReader(byte[] input, JsonTokenInfo[] expectedTokens, JsonStringDictionary jsonStringDictionary = null, Exception expectedException = null)
        {
            // Test binary reader created with the array API
            this.VerifyReader(() => JsonReader.Create(input, jsonStringDictionary), expectedTokens, expectedException, Encoding.UTF8);

            // Test binary reader created with the stream API
            this.VerifyReader(() => JsonReader.Create(new MemoryStream(input), jsonStringDictionary), expectedTokens, expectedException, Encoding.UTF8);

            //// TODO: have a test where you are reading from a file and over the network.
        }

        /// <summary>
        /// Verifies the reader by constructing a JsonReader from the memorystream with the specified encoding and then reads tokens from to see if they match the expected tokens. If there is an exception provided it also tries to read until it hits that exception.
        /// </summary>
        private void VerifyReader(Func<IJsonReader> createJsonReader, JsonTokenInfo[] expectedTokens, Exception expectedException, Encoding encoding)
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

                    foreach (JsonTokenInfo expectedToken in expectedTokens)
                    {
                        jsonReader.Read();

                        switch (expectedToken.JsonTokenType)
                        {
                            case JsonTokenType.BeginArray:
                                this.VerifyBeginArray(jsonReader, encoding);
                                break;
                            case JsonTokenType.EndArray:
                                this.VerifyEndArray(jsonReader, encoding);
                                break;
                            case JsonTokenType.BeginObject:
                                this.VerifyBeginObject(jsonReader, encoding);
                                break;
                            case JsonTokenType.EndObject:
                                this.VerifyEndObject(jsonReader, encoding);
                                break;
                            case JsonTokenType.String:
                                this.VerifyString(jsonReader, expectedToken.BufferedToken, encoding);
                                break;
                            case JsonTokenType.Number:
                                this.VerifyNumber(jsonReader, expectedToken.Value, encoding);
                                break;
                            case JsonTokenType.True:
                                this.VerifyTrue(jsonReader, encoding);
                                break;
                            case JsonTokenType.False:
                                this.VerifyFalse(jsonReader, encoding);
                                break;
                            case JsonTokenType.Null:
                                this.VerifyNull(jsonReader, encoding);
                                break;
                            case JsonTokenType.FieldName:
                                this.VerifyFieldName(jsonReader, expectedToken.BufferedToken, encoding);
                                break;
                            case JsonTokenType.NotStarted:
                            default:
                                Assert.Fail(string.Format("Got an unexpected JsonTokenType: {0} as an expected token type", expectedToken.JsonTokenType));
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

        private void VerifyBeginArray(IJsonReader jsonReader, Encoding encoding)
        {
            this.VerifyToken(jsonReader, JsonTokenType.BeginArray, "[", encoding);
        }

        private void VerifyEndArray(IJsonReader jsonReader, Encoding encoding)
        {
            this.VerifyToken(jsonReader, JsonTokenType.EndArray, "]", encoding);
        }

        private void VerifyBeginObject(IJsonReader jsonReader, Encoding encoding)
        {
            this.VerifyToken(jsonReader, JsonTokenType.BeginObject, "{", encoding);
        }

        private void VerifyEndObject(IJsonReader jsonReader, Encoding encoding)
        {
            this.VerifyToken(jsonReader, JsonTokenType.EndObject, "}", encoding);
        }

        private void VerifyString(IJsonReader jsonReader, IReadOnlyList<byte> expectedBufferedToken, Encoding encoding)
        {
            JsonTokenType jsonTokenType = jsonReader.CurrentTokenType;
            Assert.AreEqual(JsonTokenType.String, jsonTokenType);

            string stringValue = jsonReader.GetStringValue();
            string expectedBufferedTokenString = Encoding.Unicode.GetString(expectedBufferedToken.ToArray());
            string expectedString = expectedBufferedTokenString.Substring(1, expectedBufferedTokenString.Length - 2);
            Assert.AreEqual(expectedString, stringValue);

            //Additionally check if the text is correct
            if (jsonReader.SerializationFormat == JsonSerializationFormat.Text)
            {
                IReadOnlyList<byte> bufferedRawJsonToken = jsonReader.GetBufferedRawJsonToken();
                string bufferdRawJsonTokenString = encoding.GetString(bufferedRawJsonToken.ToArray());
                const char DoubleQuote = '"';

                int literalStringLength = 2 + expectedString.Length;
                Assert.IsTrue(bufferedRawJsonToken.Count >= literalStringLength);

                Assert.AreEqual(DoubleQuote, bufferdRawJsonTokenString[0]);
                Assert.AreEqual(DoubleQuote, bufferdRawJsonTokenString[bufferdRawJsonTokenString.Length - 1]);

                string escapedString = bufferdRawJsonTokenString.Substring(1, bufferdRawJsonTokenString.Length - 2);
                string stringValueFromBuffer = Regex.Unescape(escapedString);

                Assert.AreEqual(expectedString, stringValueFromBuffer);
            }
        }

        private void VerifyNumber(IJsonReader jsonReader, double expectedNumberValue, Encoding encoding)
        {
            JsonTokenType jsonTokenType = jsonReader.CurrentTokenType;
            Assert.AreEqual(JsonTokenType.Number, jsonTokenType);

            double value = jsonReader.GetNumberValue();

            // Do we need to worry about finite precision here?
            Assert.AreEqual(expectedNumberValue, value);

            //Additionally check if the text is correct
            if (jsonReader.SerializationFormat == JsonSerializationFormat.Text)
            {
                IReadOnlyList<byte> bufferedRawJsonToken = jsonReader.GetBufferedRawJsonToken();
                string stringRawJsonToken = encoding.GetString(bufferedRawJsonToken.ToArray());

                double valueFromString = double.Parse(stringRawJsonToken, CultureInfo.InvariantCulture);
                Assert.AreEqual(expectedNumberValue, valueFromString);
            }
        }

        private void VerifyTrue(IJsonReader jsonReader, Encoding encoding)
        {
            this.VerifyToken(jsonReader, JsonTokenType.True, "true", encoding);
        }

        private void VerifyFalse(IJsonReader jsonReader, Encoding encoding)
        {
            this.VerifyToken(jsonReader, JsonTokenType.False, "false", encoding);
        }

        private void VerifyNull(IJsonReader jsonReader, Encoding encoding)
        {
            this.VerifyToken(jsonReader, JsonTokenType.Null, "null", encoding);
        }

        private void VerifyFieldName(IJsonReader jsonReader, IReadOnlyList<byte> expectedFieldName, Encoding encoding)
        {
            JsonTokenType jsonTokenType = jsonReader.CurrentTokenType;
            Assert.AreEqual(JsonTokenType.FieldName, jsonTokenType);

            string fieldName = jsonReader.GetStringValue();
            string expectedFieldNameString = Encoding.Unicode.GetString(expectedFieldName.ToArray());
            string expectedFieldNameStringNoQoutes = expectedFieldNameString.Substring(1, expectedFieldNameString.Length - 2);
            Assert.AreEqual(expectedFieldNameStringNoQoutes, fieldName);

            //Additionally check if the text is correct
            if (jsonReader.SerializationFormat == JsonSerializationFormat.Text)
            {
                IReadOnlyList<byte> bufferedRawJsonToken = jsonReader.GetBufferedRawJsonToken();
                const char DoubleQuote = '"';
                int literalStringLength = expectedFieldNameString.Length;
                Assert.IsTrue(bufferedRawJsonToken.Count >= literalStringLength);

                string bufferedRawJsonTokenString = encoding.GetString(bufferedRawJsonToken.ToArray());
                Assert.AreEqual(DoubleQuote, bufferedRawJsonTokenString[0]);
                Assert.AreEqual(DoubleQuote, bufferedRawJsonTokenString[bufferedRawJsonTokenString.Length - 1]);

                string escapedFieldName = bufferedRawJsonTokenString.Substring(1, bufferedRawJsonTokenString.Length - 2);
                string fieldNameFromBuffer = Regex.Unescape(escapedFieldName);

                Assert.AreEqual(fieldNameFromBuffer, fieldName);
            }
        }

        private void VerifyToken(IJsonReader jsonReader, JsonTokenType expectedJsonTokenType, string fragmentString, Encoding encoding)
        {
            JsonTokenType jsonTokenType = jsonReader.CurrentTokenType;

            Assert.AreEqual(expectedJsonTokenType, jsonTokenType);

            if (jsonReader.SerializationFormat == JsonSerializationFormat.Text)
            {
                this.VerifyFragment(jsonReader, fragmentString, encoding);
            }
        }

        private void VerifyFragment(IJsonReader jsonReader, string fragment, Encoding encoding)
        {
            IReadOnlyList<byte> bufferedRawJsonToken = jsonReader.GetBufferedRawJsonToken();
            string stringRawJsonToken = encoding.GetString(bufferedRawJsonToken.ToArray());
            Assert.AreEqual(fragment, stringRawJsonToken);
        }
    }
}