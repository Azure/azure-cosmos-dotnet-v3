namespace Microsoft.Azure.Cosmos.NetFramework.Tests.Json
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System.Reflection;
    using System.Globalization;

    [TestClass]
    public class JsonWriterTests
    {
        private const byte BinaryFormat = 128;

        [TestInitialize]
        public void TestInitialize()
        {
            // Put test init code here
        }

        [ClassInitialize]
        public static void Initialize(TestContext textContext)
        {
            // put class init code here
        }

        #region Literals
        [TestMethod]
        [Owner("brchon")]
        public void TrueTest()
        {
            string expectedString = "true";
            byte[] binaryOutput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.True
            };

            JsonTokenInfo[] tokensToWrite =
            {
                JsonTokenInfo.Boolean(true)
            };

            this.VerifyWriter(tokensToWrite, expectedString);
            this.VerifyWriter(tokensToWrite, binaryOutput);
            this.VerifyWriter(tokensToWrite, binaryOutput, new JsonStringDictionary(capacity: 100));
        }

        [TestMethod]
        [Owner("brchon")]
        public void FalseTest()
        {
            string expectedString = "false";
            byte[] binaryOutput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.False
            };

            JsonTokenInfo[] tokensToWrite =
            {
                JsonTokenInfo.Boolean(false)
            };

            this.VerifyWriter(tokensToWrite, expectedString);
            this.VerifyWriter(tokensToWrite, binaryOutput);
            this.VerifyWriter(tokensToWrite, binaryOutput, new JsonStringDictionary(capacity: 100));
        }

        [TestMethod]
        [Owner("brchon")]
        public void NullTest()
        {
            string expectedString = "null";
            byte[] binaryOutput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.Null
            };

            JsonTokenInfo[] tokensToWrite =
            {
                JsonTokenInfo.Null()
            };

            this.VerifyWriter(tokensToWrite, expectedString);
            this.VerifyWriter(tokensToWrite, binaryOutput);
            this.VerifyWriter(tokensToWrite, binaryOutput, new JsonStringDictionary(capacity: 100));
        }
        #endregion
        #region Numbers
        [TestMethod]
        [Owner("brchon")]
        public void IntegerTest()
        {
            string expectedString = "1337";
            byte[] binaryOutput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.Int16,
                // 1337 in litte endian hex,
                0x39, 0x05,
            };

            JsonTokenInfo[] tokensToWrite =
            {
                JsonTokenInfo.Number(1337)
            };

            this.VerifyWriter(tokensToWrite, expectedString);
            this.VerifyWriter(tokensToWrite, binaryOutput);
            this.VerifyWriter(tokensToWrite, binaryOutput, new JsonStringDictionary(capacity: 100));
        }

        [TestMethod]
        [Owner("brchon")]
        public void DoubleTest()
        {
            string expectedString = "1337.1337";
            byte[] binaryOutput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.Double,
                // 1337.1337 in litte endian hex for a double
                0xE7, 0x1D, 0xA7, 0xE8, 0x88, 0xE4, 0x94, 0x40,
            };

            JsonTokenInfo[] tokensToWrite =
            {
                JsonTokenInfo.Number(1337.1337)
            };

            this.VerifyWriter(tokensToWrite, expectedString);
            this.VerifyWriter(tokensToWrite, binaryOutput);
            this.VerifyWriter(tokensToWrite, binaryOutput, new JsonStringDictionary(capacity: 100));
        }

        [TestMethod]
        [Owner("brchon")]
        public void NaNTest()
        {
            string expectedString = "\"NaN\"";
            byte[] binaryOutput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.Double,
                // NaN in litte endian hex for a double
                0, 0, 0, 0, 0, 0, 248, 255
            };

            JsonTokenInfo[] tokensToWrite =
            {
                JsonTokenInfo.Number(double.NaN)
            };

            this.VerifyWriter(tokensToWrite, expectedString);
            this.VerifyWriter(tokensToWrite, binaryOutput);
            this.VerifyWriter(tokensToWrite, binaryOutput, new JsonStringDictionary(capacity: 100));
        }

        [TestMethod]
        [Owner("brchon")]
        public void PositiveInfinityTest()
        {
            string expectedString = "\"Infinity\"";
            byte[] binaryOutput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.Double,
                // Infinity in litte endian hex for a double
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xF0, 0x7F,
            };

            JsonTokenInfo[] tokensToWrite =
            {
                JsonTokenInfo.Number(double.PositiveInfinity)
            };

            this.VerifyWriter(tokensToWrite, expectedString);
            this.VerifyWriter(tokensToWrite, binaryOutput);
            this.VerifyWriter(tokensToWrite, binaryOutput, new JsonStringDictionary(capacity: 100));
        }

        [TestMethod]
        [Owner("brchon")]
        public void NegativeInfinityTest()
        {
            string expectedString = "\"-Infinity\"";
            byte[] binaryOutput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.Double,
                // Infinity in litte endian hex for a double
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xF0, 0xFF,
            };

            JsonTokenInfo[] tokensToWrite =
            {
                JsonTokenInfo.Number(double.NegativeInfinity)
            };

            this.VerifyWriter(tokensToWrite, expectedString);
            this.VerifyWriter(tokensToWrite, binaryOutput);
            this.VerifyWriter(tokensToWrite, binaryOutput, new JsonStringDictionary(capacity: 100));
        }

        [TestMethod]
        [Owner("brchon")]
        public void NegativeNumberTest()
        {
            string expectedString = "-1337.1337";
            byte[] binaryOutput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.Double,
                // Infinity in litte endian hex for a double
                0xE7, 0x1D, 0xA7, 0xE8, 0x88, 0xE4, 0x94, 0xC0,
            };

            JsonTokenInfo[] tokensToWrite =
            {
                JsonTokenInfo.Number(-1337.1337)
            };

            this.VerifyWriter(tokensToWrite, expectedString);
            this.VerifyWriter(tokensToWrite, binaryOutput);
            this.VerifyWriter(tokensToWrite, binaryOutput, new JsonStringDictionary(capacity: 100));
        }

        [TestMethod]
        [Owner("brchon")]
        public void NumberWithScientificNotationTest()
        {
            string expectedString = "6.02252E+23";
            byte[] binaryOutput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.Double,
                // 6.02252e23 in litte endian hex for a double
                0x93, 0x09, 0x9F, 0x5D, 0x09, 0xE2, 0xDF, 0x44
            };

            JsonTokenInfo[] tokensToWrite =
            {
                JsonTokenInfo.Number(6.02252e23)
            };

            this.VerifyWriter(tokensToWrite, expectedString);
            this.VerifyWriter(tokensToWrite, binaryOutput);
            this.VerifyWriter(tokensToWrite, binaryOutput, new JsonStringDictionary(capacity: 100));
        }

        [TestMethod]
        [Owner("brchon")]
        public void NumberRegressionTest()
        {
            // regression test - the value 0.00085647800000000004 was being incorrectly rejected

            // This is the number truncated to fit within a double.
            string numberValueString = "0.000856478";
            byte[] binaryOutput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.Double,
                // 0.00085647800000000004 in litte endian hex for a double
                0x39, 0x98, 0xF7, 0x7F, 0xA8, 0x10, 0x4C, 0x3F
            };

            JsonTokenInfo[] tokensToWrite =
            {
                JsonTokenInfo.Number(0.00085647800000000004)
            };

            this.VerifyWriter(tokensToWrite, numberValueString);
            this.VerifyWriter(tokensToWrite, binaryOutput);
            this.VerifyWriter(tokensToWrite, binaryOutput, new JsonStringDictionary(capacity: 100));
        }

        [TestMethod]
        [Owner("brchon")]
        public void NumberPrecisionTest()
        {
            string expectedString = "[2.7620553993338772e+018,2.7620553993338778e+018]";
            List<byte> binaryOutputBuilder = new List<byte>();
            binaryOutputBuilder.Add(BinaryFormat);
            binaryOutputBuilder.Add(JsonBinaryEncoding.TypeMarker.Array1ByteLength);
            binaryOutputBuilder.Add(sizeof(byte) + sizeof(double) + sizeof(byte) + sizeof(double));
            binaryOutputBuilder.Add(JsonBinaryEncoding.TypeMarker.Double);
            binaryOutputBuilder.AddRange(BitConverter.GetBytes(2.7620553993338772e+018));
            binaryOutputBuilder.Add(JsonBinaryEncoding.TypeMarker.Double);
            binaryOutputBuilder.AddRange(BitConverter.GetBytes(2.7620553993338778e+018));
            byte[] binaryOutput = binaryOutputBuilder.ToArray();

            JsonTokenInfo[] tokensToWrite =
            {
                JsonTokenInfo.ArrayStart(),
                JsonTokenInfo.Number(2.7620553993338772e+018),
                JsonTokenInfo.Number(2.7620553993338778e+018),
                JsonTokenInfo.ArrayEnd(),
            };

            this.VerifyWriter(tokensToWrite, expectedString);
            this.VerifyWriter(tokensToWrite, binaryOutput);
            this.VerifyWriter(tokensToWrite, binaryOutput, new JsonStringDictionary(capacity: 100));
        }

        [TestMethod]
        [Owner("brchon")]
        public void LargeNumbersTest()
        {
            string expectedString = @"
            [1,-1,10,-10,1.49974603574112E+16,1.4997460357411E+16,1499746035741101,1499746035741109,-1.49974603574112E+16,-1.4997460357411E+16,-1499746035741101,-1499746035741109,1499746035741128,1499752659822592,1499752939110661,1499753827614475,1499970126403840,1499970590815128,1499970842400644,1499971371510025,1499972760675685,1499972969962006,1499973086735836,1499973302072392,1499976826748983]";
            JsonTokenInfo[] tokensToWrite =
            {
                JsonTokenInfo.ArrayStart(),
                JsonTokenInfo.Number(1),
                JsonTokenInfo.Number(-1),
                JsonTokenInfo.Number(10),
                JsonTokenInfo.Number(-10),
                JsonTokenInfo.Number(14997460357411200),
                JsonTokenInfo.Number(14997460357411000),
                JsonTokenInfo.Number(1499746035741101),
                JsonTokenInfo.Number(1499746035741109),
                JsonTokenInfo.Number(-14997460357411200),
                JsonTokenInfo.Number(-14997460357411000),
                JsonTokenInfo.Number(-1499746035741101),
                JsonTokenInfo.Number(-1499746035741109),
                JsonTokenInfo.Number(1499746035741128),
                JsonTokenInfo.Number(1499752659822592),
                JsonTokenInfo.Number(1499752939110661),
                JsonTokenInfo.Number(1499753827614475),
                JsonTokenInfo.Number(1499970126403840),
                JsonTokenInfo.Number(1499970590815128),
                JsonTokenInfo.Number(1499970842400644),
                JsonTokenInfo.Number(1499971371510025),
                JsonTokenInfo.Number(1499972760675685),
                JsonTokenInfo.Number(1499972969962006),
                JsonTokenInfo.Number(1499973086735836),
                JsonTokenInfo.Number(1499973302072392),
                JsonTokenInfo.Number(1499976826748983),
                JsonTokenInfo.ArrayEnd(),
            };

            this.VerifyWriter(tokensToWrite, expectedString);
        }
        #endregion
        [TestMethod]
        [Owner("brchon")]
        public void EmptyStringTest()
        {
            string expectedString = "\"\"";
            byte[] binaryOutput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin,
            };

            JsonTokenInfo[] tokensToWrite =
            {
                JsonTokenInfo.String(string.Empty)
            };

            this.VerifyWriter(tokensToWrite, expectedString);
            this.VerifyWriter(tokensToWrite, binaryOutput);
            this.VerifyWriter(tokensToWrite, binaryOutput, new JsonStringDictionary(capacity: 100));
        }

        #region String
        [TestMethod]
        [Owner("brchon")]
        public void StringTest()
        {
            string expectedString = "\"Hello World\"";
            byte[] binaryOutput =
            {
                BinaryFormat,
                (byte)(JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + "Hello World".Length),
                // Hello World as a utf8 string
                72, 101, 108, 108, 111, 32, 87, 111, 114, 108, 100
            };

            JsonTokenInfo[] tokensToWrite =
            {
                JsonTokenInfo.String("Hello World")
            };

            this.VerifyWriter(tokensToWrite, expectedString);
            this.VerifyWriter(tokensToWrite, binaryOutput);
            this.VerifyWriter(tokensToWrite, binaryOutput, new JsonStringDictionary(capacity: 100));
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
                string expectedString = "\"" + systemString + "\"";
                byte[] binaryOutput =
                {
                    BinaryFormat,
                    (byte)(JsonBinaryEncoding.TypeMarker.SystemString1ByteLengthMin + ((int)systemStringId)),
                };

                JsonTokenInfo[] tokensToWrite =
                {
                    JsonTokenInfo.String(systemString)
                };

                this.VerifyWriter(tokensToWrite, expectedString);
                this.VerifyWriter(tokensToWrite, binaryOutput);
                this.VerifyWriter(tokensToWrite, binaryOutput, new JsonStringDictionary(capacity: 100));
                systemStringId++;
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public void UserStringTest()
        {
            // Object with 33 field names. This creates a user string with 2 byte type marker.

            List<JsonTokenInfo> tokensToWrite = new List<JsonTokenInfo>() { JsonTokenInfo.ObjectStart() };
            StringBuilder textOutput = new StringBuilder("{");
            List<byte> binaryOutput = new List<byte>() { BinaryFormat, JsonBinaryEncoding.TypeMarker.Object1ByteLength, };
            List<byte> binaryOutputWithEncoding = new List<byte>() { BinaryFormat, JsonBinaryEncoding.TypeMarker.Object1ByteLength };

            const byte OneByteCount = JsonBinaryEncoding.TypeMarker.UserString1ByteLengthMax - JsonBinaryEncoding.TypeMarker.UserString1ByteLengthMin;
            for (int i = 0; i < OneByteCount + 1; i++)
            {
                string userEncodedString = "a" + i.ToString();

                tokensToWrite.Add(JsonTokenInfo.FieldName(userEncodedString));
                tokensToWrite.Add(JsonTokenInfo.String(userEncodedString));

                if (i > 0)
                {
                    textOutput.Append(",");
                }

                textOutput.Append($@"""{userEncodedString}"":""{userEncodedString}""");

                for (int j = 0; j < 2; j++)
                {
                    binaryOutput.Add((byte)(JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + userEncodedString.Length));
                    binaryOutput.AddRange(Encoding.UTF8.GetBytes(userEncodedString));
                }

                if (i < OneByteCount)
                {
                    binaryOutputWithEncoding.Add((byte)(JsonBinaryEncoding.TypeMarker.UserString1ByteLengthMin + i));
                }
                else
                {
                    int twoByteOffset = i - OneByteCount;
                    binaryOutputWithEncoding.Add((byte)((twoByteOffset / 0xFF) + JsonBinaryEncoding.TypeMarker.UserString2ByteLengthMin));
                    binaryOutputWithEncoding.Add((byte)(twoByteOffset % 0xFF));
                }

                binaryOutputWithEncoding.Add((byte)(JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + userEncodedString.Length));
                binaryOutputWithEncoding.AddRange(Encoding.UTF8.GetBytes(userEncodedString));
            }

            tokensToWrite.Add(JsonTokenInfo.ObjectEnd());
            textOutput.Append("}");
            binaryOutput.Insert(2, (byte)(binaryOutput.Count() - 2));
            binaryOutputWithEncoding.Insert(2, (byte)(binaryOutputWithEncoding.Count() - 2));

            this.VerifyWriter(tokensToWrite.ToArray(), textOutput.ToString());
            this.VerifyWriter(tokensToWrite.ToArray(), binaryOutput.ToArray());

            JsonStringDictionary jsonStringDictionary = new JsonStringDictionary(capacity: 100);
            this.VerifyWriter(tokensToWrite.ToArray(), binaryOutputWithEncoding.ToArray(), jsonStringDictionary);

            for (int i = 0; i < OneByteCount + 1; i++)
            {
                string userEncodedString = "a" + i.ToString();
                Assert.IsTrue(jsonStringDictionary.TryGetStringAtIndex(i, out string value));
                Assert.AreEqual(userEncodedString, value);
            }
        }
        #endregion
        #region Array
        [TestMethod]
        [Owner("brchon")]
        public void EmptyArrayTest()
        {
            string expectedString = "[  ]  ";
            byte[] binaryOutput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.EmptyArray
            };

            JsonTokenInfo[] tokensToWrite =
            {
                JsonTokenInfo.ArrayStart(),
                JsonTokenInfo.ArrayEnd(),
            };

            this.VerifyWriter(tokensToWrite, expectedString);
            this.VerifyWriter(tokensToWrite, binaryOutput);
        }

        [TestMethod]
        [Owner("brchon")]
        public void SingleItemArrayTest()
        {
            string expectedString = "[ true ]  ";
            byte[] binaryOutput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.SingleItemArray,
                JsonBinaryEncoding.TypeMarker.True
            };

            JsonTokenInfo[] tokensToWrite =
            {
                JsonTokenInfo.ArrayStart(),
                JsonTokenInfo.Boolean(true),
                JsonTokenInfo.ArrayEnd(),
            };

            this.VerifyWriter(tokensToWrite, expectedString);
            this.VerifyWriter(tokensToWrite, binaryOutput);
        }

        [TestMethod]
        [Owner("brchon")]
        public void IntArrayTest()
        {
            string expectedString = "[ -2, -1, 0, 1, 2]  ";
            List<byte[]> binaryOutputBuilder = new List<byte[]>();
            binaryOutputBuilder.Add(new byte[] { BinaryFormat, JsonBinaryEncoding.TypeMarker.Array1ByteLength });

            List<byte[]> numbers = new List<byte[]>();
            numbers.Add(new byte[] { JsonBinaryEncoding.TypeMarker.Int16, 0xFE, 0xFF });
            numbers.Add(new byte[] { JsonBinaryEncoding.TypeMarker.Int16, 0xFF, 0xFF });
            numbers.Add(new byte[] { JsonBinaryEncoding.TypeMarker.LiteralIntMin });
            numbers.Add(new byte[] { JsonBinaryEncoding.TypeMarker.LiteralIntMin + 1 });
            numbers.Add(new byte[] { JsonBinaryEncoding.TypeMarker.LiteralIntMin + 2 });
            byte[] numbersBytes = numbers.SelectMany(x => x).ToArray();

            binaryOutputBuilder.Add(new byte[] { (byte)numbersBytes.Length });
            binaryOutputBuilder.Add(numbersBytes);
            byte[] binaryOutput = binaryOutputBuilder.SelectMany(x => x).ToArray();

            JsonTokenInfo[] tokensToWrite =
            {
                JsonTokenInfo.ArrayStart(),
                JsonTokenInfo.Number(-2),
                JsonTokenInfo.Number(-1),
                JsonTokenInfo.Number(0),
                JsonTokenInfo.Number(1),
                JsonTokenInfo.Number(2),
                JsonTokenInfo.ArrayEnd(),
            };

            this.VerifyWriter(tokensToWrite, expectedString);
            this.VerifyWriter(tokensToWrite, binaryOutput);
        }

        [TestMethod]
        [Owner("brchon")]
        public void NumberArrayTest()
        {
            string expectedString = "[15,  22, 0.1, -0.073, 7.70001E+91 ]  ";
            List<byte[]> binaryOutputBuilder = new List<byte[]>();
            binaryOutputBuilder.Add(new byte[] { BinaryFormat, JsonBinaryEncoding.TypeMarker.Array1ByteLength });

            List<byte[]> numbers = new List<byte[]>();
            numbers.Add(new byte[] { JsonBinaryEncoding.TypeMarker.LiteralIntMin + 15 });
            numbers.Add(new byte[] { JsonBinaryEncoding.TypeMarker.LiteralIntMin + 22 });
            numbers.Add(new byte[] { JsonBinaryEncoding.TypeMarker.Double, 0x9A, 0x99, 0x99, 0x99, 0x99, 0x99, 0xB9, 0x3F });
            numbers.Add(new byte[] { JsonBinaryEncoding.TypeMarker.Double, 0xE3, 0xA5, 0x9B, 0xC4, 0x20, 0xB0, 0xB2, 0xBF });
            numbers.Add(new byte[] { JsonBinaryEncoding.TypeMarker.Double, 0xBE, 0xDA, 0x50, 0xA7, 0x68, 0xE6, 0x02, 0x53 });
            byte[] numbersBytes = numbers.SelectMany(x => x).ToArray();

            binaryOutputBuilder.Add(new byte[] { (byte)numbersBytes.Length });
            binaryOutputBuilder.Add(numbersBytes);
            byte[] binaryOutput = binaryOutputBuilder.SelectMany(x => x).ToArray();

            JsonTokenInfo[] tokensToWrite =
            {
                JsonTokenInfo.ArrayStart(),
                JsonTokenInfo.Number(15),
                JsonTokenInfo.Number(22),
                JsonTokenInfo.Number(0.1),
                JsonTokenInfo.Number(-7.3e-2),
                JsonTokenInfo.Number(77.0001e90),
                JsonTokenInfo.ArrayEnd(),
            };

            this.VerifyWriter(tokensToWrite, expectedString);
            this.VerifyWriter(tokensToWrite, binaryOutput);
        }

        [TestMethod]
        [Owner("brchon")]
        public void BooleanArrayTest()
        {
            string expectedString = "[ true, false]  ";
            byte[] binaryOutput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.Array1ByteLength,
                // length
                2,
                JsonBinaryEncoding.TypeMarker.True,
                JsonBinaryEncoding.TypeMarker.False,
            };

            JsonTokenInfo[] tokensToWrite =
            {
                JsonTokenInfo.ArrayStart(),
                JsonTokenInfo.Boolean(true),
                JsonTokenInfo.Boolean(false),
                JsonTokenInfo.ArrayEnd(),
            };

            this.VerifyWriter(tokensToWrite, expectedString);
            this.VerifyWriter(tokensToWrite, binaryOutput);
        }

        [TestMethod]
        [Owner("brchon")]
        public void StringArrayTest()
        {
            string expectedString = @"[""Hello"", ""World"", ""Bye""]";

            List<byte[]> binaryOutputBuilder = new List<byte[]>();
            binaryOutputBuilder.Add(new byte[] { BinaryFormat, JsonBinaryEncoding.TypeMarker.Array1ByteLength });

            List<byte[]> strings = new List<byte[]>();
            strings.Add(new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + "Hello".Length) });
            strings.Add(Encoding.UTF8.GetBytes("Hello"));
            strings.Add(new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + "World".Length) });
            strings.Add(Encoding.UTF8.GetBytes("World"));
            strings.Add(new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + "Bye".Length) });
            strings.Add(Encoding.UTF8.GetBytes("Bye"));
            byte[] stringBytes = strings.SelectMany(x => x).ToArray();

            binaryOutputBuilder.Add(new byte[] { (byte)stringBytes.Length });
            binaryOutputBuilder.Add(stringBytes);
            byte[] binaryOutput = binaryOutputBuilder.SelectMany(x => x).ToArray();

            JsonTokenInfo[] tokensToWrite =
            {
                JsonTokenInfo.ArrayStart(),
                JsonTokenInfo.String("Hello"),
                JsonTokenInfo.String("World"),
                JsonTokenInfo.String("Bye"),
                JsonTokenInfo.ArrayEnd(),
            };

            this.VerifyWriter(tokensToWrite, expectedString);
            this.VerifyWriter(tokensToWrite, binaryOutput);
        }

        [TestMethod]
        [Owner("brchon")]
        public void NullArrayTest()
        {
            string expectedString = "[ null, null, null]  ";
            byte[] binaryOutput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.Array1ByteLength,
                // length
                3,
                JsonBinaryEncoding.TypeMarker.Null,
                JsonBinaryEncoding.TypeMarker.Null,
                JsonBinaryEncoding.TypeMarker.Null,
            };

            JsonTokenInfo[] tokensToWrite =
            {
                JsonTokenInfo.ArrayStart(),
                JsonTokenInfo.Null(),
                JsonTokenInfo.Null(),
                JsonTokenInfo.Null(),
                JsonTokenInfo.ArrayEnd(),
            };

            this.VerifyWriter(tokensToWrite, expectedString);
            this.VerifyWriter(tokensToWrite, binaryOutput);
        }

        [TestMethod]
        [Owner("brchon")]
        public void ObjectArrayTest()
        {
            string expectedString = "[{}, {}]  ";
            byte[] binaryOutput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.Array1ByteLength,
                // length
                2,
                JsonBinaryEncoding.TypeMarker.EmptyObject,
                JsonBinaryEncoding.TypeMarker.EmptyObject,
            };

            JsonTokenInfo[] tokensToWrite =
            {
                JsonTokenInfo.ArrayStart(),
                JsonTokenInfo.ObjectStart(),
                JsonTokenInfo.ObjectEnd(),
                JsonTokenInfo.ObjectStart(),
                JsonTokenInfo.ObjectEnd(),
                JsonTokenInfo.ArrayEnd(),
            };

            this.VerifyWriter(tokensToWrite, expectedString);
            this.VerifyWriter(tokensToWrite, binaryOutput);
        }

        [TestMethod]
        [Owner("brchon")]
        public void AllPrimitiveArrayTest()
        {
            string expectedString = "[0, 0, -1, -1.1, 1, 2, \"hello\", null, true, false]  ";
            List<byte[]> binaryOutputBuilder = new List<byte[]>();
            binaryOutputBuilder.Add(new byte[] { BinaryFormat, JsonBinaryEncoding.TypeMarker.Array1ByteLength });

            List<byte[]> elements = new List<byte[]>();
            elements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.LiteralIntMin });
            elements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.LiteralIntMin });
            elements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.Int16, 0xFF, 0xFF });
            elements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.Double, 0x9A, 0x99, 0x99, 0x99, 0x99, 0x99, 0xF1, 0xBF });
            elements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.LiteralIntMin + 1 });
            elements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.LiteralIntMin + 2 });
            elements.Add(new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + "hello".Length), 104, 101, 108, 108, 111 });
            elements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.Null });
            elements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.True });
            elements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.False });
            byte[] elementsBytes = elements.SelectMany(x => x).ToArray();

            binaryOutputBuilder.Add(new byte[] { (byte)elementsBytes.Length });
            binaryOutputBuilder.Add(elementsBytes);
            byte[] binaryOutput = binaryOutputBuilder.SelectMany(x => x).ToArray();

            JsonTokenInfo[] tokensToWrite =
            {
                JsonTokenInfo.ArrayStart(),
                JsonTokenInfo.Number(0),
                JsonTokenInfo.Number(0.0),
                JsonTokenInfo.Number(-1),
                JsonTokenInfo.Number(-1.1),
                JsonTokenInfo.Number(1),
                JsonTokenInfo.Number(2),
                JsonTokenInfo.String("hello"),
                JsonTokenInfo.Null(),
                JsonTokenInfo.Boolean(true),
                JsonTokenInfo.Boolean(false),
                JsonTokenInfo.ArrayEnd(),
            };

            this.VerifyWriter(tokensToWrite, expectedString);
            this.VerifyWriter(tokensToWrite, binaryOutput);
        }

        [TestMethod]
        [Owner("brchon")]
        public void NestedArrayTest()
        {
            string expectedString = "[[], []]  ";
            byte[] binaryOutput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.Array1ByteLength,
                // length
                2,
                JsonBinaryEncoding.TypeMarker.EmptyArray,
                JsonBinaryEncoding.TypeMarker.EmptyArray,
            };

            JsonTokenInfo[] tokensToWrite =
            {
                JsonTokenInfo.ArrayStart(),
                JsonTokenInfo.ArrayStart(),
                JsonTokenInfo.ArrayEnd(),
                JsonTokenInfo.ArrayStart(),
                JsonTokenInfo.ArrayEnd(),
                JsonTokenInfo.ArrayEnd(),
            };

            this.VerifyWriter(tokensToWrite, expectedString);
            this.VerifyWriter(tokensToWrite, binaryOutput);
        }

        [TestMethod]
        [Owner("brchon")]
        public void StrangeNumberArrayTest()
        {
            string expectedString = @"[
                35,
                70,
                140,
                1.1111111101111111E+39,
                1.1111111101111111E+69,
                1.1111111101111111E+139,
                1.1111111101111111E+279
            ]";
            List<byte[]> binaryOutputBuilder = new List<byte[]>();
            binaryOutputBuilder.Add(new byte[] { BinaryFormat, JsonBinaryEncoding.TypeMarker.Array1ByteLength });

            List<byte[]> elements = new List<byte[]>();
            elements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.UInt8, 35 });
            elements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.UInt8, 70 });
            elements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.UInt8, 140 });
            elements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.Double, 0xBC, 0xCA, 0x0F, 0xBA, 0x41, 0x1F, 0x0A, 0x48 });
            elements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.Double, 0xDB, 0x5E, 0xAE, 0xBE, 0x50, 0x9B, 0x44, 0x4E });
            elements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.Double, 0x32, 0x80, 0x84, 0x3C, 0x73, 0xDB, 0xCD, 0x5C });
            elements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.Double, 0x8D, 0x0D, 0x28, 0x0B, 0x16, 0x57, 0xDF, 0x79 });
            byte[] elementsBytes = elements.SelectMany(x => x).ToArray();

            binaryOutputBuilder.Add(new byte[] { (byte)elementsBytes.Length });
            binaryOutputBuilder.Add(elementsBytes);
            byte[] binaryOutput = binaryOutputBuilder.SelectMany(x => x).ToArray();

            JsonTokenInfo[] tokensToWrite =
            {
                JsonTokenInfo.ArrayStart(),
                JsonTokenInfo.Number(00000000000000000000000000000000035),
                JsonTokenInfo.Number(0000000000000000000000000000000000000000000000000000000000000000000070),
                JsonTokenInfo.Number(00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000140),
                JsonTokenInfo.Number(1111111110111111111011111111101111111110.0),
                JsonTokenInfo.Number(1111111110111111111011111111101111111110111111111011111111101111111110.0),
                JsonTokenInfo.Number(1.1111111101111111e+139),
                JsonTokenInfo.Number(1.1111111101111111e+279),
                JsonTokenInfo.ArrayEnd(),
            };

            this.VerifyWriter(tokensToWrite, expectedString);
            this.VerifyWriter(tokensToWrite, binaryOutput);
        }
        #endregion Array
        #region Escaping
        [TestMethod]
        [Owner("brchon")]
        public void EscapeCharacterTest()
        {
            /// <summary>
            /// Set of all escape characters in JSON.
            /// </summary>
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
                string expectedString = "\"" + escapeCharacter.Item1 + "\"";

                JsonTokenInfo[] tokensToWrite =
                {
                     JsonTokenInfo.String(escapeCharacter.Item2),
                };

                this.VerifyWriter(tokensToWrite, expectedString);
                // Binary does not test this since you would just put the literal character if you wanted it.
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public void UnicodeEscapeTest()
        {
            // You don't have to escape a regular unicode character
            string expectedString = "\"\x20AC\"";

            JsonTokenInfo[] tokensToWrite =
            {
                 JsonTokenInfo.String("\x20AC"),
            };

            this.VerifyWriter(tokensToWrite, expectedString);
            // Binary does not test this since you would just put the literal character if you wanted it.
        }

        [TestMethod]
        [Owner("brchon")]
        public void TwoAdjacentUnicodeCharactersTest()
        {
            // 2 unicode escape characters that are not surrogate pairs
            // You don't have to escape a regular unicode character
            string unicodeEscapedString = "\"\x20AC\x20AC\"";

            JsonTokenInfo[] tokensToWrite =
            {
                 JsonTokenInfo.String("\x20AC\x20AC"),
            };

            this.VerifyWriter(tokensToWrite, unicodeEscapedString);
            // Binary does not test this since you would just put the literal character if you wanted it.
        }

        [TestMethod]
        [Owner("brchon")]
        public void UnicodeTest()
        {
            // You don't have to escape a regular unicode character
            string expectedString = @"""€""";
            byte[] binaryInput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + 3,
                // € in utf8 hex
                0xE2, 0x82, 0xAC
            };

            JsonTokenInfo[] tokensToWrite =
            {
                 JsonTokenInfo.String("€"),
            };

            this.VerifyWriter(tokensToWrite, expectedString);
            this.VerifyWriter(tokensToWrite, binaryInput);
        }

        [TestMethod]
        [Owner("brchon")]
        public void EmojiUTF32Test()
        {
            // You don't have to escape a regular unicode character
            string expectedString = @"""💩""";
            byte[] binaryInput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + 4,
                // 💩 in utf8 hex
                0xF0, 0x9F, 0x92, 0xA9
            };

            JsonTokenInfo[] tokensToWrite =
            {
                 JsonTokenInfo.String("💩"),
            };

            this.VerifyWriter(tokensToWrite, expectedString);
            this.VerifyWriter(tokensToWrite, binaryInput);
        }

        [TestMethod]
        [Owner("brchon")]
        public void ControlCharacterTests()
        {
            HashSet<char> escapeCharacters = new HashSet<char> { '\b', '\f', '\n', '\r', '\t', '\\', '"', '/' };

            // control characters (U+0000 through U+001F)
            for (byte controlCharacter = 0; controlCharacter <= 0x1F; controlCharacter++)
            {
                // Whitespace characters have special escaping
                if (!escapeCharacters.Contains((char)controlCharacter))
                {
                    string expectedString = "\"" + "\\u" + "00" + controlCharacter.ToString("x2") + "\"";

                    JsonTokenInfo[] tokensToWrite =
                    {
                        JsonTokenInfo.String("" + (char)controlCharacter)
                    };

                    this.VerifyWriter(tokensToWrite, expectedString);
                }
            }
        }
        #endregion
        #region Objects
        [TestMethod]
        [Owner("brchon")]
        public void EmptyObjectTest()
        {
            string expectedString = "{}";
            byte[] binaryOutput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.EmptyObject,
            };

            JsonTokenInfo[] tokensToWrite =
            {
                 JsonTokenInfo.ObjectStart(),
                 JsonTokenInfo.ObjectEnd(),
            };

            this.VerifyWriter(tokensToWrite, expectedString);
            this.VerifyWriter(tokensToWrite, binaryOutput);
            this.VerifyWriter(tokensToWrite, binaryOutput, new JsonStringDictionary(capacity: 100));
        }

        [TestMethod]
        [Owner("brchon")]
        public void SimpleObjectTest()
        {
            string expectedString = "{\"GlossDiv\":10,\"title\": \"example glossary\" }";

            byte[] binaryOutput;
            {
                List<byte[]> binaryOutputBuilder = new List<byte[]>();
                binaryOutputBuilder.Add(new byte[] { BinaryFormat, JsonBinaryEncoding.TypeMarker.Object1ByteLength });

                List<byte[]> elements = new List<byte[]>();
                elements.Add(new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + "GlossDiv".Length), 71, 108, 111, 115, 115, 68, 105, 118 });
                elements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.LiteralIntMin + 10 });
                elements.Add(new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + "title".Length), 116, 105, 116, 108, 101 });
                elements.Add(new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + "example glossary".Length), 101, 120, 97, 109, 112, 108, 101, 32, 103, 108, 111, 115, 115, 97, 114, 121 });
                byte[] elementsBytes = elements.SelectMany(x => x).ToArray();

                binaryOutputBuilder.Add(new byte[] { (byte)elementsBytes.Length });
                binaryOutputBuilder.Add(elementsBytes);
                binaryOutput = binaryOutputBuilder.SelectMany(x => x).ToArray();
            }

            byte[] binaryOutputWithEncoding;
            {
                List<byte[]> binaryOutputBuilder = new List<byte[]>();
                binaryOutputBuilder.Add(new byte[] { BinaryFormat, JsonBinaryEncoding.TypeMarker.Object1ByteLength });

                List<byte[]> elements = new List<byte[]>();
                elements.Add(new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.UserString1ByteLengthMin) });
                elements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.LiteralIntMin + 10 });
                elements.Add(new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.UserString1ByteLengthMin + 1) });
                elements.Add(new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + "example glossary".Length), 101, 120, 97, 109, 112, 108, 101, 32, 103, 108, 111, 115, 115, 97, 114, 121 });
                byte[] elementsBytes = elements.SelectMany(x => x).ToArray();

                binaryOutputBuilder.Add(new byte[] { (byte)elementsBytes.Length });
                binaryOutputBuilder.Add(elementsBytes);
                binaryOutputWithEncoding = binaryOutputBuilder.SelectMany(x => x).ToArray();
            }

            JsonTokenInfo[] tokensToWrite =
            {
                JsonTokenInfo.ObjectStart(),
                JsonTokenInfo.FieldName("GlossDiv"),
                JsonTokenInfo.Number(10),
                JsonTokenInfo.FieldName("title"),
                JsonTokenInfo.String("example glossary"),
                JsonTokenInfo.ObjectEnd(),
            };

            this.VerifyWriter(tokensToWrite, expectedString);
            this.VerifyWriter(tokensToWrite, binaryOutput);
            this.VerifyWriter(tokensToWrite, binaryOutputWithEncoding, new JsonStringDictionary(capacity: 100));
        }

        [TestMethod]
        [Owner("brchon")]
        public void AllPrimitivesObjectTest()
        {
            string expectedString = @"{
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

            byte[] binaryOutput;
            {
                List<byte[]> binaryOutputBuilder = new List<byte[]>();
                binaryOutputBuilder.Add(new byte[] { BinaryFormat, JsonBinaryEncoding.TypeMarker.Object2ByteLength });

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

                binaryOutputBuilder.Add(BitConverter.GetBytes((short)elementsBytes.Length));
                binaryOutputBuilder.Add(elementsBytes);
                binaryOutput = binaryOutputBuilder.SelectMany(x => x).ToArray();
            }

            byte[] binaryOutputWithEncoding;
            {
                List<byte[]> binaryOutputBuilder = new List<byte[]>();
                binaryOutputBuilder.Add(new byte[] { BinaryFormat, JsonBinaryEncoding.TypeMarker.Object2ByteLength });

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

                binaryOutputBuilder.Add(BitConverter.GetBytes((short)elementsBytes.Length));
                binaryOutputBuilder.Add(elementsBytes);
                binaryOutputWithEncoding = binaryOutputBuilder.SelectMany(x => x).ToArray();
            }

            JsonTokenInfo[] tokensToWrite =
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

            this.VerifyWriter(tokensToWrite, expectedString);
            this.VerifyWriter(tokensToWrite, binaryOutput);
            this.VerifyWriter(tokensToWrite, binaryOutputWithEncoding, new JsonStringDictionary(capacity: 100));
        }
        #endregion
        #region Exceptions
        [TestMethod]
        [Owner("brchon")]
        public void ArrayNotStartedTest()
        {
            JsonTokenInfo[] tokensToWrite =
            {
                JsonTokenInfo.ArrayEnd()
            };

            this.VerifyWriter(tokensToWrite, new JsonArrayNotStartedException());
            // Binary does not test this.
        }

        [TestMethod]
        [Owner("brchon")]
        public void ObjectNotStartedTest()
        {
            JsonTokenInfo[] tokensToWrite =
            {
                JsonTokenInfo.FieldName("Writing a fieldname before an object has been started.")
            };

            this.VerifyWriter(tokensToWrite, new JsonObjectNotStartedException());
            // Binary does not test this.
        }

        [TestMethod]
        [Owner("brchon")]
        public void PropertyArrayOrObjectNotStartedTest()
        {
            JsonTokenInfo[] tokensToWrite =
            {
                JsonTokenInfo.ObjectStart(),
                JsonTokenInfo.ObjectEnd(),
                JsonTokenInfo.Number(42)
            };

            this.VerifyWriter(tokensToWrite, new JsonPropertyArrayOrObjectNotStartedException());
            // Binary does not test this.
        }

        [TestMethod]
        [Owner("brchon")]
        public void MissingPropertyTest()
        {
            JsonTokenInfo[] tokensToWrite =
            {
                JsonTokenInfo.ObjectStart(),
                JsonTokenInfo.String("Creating a property value without a correpsonding fieldname"),
                JsonTokenInfo.ObjectEnd(),
            };

            this.VerifyWriter(tokensToWrite, new JsonMissingPropertyException());
            // Binary does not test this.
        }

        [TestMethod]
        [Owner("brchon")]
        public void PropertyAlreadyAddedTest()
        {
            string duplicateFieldName = "This property is added twice";
            JsonTokenInfo[] tokensToWrite =
            {
                JsonTokenInfo.ObjectStart(),
                JsonTokenInfo.FieldName(duplicateFieldName),
                JsonTokenInfo.Number(42),
                JsonTokenInfo.FieldName(duplicateFieldName),
                JsonTokenInfo.Number(56),
                JsonTokenInfo.ObjectEnd(),
            };

            this.VerifyWriter(tokensToWrite, new JsonPropertyAlreadyAddedException());
            // Binary does not test this.
        }
        #endregion

        private void VerifyWriter(JsonTokenInfo[] tokensToWrite, string expectedString)
        {
            this.VerifyWriter(tokensToWrite, expectedString, null);
        }

        private void VerifyWriter(JsonTokenInfo[] tokensToWrite, Exception expectedException)
        {
            this.VerifyWriter(tokensToWrite, (string)null, expectedException);
        }

        private void VerifyWriter(JsonTokenInfo[] tokensToWrite, string expectedString = null, Exception expectedException = null)
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

                    Encoding[] encodings =
                    {
                        Encoding.UTF8,
                        Encoding.Unicode,
                        Encoding.UTF32,
                    };

                    foreach (Encoding encoding in encodings)
                    {
                        // Create through encoding API
                        IJsonWriter jsonWriterWithEncoding = JsonWriter.Create(encoding);
                        if (expectedString != null)
                        {
                            // remove formatting on the json and also replace "/" with "\/" since newtonsoft is dumb.
                            string expectedStringNoWhiteSpace = Newtonsoft.Json.Linq.JToken.Parse(expectedString).ToString(Newtonsoft.Json.Formatting.None).Replace("/", @"\/");
                            this.VerifyWriter(jsonWriterWithEncoding, tokensToWrite, encoding.GetBytes(expectedStringNoWhiteSpace), JsonSerializationFormat.Text, expectedException);
                        }
                        else
                        {
                            this.VerifyWriter(jsonWriterWithEncoding, tokensToWrite, null, JsonSerializationFormat.Text, expectedException);
                        }
                    }

                    // Create through serializtion api
                    IJsonWriter jsonWriter = JsonWriter.Create(JsonSerializationFormat.Text);
                    byte[] expectedOutput;
                    if (expectedString != null)
                    {
                        string expectedStringNoWhiteSpace = Newtonsoft.Json.Linq.JToken.Parse(expectedString).ToString(Newtonsoft.Json.Formatting.None).Replace("/", @"\/");
                        expectedOutput = Encoding.UTF8.GetBytes(expectedStringNoWhiteSpace);
                    }
                    else
                    {
                        expectedOutput = null;
                    }

                    this.VerifyWriter(jsonWriter, tokensToWrite, expectedOutput, JsonSerializationFormat.Text, expectedException);
                }
            }
            finally
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = defaultCultureInfo;
            }
        }

        private void VerifyWriter(JsonTokenInfo[] tokensToWrite, byte[] binaryOutput, Exception expectedException = null)
        {
            IJsonWriter jsonWriter = JsonWriter.Create(JsonSerializationFormat.Binary);
            this.VerifyWriter(jsonWriter, tokensToWrite, binaryOutput, JsonSerializationFormat.Binary, expectedException);
        }

        private void VerifyWriter(JsonTokenInfo[] tokensToWrite, byte[] binaryOutput, JsonStringDictionary jsonStringDictionary, Exception expectedException = null)
        {
            IJsonWriter jsonWriter = JsonWriter.Create(JsonSerializationFormat.Binary, jsonStringDictionary);
            this.VerifyWriter(jsonWriter, tokensToWrite, binaryOutput, JsonSerializationFormat.Binary, expectedException);
        }

        private void VerifyWriter(IJsonWriter jsonWriter, JsonTokenInfo[] tokensToWrite, byte[] expectedOutput, JsonSerializationFormat jsonSerializationFormat, Exception expectedException = null)
        {
            Assert.AreEqual(jsonSerializationFormat == JsonSerializationFormat.Text ? 0 : 1, jsonWriter.CurrentLength);
            Assert.AreEqual(jsonWriter.SerializationFormat, jsonSerializationFormat);

            try
            {
                foreach (JsonTokenInfo token in tokensToWrite)
                {
                    switch (token.JsonTokenType)
                    {
                        case JsonTokenType.BeginArray:
                            jsonWriter.WriteArrayStart();
                            break;
                        case JsonTokenType.EndArray:
                            jsonWriter.WriteArrayEnd();
                            break;
                        case JsonTokenType.BeginObject:
                            jsonWriter.WriteObjectStart();
                            break;
                        case JsonTokenType.EndObject:
                            jsonWriter.WriteObjectEnd();
                            break;
                        case JsonTokenType.String:
                            string stringWithQuotes = Encoding.Unicode.GetString(token.BufferedToken.ToArray());
                            string value = stringWithQuotes.Substring(1, stringWithQuotes.Length - 2);
                            jsonWriter.WriteStringValue(value);
                            break;
                        case JsonTokenType.Number:
                            jsonWriter.WriteNumberValue(token.Value);
                            break;
                        case JsonTokenType.True:
                            jsonWriter.WriteBoolValue(true);
                            break;
                        case JsonTokenType.False:
                            jsonWriter.WriteBoolValue(false);
                            break;
                        case JsonTokenType.Null:
                            jsonWriter.WriteNullValue();
                            break;
                        case JsonTokenType.FieldName:
                            string fieldNameWithQuotes = Encoding.Unicode.GetString(token.BufferedToken.ToArray());
                            string fieldName = fieldNameWithQuotes.Substring(1, fieldNameWithQuotes.Length - 2);
                            jsonWriter.WriteFieldName(fieldName);
                            break;
                        case JsonTokenType.NotStarted:
                        default:
                            Assert.Fail(string.Format("Got an unexpected JsonTokenType: {0} as an expected token type", token.JsonTokenType));
                            break;
                    }
                }
            }
            catch (Exception exception)
            {
                Assert.IsNotNull(expectedException, "Got an exception when none was expected");
                Assert.AreEqual(expectedException.GetType(), exception.GetType());
            }

            if (expectedException == null)
            {
                byte[] result = jsonWriter.GetResult();
                Assert.IsTrue(expectedOutput.SequenceEqual(result),
                    string.Format("Expected : {0}, Actual :{1}",
                    string.Join(", ", expectedOutput),
                    string.Join(", ", result)));
            }
        }
    }
}
