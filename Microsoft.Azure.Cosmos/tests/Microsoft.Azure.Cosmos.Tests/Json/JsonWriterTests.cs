namespace Microsoft.Azure.Cosmos.Tests.Json
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System.Globalization;
    using Microsoft.Azure.Cosmos.Core.Utf8;

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

            JsonToken[] tokensToWrite =
            {
                JsonToken.Boolean(true)
            };

            this.VerifyWriter(tokensToWrite, expectedString);
            this.VerifyWriter(tokensToWrite, binaryOutput);
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

            JsonToken[] tokensToWrite =
            {
                JsonToken.Boolean(false)
            };

            this.VerifyWriter(tokensToWrite, expectedString);
            this.VerifyWriter(tokensToWrite, binaryOutput);
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

            JsonToken[] tokensToWrite =
            {
                JsonToken.Null()
            };

            this.VerifyWriter(tokensToWrite, expectedString);
            this.VerifyWriter(tokensToWrite, binaryOutput);
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
                JsonBinaryEncoding.TypeMarker.NumberInt16,
                // 1337 in litte endian hex,
                0x39, 0x05,
            };

            JsonToken[] tokensToWrite =
            {
                JsonToken.Number(1337)
            };

            this.VerifyWriter(tokensToWrite, expectedString);
            this.VerifyWriter(tokensToWrite, binaryOutput);
        }

        [TestMethod]
        [Owner("brchon")]
        public void DoubleTest()
        {
            string expectedString = "1337.1337";
            byte[] binaryOutput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.NumberDouble,
                // 1337.1337 in litte endian hex for a double
                0xE7, 0x1D, 0xA7, 0xE8, 0x88, 0xE4, 0x94, 0x40,
            };

            JsonToken[] tokensToWrite =
            {
                JsonToken.Number(1337.1337)
            };

            this.VerifyWriter(tokensToWrite, expectedString);
            this.VerifyWriter(tokensToWrite, binaryOutput);
        }

        [TestMethod]
        [Owner("brchon")]
        public void NaNTest()
        {
            string expectedString = "\"NaN\"";
            byte[] binaryOutput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.NumberDouble,
                // NaN in litte endian hex for a double
                0, 0, 0, 0, 0, 0, 248, 255
            };

            JsonToken[] tokensToWrite =
            {
                JsonToken.Number(double.NaN)
            };

            this.VerifyWriter(tokensToWrite, expectedString);
            this.VerifyWriter(tokensToWrite, binaryOutput);
        }

        [TestMethod]
        [Owner("brchon")]
        public void PositiveInfinityTest()
        {
            string expectedString = "\"Infinity\"";
            byte[] binaryOutput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.NumberDouble,
                // Infinity in litte endian hex for a double
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xF0, 0x7F,
            };

            JsonToken[] tokensToWrite =
            {
                JsonToken.Number(double.PositiveInfinity)
            };

            this.VerifyWriter(tokensToWrite, expectedString);
            this.VerifyWriter(tokensToWrite, binaryOutput);
        }

        [TestMethod]
        [Owner("brchon")]
        public void NegativeInfinityTest()
        {
            string expectedString = "\"-Infinity\"";
            byte[] binaryOutput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.NumberDouble,
                // Infinity in litte endian hex for a double
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xF0, 0xFF,
            };

            JsonToken[] tokensToWrite =
            {
                JsonToken.Number(double.NegativeInfinity)
            };

            this.VerifyWriter(tokensToWrite, expectedString);
            this.VerifyWriter(tokensToWrite, binaryOutput);
        }

        [TestMethod]
        [Owner("brchon")]
        public void NegativeNumberTest()
        {
            string expectedString = "-1337.1337";
            byte[] binaryOutput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.NumberDouble,
                // Infinity in litte endian hex for a double
                0xE7, 0x1D, 0xA7, 0xE8, 0x88, 0xE4, 0x94, 0xC0,
            };

            JsonToken[] tokensToWrite =
            {
                JsonToken.Number(-1337.1337)
            };

            this.VerifyWriter(tokensToWrite, expectedString);
            this.VerifyWriter(tokensToWrite, binaryOutput);
        }

        [TestMethod]
        [Owner("brchon")]
        public void NumberWithScientificNotationTest()
        {
            string expectedString = "6.02252E+23";
            byte[] binaryOutput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.NumberDouble,
                // 6.02252e23 in litte endian hex for a double
                0x93, 0x09, 0x9F, 0x5D, 0x09, 0xE2, 0xDF, 0x44
            };

            JsonToken[] tokensToWrite =
            {
                JsonToken.Number(6.02252e23)
            };

            this.VerifyWriter(tokensToWrite, expectedString);
            this.VerifyWriter(tokensToWrite, binaryOutput);
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
                JsonBinaryEncoding.TypeMarker.NumberDouble,
                // 0.00085647800000000004 in litte endian hex for a double
                0x39, 0x98, 0xF7, 0x7F, 0xA8, 0x10, 0x4C, 0x3F
            };

            JsonToken[] tokensToWrite =
            {
                JsonToken.Number(0.00085647800000000004)
            };

            this.VerifyWriter(tokensToWrite, numberValueString);
            this.VerifyWriter(tokensToWrite, binaryOutput);
        }

        [TestMethod]
        [Owner("brchon")]
        public void NumberPrecisionTest()
        {
            string expectedString = "[2.7620553993338772e+018,2.7620553993338778e+018]";
            // remove formatting on the json and also replace "/" with "\/" since newtonsoft is dumb.
            expectedString = Newtonsoft.Json.Linq.JToken
                .Parse(expectedString)
                .ToString(Newtonsoft.Json.Formatting.None)
                .Replace("/", @"\/");

            List<byte> binaryOutputBuilder = new List<byte>
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.Array1ByteLength,
                sizeof(byte) + sizeof(double) + sizeof(byte) + sizeof(double),
                JsonBinaryEncoding.TypeMarker.NumberDouble
            };
            binaryOutputBuilder.AddRange(BitConverter.GetBytes(2.7620553993338772e+018));
            binaryOutputBuilder.Add(JsonBinaryEncoding.TypeMarker.NumberDouble);
            binaryOutputBuilder.AddRange(BitConverter.GetBytes(2.7620553993338778e+018));
            byte[] binaryOutput = binaryOutputBuilder.ToArray();

            JsonToken[] tokensToWrite =
            {
                JsonToken.ArrayStart(),
                JsonToken.Number(2.7620553993338772e+018),
                JsonToken.Number(2.7620553993338778e+018),
                JsonToken.ArrayEnd(),
            };

            this.VerifyWriter(tokensToWrite, expectedString);
            this.VerifyWriter(tokensToWrite, binaryOutput);
        }

        [TestMethod]
        [Owner("brchon")]
        public void LargeNumbersTest()
        {
            string expectedString = @"[1,-1,10,-10,14997460357411200,14997460357411000,1499746035741101,1499746035741109,-14997460357411200,-14997460357411000,-1499746035741101,-1499746035741109,1499746035741128,1499752659822592,1499752939110661,1499753827614475,1499970126403840,1499970590815128,1499970842400644,1499971371510025,1499972760675685,1499972969962006,1499973086735836,1499973302072392,1499976826748983]";
            JsonToken[] tokensToWrite =
            {
                JsonToken.ArrayStart(),
                JsonToken.Number(1),
                JsonToken.Number(-1),
                JsonToken.Number(10),
                JsonToken.Number(-10),
                JsonToken.Number(14997460357411200),
                JsonToken.Number(14997460357411000),
                JsonToken.Number(1499746035741101),
                JsonToken.Number(1499746035741109),
                JsonToken.Number(-14997460357411200),
                JsonToken.Number(-14997460357411000),
                JsonToken.Number(-1499746035741101),
                JsonToken.Number(-1499746035741109),
                JsonToken.Number(1499746035741128),
                JsonToken.Number(1499752659822592),
                JsonToken.Number(1499752939110661),
                JsonToken.Number(1499753827614475),
                JsonToken.Number(1499970126403840),
                JsonToken.Number(1499970590815128),
                JsonToken.Number(1499970842400644),
                JsonToken.Number(1499971371510025),
                JsonToken.Number(1499972760675685),
                JsonToken.Number(1499972969962006),
                JsonToken.Number(1499973086735836),
                JsonToken.Number(1499973302072392),
                JsonToken.Number(1499976826748983),
                JsonToken.ArrayEnd(),
            };

            this.VerifyWriter(tokensToWrite, expectedString);
        }
        #endregion
        #region String
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

            JsonToken[] tokensToWrite =
            {
                JsonToken.String(string.Empty)
            };

            this.VerifyWriter(tokensToWrite, expectedString);
            this.VerifyWriter(tokensToWrite, binaryOutput);
        }

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

            JsonToken[] tokensToWrite =
            {
                JsonToken.String("Hello World")
            };

            this.VerifyWriter(tokensToWrite, expectedString);
            this.VerifyWriter(tokensToWrite, binaryOutput);
        }

        [TestMethod]
        [Owner("brchon")]
        public void SystemStringTest()
        {
            int systemStringId = 0;
            while (JsonBinaryEncoding.SystemStrings.TryGetSystemStringById(systemStringId, out UtfAllString systemString))
            {
                string expectedString = "\"" + systemString.Utf16String + "\"";
                // remove formatting on the json and also replace "/" with "\/" since newtonsoft is dumb.
                expectedString = Newtonsoft.Json.Linq.JToken
                    .Parse(expectedString)
                    .ToString(Newtonsoft.Json.Formatting.None);

                byte[] binaryOutput =
                {
                    BinaryFormat,
                    (byte)(JsonBinaryEncoding.TypeMarker.SystemString1ByteLengthMin + ((int)systemStringId)),
                };

                JsonToken[] tokensToWrite =
                {
                    JsonToken.String(systemString.Utf16String)
                };

                this.VerifyWriter(tokensToWrite, expectedString);
                this.VerifyWriter(tokensToWrite, binaryOutput);
                systemStringId++;
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public void DateTimeStringsTest()
        {
            {
                string dateTimeString = "2015-06-30 23:45:13";
                string stringPayload = $"\"{dateTimeString}\"";
                JsonToken[] tokensToWrite =
                {
                    JsonToken.String(dateTimeString)
                };

                byte[] compressedBinaryPayload =
                {
                    BinaryFormat,
                    JsonBinaryEncoding.TypeMarker.CompressedDateTimeString,
                    0x13, 0x13, 0x62, 0x1C, 0xC7, 0x14, 0x30, 0xB4, 0x65, 0x2B, 0x04
                };

                this.VerifyWriter(tokensToWrite, stringPayload);
                this.VerifyWriter(tokensToWrite, compressedBinaryPayload);
            }

            {
                string dateTimeString = "2010-08-02T14:27:44-07:00";
                string stringPayload = $"\"{dateTimeString}\"";
                JsonToken[] tokensToWrite =
                {
                    JsonToken.String(dateTimeString)
                };

                byte[] compressedBinaryPayload =
                {
                    BinaryFormat,
                    JsonBinaryEncoding.TypeMarker.CompressedDateTimeString,
                    0x19, 0x13, 0x12, 0x1C, 0xC9, 0x31, 0x2E, 0xB5, 0x83, 0x5B, 0xC5, 0x81, 0x1B, 0x01
                };

                this.VerifyWriter(tokensToWrite, stringPayload);
                this.VerifyWriter(tokensToWrite, compressedBinaryPayload);
            }

            {
                string dateTimeString = "2007-03-01T13:00:00Z";
                string stringPayload = $"\"{dateTimeString}\"";
                JsonToken[] tokensToWrite =
                {
                    JsonToken.String(dateTimeString)
                };

                byte[] compressedBinaryPayload =
                {
                    BinaryFormat,
                    JsonBinaryEncoding.TypeMarker.CompressedDateTimeString,
                    0x14, 0x13, 0x81, 0x1C, 0xC4, 0x21, 0x2E, 0xB4, 0x11, 0x1B, 0xF1
                };

                this.VerifyWriter(tokensToWrite, stringPayload);
                this.VerifyWriter(tokensToWrite, compressedBinaryPayload);
            }

            {
                string dateTimeString = "2007-03-01T13:00:00Z";
                string stringPayload = $"\"{dateTimeString}\"";
                JsonToken[] tokensToWrite =
                {
                    JsonToken.String(dateTimeString)
                };

                byte[] compressedBinaryPayload =
                {
                    BinaryFormat,
                    JsonBinaryEncoding.TypeMarker.CompressedDateTimeString,
                    0x14, 0x13, 0x81, 0x1C, 0xC4, 0x21, 0x2E, 0xB4, 0x11, 0x1B, 0xF1
                };

                this.VerifyWriter(tokensToWrite, stringPayload);
                this.VerifyWriter(tokensToWrite, compressedBinaryPayload);
            }

            {
                string dateTimeString = "2014-10-18T14:18:17.5337932-07:00";
                string stringPayload = $"\"{dateTimeString}\"";
                JsonToken[] tokensToWrite =
                {
                    JsonToken.String(dateTimeString)
                };

                byte[] compressedBinaryPayload =
                {
                    BinaryFormat,
                    JsonBinaryEncoding.TypeMarker.CompressedDateTimeString,
                    0x21, 0x13, 0x52, 0x2C, 0xC1, 0x92, 0x2E, 0xB5, 0x92, 0x2B, 0xD8, 0x46, 0x84, 0x4A, 0xC3, 0x81, 0x1B, 0x01
                };

                this.VerifyWriter(tokensToWrite, stringPayload);
                this.VerifyWriter(tokensToWrite, compressedBinaryPayload);
            }

            {
                string dateTimeString = "2014-10-18T14:18:17.5337932-07:00";
                string stringPayload = $"\"{dateTimeString}\"";
                JsonToken[] tokensToWrite =
                {
                    JsonToken.String(dateTimeString)
                };

                byte[] compressedBinaryPayload =
                {
                    BinaryFormat,
                    JsonBinaryEncoding.TypeMarker.CompressedDateTimeString,
                    0x21, 0x13, 0x52, 0x2C, 0xC1, 0x92, 0x2E, 0xB5, 0x92, 0x2B, 0xD8, 0x46, 0x84, 0x4A, 0xC3, 0x81, 0x1B, 0x01
                };

                this.VerifyWriter(tokensToWrite, stringPayload);
                this.VerifyWriter(tokensToWrite, compressedBinaryPayload);
            }

            {
                string dateTimeString = "0123456789:.-TZ ";
                string stringPayload = $"\"{dateTimeString}\"";
                JsonToken[] tokensToWrite =
                {
                    JsonToken.String(dateTimeString)
                };

                byte[] compressedBinaryPayload =
                {
                    BinaryFormat,
                    JsonBinaryEncoding.TypeMarker.CompressedDateTimeString,
                    0x10, 0x21, 0x43, 0x65, 0x87, 0xA9, 0xDB, 0xEC, 0x0F
                };

                this.VerifyWriter(tokensToWrite, stringPayload);
                this.VerifyWriter(tokensToWrite, compressedBinaryPayload);
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public void HexStringsTest()
        {
            {
                string hexString = "eccab3900d55b946";
                string stringPayload = $"\"{hexString}\"";
                JsonToken[] tokensToWrite =
                {
                    JsonToken.String(hexString)
                };

                byte[] compressedBinaryPayload =
                {
                    BinaryFormat,
                    JsonBinaryEncoding.TypeMarker.CompressedLowercaseHexString,
                    0x10, 0xCE, 0xAC, 0x3B, 0x09, 0xD0, 0x55, 0x9B, 0x64
                };

                this.VerifyWriter(tokensToWrite, stringPayload);
                this.VerifyWriter(tokensToWrite, compressedBinaryPayload);
            }

            {
                string hexString = "ECCAB3900D55B946";
                string stringPayload = $"\"{hexString}\"";
                JsonToken[] tokensToWrite =
                {
                    JsonToken.String(hexString)
                };

                byte[] compressedBinaryPayload =
                {
                    BinaryFormat,
                    JsonBinaryEncoding.TypeMarker.CompressedUppercaseHexString,
                    0x10, 0xCE, 0xAC, 0x3B, 0x09, 0xD0, 0x55, 0x9B, 0x64
                };

                this.VerifyWriter(tokensToWrite, stringPayload);
                this.VerifyWriter(tokensToWrite, compressedBinaryPayload);
            }

            {
                // (mixed case) regular string
                string hexString = "eccAB3900d55b946";
                string stringPayload = $"\"{hexString}\"";
                JsonToken[] tokensToWrite =
                {
                    JsonToken.String(hexString)
                };

                byte[] compressedBinaryPayload =
                {
                    BinaryFormat,
                    (byte)(JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + hexString.Length),
                };
                compressedBinaryPayload = compressedBinaryPayload.Concat(Encoding.UTF8.GetBytes(hexString)).ToArray();

                this.VerifyWriter(tokensToWrite, stringPayload);
                this.VerifyWriter(tokensToWrite, compressedBinaryPayload);
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public void CompressedStringsTest()
        {
            {
                // 4 bit packed string
                string compressedString = "ababababababababababababababababg";
                string stringPayload = $"\"{compressedString}\"";
                JsonToken[] tokensToWrite =
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

                this.VerifyWriter(tokensToWrite, stringPayload);
                this.VerifyWriter(tokensToWrite, compressedBinaryPayload);
            }

            {
                // 5 bit packed string
                string compressedString = "thequickbrownfoxjumpedoverthelazydog";
                string stringPayload = $"\"{compressedString}\"";
                JsonToken[] tokensToWrite =
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

                this.VerifyWriter(tokensToWrite, stringPayload);
                this.VerifyWriter(tokensToWrite, compressedBinaryPayload);
            }

            {
                // 6 bit packed string
                string compressedString = "thequickbrownfoxjumpedoverthelazydogTHEQUICKBROWNFOXJUMPEDOVERTHELAZYDOG";
                string stringPayload = $"\"{compressedString}\"";
                JsonToken[] tokensToWrite =
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

                this.VerifyWriter(tokensToWrite, stringPayload);
                this.VerifyWriter(tokensToWrite, compressedBinaryPayload);
            }

            {
                // 7 bit packed string length 1
                string compressedString = "thequickbrownfoxjumpedoverthelazydogTHEQUICKBROWNFOXJUMPEDOVERTHELAZYDOG0123456789!@#$%^&*";
                string stringPayload = $"\"{compressedString}\"";
                JsonToken[] tokensToWrite =
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

                this.VerifyWriter(tokensToWrite, stringPayload);
                this.VerifyWriter(tokensToWrite, compressedBinaryPayload);
            }

            {
                // 7 bit packed string length 2
                string compressedString = "thequickbrownfoxjumpedoverthelazydogTHEQUICKBROWNFOXJUMPEDOVERTHELAZYDOG0123456789!@#$%^&*";
                compressedString = compressedString + compressedString + compressedString;
                string stringPayload = $"\"{compressedString}\"";
                JsonToken[] tokensToWrite =
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

                this.VerifyWriter(tokensToWrite, stringPayload);
                this.VerifyWriter(tokensToWrite, compressedBinaryPayload);
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public void GuidStringsTest()
        {
            {
                // Empty Guid
                string guidString = "00000000-0000-0000-0000-000000000000";
                string stringPayload = $"\"{guidString}\"";
                JsonToken[] tokensToWrite =
                {
                    JsonToken.String(guidString)
                };

                byte[] compressedBinaryPayload =
                {
                    BinaryFormat,
                    JsonBinaryEncoding.TypeMarker.LowercaseGuidString,
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                };

                this.VerifyWriter(tokensToWrite, stringPayload);
                this.VerifyWriter(tokensToWrite, compressedBinaryPayload);
            }

            {
                // All numbers
                string guidString = "11111111-2222-3333-4444-555555555555";
                string stringPayload = $"\"{guidString}\"";
                JsonToken[] tokensToWrite =
                {
                    JsonToken.String(guidString)
                };

                byte[] compressedBinaryPayload =
                {
                    BinaryFormat,
                    JsonBinaryEncoding.TypeMarker.LowercaseGuidString,
                    0x11, 0x11, 0x11, 0x11, 0x22, 0x22, 0x33, 0x33,
                    0x44, 0x44, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55,
                };

                this.VerifyWriter(tokensToWrite, stringPayload);
                this.VerifyWriter(tokensToWrite, compressedBinaryPayload);
            }

            {
                // All lower-case letters
                string guidString = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";
                string stringPayload = $"\"{guidString}\"";
                JsonToken[] tokensToWrite =
                {
                    JsonToken.String(guidString)
                };

                byte[] compressedBinaryPayload =
                {
                    BinaryFormat,
                    JsonBinaryEncoding.TypeMarker.LowercaseGuidString,
                    0xAA, 0xAA, 0xAA, 0xAA, 0xBB, 0xBB, 0xCC, 0xCC,
                    0xDD, 0xDD, 0xEE, 0xEE, 0xEE, 0xEE, 0xEE, 0xEE,
                };

                this.VerifyWriter(tokensToWrite, stringPayload);
                this.VerifyWriter(tokensToWrite, compressedBinaryPayload);
            }

            {
                // All upper-case letters
                string guidString = "AAAAAAAA-BBBB-CCCC-DDDD-EEEEEEEEEEEE";
                string stringPayload = $"\"{guidString}\"";
                JsonToken[] tokensToWrite =
                {
                    JsonToken.String(guidString)
                };

                byte[] compressedBinaryPayload =
                {
                    BinaryFormat,
                    JsonBinaryEncoding.TypeMarker.UppercaseGuidString,
                    0xAA, 0xAA, 0xAA, 0xAA, 0xBB, 0xBB, 0xCC, 0xCC,
                    0xDD, 0xDD, 0xEE, 0xEE, 0xEE, 0xEE, 0xEE, 0xEE,
                };

                this.VerifyWriter(tokensToWrite, stringPayload);
                this.VerifyWriter(tokensToWrite, compressedBinaryPayload);
            }

            {
                // Lower-case GUID
                string guidString = "ed7e38aa-074e-4a74-bab0-2a4f41079baa";
                string stringPayload = $"\"{guidString}\"";
                JsonToken[] tokensToWrite =
                {
                    JsonToken.String(guidString)
                };

                byte[] compressedBinaryPayload =
                {
                    BinaryFormat,
                    JsonBinaryEncoding.TypeMarker.LowercaseGuidString,
                    0xDE, 0xE7, 0x83, 0xAA, 0x70, 0xE4, 0xA4, 0x47,
                    0xAB, 0x0B, 0xA2, 0xF4, 0x14, 0x70, 0xB9, 0xAA,
                };

                this.VerifyWriter(tokensToWrite, stringPayload);
                this.VerifyWriter(tokensToWrite, compressedBinaryPayload);
            }

            {
                // Upper-case GUID
                string guidString = "ED7E38AA-074E-4A74-BAB0-2A4F41079BAA";
                string stringPayload = $"\"{guidString}\"";
                JsonToken[] tokensToWrite =
                {
                    JsonToken.String(guidString)
                };

                byte[] compressedBinaryPayload =
                {
                    BinaryFormat,
                    JsonBinaryEncoding.TypeMarker.UppercaseGuidString,
                    0xDE, 0xE7, 0x83, 0xAA, 0x70, 0xE4, 0xA4, 0x47,
                    0xAB, 0x0B, 0xA2, 0xF4, 0x14, 0x70, 0xB9, 0xAA,
                };

                this.VerifyWriter(tokensToWrite, stringPayload);
                this.VerifyWriter(tokensToWrite, compressedBinaryPayload);
            }

            {
                // Upper-case GUID
                string guidString = "ED7E38AA-074E-4A74-BAB0-2A4F41079BAA";
                string stringPayload = $"\"{guidString}\"";
                JsonToken[] tokensToWrite =
                {
                    JsonToken.String(guidString)
                };

                byte[] compressedBinaryPayload =
                {
                    BinaryFormat,
                    JsonBinaryEncoding.TypeMarker.UppercaseGuidString,
                    0xDE, 0xE7, 0x83, 0xAA, 0x70, 0xE4, 0xA4, 0x47,
                    0xAB, 0x0B, 0xA2, 0xF4, 0x14, 0x70, 0xB9, 0xAA,
                };

                this.VerifyWriter(tokensToWrite, stringPayload);
                this.VerifyWriter(tokensToWrite, compressedBinaryPayload);
            }

            {
                // Mixed-case GUID (just a regular string)
                string guidString = "412D5baf-acf2-4c43-9ccb-c80a9d6f267D";
                string stringPayload = $"\"{guidString}\"";
                JsonToken[] tokensToWrite =
                {
                    JsonToken.String(guidString)
                };

                byte[] binaryPayload =
                {
                    BinaryFormat,
                    (byte)(JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + guidString.Length),
                };
                binaryPayload = binaryPayload.Concat(Encoding.UTF8.GetBytes(guidString)).ToArray();

                this.VerifyWriter(tokensToWrite, stringPayload);
                this.VerifyWriter(tokensToWrite, binaryPayload);
            }

            {
                // Max-value GUID
                string guidString = "ffffffff-ffff-ffff-ffff-ffffffffffff";
                string stringPayload = $"\"{guidString}\"";
                JsonToken[] tokensToWrite =
                {
                    JsonToken.String(guidString)
                };

                byte[] compressedBinaryPayload =
                {
                    BinaryFormat,
                    JsonBinaryEncoding.TypeMarker.LowercaseGuidString,
                    0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
                    0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
                };

                this.VerifyWriter(tokensToWrite, stringPayload);
                this.VerifyWriter(tokensToWrite, compressedBinaryPayload);
            }

            {
                // Lowercase quoted guid
                string guidString = "\"ffffffff-ffff-ffff-ffff-ffffffffffff\"";
                string stringPayload = $"\"\\\"ffffffff-ffff-ffff-ffff-ffffffffffff\\\"\"";
                JsonToken[] tokensToWrite =
                {
                    JsonToken.String(guidString)
                };

                byte[] compressedBinaryPayload =
                {
                    BinaryFormat,
                    JsonBinaryEncoding.TypeMarker.DoubleQuotedLowercaseGuidString,
                    0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
                    0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
                };

                this.VerifyWriter(tokensToWrite, stringPayload);
                this.VerifyWriter(tokensToWrite, compressedBinaryPayload);
            }

            {
                // Uppercase quoted guid (just a regular string)
                string guidString = "\"A58C8319-4FCB-43A9-AF31-BAA24EDD4FDC\"";
                string stringPayload = $"\"\\\"A58C8319-4FCB-43A9-AF31-BAA24EDD4FDC\\\"\"";
                JsonToken[] tokensToWrite =
                {
                    JsonToken.String(guidString)
                };

                byte[] compressedBinaryPayload =
                {
                    BinaryFormat,
                    (byte)(JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + guidString.Length),
                };
                compressedBinaryPayload = compressedBinaryPayload.Concat(Encoding.UTF8.GetBytes(guidString)).ToArray();

                this.VerifyWriter(tokensToWrite, stringPayload);
                this.VerifyWriter(tokensToWrite, compressedBinaryPayload);
            }

            {
                // malformed guid (just a regular string)
                string guidString = "E81F42C4-E62A-4C12-B6E1--9828038374E";
                string stringPayload = $"\"{guidString}\"";
                JsonToken[] tokensToWrite =
                {
                    JsonToken.String(guidString)
                };

                byte[] compressedBinaryPayload =
                {
                    BinaryFormat,
                    JsonBinaryEncoding.TypeMarker.Packed5BitString,
                    36,
                    45, 120, 145, 124, 138, 61, 0, 167, 66, 193, 177, 164, 128, 154, 48, 1, 128, 173, 178, 134, 89, 70, 29, 60
                };

                this.VerifyWriter(tokensToWrite, stringPayload);
                this.VerifyWriter(tokensToWrite, compressedBinaryPayload);
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public void ReferenceStringsTest()
        {
            {
                // 1 byte reference string
                string stringValue = "hello";
                string stringPayload = "[\"hello\",\"hello\"]";
                JsonToken[] tokensToWrite =
                {
                    JsonToken.ArrayStart(),
                    JsonToken.String(stringValue),
                    JsonToken.String(stringValue),
                    JsonToken.ArrayEnd()
                };

                byte[] binaryPayload =
                {
                    BinaryFormat,
                    JsonBinaryEncoding.TypeMarker.Array1ByteLength,
                    8,
                    (byte)(JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + "hello".Length),
                    (byte)'h', (byte)'e', (byte)'l', (byte)'l', (byte)'o',
                    JsonBinaryEncoding.TypeMarker.ReferenceString1ByteOffset,
                    3,
                };

                this.VerifyWriter(tokensToWrite, stringPayload);
                this.VerifyWriter(tokensToWrite, binaryPayload);
            }

            {
                // 2 byte reference string
                JsonToken[] tokensToWrite = {
                    JsonToken.ObjectStart(),
                        JsonToken.FieldName("property1"),
                        JsonToken.String("value1"),
                        JsonToken.FieldName("property2"),
                        JsonToken.String("value1"),
                        JsonToken.FieldName("property3"),
                        JsonToken.ArrayStart(),
                            JsonToken.String("property0"),
                            JsonToken.String("property1"),
                            JsonToken.String("property2"),
                            JsonToken.String("property3"),
                            JsonToken.String("property4"),
                            JsonToken.String("property5"),
                            JsonToken.String("property6"),
                            JsonToken.String("property7"),
                            JsonToken.String("property8"),
                            JsonToken.String("property9"),
                            JsonToken.String("value1"),
                            JsonToken.String("value1"),
                            JsonToken.String("value1"),
                            JsonToken.String("property9"),
                            JsonToken.String("property8"),
                            JsonToken.String("property7"),
                            JsonToken.String("property5"),
                            JsonToken.String("property4"),
                            JsonToken.String("property3"),
                            JsonToken.String("property2"),
                            JsonToken.String("property1"),
                            JsonToken.String("property0"),
                        JsonToken.ArrayEnd(),
                        JsonToken.FieldName("property3"),
                        JsonToken.String("value1"),
                        JsonToken.FieldName("property4"),
                        JsonToken.String("value1"),
                        JsonToken.FieldName("property5"),
                        JsonToken.ArrayStart(),
                            JsonToken.String("StringValue__0"),
                            JsonToken.String("StringValue__1"),
                            JsonToken.String("StringValue__2"),
                            JsonToken.String("StringValue__3"),
                            JsonToken.String("StringValue__4"),
                            JsonToken.String("StringValue__5"),
                            JsonToken.String("StringValue__6"),
                            JsonToken.String("StringValue__7"),
                            JsonToken.String("StringValue__8"),
                            JsonToken.String("StringValue__9"),
                            JsonToken.String("value2"),
                            JsonToken.String("value2"),
                            JsonToken.String("value2"),
                            JsonToken.String("StringValue__9"),
                            JsonToken.String("StringValue__8"),
                            JsonToken.String("StringValue__7"),
                            JsonToken.String("StringValue__5"),
                            JsonToken.String("StringValue__4"),
                            JsonToken.String("StringValue__3"),
                            JsonToken.String("StringValue__2"),
                            JsonToken.String("StringValue__1"),
                            JsonToken.String("StringValue__0"),
                        JsonToken.ArrayEnd(),
                        JsonToken.FieldName("property5"),
                        JsonToken.String("value2"),
                        JsonToken.FieldName("property6"),
                        JsonToken.String("value2"),
                        JsonToken.FieldName("property7"),
                        JsonToken.ArrayStart(),
                            JsonToken.String("TextValue__0"),
                            JsonToken.String("TextValue__1"),
                            JsonToken.String("TextValue__2"),
                            JsonToken.String("TextValue__3"),
                            JsonToken.String("TextValue__4"),
                            JsonToken.String("TextValue__5"),
                            JsonToken.String("TextValue__6"),
                            JsonToken.String("TextValue__7"),
                            JsonToken.String("TextValue__8"),
                            JsonToken.String("TextValue__9"),
                            JsonToken.String("value3"),
                            JsonToken.String("value3"),
                            JsonToken.String("value3"),
                            JsonToken.String("TextValue__9"),
                            JsonToken.String("TextValue__8"),
                            JsonToken.String("TextValue__7"),
                            JsonToken.String("TextValue__5"),
                            JsonToken.String("TextValue__4"),
                            JsonToken.String("TextValue__3"),
                            JsonToken.String("TextValue__2"),
                            JsonToken.String("TextValue__1"),
                            JsonToken.String("TextValue__0"),
                        JsonToken.ArrayEnd(),
                        JsonToken.FieldName("property8"),
                        JsonToken.String("value3"),
                        JsonToken.FieldName("property9"),
                        JsonToken.String("value3"),
                        JsonToken.FieldName("property0"),
                        JsonToken.ArrayStart(),
                            JsonToken.String("BrownDog__0"),
                            JsonToken.String("BrownDog__1"),
                            JsonToken.String("BrownDog__2"),
                            JsonToken.String("BrownDog__3"),
                            JsonToken.String("BrownDog__4"),
                            JsonToken.String("BrownDog__5"),
                            JsonToken.String("BrownDog__6"),
                            JsonToken.String("BrownDog__7"),
                            JsonToken.String("BrownDog__8"),
                            JsonToken.String("BrownDog__9"),
                            JsonToken.String("value4"),
                            JsonToken.String("value4"),
                            JsonToken.String("value4"),
                            JsonToken.String("BrownDog__9"),
                            JsonToken.String("BrownDog__8"),
                            JsonToken.String("BrownDog__7"),
                            JsonToken.String("BrownDog__5"),
                            JsonToken.String("BrownDog__4"),
                            JsonToken.String("BrownDog__3"),
                            JsonToken.String("BrownDog__2"),
                            JsonToken.String("BrownDog__1"),
                            JsonToken.String("BrownDog__0"),
                        JsonToken.ArrayEnd(),
                        JsonToken.FieldName("propertyA"),
                        JsonToken.String("valueA"),
                        JsonToken.FieldName("propertyA"),
                        JsonToken.String("valueA"),
                    JsonToken.ObjectEnd(),
                };
                byte[] binaryPayload = new byte[]
                {
                    0x80, 0xEB, 0xCE, 0x02, 0x89, 0x70, 0x72, 0x6F,
                    0x70, 0x65, 0x72, 0x74, 0x79, 0x31, 0x86, 0x76, 
                    0x61, 0x6C, 0x75, 0x65, 0x31, 0x89, 0x70, 0x72,
                    0x6F, 0x70, 0x65, 0x72, 0x74, 0x79, 0x32, 0xC3,
                    0x0E, 0x89, 0x70, 0x72, 0x6F, 0x70, 0x65, 0x72,
                    0x74, 0x79, 0x33, 0xE2, 0x64, 0x89, 0x70, 0x72,
                    0x6F, 0x70, 0x65, 0x72, 0x74, 0x79, 0x30, 0xC3,
                    0x04, 0xC3, 0x15, 0xC3, 0x21, 0x89, 0x70, 0x72, 
                    0x6F, 0x70, 0x65, 0x72, 0x74, 0x79, 0x34, 0x89,
                    0x70, 0x72, 0x6F, 0x70, 0x65, 0x72, 0x74, 0x79,
                    0x35, 0x89, 0x70, 0x72, 0x6F, 0x70, 0x65, 0x72,
                    0x74, 0x79, 0x36, 0x89, 0x70, 0x72, 0x6F, 0x70,
                    0x65, 0x72, 0x74, 0x79, 0x37, 0x89, 0x70, 0x72,
                    0x6F, 0x70, 0x65, 0x72, 0x74, 0x79, 0x38, 0x89,
                    0x70, 0x72, 0x6F, 0x70, 0x65, 0x72, 0x74, 0x79,
                    0x39, 0xC3, 0x0E, 0xC3, 0x0E, 0xC3, 0x0E, 0xC3,
                    0x6F, 0xC3, 0x65, 0xC3, 0x5B, 0xC3, 0x47, 0xC3,
                    0x3D, 0xC3, 0x21, 0xC3, 0x15, 0xC3, 0x04, 0xC3,
                    0x2D, 0xC3, 0x21, 0xC3, 0x0E, 0xC3, 0x3D, 0xC3,
                    0x0E, 0xC3, 0x47, 0xE2, 0xB8, 0x8E, 0x53, 0x74,
                    0x72, 0x69, 0x6E, 0x67, 0x56, 0x61, 0x6C, 0x75,
                    0x65, 0x5F, 0x5F, 0x30, 0x8E, 0x53, 0x74, 0x72, 
                    0x69, 0x6E, 0x67, 0x56, 0x61, 0x6C, 0x75, 0x65,
                    0x5F, 0x5F, 0x31, 0x8E, 0x53, 0x74, 0x72, 0x69,
                    0x6E, 0x67, 0x56, 0x61, 0x6C, 0x75, 0x65, 0x5F,
                    0x5F, 0x32, 0x8E, 0x53, 0x74, 0x72, 0x69, 0x6E,
                    0x67, 0x56, 0x61, 0x6C, 0x75, 0x65, 0x5F, 0x5F,
                    0x33, 0x8E, 0x53, 0x74, 0x72, 0x69, 0x6E, 0x67,
                    0x56, 0x61, 0x6C, 0x75, 0x65, 0x5F, 0x5F, 0x34,
                    0x8E, 0x53, 0x74, 0x72, 0x69, 0x6E, 0x67, 0x56,
                    0x61, 0x6C, 0x75, 0x65, 0x5F, 0x5F, 0x35, 0x8E,
                    0x53, 0x74, 0x72, 0x69, 0x6E, 0x67, 0x56, 0x61,
                    0x6C, 0x75, 0x65, 0x5F, 0x5F, 0x36, 0x8E, 0x53, 
                    0x74, 0x72, 0x69, 0x6E, 0x67, 0x56, 0x61, 0x6C,
                    0x75, 0x65, 0x5F, 0x5F, 0x37, 0x8E, 0x53, 0x74,
                    0x72, 0x69, 0x6E, 0x67, 0x56, 0x61, 0x6C, 0x75,
                    0x65, 0x5F, 0x5F, 0x38, 0x8E, 0x53, 0x74, 0x72,
                    0x69, 0x6E, 0x67, 0x56, 0x61, 0x6C, 0x75, 0x65,
                    0x5F, 0x5F, 0x39, 0x86, 0x76, 0x61, 0x6C, 0x75,
                    0x65, 0x32, 0xC4, 0x33, 0x01, 0xC4, 0x33, 0x01,
                    0xC4, 0x24, 0x01, 0xC4, 0x15, 0x01, 0xC4, 0x06,
                    0x01, 0xC3, 0xE8, 0xC3, 0xD9, 0xC3, 0xCA, 0xC3,
                    0xBB, 0xC3, 0xAC, 0xC3, 0x9D, 0xC3, 0x47, 0xC4,
                    0x33, 0x01, 0xC3, 0x51, 0xC4, 0x33, 0x01, 0xC3,
                    0x5B, 0xE2, 0xAA, 0x8C, 0x54, 0x65, 0x78, 0x74,
                    0x56, 0x61, 0x6C, 0x75, 0x65, 0x5F, 0x5F, 0x30,
                    0x8C, 0x54, 0x65, 0x78, 0x74, 0x56, 0x61, 0x6C,
                    0x75, 0x65, 0x5F, 0x5F, 0x31, 0x8C, 0x54, 0x65,
                    0x78, 0x74, 0x56, 0x61, 0x6C, 0x75, 0x65, 0x5F,
                    0x5F, 0x32, 0x8C, 0x54, 0x65, 0x78, 0x74, 0x56,
                    0x61, 0x6C, 0x75, 0x65, 0x5F, 0x5F, 0x33, 0x8C,
                    0x54, 0x65, 0x78, 0x74, 0x56, 0x61, 0x6C, 0x75,
                    0x65, 0x5F, 0x5F, 0x34, 0x8C, 0x54, 0x65, 0x78,
                    0x74, 0x56, 0x61, 0x6C, 0x75, 0x65, 0x5F, 0x5F,
                    0x35, 0x8C, 0x54, 0x65, 0x78, 0x74, 0x56, 0x61,
                    0x6C, 0x75, 0x65, 0x5F, 0x5F, 0x36, 0x8C, 0x54,
                    0x65, 0x78, 0x74, 0x56, 0x61, 0x6C, 0x75, 0x65,
                    0x5F, 0x5F, 0x37, 0x8C, 0x54, 0x65, 0x78, 0x74,
                    0x56, 0x61, 0x6C, 0x75, 0x65, 0x5F, 0x5F, 0x38,
                    0x8C, 0x54, 0x65, 0x78, 0x74, 0x56, 0x61, 0x6C,
                    0x75, 0x65, 0x5F, 0x5F, 0x39, 0x86, 0x76, 0x61,
                    0x6C, 0x75, 0x65, 0x33, 0xC4, 0xE5, 0x01, 0xC4,
                    0xE5, 0x01, 0xC4, 0xD8, 0x01, 0xC4, 0xCB, 0x01,
                    0xC4, 0xBE, 0x01, 0xC4, 0xA4, 0x01, 0xC4, 0x97,
                    0x01, 0xC4, 0x8A, 0x01, 0xC4, 0x7D, 0x01, 0xC4,
                    0x70, 0x01, 0xC4, 0x63, 0x01, 0xC3, 0x65, 0xC4,
                    0xE5, 0x01, 0xC3, 0x6F, 0xC4, 0xE5, 0x01, 0xC3,
                    0x2D, 0xE2, 0xA0, 0x8B, 0x42, 0x72, 0x6F, 0x77,
                    0x6E, 0x44, 0x6F, 0x67, 0x5F, 0x5F, 0x30, 0x8B,
                    0x42, 0x72, 0x6F, 0x77, 0x6E, 0x44, 0x6F, 0x67,
                    0x5F, 0x5F, 0x31, 0x8B, 0x42, 0x72, 0x6F, 0x77,
                    0x6E, 0x44, 0x6F, 0x67, 0x5F, 0x5F, 0x32, 0x8B,
                    0x42, 0x72, 0x6F, 0x77, 0x6E, 0x44, 0x6F, 0x67,
                    0x5F, 0x5F, 0x33, 0x8B, 0x42, 0x72, 0x6F, 0x77,
                    0x6E, 0x44, 0x6F, 0x67, 0x5F, 0x5F, 0x34, 0x8B,
                    0x42, 0x72, 0x6F, 0x77, 0x6E, 0x44, 0x6F, 0x67,
                    0x5F, 0x5F, 0x35, 0x8B, 0x42, 0x72, 0x6F, 0x77,
                    0x6E, 0x44, 0x6F, 0x67, 0x5F, 0x5F, 0x36, 0x8B, 
                    0x42, 0x72, 0x6F, 0x77, 0x6E, 0x44, 0x6F, 0x67, 
                    0x5F, 0x5F, 0x37, 0x8B, 0x42, 0x72, 0x6F, 0x77, 
                    0x6E, 0x44, 0x6F, 0x67, 0x5F, 0x5F, 0x38, 0x8B, 
                    0x42, 0x72, 0x6F, 0x77, 0x6E, 0x44, 0x6F, 0x67, 
                    0x5F, 0x5F, 0x39, 0x86, 0x76, 0x61, 0x6C, 0x75, 
                    0x65, 0x34, 0xC4, 0x93, 0x02, 0xC4, 0x93, 0x02, 
                    0xC4, 0x87, 0x02, 0xC4, 0x7B, 0x02, 0xC4, 0x6F, 
                    0x02, 0xC4, 0x57, 0x02, 0xC4, 0x4B, 0x02, 0xC4, 
                    0x3F, 0x02, 0xC4, 0x33, 0x02, 0xC4, 0x27, 0x02, 
                    0xC4, 0x1B, 0x02, 0x89, 0x70, 0x72, 0x6F, 0x70, 
                    0x65, 0x72, 0x74, 0x79, 0x41, 0x86, 0x76, 0x61, 
                    0x6C, 0x75, 0x65, 0x41, 0xC4, 0xBB, 0x02, 0xC4, 
                    0xC5, 0x02
                };

                this.VerifyWriter(tokensToWrite, binaryPayload);
            }
        }
        #endregion
        #region Array
        [TestMethod]
        [Owner("brchon")]
        public void EmptyArrayTest()
        {
            string expectedString = "[]";
            byte[] binaryOutput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.EmptyArray
            };

            JsonToken[] tokensToWrite =
            {
                JsonToken.ArrayStart(),
                JsonToken.ArrayEnd(),
            };

            this.VerifyWriter(tokensToWrite, expectedString);
            this.VerifyWriter(tokensToWrite, binaryOutput);
        }

        [TestMethod]
        [Owner("brchon")]
        public void SingleItemArrayTest()
        {
            string expectedString = "[true]";
            byte[] binaryOutput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.SingleItemArray,
                JsonBinaryEncoding.TypeMarker.True
            };

            JsonToken[] tokensToWrite =
            {
                JsonToken.ArrayStart(),
                JsonToken.Boolean(true),
                JsonToken.ArrayEnd(),
            };

            this.VerifyWriter(tokensToWrite, expectedString);
            this.VerifyWriter(tokensToWrite, binaryOutput);
        }

        [TestMethod]
        [Owner("brchon")]
        public void IntArrayTest()
        {
            string expectedString = "[-2,-1,0,1,2]";
            List<byte[]> binaryOutputBuilder = new List<byte[]>
            {
                new byte[] { BinaryFormat, JsonBinaryEncoding.TypeMarker.Array1ByteLength }
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

            binaryOutputBuilder.Add(new byte[] { (byte)numbersBytes.Length });
            binaryOutputBuilder.Add(numbersBytes);
            byte[] binaryOutput = binaryOutputBuilder.SelectMany(x => x).ToArray();

            JsonToken[] tokensToWrite =
            {
                JsonToken.ArrayStart(),
                JsonToken.Number(-2),
                JsonToken.Number(-1),
                JsonToken.Number(0),
                JsonToken.Number(1),
                JsonToken.Number(2),
                JsonToken.ArrayEnd(),
            };

            this.VerifyWriter(tokensToWrite, expectedString);
            this.VerifyWriter(tokensToWrite, binaryOutput);
        }

        [TestMethod]
        [Owner("brchon")]
        public void NumberArrayTest()
        {
            string expectedString = "[15,22,0.1,-0.073,7.70001E+91]";
            List<byte[]> binaryOutputBuilder = new List<byte[]>
            {
                new byte[] { BinaryFormat, JsonBinaryEncoding.TypeMarker.Array1ByteLength }
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

            binaryOutputBuilder.Add(new byte[] { (byte)numbersBytes.Length });
            binaryOutputBuilder.Add(numbersBytes);
            byte[] binaryOutput = binaryOutputBuilder.SelectMany(x => x).ToArray();

            JsonToken[] tokensToWrite =
            {
                JsonToken.ArrayStart(),
                JsonToken.Number(15),
                JsonToken.Number(22),
                JsonToken.Number(0.1),
                JsonToken.Number(-7.3e-2),
                JsonToken.Number(77.0001e90),
                JsonToken.ArrayEnd(),
            };

            this.VerifyWriter(tokensToWrite, expectedString);
            this.VerifyWriter(tokensToWrite, binaryOutput);
        }

        [TestMethod]
        [Owner("brchon")]
        public void BooleanArrayTest()
        {
            string expectedString = "[true,false]";
            byte[] binaryOutput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.Array1ByteLength,
                // length
                2,
                JsonBinaryEncoding.TypeMarker.True,
                JsonBinaryEncoding.TypeMarker.False,
            };

            JsonToken[] tokensToWrite =
            {
                JsonToken.ArrayStart(),
                JsonToken.Boolean(true),
                JsonToken.Boolean(false),
                JsonToken.ArrayEnd(),
            };

            this.VerifyWriter(tokensToWrite, expectedString);
            this.VerifyWriter(tokensToWrite, binaryOutput);
        }

        [TestMethod]
        [Owner("brchon")]
        public void StringArrayTest()
        {
            string expectedString = @"[""Hello"",""World"",""Bye""]";

            List<byte[]> binaryOutputBuilder = new List<byte[]>
            {
                new byte[] { BinaryFormat, JsonBinaryEncoding.TypeMarker.Array1ByteLength }
            };

            List<byte[]> strings = new List<byte[]>
            {
                new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + "Hello".Length) },
                Encoding.UTF8.GetBytes("Hello"),
                new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + "World".Length) },
                Encoding.UTF8.GetBytes("World"),
                new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + "Bye".Length) },
                Encoding.UTF8.GetBytes("Bye")
            };
            byte[] stringBytes = strings.SelectMany(x => x).ToArray();

            binaryOutputBuilder.Add(new byte[] { (byte)stringBytes.Length });
            binaryOutputBuilder.Add(stringBytes);
            byte[] binaryOutput = binaryOutputBuilder.SelectMany(x => x).ToArray();

            JsonToken[] tokensToWrite =
            {
                JsonToken.ArrayStart(),
                JsonToken.String("Hello"),
                JsonToken.String("World"),
                JsonToken.String("Bye"),
                JsonToken.ArrayEnd(),
            };

            this.VerifyWriter(tokensToWrite, expectedString);
            this.VerifyWriter(tokensToWrite, binaryOutput);
        }

        [TestMethod]
        [Owner("brchon")]
        public void NullArrayTest()
        {
            string expectedString = "[null,null,null]";
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

            JsonToken[] tokensToWrite =
            {
                JsonToken.ArrayStart(),
                JsonToken.Null(),
                JsonToken.Null(),
                JsonToken.Null(),
                JsonToken.ArrayEnd(),
            };

            this.VerifyWriter(tokensToWrite, expectedString);
            this.VerifyWriter(tokensToWrite, binaryOutput);
        }

        [TestMethod]
        [Owner("brchon")]
        public void ObjectArrayTest()
        {
            string expectedString = "[{},{}]";
            byte[] binaryOutput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.Array1ByteLength,
                // length
                2,
                JsonBinaryEncoding.TypeMarker.EmptyObject,
                JsonBinaryEncoding.TypeMarker.EmptyObject,
            };

            JsonToken[] tokensToWrite =
            {
                JsonToken.ArrayStart(),
                JsonToken.ObjectStart(),
                JsonToken.ObjectEnd(),
                JsonToken.ObjectStart(),
                JsonToken.ObjectEnd(),
                JsonToken.ArrayEnd(),
            };

            this.VerifyWriter(tokensToWrite, expectedString);
            this.VerifyWriter(tokensToWrite, binaryOutput);
        }

        [TestMethod]
        [Owner("brchon")]
        public void AllPrimitiveArrayTest()
        {
            string expectedString = "[0,0,-1,-1.1,1,2,\"hello\",null,true,false]";
            List<byte[]> binaryOutputBuilder = new List<byte[]>
            {
                new byte[] { BinaryFormat, JsonBinaryEncoding.TypeMarker.Array1ByteLength }
            };

            List<byte[]> elements = new List<byte[]>
            {
                new byte[] { JsonBinaryEncoding.TypeMarker.LiteralIntMin },
                new byte[] { JsonBinaryEncoding.TypeMarker.NumberDouble },
                BitConverter.GetBytes(0.0),
                new byte[] { JsonBinaryEncoding.TypeMarker.NumberInt16, 0xFF, 0xFF },
                new byte[] { JsonBinaryEncoding.TypeMarker.NumberDouble, 0x9A, 0x99, 0x99, 0x99, 0x99, 0x99, 0xF1, 0xBF },
                new byte[] { JsonBinaryEncoding.TypeMarker.LiteralIntMin + 1 },
                new byte[] { JsonBinaryEncoding.TypeMarker.LiteralIntMin + 2 },
                new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + "hello".Length), 104, 101, 108, 108, 111 },
                new byte[] { JsonBinaryEncoding.TypeMarker.Null },
                new byte[] { JsonBinaryEncoding.TypeMarker.True },
                new byte[] { JsonBinaryEncoding.TypeMarker.False }
            };
            byte[] elementsBytes = elements.SelectMany(x => x).ToArray();

            binaryOutputBuilder.Add(new byte[] { (byte)elementsBytes.Length });
            binaryOutputBuilder.Add(elementsBytes);
            byte[] binaryOutput = binaryOutputBuilder.SelectMany(x => x).ToArray();

            JsonToken[] tokensToWrite =
            {
                JsonToken.ArrayStart(),
                JsonToken.Number(0),
                JsonToken.Number(0.0),
                JsonToken.Number(-1),
                JsonToken.Number(-1.1),
                JsonToken.Number(1),
                JsonToken.Number(2),
                JsonToken.String("hello"),
                JsonToken.Null(),
                JsonToken.Boolean(true),
                JsonToken.Boolean(false),
                JsonToken.ArrayEnd(),
            };

            this.VerifyWriter(tokensToWrite, expectedString);
            this.VerifyWriter(tokensToWrite, binaryOutput);
        }

        [TestMethod]
        [Owner("brchon")]
        public void NestedArrayTest()
        {
            string expectedString = "[[],[]]";
            byte[] binaryOutput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.Array1ByteLength,
                // length
                2,
                JsonBinaryEncoding.TypeMarker.EmptyArray,
                JsonBinaryEncoding.TypeMarker.EmptyArray,
            };

            JsonToken[] tokensToWrite =
            {
                JsonToken.ArrayStart(),
                JsonToken.ArrayStart(),
                JsonToken.ArrayEnd(),
                JsonToken.ArrayStart(),
                JsonToken.ArrayEnd(),
                JsonToken.ArrayEnd(),
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
            // remove formatting on the json and also replace "/" with "\/" since newtonsoft is dumb.
            expectedString = Newtonsoft.Json.Linq.JToken
                .Parse(expectedString)
                .ToString(Newtonsoft.Json.Formatting.None)
                .Replace("/", @"\/");

            List<byte[]> binaryOutputBuilder = new List<byte[]>
            {
                new byte[] { BinaryFormat, JsonBinaryEncoding.TypeMarker.Array1ByteLength }
            };

            List<byte[]> elements = new List<byte[]>
            {
                new byte[] { JsonBinaryEncoding.TypeMarker.NumberUInt8, 35 },
                new byte[] { JsonBinaryEncoding.TypeMarker.NumberUInt8, 70 },
                new byte[] { JsonBinaryEncoding.TypeMarker.NumberUInt8, 140 },
                new byte[] { JsonBinaryEncoding.TypeMarker.NumberDouble, 0xBC, 0xCA, 0x0F, 0xBA, 0x41, 0x1F, 0x0A, 0x48 },
                new byte[] { JsonBinaryEncoding.TypeMarker.NumberDouble, 0xDB, 0x5E, 0xAE, 0xBE, 0x50, 0x9B, 0x44, 0x4E },
                new byte[] { JsonBinaryEncoding.TypeMarker.NumberDouble, 0x32, 0x80, 0x84, 0x3C, 0x73, 0xDB, 0xCD, 0x5C },
                new byte[] { JsonBinaryEncoding.TypeMarker.NumberDouble, 0x8D, 0x0D, 0x28, 0x0B, 0x16, 0x57, 0xDF, 0x79 }
            };
            byte[] elementsBytes = elements.SelectMany(x => x).ToArray();

            binaryOutputBuilder.Add(new byte[] { (byte)elementsBytes.Length });
            binaryOutputBuilder.Add(elementsBytes);
            byte[] binaryOutput = binaryOutputBuilder.SelectMany(x => x).ToArray();

            JsonToken[] tokensToWrite =
            {
                JsonToken.ArrayStart(),
                JsonToken.Number(00000000000000000000000000000000035),
                JsonToken.Number(0000000000000000000000000000000000000000000000000000000000000000000070),
                JsonToken.Number(00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000140),
                JsonToken.Number(1111111110111111111011111111101111111110.0),
                JsonToken.Number(1111111110111111111011111111101111111110111111111011111111101111111110.0),
                JsonToken.Number(1.1111111101111111e+139),
                JsonToken.Number(1.1111111101111111e+279),
                JsonToken.ArrayEnd(),
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
            };

            foreach (Tuple<string, string> escapeCharacter in escapeCharacters)
            {
                string expectedString = "\"" + escapeCharacter.Item1 + "\"";

                JsonToken[] tokensToWrite =
                {
                     JsonToken.String(escapeCharacter.Item2),
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

            JsonToken[] tokensToWrite =
            {
                 JsonToken.String("\x20AC"),
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

            JsonToken[] tokensToWrite =
            {
                 JsonToken.String("\x20AC\x20AC"),
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
            byte[] expectedBinaryOutput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + 3,
                // € in utf8 hex
                0xE2, 0x82, 0xAC
            };

            JsonToken[] tokensToWrite =
            {
                 JsonToken.String("€"),
            };

            this.VerifyWriter(tokensToWrite, expectedString);
            this.VerifyWriter(tokensToWrite, expectedBinaryOutput);
        }

        [TestMethod]
        [Owner("brchon")]
        public void EmojiUTF32Test()
        {
            // You don't have to escape a regular unicode character
            string expectedString = @"""💩""";
            byte[] expectedBinaryOutput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + 4,
                // 💩 in utf8 hex
                0xF0, 0x9F, 0x92, 0xA9
            };

            JsonToken[] tokensToWrite =
            {
                 JsonToken.String("💩"),
            };

            this.VerifyWriter(tokensToWrite, expectedString);
            this.VerifyWriter(tokensToWrite, expectedBinaryOutput);
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
                    string expectedString = "\"" + "\\u" + "00" + controlCharacter.ToString("X2") + "\"";

                    JsonToken[] tokensToWrite =
                    {
                        JsonToken.String("" + (char)controlCharacter)
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

            JsonToken[] tokensToWrite =
            {
                 JsonToken.ObjectStart(),
                 JsonToken.ObjectEnd(),
            };

            this.VerifyWriter(tokensToWrite, expectedString);
            this.VerifyWriter(tokensToWrite, binaryOutput);
        }

        [TestMethod]
        [Owner("brchon")]
        public void SimpleObjectTest()
        {
            string expectedString = "{\"GlossDiv\":10,\"title\": \"example glossary\" }";
            // remove formatting on the json and also replace "/" with "\/" since newtonsoft is dumb.
            expectedString = Newtonsoft.Json.Linq.JToken
                .Parse(expectedString)
                .ToString(Newtonsoft.Json.Formatting.None)
                .Replace("/", @"\/");

            byte[] binaryOutput;
            {
                List<byte[]> binaryOutputBuilder = new List<byte[]>
                {
                    new byte[] { BinaryFormat, JsonBinaryEncoding.TypeMarker.Object1ByteLength }
                };

                List<byte[]> elements = new List<byte[]>
                {
                    new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + "GlossDiv".Length), 71, 108, 111, 115, 115, 68, 105, 118 },
                    new byte[] { JsonBinaryEncoding.TypeMarker.LiteralIntMin + 10 },
                    new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + "title".Length), 116, 105, 116, 108, 101 },
                    new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + "example glossary".Length), 101, 120, 97, 109, 112, 108, 101, 32, 103, 108, 111, 115, 115, 97, 114, 121 }
                };
                byte[] elementsBytes = elements.SelectMany(x => x).ToArray();

                binaryOutputBuilder.Add(new byte[] { (byte)elementsBytes.Length });
                binaryOutputBuilder.Add(elementsBytes);
                binaryOutput = binaryOutputBuilder.SelectMany(x => x).ToArray();
            }

            byte[] binaryOutputWithEncoding;
            {
                List<byte[]> binaryOutputBuilder = new List<byte[]>
                {
                    new byte[] { BinaryFormat, JsonBinaryEncoding.TypeMarker.Object1ByteLength }
                };

                List<byte[]> elements = new List<byte[]>
                {
                    new byte[] { (byte)JsonBinaryEncoding.TypeMarker.UserString1ByteLengthMin },
                    new byte[] { JsonBinaryEncoding.TypeMarker.LiteralIntMin + 10 },
                    new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.UserString1ByteLengthMin + 1) },
                    new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + "example glossary".Length), 101, 120, 97, 109, 112, 108, 101, 32, 103, 108, 111, 115, 115, 97, 114, 121 }
                };
                byte[] elementsBytes = elements.SelectMany(x => x).ToArray();

                binaryOutputBuilder.Add(new byte[] { (byte)elementsBytes.Length });
                binaryOutputBuilder.Add(elementsBytes);
                binaryOutputWithEncoding = binaryOutputBuilder.SelectMany(x => x).ToArray();
            }

            JsonToken[] tokensToWrite =
            {
                JsonToken.ObjectStart(),
                JsonToken.FieldName("GlossDiv"),
                JsonToken.Number(10),
                JsonToken.FieldName("title"),
                JsonToken.String("example glossary"),
                JsonToken.ObjectEnd(),
            };

            this.VerifyWriter(tokensToWrite, expectedString);
            this.VerifyWriter(tokensToWrite, binaryOutput);
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

            // remove formatting on the json and also replace "/" with "\/" since newtonsoft is dumb.
            expectedString = Newtonsoft.Json.Linq.JToken
                .Parse(expectedString)
                .ToString(Newtonsoft.Json.Formatting.None)
                .Replace("/", @"\/");

            byte[] binaryOutput;
            {
                List<byte[]> binaryOutputBuilder = new List<byte[]>
                {
                    new byte[] { BinaryFormat, JsonBinaryEncoding.TypeMarker.Object2ByteLength }
                };

                List<byte[]> elements = new List<byte[]>
                {
                    new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.SystemString1ByteLengthMin + 12) },
                    new byte[] { JsonBinaryEncoding.TypeMarker.LowercaseGuidString, 0x07, 0x92, 0x0D, 0x97, 0x04, 0x61, 0x44, 0x63, 0x7B, 0xAD, 0x63, 0x0C, 0xAB, 0x5E, 0xF4, 0x6F },

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
                    new byte[] { JsonBinaryEncoding.TypeMarker.CompressedDateTimeString, 0x1B, 0x63, 0x73, 0x1C, 0xC8, 0x22, 0x2E, 0xB9, 0x92, 0x2B, 0xD7, 0x65, 0x13, 0x28, 0x07 },

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

                innerObjectElements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.Array1ByteLength, (byte)innerArrayElementsBytes.Length });
                innerObjectElements.Add(innerArrayElementsBytes);

                byte[] innerObjectElementsBytes = innerObjectElements.SelectMany(x => x).ToArray();
                elements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.Object1ByteLength, (byte)innerObjectElementsBytes.Length });
                elements.Add(innerObjectElementsBytes);

                elements.Add(new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + "text".Length), 116, 101, 120, 116 });
                elements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.Packed7BitStringLength1, (byte)"tiger diamond newbrunswick snowleopard chocolate dog snowleopard turtle cat sapphire peach sapphire vancouver white chocolate horse diamond lion superlongcolourname ruby".Length, 0xF4, 0xF4, 0xB9, 0x2C, 0x07, 0x91, 0xD3, 0xE1, 0xF6, 0xDB, 0x4D, 0x06, 0xB9, 0xCB, 0x77, 0xB1, 0xBC, 0xEE, 0x9E, 0xDF, 0xD3, 0xE3, 0x35, 0x68, 0xEE, 0x7E, 0xDF, 0xD9, 0xE5, 0x37, 0x3C, 0x2C, 0x27, 0x83, 0xC6, 0xE8, 0xF7, 0xF8, 0xCD, 0x0E, 0xD3, 0xCB, 0x20, 0xF2, 0xFB, 0x0C, 0x9A, 0xBB, 0xDF, 0x77, 0x76, 0xF9, 0x0D, 0x0F, 0xCB, 0xC9, 0x20, 0x7A, 0x5D, 0x4E, 0x67, 0x97, 0x41, 0xE3, 0x30, 0x1D, 0x34, 0x0F, 0xC3, 0xE1, 0xE8, 0xB4, 0xBC, 0x0C, 0x82, 0x97, 0xC3, 0x63, 0x34, 0x68, 0x1E, 0x86, 0xC3, 0xD1, 0x69, 0x79, 0x19, 0x64, 0x0F, 0xBB, 0xC7, 0xEF, 0xBA, 0xBD, 0x2C, 0x07, 0xDD, 0xD1, 0x69, 0x7A, 0x19, 0x34, 0x46, 0xBF, 0xC7, 0x6F, 0x76, 0x98, 0x5E, 0x06, 0xA1, 0xDF, 0xF2, 0x79, 0x19, 0x44, 0x4E, 0x87, 0xDB, 0x6F, 0x37, 0x19, 0xC4, 0x4E, 0xBF, 0xDD, 0xA0, 0x79, 0x1D, 0x5E, 0x96, 0xB3, 0xDF, 0xEE, 0xF3, 0xF8, 0xCD, 0x7E, 0xD7, 0xE5, 0xEE, 0x70, 0xBB, 0x0C, 0x92, 0xD7, 0xC5, 0x79 });

                byte[] elementsBytes = elements.SelectMany(x => x).ToArray();

                binaryOutputBuilder.Add(BitConverter.GetBytes((ushort)elementsBytes.Length));
                binaryOutputBuilder.Add(elementsBytes);
                binaryOutput = binaryOutputBuilder.SelectMany(x => x).ToArray();
            }

            byte[] binaryOutputWithEncoding;
            {
                List<byte[]> binaryOutputBuilder = new List<byte[]>
                {
                    new byte[] { BinaryFormat, JsonBinaryEncoding.TypeMarker.Object1ByteLength }
                };

                List<byte[]> elements = new List<byte[]>
                {
                    new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.SystemString1ByteLengthMin + 12) },
                    new byte[] { JsonBinaryEncoding.TypeMarker.LowercaseGuidString, 0x07, 0x92, 0x0D, 0x97, 0x04, 0x61, 0x44, 0x63, 0x7B, 0xAD, 0x63, 0x0C, 0xAB, 0x5E, 0xF4, 0x6F },

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
                    new byte[] { JsonBinaryEncoding.TypeMarker.CompressedDateTimeString, 0x1B, 0x63, 0x73, 0x1C, 0xC8, 0x22, 0x2E, 0xB9, 0x92, 0x2B, 0xD7, 0x65, 0x13, 0x28, 0x07 },

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

                innerObjectElements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.Array1ByteLength, (byte)innerArrayElementsBytes.Length });
                innerObjectElements.Add(innerArrayElementsBytes);

                byte[] innerObjectElementsBytes = innerObjectElements.SelectMany(x => x).ToArray();
                elements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.Object1ByteLength, (byte)innerObjectElementsBytes.Length });
                elements.Add(innerObjectElementsBytes);

                elements.Add(new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.UserString1ByteLengthMin + 7) });
                elements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.Packed7BitStringLength1, (byte)"tiger diamond newbrunswick snowleopard chocolate dog snowleopard turtle cat sapphire peach sapphire vancouver white chocolate horse diamond lion superlongcolourname ruby".Length, 0xF4, 0xF4, 0xB9, 0x2C, 0x07, 0x91, 0xD3, 0xE1, 0xF6, 0xDB, 0x4D, 0x06, 0xB9, 0xCB, 0x77, 0xB1, 0xBC, 0xEE, 0x9E, 0xDF, 0xD3, 0xE3, 0x35, 0x68, 0xEE, 0x7E, 0xDF, 0xD9, 0xE5, 0x37, 0x3C, 0x2C, 0x27, 0x83, 0xC6, 0xE8, 0xF7, 0xF8, 0xCD, 0x0E, 0xD3, 0xCB, 0x20, 0xF2, 0xFB, 0x0C, 0x9A, 0xBB, 0xDF, 0x77, 0x76, 0xF9, 0x0D, 0x0F, 0xCB, 0xC9, 0x20, 0x7A, 0x5D, 0x4E, 0x67, 0x97, 0x41, 0xE3, 0x30, 0x1D, 0x34, 0x0F, 0xC3, 0xE1, 0xE8, 0xB4, 0xBC, 0x0C, 0x82, 0x97, 0xC3, 0x63, 0x34, 0x68, 0x1E, 0x86, 0xC3, 0xD1, 0x69, 0x79, 0x19, 0x64, 0x0F, 0xBB, 0xC7, 0xEF, 0xBA, 0xBD, 0x2C, 0x07, 0xDD, 0xD1, 0x69, 0x7A, 0x19, 0x34, 0x46, 0xBF, 0xC7, 0x6F, 0x76, 0x98, 0x5E, 0x06, 0xA1, 0xDF, 0xF2, 0x79, 0x19, 0x44, 0x4E, 0x87, 0xDB, 0x6F, 0x37, 0x19, 0xC4, 0x4E, 0xBF, 0xDD, 0xA0, 0x79, 0x1D, 0x5E, 0x96, 0xB3, 0xDF, 0xEE, 0xF3, 0xF8, 0xCD, 0x7E, 0xD7, 0xE5, 0xEE, 0x70, 0xBB, 0x0C, 0x92, 0xD7, 0xC5, 0x79 });

                byte[] elementsBytes = elements.SelectMany(x => x).ToArray();

                binaryOutputBuilder.Add(new byte[] { (byte)elementsBytes.Length });
                binaryOutputBuilder.Add(elementsBytes);
                binaryOutputWithEncoding = binaryOutputBuilder.SelectMany(x => x).ToArray();
            }

            JsonToken[] tokensToWrite =
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

            this.VerifyWriter(tokensToWrite, expectedString);
            this.VerifyWriter(tokensToWrite, binaryOutput);
        }
        #endregion
        #region Exceptions
        [TestMethod]
        [Owner("brchon")]
        public void ArrayNotStartedTest()
        {
            JsonToken[] tokensToWrite =
            {
                JsonToken.ArrayEnd()
            };

            this.VerifyWriter(tokensToWrite, new JsonArrayNotStartedException());
            // Binary does not test this.
        }

        [TestMethod]
        [Owner("brchon")]
        public void ObjectNotStartedTest()
        {
            JsonToken[] tokensToWrite =
            {
                JsonToken.FieldName("Writing a fieldname before an object has been started.")
            };

            this.VerifyWriter(tokensToWrite, new JsonObjectNotStartedException());
            // Binary does not test this.
        }

        [TestMethod]
        [Owner("brchon")]
        public void PropertyArrayOrObjectNotStartedTest()
        {
            JsonToken[] tokensToWrite =
            {
                JsonToken.ObjectStart(),
                JsonToken.ObjectEnd(),
                JsonToken.Number(42)
            };

            this.VerifyWriter(tokensToWrite, new JsonPropertyArrayOrObjectNotStartedException());
            // Binary does not test this.
        }

        [TestMethod]
        [Owner("brchon")]
        public void MissingPropertyTest()
        {
            JsonToken[] tokensToWrite =
            {
                JsonToken.ObjectStart(),
                JsonToken.String("Creating a property value without a correpsonding fieldname"),
                JsonToken.ObjectEnd(),
            };

            this.VerifyWriter(tokensToWrite, new JsonMissingPropertyException());
            // Binary does not test this.
        }

        [TestMethod]
        [Owner("brchon")]
        public void PropertyAlreadyAddedTest()
        {
            string duplicateFieldName = "This property is added twice";
            JsonToken[] tokensToWrite =
            {
                JsonToken.ObjectStart(),
                JsonToken.FieldName(duplicateFieldName),
                JsonToken.Number(42),
                JsonToken.FieldName(duplicateFieldName),
                JsonToken.Number(56),
                JsonToken.ObjectEnd(),
            };

            this.VerifyWriter(tokensToWrite, new JsonPropertyAlreadyAddedException());
            // Binary does not test this.
        }
        #endregion
        #region ExtendedTypes
        [TestMethod]
        [Owner("brchon")]
        public void Int8Test()
        {
            sbyte[] values = new sbyte[] { sbyte.MinValue, sbyte.MinValue + 1, -1, 0, 1, sbyte.MaxValue, sbyte.MaxValue - 1 };
            foreach (sbyte value in values)
            {
                string expectedStringOutput = $"I{value}";
                byte[] expectedBinaryOutput;
                unchecked
                {
                    expectedBinaryOutput = new byte[]
                    {
                        BinaryFormat,
                        JsonBinaryEncoding.TypeMarker.Int8,
                        (byte)value
                    };
                }

                JsonToken[] tokensToWrite =
                {
                    JsonToken.Int8(value)
                };

                this.VerifyWriter(tokensToWrite, expectedStringOutput);
                this.VerifyWriter(tokensToWrite, expectedBinaryOutput);
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public void Int16Test()
        {
            short[] values = new short[] { short.MinValue, short.MinValue + 1, -1, 0, 1, short.MaxValue, short.MaxValue - 1 };
            foreach (short value in values)
            {
                string expectedStringOutput = $"H{value}";
                byte[] expectedBinaryOutput;
                unchecked
                {
                    expectedBinaryOutput = new byte[]
                    {
                        BinaryFormat,
                        JsonBinaryEncoding.TypeMarker.Int16,
                    };
                    expectedBinaryOutput = expectedBinaryOutput.Concat(BitConverter.GetBytes(value)).ToArray();
                }

                JsonToken[] tokensToWrite =
                {
                    JsonToken.Int16(value)
                };

                this.VerifyWriter(tokensToWrite, expectedStringOutput);
                this.VerifyWriter(tokensToWrite, expectedBinaryOutput);
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public void Int32Test()
        {
            int[] values = new int[] { int.MinValue, int.MinValue + 1, -1, 0, 1, int.MaxValue, int.MaxValue - 1 };
            foreach (int value in values)
            {
                string expectedStringOutput = $"L{value}";
                byte[] expectedBinaryOutput;
                unchecked
                {
                    expectedBinaryOutput = new byte[]
                    {
                        BinaryFormat,
                        JsonBinaryEncoding.TypeMarker.Int32,
                    };
                    expectedBinaryOutput = expectedBinaryOutput.Concat(BitConverter.GetBytes(value)).ToArray();
                }

                JsonToken[] tokensToWrite =
                {
                    JsonToken.Int32(value)
                };

                this.VerifyWriter(tokensToWrite, expectedStringOutput);
                this.VerifyWriter(tokensToWrite, expectedBinaryOutput);
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public void Int64Test()
        {
            long[] values = new long[] { long.MinValue, long.MinValue + 1, -1, 0, 1, long.MaxValue, long.MaxValue - 1 };
            foreach (long value in values)
            {
                string expectedStringOutput = $"LL{value}";
                byte[] expectedBinaryOutput;
                unchecked
                {
                    expectedBinaryOutput = new byte[]
                    {
                        BinaryFormat,
                        JsonBinaryEncoding.TypeMarker.Int64,
                    };
                    expectedBinaryOutput = expectedBinaryOutput.Concat(BitConverter.GetBytes(value)).ToArray();
                }

                JsonToken[] tokensToWrite =
                {
                    JsonToken.Int64(value)
                };

                this.VerifyWriter(tokensToWrite, expectedStringOutput);
                this.VerifyWriter(tokensToWrite, expectedBinaryOutput);
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public void UInt32Test()
        {
            uint[] values = new uint[] { uint.MinValue, uint.MinValue + 1, 0, 1, uint.MaxValue, uint.MaxValue - 1 };
            foreach (uint value in values)
            {
                string expectedStringOutput = $"UL{value}";
                byte[] expectedBinaryOutput;
                unchecked
                {
                    expectedBinaryOutput = new byte[]
                    {
                        BinaryFormat,
                        JsonBinaryEncoding.TypeMarker.UInt32,
                    };
                    expectedBinaryOutput = expectedBinaryOutput.Concat(BitConverter.GetBytes(value)).ToArray();
                }

                JsonToken[] tokensToWrite =
                {
                    JsonToken.UInt32(value)
                };

                this.VerifyWriter(tokensToWrite, expectedStringOutput);
                this.VerifyWriter(tokensToWrite, expectedBinaryOutput);
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public void Float32Test()
        {
            float[] values = new float[] { float.MinValue, float.MinValue + 1, 0, 1, float.MaxValue, float.MaxValue - 1 };
            foreach (float value in values)
            {
                string expectedStringOutput = $"S{value.ToString("R", CultureInfo.InvariantCulture)}";
                byte[] expectedBinaryOutput;
                unchecked
                {
                    expectedBinaryOutput = new byte[]
                    {
                        BinaryFormat,
                        JsonBinaryEncoding.TypeMarker.Float32,
                    };
                    expectedBinaryOutput = expectedBinaryOutput.Concat(BitConverter.GetBytes(value)).ToArray();
                }

                JsonToken[] tokensToWrite =
                {
                    JsonToken.Float32(value)
                };

                this.VerifyWriter(tokensToWrite, expectedStringOutput);
                this.VerifyWriter(tokensToWrite, expectedBinaryOutput);
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public void Float64Test()
        {
            double[] values = new double[] { double.MinValue, double.MinValue + 1, 0, 1, double.MaxValue, double.MaxValue - 1 };
            foreach (double value in values)
            {
                string expectedStringOutput = $"D{value.ToString("R", CultureInfo.InvariantCulture)}";
                byte[] expectedBinaryOutput;
                unchecked
                {
                    expectedBinaryOutput = new byte[]
                    {
                        BinaryFormat,
                        JsonBinaryEncoding.TypeMarker.Float64,
                    };
                    expectedBinaryOutput = expectedBinaryOutput.Concat(BitConverter.GetBytes(value)).ToArray();
                }

                JsonToken[] tokensToWrite =
                {
                    JsonToken.Float64(value)
                };

                this.VerifyWriter(tokensToWrite, expectedStringOutput);
                this.VerifyWriter(tokensToWrite, expectedBinaryOutput);
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public void GuidTest()
        {
            Guid[] values = new Guid[] { Guid.Empty, Guid.NewGuid() };
            foreach (Guid value in values)
            {
                string expectedStringOutput = $"G{value.ToString()}";
                byte[] expectedBinaryOutput;
                unchecked
                {
                    expectedBinaryOutput = new byte[]
                    {
                        BinaryFormat,
                        JsonBinaryEncoding.TypeMarker.Guid,
                    };
                    expectedBinaryOutput = expectedBinaryOutput.Concat(value.ToByteArray()).ToArray();
                }

                JsonToken[] tokensToWrite =
                {
                    JsonToken.Guid(value)
                };

                this.VerifyWriter(tokensToWrite, expectedStringOutput);
                this.VerifyWriter(tokensToWrite, expectedBinaryOutput);
            }
        }

        [TestMethod]
        [Owner("brchon")]
        public void BinaryTest()
        {
            {
                // Empty Binary
                string expectedStringOutput = $"B";
                byte[] expectedBinaryOutput;
                unchecked
                {
                    expectedBinaryOutput = new byte[]
                    {
                        BinaryFormat,
                        JsonBinaryEncoding.TypeMarker.Binary1ByteLength,
                        0,
                    };
                }

                JsonToken[] tokensToWrite =
                {
                    JsonToken.Binary(new byte[]{ })
                };

                this.VerifyWriter(tokensToWrite, expectedStringOutput);
                this.VerifyWriter(tokensToWrite, expectedBinaryOutput);
            }

            {
                // Binary 1 Byte Length
                byte[] binary = Enumerable.Range(0, 25).Select(x => (byte)x).ToArray();
                string expectedStringOutput = $"B{Convert.ToBase64String(binary.ToArray())}";
                byte[] expectedBinaryOutput;
                unchecked
                {
                    expectedBinaryOutput = new byte[]
                    {
                        BinaryFormat,
                        JsonBinaryEncoding.TypeMarker.Binary1ByteLength,
                        (byte)binary.Count(),
                    };
                    expectedBinaryOutput = expectedBinaryOutput.Concat(binary).ToArray();
                }

                JsonToken[] tokensToWrite =
                {
                    JsonToken.Binary(binary)
                };

                this.VerifyWriter(tokensToWrite, expectedStringOutput);
                this.VerifyWriter(tokensToWrite, expectedBinaryOutput);
            }
        }
        #endregion

        private void VerifyWriter(JsonToken[] tokensToWrite, string expectedString)
        {
            this.VerifyWriter(tokensToWrite, expectedString, null);
        }

        private void VerifyWriter(JsonToken[] tokensToWrite, Exception expectedException)
        {
            this.VerifyWriter(tokensToWrite, (string)null, expectedException);
        }

        private void VerifyWriter(JsonToken[] tokensToWrite, string expectedString = null, Exception expectedException = null)
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
                    foreach (bool writeAsUtf8String in new bool[] { false, true })
                    {
                        System.Threading.Thread.CurrentThread.CurrentCulture = cultureInfo;

                        // Create through serializtion api
                        IJsonWriter jsonWriter = JsonWriter.Create(JsonSerializationFormat.Text);
                        byte[] expectedOutput = expectedString != null ? Encoding.UTF8.GetBytes(expectedString) : null;
                        this.VerifyWriter(
                            jsonWriter,
                            tokensToWrite,
                            expectedOutput,
                            JsonSerializationFormat.Text,
                            writeAsUtf8String,
                            expectedException);
                    }
                }
            }
            finally
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = defaultCultureInfo;
            }
        }

        private void VerifyWriter(JsonToken[] tokensToWrite, byte[] binaryOutput, Exception expectedException = null)
        {
            foreach (bool writeAsUtf8String in new bool[] { false, true })
            {
                IJsonWriter jsonWriter = JsonWriter.Create(JsonSerializationFormat.Binary);
                this.VerifyWriter(jsonWriter, tokensToWrite, binaryOutput, JsonSerializationFormat.Binary, writeAsUtf8String, expectedException);
            }
        }

        private void VerifyWriter(
            IJsonWriter jsonWriter,
            JsonToken[] tokensToWrite,
            byte[] expectedOutput,
            JsonSerializationFormat jsonSerializationFormat,
            bool writeAsUtf8String,
            Exception expectedException = null)
        {
            Assert.AreEqual(jsonSerializationFormat == JsonSerializationFormat.Text ? 0 : 1, jsonWriter.CurrentLength);
            Assert.AreEqual(jsonWriter.SerializationFormat, jsonSerializationFormat);

            try
            {
                foreach (JsonToken token in tokensToWrite)
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
                            string stringValue = (token as JsonStringToken).Value;
                            if (writeAsUtf8String)
                            {
                                jsonWriter.WriteStringValue(Utf8Span.TranscodeUtf16(stringValue));
                            }
                            else
                            {
                                jsonWriter.WriteStringValue(stringValue);
                            }
                            break;

                        case JsonTokenType.Number:
                            Number64 numberValue = (token as JsonNumberToken).Value;
                            jsonWriter.WriteNumber64Value(numberValue);
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
                            string fieldNameValue = (token as JsonFieldNameToken).Value;
                            if (writeAsUtf8String)
                            {
                                jsonWriter.WriteFieldName(Utf8Span.TranscodeUtf16(fieldNameValue));
                            }
                            else
                            {
                                jsonWriter.WriteFieldName(fieldNameValue);
                            }
                            break;

                        case JsonTokenType.Int8:
                            sbyte int8Value = (token as JsonInt8Token).Value;
                            jsonWriter.WriteInt8Value(int8Value);
                            break;

                        case JsonTokenType.Int16:
                            short int16Value = (token as JsonInt16Token).Value;
                            jsonWriter.WriteInt16Value(int16Value);
                            break;

                        case JsonTokenType.Int32:
                            int int32Value = (token as JsonInt32Token).Value;
                            jsonWriter.WriteInt32Value(int32Value);
                            break;

                        case JsonTokenType.Int64:
                            long int64Value = (token as JsonInt64Token).Value;
                            jsonWriter.WriteInt64Value(int64Value);
                            break;

                        case JsonTokenType.UInt32:
                            uint uint32Value = (token as JsonUInt32Token).Value;
                            jsonWriter.WriteUInt32Value(uint32Value);
                            break;

                        case JsonTokenType.Float32:
                            float float32Value = (token as JsonFloat32Token).Value;
                            jsonWriter.WriteFloat32Value(float32Value);
                            break;

                        case JsonTokenType.Float64:
                            double float64Value = (token as JsonFloat64Token).Value;
                            jsonWriter.WriteFloat64Value(float64Value);
                            break;

                        case JsonTokenType.Guid:
                            Guid guidValue = (token as JsonGuidToken).Value;
                            jsonWriter.WriteGuidValue(guidValue);
                            break;

                        case JsonTokenType.Binary:
                            ReadOnlyMemory<byte> binaryValue = (token as JsonBinaryToken).Value;
                            jsonWriter.WriteBinaryValue(binaryValue.Span);
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
                Assert.IsNotNull(expectedException, $"Got an exception when none was expected: {exception}");
                Assert.AreEqual(expectedException.GetType(), exception.GetType());
            }

            if (expectedException == null)
            {
                byte[] result = jsonWriter.GetResult().ToArray();
                if (jsonSerializationFormat == JsonSerializationFormat.Text)
                {
                    Assert.AreEqual(Encoding.UTF8.GetString(expectedOutput), Encoding.UTF8.GetString(result));
                }
                else
                {
                    Assert.IsTrue(expectedOutput.SequenceEqual(result),
                        string.Format("Expected : {0}, Actual :{1}",
                        string.Join(", ", expectedOutput),
                        string.Join(", ", result)));
                }
            }
        }
    }
}
