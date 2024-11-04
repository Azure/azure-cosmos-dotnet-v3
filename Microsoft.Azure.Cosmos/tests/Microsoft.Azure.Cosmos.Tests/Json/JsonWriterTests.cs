namespace Microsoft.Azure.Cosmos.Tests.Json
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using static Microsoft.Azure.Cosmos.Tests.Json.JsonTestUtils;

    [TestClass]
    public class JsonWriterTests
    {
        private const byte BinaryFormat = 128;

        #region Literals
        [TestMethod]
        [Owner("mayapainter")]
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
        [Owner("mayapainter")]
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
        [Owner("mayapainter")]
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
        [Owner("mayapainter")]
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
        [Owner("mayapainter")]
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
        [Owner("mayapainter")]
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
        [Owner("mayapainter")]
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
        [Owner("mayapainter")]
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
        [Owner("mayapainter")]
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
        [Owner("mayapainter")]
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
        [Owner("mayapainter")]
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
        [Owner("mayapainter")]
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
                JsonBinaryEncoding.TypeMarker.ArrL1,
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
        [Owner("mayapainter")]
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
        [Owner("mayapainter")]
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
        [Owner("mayapainter")]
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
        [Owner("mayapainter")]
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
        [Owner("mayapainter")]
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
        [Owner("mayapainter")]
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
        [Owner("mayapainter")]
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
        [Owner("mayapainter")]
        public void GuidStringsTest()
        {
            {
                // Empty Guid
                string guidString = "00000000-0000-0000-0000-000000000000";
                JsonToken[] tokensToWrite =
                {
                    JsonToken.String(guidString)
                };

                {
                    string stringPayload = $"\"{guidString}\"";
                    this.VerifyWriter(tokensToWrite, stringPayload);
                }

                {
                    byte[] compressedBinaryPayload =
                    {
                    BinaryFormat,
                    JsonBinaryEncoding.TypeMarker.LowercaseGuidString,
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                };

                    this.VerifyWriter(tokensToWrite, compressedBinaryPayload);
                }
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
        [Owner("mayapainter")]
        public void ReferenceStringsTest()
        {
            {
                // 1 byte reference string
                string stringValue = "hello";
                JsonToken[] tokensToWrite =
                {
                    JsonToken.ArrayStart(),
                    JsonToken.String(stringValue),
                    JsonToken.String(stringValue),
                    JsonToken.ArrayEnd()
                };

                string stringPayload = "[\"hello\",\"hello\"]";
                this.VerifyWriter(tokensToWrite, stringPayload);

                {
                    byte[] binaryPayload =
                    {
                    BinaryFormat,
                    JsonBinaryEncoding.TypeMarker.ArrL1,
                    8,
                    (byte)(JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + "hello".Length),
                    (byte)'h', (byte)'e', (byte)'l', (byte)'l', (byte)'o',
                    JsonBinaryEncoding.TypeMarker.ReferenceString1ByteOffset,
                    3,
                };

                    this.VerifyWriter(tokensToWrite, binaryPayload);
                }
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
                    0x80, 0xEB, 0xD2, 0x02, 0x89, 0x70, 0x72, 0x6F,
                    0x70, 0x65, 0x72, 0x74, 0x79, 0x31, 0x86, 0x76,
                    0x61, 0x6C, 0x75, 0x65, 0x31, 0x89, 0x70, 0x72,
                    0x6F, 0x70, 0x65, 0x72, 0x74, 0x79, 0x32, 0xC3,
                    0x0E, 0x89, 0x70, 0x72, 0x6F, 0x70, 0x65, 0x72,
                    0x74, 0x79, 0x33, 0xE5, 0x64, 0x16, 0x89, 0x70,
                    0x72, 0x6F, 0x70, 0x65, 0x72, 0x74, 0x79, 0x30,
                    0xC3, 0x04, 0xC3, 0x15, 0xC3, 0x21, 0x89, 0x70,
                    0x72, 0x6F, 0x70, 0x65, 0x72, 0x74, 0x79, 0x34,
                    0x89, 0x70, 0x72, 0x6F, 0x70, 0x65, 0x72, 0x74,
                    0x79, 0x35, 0x89, 0x70, 0x72, 0x6F, 0x70, 0x65,
                    0x72, 0x74, 0x79, 0x36, 0x89, 0x70, 0x72, 0x6F,
                    0x70, 0x65, 0x72, 0x74, 0x79, 0x37, 0x89, 0x70,
                    0x72, 0x6F, 0x70, 0x65, 0x72, 0x74, 0x79, 0x38,
                    0x89, 0x70, 0x72, 0x6F, 0x70, 0x65, 0x72, 0x74,
                    0x79, 0x39, 0xC3, 0x0E, 0xC3, 0x0E, 0xC3, 0x0E,
                    0xC3, 0x70, 0xC3, 0x66, 0xC3, 0x5C, 0xC3, 0x48,
                    0xC3, 0x3E, 0xC3, 0x21, 0xC3, 0x15, 0xC3, 0x04,
                    0xC3, 0x2E, 0xC3, 0x21, 0xC3, 0x0E, 0xC3, 0x3E,
                    0xC3, 0x0E, 0xC3, 0x48, 0xE5, 0xB8, 0x16, 0x8E,
                    0x53, 0x74, 0x72, 0x69, 0x6E, 0x67, 0x56, 0x61,
                    0x6C, 0x75, 0x65, 0x5F, 0x5F, 0x30, 0x8E, 0x53,
                    0x74, 0x72, 0x69, 0x6E, 0x67, 0x56, 0x61, 0x6C,
                    0x75, 0x65, 0x5F, 0x5F, 0x31, 0x8E, 0x53, 0x74,
                    0x72, 0x69, 0x6E, 0x67, 0x56, 0x61, 0x6C, 0x75,
                    0x65, 0x5F, 0x5F, 0x32, 0x8E, 0x53, 0x74, 0x72,
                    0x69, 0x6E, 0x67, 0x56, 0x61, 0x6C, 0x75, 0x65,
                    0x5F, 0x5F, 0x33, 0x8E, 0x53, 0x74, 0x72, 0x69,
                    0x6E, 0x67, 0x56, 0x61, 0x6C, 0x75, 0x65, 0x5F,
                    0x5F, 0x34, 0x8E, 0x53, 0x74, 0x72, 0x69, 0x6E,
                    0x67, 0x56, 0x61, 0x6C, 0x75, 0x65, 0x5F, 0x5F,
                    0x35, 0x8E, 0x53, 0x74, 0x72, 0x69, 0x6E, 0x67,
                    0x56, 0x61, 0x6C, 0x75, 0x65, 0x5F, 0x5F, 0x36,
                    0x8E, 0x53, 0x74, 0x72, 0x69, 0x6E, 0x67, 0x56,
                    0x61, 0x6C, 0x75, 0x65, 0x5F, 0x5F, 0x37, 0x8E,
                    0x53, 0x74, 0x72, 0x69, 0x6E, 0x67, 0x56, 0x61,
                    0x6C, 0x75, 0x65, 0x5F, 0x5F, 0x38, 0x8E, 0x53,
                    0x74, 0x72, 0x69, 0x6E, 0x67, 0x56, 0x61, 0x6C,
                    0x75, 0x65, 0x5F, 0x5F, 0x39, 0x86, 0x76, 0x61,
                    0x6C, 0x75, 0x65, 0x32, 0xC4, 0x35, 0x01, 0xC4,
                    0x35, 0x01, 0xC4, 0x26, 0x01, 0xC4, 0x17, 0x01,
                    0xC4, 0x08, 0x01, 0xC3, 0xEA, 0xC3, 0xDB, 0xC3,
                    0xCC, 0xC3, 0xBD, 0xC3, 0xAE, 0xC3, 0x9F, 0xC3,
                    0x48, 0xC4, 0x35, 0x01, 0xC3, 0x52, 0xC4, 0x35,
                    0x01, 0xC3, 0x5C, 0xE5, 0xAA, 0x16, 0x8C, 0x54,
                    0x65, 0x78, 0x74, 0x56, 0x61, 0x6C, 0x75, 0x65,
                    0x5F, 0x5F, 0x30, 0x8C, 0x54, 0x65, 0x78, 0x74,
                    0x56, 0x61, 0x6C, 0x75, 0x65, 0x5F, 0x5F, 0x31,
                    0x8C, 0x54, 0x65, 0x78, 0x74, 0x56, 0x61, 0x6C,
                    0x75, 0x65, 0x5F, 0x5F, 0x32, 0x8C, 0x54, 0x65,
                    0x78, 0x74, 0x56, 0x61, 0x6C, 0x75, 0x65, 0x5F,
                    0x5F, 0x33, 0x8C, 0x54, 0x65, 0x78, 0x74, 0x56,
                    0x61, 0x6C, 0x75, 0x65, 0x5F, 0x5F, 0x34, 0x8C,
                    0x54, 0x65, 0x78, 0x74, 0x56, 0x61, 0x6C, 0x75,
                    0x65, 0x5F, 0x5F, 0x35, 0x8C, 0x54, 0x65, 0x78,
                    0x74, 0x56, 0x61, 0x6C, 0x75, 0x65, 0x5F, 0x5F,
                    0x36, 0x8C, 0x54, 0x65, 0x78, 0x74, 0x56, 0x61,
                    0x6C, 0x75, 0x65, 0x5F, 0x5F, 0x37, 0x8C, 0x54,
                    0x65, 0x78, 0x74, 0x56, 0x61, 0x6C, 0x75, 0x65,
                    0x5F, 0x5F, 0x38, 0x8C, 0x54, 0x65, 0x78, 0x74,
                    0x56, 0x61, 0x6C, 0x75, 0x65, 0x5F, 0x5F, 0x39,
                    0x86, 0x76, 0x61, 0x6C, 0x75, 0x65, 0x33, 0xC4,
                    0xE8, 0x01, 0xC4, 0xE8, 0x01, 0xC4, 0xDB, 0x01,
                    0xC4, 0xCE, 0x01, 0xC4, 0xC1, 0x01, 0xC4, 0xA7,
                    0x01, 0xC4, 0x9A, 0x01, 0xC4, 0x8D, 0x01, 0xC4,
                    0x80, 0x01, 0xC4, 0x73, 0x01, 0xC4, 0x66, 0x01,
                    0xC3, 0x66, 0xC4, 0xE8, 0x01, 0xC3, 0x70, 0xC4,
                    0xE8, 0x01, 0xC3, 0x2E, 0xE5, 0xA0, 0x16, 0x8B,
                    0x42, 0x72, 0x6F, 0x77, 0x6E, 0x44, 0x6F, 0x67,
                    0x5F, 0x5F, 0x30, 0x8B, 0x42, 0x72, 0x6F, 0x77,
                    0x6E, 0x44, 0x6F, 0x67, 0x5F, 0x5F, 0x31, 0x8B,
                    0x42, 0x72, 0x6F, 0x77, 0x6E, 0x44, 0x6F, 0x67,
                    0x5F, 0x5F, 0x32, 0x8B, 0x42, 0x72, 0x6F, 0x77,
                    0x6E, 0x44, 0x6F, 0x67, 0x5F, 0x5F, 0x33, 0x8B,
                    0x42, 0x72, 0x6F, 0x77, 0x6E, 0x44, 0x6F, 0x67,
                    0x5F, 0x5F, 0x34, 0x8B, 0x42, 0x72, 0x6F, 0x77,
                    0x6E, 0x44, 0x6F, 0x67, 0x5F, 0x5F, 0x35, 0x8B,
                    0x42, 0x72, 0x6F, 0x77, 0x6E, 0x44, 0x6F, 0x67,
                    0x5F, 0x5F, 0x36, 0x8B, 0x42, 0x72, 0x6F, 0x77,
                    0x6E, 0x44, 0x6F, 0x67, 0x5F, 0x5F, 0x37, 0x8B,
                    0x42, 0x72, 0x6F, 0x77, 0x6E, 0x44, 0x6F, 0x67,
                    0x5F, 0x5F, 0x38, 0x8B, 0x42, 0x72, 0x6F, 0x77,
                    0x6E, 0x44, 0x6F, 0x67, 0x5F, 0x5F, 0x39, 0x86,
                    0x76, 0x61, 0x6C, 0x75, 0x65, 0x34, 0xC4, 0x97,
                    0x02, 0xC4, 0x97, 0x02, 0xC4, 0x8B, 0x02, 0xC4,
                    0x7F, 0x02, 0xC4, 0x73, 0x02, 0xC4, 0x5B, 0x02,
                    0xC4, 0x4F, 0x02, 0xC4, 0x43, 0x02, 0xC4, 0x37,
                    0x02, 0xC4, 0x2B, 0x02, 0xC4, 0x1F, 0x02, 0x89,
                    0x70, 0x72, 0x6F, 0x70, 0x65, 0x72, 0x74, 0x79,
                    0x41, 0x86, 0x76, 0x61, 0x6C, 0x75, 0x65, 0x41,
                    0xC4, 0xBF, 0x02, 0xC4, 0xC9, 0x02
                };

                this.VerifyWriter(tokensToWrite, binaryPayload);
            }
        }
        #endregion

        #region Array
        [TestMethod]
        [Owner("mayapainter")]
        public void EmptyArrayTest()
        {
            string expectedString = "[]";
            byte[] binaryOutput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.Arr0
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
        [Owner("mayapainter")]
        public void SingleItemArrayTest()
        {
            string expectedString = "[true]";
            byte[] binaryOutput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.Arr1,
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
        [Owner("mayapainter")]
        public void IntArrayTest()
        {
            string expectedString = "[-2,-1,0,1,2]";
            List<byte[]> binaryOutputBuilder = new List<byte[]>
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
        [Owner("mayapainter")]
        public void NumberArrayTest()
        {
            string expectedString = "[15,22,0.1,-0.073,7.70001E+91]";
            List<byte[]> binaryOutputBuilder = new List<byte[]>
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
        [Owner("mayapainter")]
        public void BooleanArrayTest()
        {
            string expectedString = "[true,false]";
            byte[] binaryOutput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.ArrL1,
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
        [Owner("mayapainter")]
        public void BooleanLargeArrayTest()
        {
            string expectedString = "[true,false,true,false,true,false,false,false,true,false,true,false,true,true,true,false,true]";
            byte[] binaryOutput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.ArrLC1,
                // length
                17,
                // count
                17,
                JsonBinaryEncoding.TypeMarker.True,
                JsonBinaryEncoding.TypeMarker.False,
                JsonBinaryEncoding.TypeMarker.True,
                JsonBinaryEncoding.TypeMarker.False,
                JsonBinaryEncoding.TypeMarker.True,
                JsonBinaryEncoding.TypeMarker.False,
                JsonBinaryEncoding.TypeMarker.False,
                JsonBinaryEncoding.TypeMarker.False,
                JsonBinaryEncoding.TypeMarker.True,
                JsonBinaryEncoding.TypeMarker.False,
                JsonBinaryEncoding.TypeMarker.True,
                JsonBinaryEncoding.TypeMarker.False,
                JsonBinaryEncoding.TypeMarker.True,
                JsonBinaryEncoding.TypeMarker.True,
                JsonBinaryEncoding.TypeMarker.True,
                JsonBinaryEncoding.TypeMarker.False,
                JsonBinaryEncoding.TypeMarker.True,
            };

            JsonToken[] tokensToWrite =
            {
                JsonToken.ArrayStart(),
                JsonToken.Boolean(true),
                JsonToken.Boolean(false),
                JsonToken.Boolean(true),
                JsonToken.Boolean(false),
                JsonToken.Boolean(true),
                JsonToken.Boolean(false),
                JsonToken.Boolean(false),
                JsonToken.Boolean(false),
                JsonToken.Boolean(true),
                JsonToken.Boolean(false),
                JsonToken.Boolean(true),
                JsonToken.Boolean(false),
                JsonToken.Boolean(true),
                JsonToken.Boolean(true),
                JsonToken.Boolean(true),
                JsonToken.Boolean(false),
                JsonToken.Boolean(true),
                JsonToken.ArrayEnd(),
            };

            this.VerifyWriter(tokensToWrite, expectedString);
            this.VerifyWriter(tokensToWrite, binaryOutput);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void StringArrayTest()
        {
            string expectedString = @"[""Hello"",""World"",""Bye""]";

            List<byte[]> binaryOutputBuilder = new List<byte[]>
            {
                new byte[] { BinaryFormat, JsonBinaryEncoding.TypeMarker.ArrL1 }
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
        [Owner("mayapainter")]
        public void StringLargeArrayTest()
        {
            int stringCount = 20;

            string expectedString = @"[";
            for (int index = 0; index < stringCount; index++)
            {
                if (index == 0)
                {
                    expectedString += @"""Hello0""";
                }
                else
                {
                    expectedString += @",""Hello" + index + @"""";
                }

            }
            expectedString += "]";

            List<byte[]> binaryOutputBuilder = new List<byte[]>
            {
                new byte[] { BinaryFormat, JsonBinaryEncoding.TypeMarker.ArrLC1 }
            };

            List<byte[]> strings = new List<byte[]>();

            for (int index = 0; index < stringCount; index++)
            {
                string value = "Hello" + index;
                strings.Add(new byte[] { (byte)(JsonBinaryEncoding.TypeMarker.EncodedStringLengthMin + value.Length) });
                strings.Add(Encoding.UTF8.GetBytes(value));
            }
            byte[] stringBytes = strings.SelectMany(x => x).ToArray();

            binaryOutputBuilder.Add(new byte[] { (byte)stringBytes.Length });
            binaryOutputBuilder.Add(new byte[] { (byte)stringCount });
            binaryOutputBuilder.Add(stringBytes);
            byte[] binaryOutput = binaryOutputBuilder.SelectMany(x => x).ToArray();

            JsonToken[] tokensToWrite = new JsonToken[stringCount + 2];
            tokensToWrite[0] = JsonToken.ArrayStart();
            for (int index = 1; index < stringCount + 1; index++)
            {
                tokensToWrite[index] = JsonToken.String("Hello" + (index - 1));
            }
            tokensToWrite[stringCount + 1] = JsonToken.ArrayEnd();

            this.VerifyWriter(tokensToWrite, expectedString);
            this.VerifyWriter(tokensToWrite, binaryOutput);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void NullArrayTest()
        {
            string expectedString = "[null,null,null]";
            byte[] binaryOutput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.ArrL1,
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
        [Owner("mayapainter")]
        public void NullLargeArrayTest()
        {
            int nullCount = 300;

            string expectedString = "[";
            for (int index = 0; index < nullCount; index++)
            {
                expectedString += (index == 0) ? "null" : ",null";
            }
            expectedString += "]";

            List<byte[]> binaryOutputBuilder = new List<byte[]>
            {
                new byte[] { BinaryFormat, JsonBinaryEncoding.TypeMarker.ArrLC2 },
                // length
                BitConverter.GetBytes((ushort)nullCount),
                // count
                BitConverter.GetBytes((ushort)nullCount),
            };

            byte[] elementsBytes = new byte[nullCount];

            for (int index = 0; index < nullCount; index++)
            {
                elementsBytes[index] = JsonBinaryEncoding.TypeMarker.Null;
            }

            binaryOutputBuilder.Add(elementsBytes);
            byte[] binaryOutput = binaryOutputBuilder.SelectMany(x => x).ToArray();

            JsonToken[] tokensToWrite = new JsonToken[nullCount + 2];
            tokensToWrite[0] = JsonToken.ArrayStart();
            for (int index = 1; index < nullCount + 1; index++)
            {
                tokensToWrite[index] = JsonToken.Null();
            }
            tokensToWrite[nullCount + 1] = JsonToken.ArrayEnd();

            this.VerifyWriter(tokensToWrite, expectedString);
            this.VerifyWriter(tokensToWrite, binaryOutput);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void ObjectArrayTest()
        {
            string expectedString = "[{},{}]";
            byte[] binaryOutput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.ArrL1,
                // length
                2,
                JsonBinaryEncoding.TypeMarker.Obj0,
                JsonBinaryEncoding.TypeMarker.Obj0,
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
        [Owner("mayapainter")]
        public void AllPrimitiveArrayTest()
        {
            string expectedString = "[0,0,-1,-1.1,1,2,\"hello\",null,true,false]";
            List<byte[]> binaryOutputBuilder = new List<byte[]>
            {
                new byte[] { BinaryFormat, JsonBinaryEncoding.TypeMarker.ArrL1 }
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
        [Owner("mayapainter")]
        public void NestedArrayTest()
        {
            string expectedString = "[[],[]]";
            byte[] binaryOutput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.ArrL1,
                // length
                2,
                JsonBinaryEncoding.TypeMarker.Arr0,
                JsonBinaryEncoding.TypeMarker.Arr0,
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
        [Owner("mayapainter")]
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
                new byte[] { BinaryFormat, JsonBinaryEncoding.TypeMarker.ArrL1 }
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

        [TestMethod]
        [Owner("sboshra")]
        public void UniformNumberArrayTest1()
        {
            // -------------------------
            // Uniform number arrays
            // -------------------------

            // Int8 Array
            {
                JsonToken[] tokensToWrite =
                {
                    JsonToken.ArrayStart(),
                        JsonToken.Number(0),
                        JsonToken.Number(-5),
                        JsonToken.Number(5),
                        JsonToken.Number(32),
                        JsonToken.Number(-32),
                        JsonToken.Number(sbyte.MaxValue),
                        JsonToken.Number(sbyte.MinValue),
                    JsonToken.ArrayEnd(),
                };

                string[] expectedText =
                {
                    @"[0,-5,5,32,-32,127,-128]"
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E2 0F 00 C9 FB FF 05  C8 20 C9 E0 FF C8 7F C9",
                    "00000010  80 FF",
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 F0 D8 07 00 FB 05 20  E0 7F 80",
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }

            // UInt8 Array
            {
                JsonToken[] tokensToWrite =
                {
                    JsonToken.ArrayStart(),
                        JsonToken.Number(byte.MaxValue),
                        JsonToken.Number(128),
                        JsonToken.Number(36),
                        JsonToken.Number(97),
                        JsonToken.Number(201),
                        JsonToken.Number(byte.MaxValue),
                        JsonToken.Number(byte.MinValue),
                        JsonToken.Number(30),
                        JsonToken.Number(1),
                    JsonToken.ArrayEnd(),
                };

                string[] expectedText =
                {
                    @"[255,128,36,97,201,255,0,30,1]",
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E2 0F C8 FF C8 80 C8  24 C8 61 C8 C9 C8 FF 00",
                    "00000010  1E 01",
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 F0 D7 09 FF 80 24 61  C9 FF 00 1E 01",
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }

            // Int16 Array
            {
                JsonToken[] tokensToWrite =
                {
                    JsonToken.ArrayStart(),
                        JsonToken.Number(short.MaxValue / 4),
                        JsonToken.Number(byte.MaxValue),
                        JsonToken.Number(5),
                        JsonToken.Number(32),
                        JsonToken.Number(byte.MinValue),
                        JsonToken.Number(short.MaxValue),
                        JsonToken.Number(short.MinValue),
                    JsonToken.ArrayEnd(),
                };

                string[] expectedText =
                {
                    @"[8191,255,5,32,0,32767,-32768]",
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E2 0F C9 FF 1F C8 FF  05 C8 20 00 C9 FF 7F C9",
                    "00000010  00 80",
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 F0 D9 07 FF 1F FF 00  05 00 20 00 00 00 FF 7F",
                    "00000010  00 80",
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }

            // Int32 Array
            {
                JsonToken[] tokensToWrite =
                {
                    JsonToken.ArrayStart(),
                        JsonToken.Number(int.MaxValue - 1),
                        JsonToken.Number(byte.MaxValue),
                        JsonToken.Number(27),
                        JsonToken.Number(sbyte.MaxValue - 4),
                        JsonToken.Number(byte.MinValue - 28),
                        JsonToken.Number(short.MaxValue + 1),
                        JsonToken.Number(int.MinValue),
                        JsonToken.Number(int.MaxValue),
                    JsonToken.ArrayEnd(),
                };

                string[] expectedText =
                {
                    @"[2147483646,255,27,123,-28,32768,-2147483648,2147483647]",
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E2 1C CA FE FF FF 7F  C8 FF 1B C8 7B C9 E4 FF",
                    "00000010  CA 00 80 00 00 CA 00 00  00 80 CA FF FF FF 7F",
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 E2 1C CA FE FF FF 7F  C8 FF 1B C8 7B C9 E4 FF",
                    "00000010  CA 00 80 00 00 CA 00 00  00 80 CA FF FF FF 7F",
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }

            // Int64 Array
            {
                JsonToken[] tokensToWrite =
                {
                    JsonToken.ArrayStart(),
                        JsonToken.Number(int.MaxValue + 1L),
                        JsonToken.Number(long.MaxValue / 4),
                        JsonToken.Number(byte.MinValue),
                        JsonToken.Number(sbyte.MaxValue),
                        JsonToken.Number(byte.MinValue),
                        JsonToken.Number(int.MinValue),
                        JsonToken.Number(uint.MaxValue),
                        JsonToken.Number(long.MinValue),
                        JsonToken.Number(int.MaxValue),
                    JsonToken.ArrayEnd(),
                };

                string[] expectedText =
                {
                    @"[2147483648,2305843009213693951,0,127,0,-2147483648,4294967295,-9223372036854775808,2147483647]",
                };

                string[] expectedBinary =
                {
                    "00000000  80 E2 32 CB 00 00 00 80  00 00 00 00 CB FF FF FF",
                    "00000010  FF FF FF FF 1F 00 C8 7F  00 CA 00 00 00 80 CB FF",
                    "00000020  FF FF FF 00 00 00 00 CB  00 00 00 00 00 00 00 80",
                    "00000030  CA FF FF FF 7F",
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary, JsonWriteOptions.EnableNumberArrays);
            }

            // Float64 Array
            {
                JsonToken[] tokensToWrite =
                {
                    JsonToken.ArrayStart(),
                        JsonToken.Number(1.1),
                        JsonToken.Number(3.3),
                        JsonToken.Number(-2.2),
                        JsonToken.Number(-4.4),
                        JsonToken.Number(0),
                        JsonToken.Number(5.5),
                        JsonToken.Number(-6.6),
                    JsonToken.ArrayEnd(),
                };

                string[] expectedText =
                {
                    @"[1.1,3.3,-2.2,-4.4,0,5.5,-6.6]",
                };

                string[] expectedBinary =
                {
                    "00000000  80 E2 37 CC 9A 99 99 99  99 99 F1 3F CC 66 66 66",
                    "00000010  66 66 66 0A 40 CC 9A 99  99 99 99 99 01 C0 CC 9A",
                    "00000020  99 99 99 99 99 11 C0 00  CC 00 00 00 00 00 00 16",
                    "00000030  40 CC 66 66 66 66 66 66  1A C0",
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary, JsonWriteOptions.EnableNumberArrays);
            }
        }

        [TestMethod]
        [Owner("sboshra")]
        public void UniformNumberArrayTest2()
        {
            // -------------------------
            // WriteNumberArray
            // -------------------------

            // Empty arrays
            {
                JsonToken[] tokensToWrite =
                {
                    JsonToken.ArrayStart(),
                        JsonToken.Int8NumberArray(Int8Array()),
                        JsonToken.UInt8NumberArray(UInt8Array()),
                        JsonToken.Int16NumberArray(Int16Array()),
                        JsonToken.Int32NumberArray(Int32Array()),
                        JsonToken.Int64NumberArray(Int64Array()),
                        JsonToken.Float32NumberArray(Float32Array()),
                        JsonToken.Float64NumberArray(Float64Array()),
                    JsonToken.ArrayEnd(),
                };

                string[] expectedText =
                {
                    @"[[],[],[],[],[],[],[]]"
                };

                string[] expectedBinary =
                {
                    "00000000  80 E2 07 E0 E0 E0 E0 E0  E0 E0"
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary, JsonWriteOptions.EnableNumberArrays);
            }

            // Single-item arrays
            {
                JsonToken[] tokensToWrite =
                {
                    JsonToken.ArrayStart(),
                        JsonToken.Int8NumberArray(Int8Array(-1)),
                        JsonToken.UInt8NumberArray(UInt8Array(5)),
                        JsonToken.Int16NumberArray(Int16Array(16233)),
                        JsonToken.Int32NumberArray(Int32Array(-16)),
                        JsonToken.Int64NumberArray(Int64Array(63)),
                        JsonToken.Float32NumberArray(Float32Array(1.1f)),
                        JsonToken.Float64NumberArray(Float64Array(-7.7)),
                    JsonToken.ArrayEnd()
                };

                string[] expectedText =
                {
                    @"[[-1],[5],[16233],[-16],[63],[1.100000023841858],[-7.7]]"
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E2 25 E1 C9 FF FF E1  05 E1 C9 69 3F E1 C9 F0",
                    "00000010  FF E1 C8 3F E1 CC 00 00  00 A0 99 99 F1 3F E1 CC",
                    "00000020  CD CC CC CC CC CC 1E C0"
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 E2 31 F0 D8 01 FF F0  D7 01 05 F0 D9 01 69 3F",
                    "00000010  F0 DA 01 F0 FF FF FF F0  DB 01 3F 00 00 00 00 00",
                    "00000020  00 00 F0 CD 01 CD CC 8C  3F F0 CE 01 CD CC CC CC",
                    "00000030  CC CC 1E C0"
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }

            // Two-item arrays
            {
                JsonToken[] tokensToWrite =
                {
                    JsonToken.ArrayStart(),
                        JsonToken.Int8NumberArray(Int8Array(-1, 1)),
                        JsonToken.UInt8NumberArray(UInt8Array(0, 0)),
                        JsonToken.Int16NumberArray(Int16Array(1600, -1600)),
                        JsonToken.Int32NumberArray(Int32Array(-16, 16)),
                        JsonToken.Int64NumberArray(Int64Array(63, -63)),
                        JsonToken.Float32NumberArray(Float32Array(1.1f, -1.1f)),
                        JsonToken.Float64NumberArray(Float64Array(-7.7, 7.7)),
                    JsonToken.ArrayEnd()
                };

                string[] expectedText =
                {
                    @"[[-1,1],[0,0],[1600,-1600],[-16,16],[63,-63],[1.100000023841858,-1.100000023841858],[-7.7,7.7]]"
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E2 47 E2 04 C9 FF FF  01 E2 02 00 00 E2 06 C9",
                    "00000010  40 06 C9 C0 F9 E2 04 C9  F0 FF 10 E2 05 C8 3F C9",
                    "00000020  C1 FF E2 12 CC 00 00 00  A0 99 99 F1 3F CC 00 00",
                    "00000030  00 A0 99 99 F1 BF E2 12  CC CD CC CC CC CC CC 1E",
                    "00000040  C0 CC CD CC CC CC CC CC  1E 40"
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 E2 4D F0 D8 02 FF 01  F0 D7 02 00 00 F0 D9 02",
                    "00000010  40 06 C0 F9 F0 DA 02 F0  FF FF FF 10 00 00 00 F0",
                    "00000020  DB 02 3F 00 00 00 00 00  00 00 C1 FF FF FF FF FF",
                    "00000030  FF FF F0 CD 02 CD CC 8C  3F CD CC 8C BF F0 CE 02",
                    "00000040  CD CC CC CC CC CC 1E C0  CD CC CC CC CC CC 1E 40",
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }
        }

        [TestMethod]
        [Owner("sboshra")]
        public void UniformNumberArrayTest3()
        {
            // -------------------------
            // INT8 number array
            // -------------------------

            sbyte[] values =
            {
                sbyte.MinValue,
                sbyte.MinValue + 1,
                sbyte.MinValue + 10,
                -10,
                -1,
                0,
                1,
                10,
                sbyte.MaxValue - 10,
                sbyte.MaxValue - 1,
                sbyte.MaxValue,
            };

            JsonToken[] tokensToWrite =
            {
                JsonToken.Int8NumberArray(values)
            };

            string[] expectedText =
            {
                "[-128,-127,-118,-10,-1,0,1,10,117,126,127]"
            };

            string[] expectedBinary1 =
            {
                "00000000  80 E2 18 C9 80 FF C9 81  FF C9 8A FF C9 F6 FF C9",
                "00000010  FF FF 00 01 0A C8 75 C8  7E C8 7F"
            };

            string[] expectedBinary2 =
            {
                "00000000  80 F0 D8 0B 80 81 8A F6  FF 00 01 0A 75 7E 7F"
            };

            ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
            ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
        }

        [TestMethod]
        [Owner("sboshra")]
        public void UniformNumberArrayTest4()
        {
            // -------------------------
            // UINT8 number array
            // -------------------------

            byte[] values =
            {
                byte.MaxValue,
                0,
                byte.MaxValue - 1,
                1,
                byte.MaxValue - 2,
                2,
                byte.MaxValue - 3,
                3,
                byte.MaxValue - 4,
                4,
                byte.MaxValue - 5,
                5,
                byte.MaxValue - 6,
                6,
                byte.MaxValue - 7,
                7,
                byte.MaxValue - 8,
                8,
                byte.MaxValue - 9,
                9,
            };

            JsonToken[] tokensToWrite =
            {
                JsonToken.UInt8NumberArray(values)
            };

            string[] expectedText =
            {
                @"[255,0,254,1,253,2,252,3,251,4,250,5,249,6,248,7,247,8,246,9]"
            };

            string[] expectedBinary1 =
            {
                "00000000  80 E5 1E 14 C8 FF 00 C8  FE 01 C8 FD 02 C8 FC 03",
                "00000010  C8 FB 04 C8 FA 05 C8 F9  06 C8 F8 07 C8 F7 08 C8",
                "00000020  F6 09"
            };

            string[] expectedBinary2 =
            {
                "00000000  80 F0 D7 14 FF 00 FE 01  FD 02 FC 03 FB 04 FA 05",
                "00000010  F9 06 F8 07 F7 08 F6 09"
            };

            ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
            ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
        }

        [TestMethod]
        [Owner("sboshra")]
        public void UniformNumberArrayTest5()
        {
            // -------------------------
            // INT16 number array
            // -------------------------

            short[] values = {
                short.MinValue,
                short.MaxValue,
                short.MinValue,
                sbyte.MaxValue,
                sbyte.MinValue,
                byte.MaxValue,
                0,
                -1000,
                1000,
                short.MaxValue,
                short.MaxValue,
                0,
                1500,
                0,
                0,
                1500,
                1500,
                1500,
                short.MinValue,
                short.MinValue,
            };

            JsonToken[] tokensToWrite =
            {
                JsonToken.Int16NumberArray(values)
            };

            string[] expectedText =
            {
                @"[-32768,32767,-32768,127,-128,255,0,-1000,1000,32767,32767,0,1500,0,0,1500,1500,1500,-32768,-32768]"
            };

            string[] expectedBinary1 =
            {
                "00000000  80 E5 32 14 C9 00 80 C9  FF 7F C9 00 80 C8 7F C9",
                "00000010  80 FF C8 FF 00 C9 18 FC  C9 E8 03 C9 FF 7F C9 FF",
                "00000020  7F 00 C9 DC 05 00 00 C9  DC 05 C9 DC 05 C9 DC 05",
                "00000030  C9 00 80 C9 00 80"
            };

            string[] expectedBinary2 =
            {
                "00000000  80 F0 D9 14 00 80 FF 7F  00 80 7F 00 80 FF FF 00",
                "00000010  00 00 18 FC E8 03 FF 7F  FF 7F 00 00 DC 05 00 00",
                "00000020  00 00 DC 05 DC 05 DC 05  00 80 00 80"
            };

            ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
            ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
        }

        [TestMethod]
        [Owner("sboshra")]
        public void UniformNumberArrayTest6()
        {
            // -------------------------
            // INT32 number array
            // -------------------------

            // Case 1
            {
                int[] values = {
                    short.MinValue,
                    short.MaxValue,
                    0,
                    int.MinValue,
                    int.MaxValue,
                    0,
                    sbyte.MaxValue,
                    sbyte.MinValue,
                    0,
                    -1000,
                    1000,
                    0,
                    short.MaxValue,
                    short.MaxValue,
                    0,
                    1500,
                    -1500,
                    0,
                    22222222,
                    -22222222,
                    0,
                    int.MinValue,
                    int.MinValue,
                    0,
                    int.MaxValue,
                    int.MaxValue,
                };

                JsonToken[] tokensToWrite =
                {
                    JsonToken.Int32NumberArray(values)
                };

                string[] expectedText =
                {
                    @"[-32768,32767,0,-2147483648,2147483647,0,127,-128,0,-1000,1000,0,32767,32767,0,1500,-1500,0,22222222",
                    @",-22222222,0,-2147483648,-2147483648,0,2147483647,2147483647]",
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E5 4D 1A C9 00 80 C9  FF 7F 00 CA 00 00 00 80",
                    "00000010  CA FF FF FF 7F 00 C8 7F  C9 80 FF 00 C9 18 FC C9",
                    "00000020  E8 03 00 C9 FF 7F C9 FF  7F 00 C9 DC 05 C9 24 FA",
                    "00000030  00 CA 8E 15 53 01 CA 72  EA AC FE 00 CA 00 00 00",
                    "00000040  80 CA 00 00 00 80 00 CA  FF FF FF 7F CA FF FF FF",
                    "00000050  7F"
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 F0 DA 1A 00 80 FF FF  FF 7F 00 00 00 00 00 00",
                    "00000010  00 00 00 80 FF FF FF 7F  00 00 00 00 7F 00 00 00",
                    "00000020  80 FF FF FF 00 00 00 00  18 FC FF FF E8 03 00 00",
                    "00000030  00 00 00 00 FF 7F 00 00  FF 7F 00 00 00 00 00 00",
                    "00000040  DC 05 00 00 24 FA FF FF  00 00 00 00 8E 15 53 01",
                    "00000050  72 EA AC FE 00 00 00 00  00 00 00 80 00 00 00 80",
                    "00000060  00 00 00 00 FF FF FF 7F  FF FF FF 7F"
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }

            // Case 2
            {
                int[] values = {
                    (int)short.MinValue + 1,
                    (int)short.MaxValue - 1,
                    -1,
                    -1,
                    int.MinValue,
                    int.MaxValue,
                    int.MinValue,
                    int.MaxValue,
                    -1000,
                    1000,
                    (int)short.MinValue + 1,
                    (int)short.MaxValue - 1,
                    150000,
                    -150000,
                    22222222,
                    -22222222,
                    int.MinValue,
                    int.MinValue,
                    int.MaxValue,
                    int.MaxValue,
                };

                JsonToken[] tokensToWrite =
                {
                    JsonToken.Int32NumberArray(values)
                };

                string[] expectedText =
                {
                    @"[-32767,32766,-1,-1,-2147483648,2147483647,-2147483648,2147483647,-1000,1000,-32767,32766,150000,-15",
                    @"0000,22222222,-22222222,-2147483648,-2147483648,2147483647,2147483647]",
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E5 54 14 C9 01 80 C9  FE 7F C9 FF FF C9 FF FF",
                    "00000010  CA 00 00 00 80 CA FF FF  FF 7F CA 00 00 00 80 CA",
                    "00000020  FF FF FF 7F C9 18 FC C9  E8 03 C9 01 80 C9 FE 7F",
                    "00000030  CA F0 49 02 00 CA 10 B6  FD FF CA 8E 15 53 01 CA",
                    "00000040  72 EA AC FE CA 00 00 00  80 CA 00 00 00 80 CA FF",
                    "00000050  FF FF 7F CA FF FF FF 7F"
                };

                string[] expectedBinary2 =
                {
                "00000000  80 F0 DA 14 01 80 FF FF  FE 7F 00 00 FF FF FF FF",
                "00000010  FF FF FF FF 00 00 00 80  FF FF FF 7F 00 00 00 80",
                "00000020  FF FF FF 7F 18 FC FF FF  E8 03 00 00 01 80 FF FF",
                "00000030  FE 7F 00 00 F0 49 02 00  10 B6 FD FF 8E 15 53 01",
                "00000040  72 EA AC FE 00 00 00 80  00 00 00 80 FF FF FF 7F",
                "00000050  FF FF FF 7F"
            };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }
        }

        [TestMethod]
        [Owner("sboshra")]
        public void UniformNumberArrayTest7()
        {
            // -------------------------
            // INT64 number array
            // -------------------------

            // Case 1
            {
                long[] values = {
                    long.MaxValue,
                    short.MinValue,
                    long.MaxValue,
                    short.MinValue,
                    long.MaxValue,
                    short.MinValue,
                    long.MaxValue,
                    short.MinValue,
                    long.MaxValue,
                    short.MinValue,
                    long.MaxValue,
                    short.MinValue,
                    long.MaxValue,
                    short.MinValue,
                    long.MaxValue,
                    short.MinValue,
                    long.MaxValue,
                    short.MinValue,
                    long.MaxValue,
                    short.MinValue,
                    long.MaxValue,
                    short.MinValue,
                    long.MaxValue,
                    short.MinValue,
                    long.MaxValue,
                };

                JsonToken[] tokensToWrite =
                {
                    JsonToken.Int64NumberArray(values)
                };

                string[] expectedText =
                {
                    @"[9223372036854775807,-32768,9223372036854775807,-32768,9223372036854775807,-32768,922337203685477580",
                    @"7,-32768,9223372036854775807,-32768,9223372036854775807,-32768,9223372036854775807,-32768,9223372036",
                    @"854775807,-32768,9223372036854775807,-32768,9223372036854775807,-32768,9223372036854775807,-32768,92",
                    @"23372036854775807,-32768,9223372036854775807]",
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E5 99 19 CB FF FF FF  FF FF FF FF 7F C9 00 80",
                    "00000010  CB FF FF FF FF FF FF FF  7F C9 00 80 CB FF FF FF",
                    "00000020  FF FF FF FF 7F C9 00 80  CB FF FF FF FF FF FF FF",
                    "00000030  7F C9 00 80 CB FF FF FF  FF FF FF FF 7F C9 00 80",
                    "00000040  CB FF FF FF FF FF FF FF  7F C9 00 80 CB FF FF FF",
                    "00000050  FF FF FF FF 7F C9 00 80  CB FF FF FF FF FF FF FF",
                    "00000060  7F C9 00 80 CB FF FF FF  FF FF FF FF 7F C9 00 80",
                    "00000070  CB FF FF FF FF FF FF FF  7F C9 00 80 CB FF FF FF",
                    "00000080  FF FF FF FF 7F C9 00 80  CB FF FF FF FF FF FF FF",
                    "00000090  7F C9 00 80 CB FF FF FF  FF FF FF FF 7F"
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 F0 DB 19 FF FF FF FF  FF FF FF 7F 00 80 FF FF",
                    "00000010  FF FF FF FF FF FF FF FF  FF FF FF 7F 00 80 FF FF",
                    "00000020  FF FF FF FF FF FF FF FF  FF FF FF 7F 00 80 FF FF",
                    "00000030  FF FF FF FF FF FF FF FF  FF FF FF 7F 00 80 FF FF",
                    "00000040  FF FF FF FF FF FF FF FF  FF FF FF 7F 00 80 FF FF",
                    "00000050  FF FF FF FF FF FF FF FF  FF FF FF 7F 00 80 FF FF",
                    "00000060  FF FF FF FF FF FF FF FF  FF FF FF 7F 00 80 FF FF",
                    "00000070  FF FF FF FF FF FF FF FF  FF FF FF 7F 00 80 FF FF",
                    "00000080  FF FF FF FF FF FF FF FF  FF FF FF 7F 00 80 FF FF",
                    "00000090  FF FF FF FF FF FF FF FF  FF FF FF 7F 00 80 FF FF",
                    "000000A0  FF FF FF FF FF FF FF FF  FF FF FF 7F 00 80 FF FF",
                    "000000B0  FF FF FF FF FF FF FF FF  FF FF FF 7F 00 80 FF FF",
                    "000000C0  FF FF FF FF FF FF FF FF  FF FF FF 7F"
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }

            // Case 2
            {
                long[] values = {
                    long.MaxValue,
                    long.MaxValue,
                    long.MaxValue,
                    int.MinValue,
                    long.MaxValue,
                    long.MaxValue,
                    long.MaxValue,
                    int.MinValue,
                    long.MaxValue,
                    long.MaxValue,
                    long.MaxValue,
                    int.MinValue,
                    long.MaxValue,
                    long.MaxValue,
                    long.MaxValue,
                    int.MinValue,
                    long.MaxValue,
                    long.MaxValue,
                    long.MaxValue,
                    int.MinValue,
                    long.MaxValue,
                    long.MaxValue,
                    long.MaxValue,
                    int.MinValue,
                    long.MaxValue,
                };

                JsonToken[] tokensToWrite =
                {
                    JsonToken.Int64NumberArray(values)
                };

                string[] expectedText =
                {
                    @"[9223372036854775807,9223372036854775807,9223372036854775807,-2147483648,9223372036854775807,9223372",
                    @"036854775807,9223372036854775807,-2147483648,9223372036854775807,9223372036854775807,922337203685477",
                    @"5807,-2147483648,9223372036854775807,9223372036854775807,9223372036854775807,-2147483648,92233720368",
                    @"54775807,9223372036854775807,9223372036854775807,-2147483648,9223372036854775807,9223372036854775807",
                    @",9223372036854775807,-2147483648,9223372036854775807]",
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E5 C9 19 CB FF FF FF  FF FF FF FF 7F CB FF FF",
                    "00000010  FF FF FF FF FF 7F CB FF  FF FF FF FF FF FF 7F CA",
                    "00000020  00 00 00 80 CB FF FF FF  FF FF FF FF 7F CB FF FF",
                    "00000030  FF FF FF FF FF 7F CB FF  FF FF FF FF FF FF 7F CA",
                    "00000040  00 00 00 80 CB FF FF FF  FF FF FF FF 7F CB FF FF",
                    "00000050  FF FF FF FF FF 7F CB FF  FF FF FF FF FF FF 7F CA",
                    "00000060  00 00 00 80 CB FF FF FF  FF FF FF FF 7F CB FF FF",
                    "00000070  FF FF FF FF FF 7F CB FF  FF FF FF FF FF FF 7F CA",
                    "00000080  00 00 00 80 CB FF FF FF  FF FF FF FF 7F CB FF FF",
                    "00000090  FF FF FF FF FF 7F CB FF  FF FF FF FF FF FF 7F CA",
                    "000000A0  00 00 00 80 CB FF FF FF  FF FF FF FF 7F CB FF FF",
                    "000000B0  FF FF FF FF FF 7F CB FF  FF FF FF FF FF FF 7F CA",
                    "000000C0  00 00 00 80 CB FF FF FF  FF FF FF FF 7F"
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 F0 DB 19 FF FF FF FF  FF FF FF 7F FF FF FF FF",
                    "00000010  FF FF FF 7F FF FF FF FF  FF FF FF 7F 00 00 00 80",
                    "00000020  FF FF FF FF FF FF FF FF  FF FF FF 7F FF FF FF FF",
                    "00000030  FF FF FF 7F FF FF FF FF  FF FF FF 7F 00 00 00 80",
                    "00000040  FF FF FF FF FF FF FF FF  FF FF FF 7F FF FF FF FF",
                    "00000050  FF FF FF 7F FF FF FF FF  FF FF FF 7F 00 00 00 80",
                    "00000060  FF FF FF FF FF FF FF FF  FF FF FF 7F FF FF FF FF",
                    "00000070  FF FF FF 7F FF FF FF FF  FF FF FF 7F 00 00 00 80",
                    "00000080  FF FF FF FF FF FF FF FF  FF FF FF 7F FF FF FF FF",
                    "00000090  FF FF FF 7F FF FF FF FF  FF FF FF 7F 00 00 00 80",
                    "000000A0  FF FF FF FF FF FF FF FF  FF FF FF 7F FF FF FF FF",
                    "000000B0  FF FF FF 7F FF FF FF FF  FF FF FF 7F 00 00 00 80",
                    "000000C0  FF FF FF FF FF FF FF FF  FF FF FF 7F"
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }
        }

        [TestMethod]
        [Owner("sboshra")]
        public void UniformNumberArrayTest8()
        {
            // -------------------------
            // FLOAT32 number array
            // -------------------------

            // Case 1
            {
                float[] values = {
                    0.010706271976232529f,
                    -0.00673653045669198f,
                    0.010706271976232529f,
                    -0.00673653045669198f,
                };

                JsonToken[] tokensToWrite =
                {
                    JsonToken.Float32NumberArray(values)
                };

                string[] expectedText =
                {
                    @"[0.010706271976232529,-0.00673653045669198,0.010706271976232529,-0.00673653045669198]"
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E2 24 CC 00 00 00 80  2B ED 85 3F CC 00 00 00",
                    "00000010  A0 C3 97 7B BF CC 00 00  00 80 2B ED 85 3F CC 00",
                    "00000020  00 00 A0 C3 97 7B BF"
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 F0 CD 04 5C 69 2F 3C  1D BE DC BB 5C 69 2F 3C",
                    "00000010  1D BE DC BB"
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }

            // Case 2
            {
                float[] values = {
                    0.010706271976232529f,
                    0.007289888337254524f,
                    0.0f,
                    (float)int.MaxValue,
                    0.0f,
                    -0.00673653045669198f,
                    -0.02682582661509514f,
                };

                JsonToken[] tokensToWrite =
                {
                    JsonToken.Float32NumberArray(values)
                };

                string[] expectedText =
                {
                    @"[0.010706271976232529,0.007289888337254524,0,2147483648,0,-0.00673653045669198,-0.02682582661509514]"
                };

                string[] expectedBinary1 =
                {
#if BACKEND_BINARY_OUTPUT // Double vs. Int64 encoding difference
                    "00000000  80 E2 2F CC 00 00 00 80  2B ED 85 3F CC 00 00 00",
                    "00000010  80 00 DC 7D 3F 00 CC 00  00 00 00 00 00 E0 41 00",
                    "00000020  CC 00 00 00 A0 C3 97 7B  BF CC 00 00 00 C0 3A 78",
                    "00000030  9B BF"
#else
                    "00000000  80 E2 3F CC 00 00 00 80  2B ED 85 3F CC 00 00 00",
                    "00000010  80 00 DC 7D 3F CC 00 00  00 00 00 00 00 00 CC 00",
                    "00000020  00 00 00 00 00 E0 41 CC  00 00 00 00 00 00 00 00",
                    "00000030  CC 00 00 00 A0 C3 97 7B  BF CC 00 00 00 C0 3A 78",
                    "00000040  9B BF"
#endif
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 F0 CD 07 5C 69 2F 3C  04 E0 EE 3B 00 00 00 00",
                    "00000010  00 00 00 4F 00 00 00 00  1D BE DC BB D6 C1 DB BC"
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }
        }

        [TestMethod]
        [Owner("sboshra")]
        public void UniformNumberArrayTest9()
        {
            // -------------------------
            // FLOAT64 number array
            // -------------------------

            // Case 1
            {
                double[] values = {
                    0.010706271976232529,
                    -0.00673653045669198,
                    0.010706271976232529,
                    -0.00673653045669198,
                };

                JsonToken[] tokensToWrite =
                {
                    JsonToken.Float64NumberArray(values)
                };

                string[] expectedText =
                {
                    @"[0.010706271976232529,-0.00673653045669198,0.010706271976232529,-0.00673653045669198]"
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E2 24 CC 00 00 00 80  2B ED 85 3F CC 00 00 00",
                    "00000010  A0 C3 97 7B BF CC 00 00  00 80 2B ED 85 3F CC 00",
                    "00000020  00 00 A0 C3 97 7B BF"
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 F0 CE 04 00 00 00 80  2B ED 85 3F 00 00 00 A0",
                    "00000010  C3 97 7B BF 00 00 00 80  2B ED 85 3F 00 00 00 A0",
                    "00000020  C3 97 7B BF"
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }

            // Case 2
            {
                double[] values = {
                    0.010706271976232529,
                    0.007289888337254524,
                    (double)int.MinValue - 1.0,
                    (double)int.MaxValue + 1.0,
                    (double)int.MinValue - 1.0,
                    -0.00673653045669198,
                    -0.02682582661509514,
                };

                JsonToken[] tokensToWrite =
                {
                    JsonToken.Float64NumberArray(values)
                };

                string[] expectedText =
                {
                    @"[0.010706271976232529,0.007289888337254524,-2147483649,2147483648,-2147483649,-0.00673653045669198,-",
                    @"0.02682582661509514]",
                };

                string[] expectedBinary1 =
                {
#if BACKEND_BINARY_OUTPUT // Double vs. Int64 encoding difference
                    "00000000  80 E2 3F CC 00 00 00 80  2B ED 85 3F CC 00 00 00",
                    "00000010  80 00 DC 7D 3F CB FF FF  FF 7F FF FF FF FF CB 00",
                    "00000020  00 00 80 00 00 00 00 CB  FF FF FF 7F FF FF FF FF",
                    "00000030  CC 00 00 00 A0 C3 97 7B  BF CC 00 00 00 C0 3A 78",
                    "00000040  9B BF"
#else
                    "00000000  80 E2 3F CC 00 00 00 80  2B ED 85 3F CC 00 00 00",
                    "00000010  80 00 DC 7D 3F CC 00 00  20 00 00 00 E0 C1 CC 00",
                    "00000020  00 00 00 00 00 E0 41 CC  00 00 20 00 00 00 E0 C1",
                    "00000030  CC 00 00 00 A0 C3 97 7B  BF CC 00 00 00 C0 3A 78",
                    "00000040  9B BF"
#endif
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 F0 CE 07 00 00 00 80  2B ED 85 3F 00 00 00 80",
                    "00000010  00 DC 7D 3F 00 00 20 00  00 00 E0 C1 00 00 00 00",
                    "00000020  00 00 E0 41 00 00 20 00  00 00 E0 C1 00 00 00 A0",
                    "00000030  C3 97 7B BF 00 00 00 C0  3A 78 9B BF"
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }

            // Case 3
            {
                double[] values = {
                    19.976000,
                    31.329000,
                    2.368000,
                    28.691999,
                    21.424999,
                    10.555000,
                    3.434000,
                    16.549000,
                    7.441000,
                    9.512000,
                    30.145000,
                    18.059999,
                    21.718000,
                    3.753000,
                    16.139000,
                    12.423000,
                    16.278999,
                    25.996000,
                    16.687000,
                    12.529000,
                    22.549000,
                    17.437000,
                    19.865999,
                    12.949000,
                    0.193000,
                    23.195000,
                    3.297000,
                    20.416000,
                    28.285999,
                    16.105000,
                    24.488001,
                    16.282000,
                    12.455000,
                    25.733999,
                    18.114000,
                    11.701000,
                    31.316000,
                    20.671000,
                    5.786000,
                    12.263000,
                    4.313000,
                    24.355000,
                    31.184999,
                    20.052999,
                    0.912000,
                    10.808000,
                    1.832000,
                    20.945000,
                    4.313000,
                    27.756001,
                    28.320999,
                    19.558001,
                    23.646000,
                    27.982000,
                    0.481000,
                    4.144000,
                    23.195999,
                    20.222000,
                    7.129000,
                    2.161000,
                    5.535000,
                    20.450001,
                    11.173000,
                    10.466000,
                    12.044000,
                    21.659000,
                    26.292000,
                    26.438999,
                    17.253000,
                    20.024000,
                    26.153999,
                    29.510000,
                    4.745000,
                    20.649000,
                    13.186000,
                    8.313000,
                    4.474000,
                    28.021999,
                    2.168000,
                    14.018000,
                    18.787001,
                    9.905000,
                    17.958000,
                    7.391000,
                    10.202000,
                    3.625000,
                    26.476999,
                    4.414000,
                    9.314000,
                    25.823999,
                    29.334000,
                    25.874001,
                    24.372000,
                    20.159000,
                    11.833000,
                    28.070000,
                    7.487000,
                    28.297001,
                    7.518000,
                    8.177000
                };

                JsonToken[] tokensToWrite =
                {
                    JsonToken.Float64NumberArray(values)
                };

                string[] expectedText =
                {
                    @"[19.976,31.329,2.368,28.691999,21.424999,10.555,3.434,16.549,7.441,9.512,30.145,18.059999,21.718,3.7",
                    @"53,16.139,12.423,16.278999,25.996,16.687,12.529,22.549,17.437,19.865999,12.949,0.193,23.195,3.297,20",
                    @".416,28.285999,16.105,24.488001,16.282,12.455,25.733999,18.114,11.701,31.316,20.671,5.786,12.263,4.3",
                    @"13,24.355,31.184999,20.052999,0.912,10.808,1.832,20.945,4.313,27.756001,28.320999,19.558001,23.646,2",
                    @"7.982,0.481,4.144,23.195999,20.222,7.129,2.161,5.535,20.450001,11.173,10.466,12.044,21.659,26.292,26",
                    @".438999,17.253,20.024,26.153999,29.51,4.745,20.649,13.186,8.313,4.474,28.021999,2.168,14.018,18.7870",
                    @"01,9.905,17.958,7.391,10.202,3.625,26.476999,4.414,9.314,25.823999,29.334,25.874001,24.372,20.159,11",
                    @".833,28.07,7.487,28.297001,7.518,8.177]",
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E6 84 03 64 00 CC 60  E5 D0 22 DB F9 33 40 CC",
                    "00000010  4E 62 10 58 39 54 3F 40  CC 8B 6C E7 FB A9 F1 02",
                    "00000020  40 CC 5D DD B1 D8 26 B1  3C 40 CC 2C D5 05 BC CC",
                    "00000030  6C 35 40 CC 5C 8F C2 F5  28 1C 25 40 CC 46 B6 F3",
                    "00000040  FD D4 78 0B 40 CC 06 81  95 43 8B 8C 30 40 CC DD",
                    "00000050  24 06 81 95 C3 1D 40 CC  A0 1A 2F DD 24 06 23 40",
                    "00000060  CC 85 EB 51 B8 1E 25 3E  40 CC EF CA 2E 18 5C 0F",
                    "00000070  32 40 CC 2B 87 16 D9 CE  B7 35 40 CC A0 1A 2F DD",
                    "00000080  24 06 0E 40 CC DD 24 06  81 95 23 30 40 CC 7F 6A",
                    "00000090  BC 74 93 D8 28 40 CC E0  9D 7C 7A 6C 47 30 40 CC",
                    "000000A0  E5 D0 22 DB F9 FE 39 40  CC 1D 5A 64 3B DF AF 30",
                    "000000B0  40 CC 02 2B 87 16 D9 0E  29 40 CC 06 81 95 43 8B",
                    "000000C0  8C 36 40 CC 1D 5A 64 3B  DF 6F 31 40 CC 63 5E 47",
                    "000000D0  1C B2 DD 33 40 CC D9 CE  F7 53 E3 E5 29 40 CC 4E",
                    "000000E0  62 10 58 39 B4 C8 3F CC  52 B8 1E 85 EB 31 37 40",
                    "000000F0  CC C7 4B 37 89 41 60 0A  40 CC D1 22 DB F9 7E 6A",
                    "00000100  34 40 CC 4F B0 FF 3A 37  49 3C 40 CC 7B 14 AE 47",
                    "00000110  E1 1A 30 40 CC 51 6A 2F  A2 ED 7C 38 40 CC D5 78",
                    "00000120  E9 26 31 48 30 40 CC 29  5C 8F C2 F5 E8 28 40 CC",
                    "00000130  F5 4B C4 5B E7 BB 39 40  CC 77 BE 9F 1A 2F 1D 32",
                    "00000140  40 CC F4 FD D4 78 E9 66  27 40 CC 37 89 41 60 E5",
                    "00000150  50 3F 40 CC B2 9D EF A7  C6 AB 34 40 CC BE 9F 1A",
                    "00000160  2F DD 24 17 40 CC 2D B2  9D EF A7 86 28 40 CC 8D",
                    "00000170  97 6E 12 83 40 11 40 CC  7B 14 AE 47 E1 5A 38 40",
                    "00000180  CC EF CA 2E 18 5C 2F 3F  40 CC 80 B8 AB 57 91 0D",
                    "00000190  34 40 CC C9 76 BE 9F 1A  2F ED 3F CC 04 56 0E 2D",
                    "000001A0  B2 9D 25 40 CC 1D 5A 64  3B DF 4F FD 3F CC 52 B8",
                    "000001B0  1E 85 EB F1 34 40 CC 8D  97 6E 12 83 40 11 40 CC",
                    "000001C0  49 BE 12 48 89 C1 3B 40  CC 78 0C 8F FD 2C 52 3C",
                    "000001D0  40 CC A3 22 4E 27 D9 8E  33 40 CC 4C 37 89 41 60",
                    "000001E0  A5 37 40 CC 08 AC 1C 5A  64 FB 3B 40 CC 62 10 58",
                    "000001F0  39 B4 C8 DE 3F CC FA 7E  6A BC 74 93 10 40 CC 78",
                    "00000200  0C 8F FD 2C 32 37 40 CC  46 B6 F3 FD D4 38 34 40",
                    "00000210  CC 6A BC 74 93 18 84 1C  40 CC 7D 3F 35 5E BA 49",
                    "00000220  01 40 CC A4 70 3D 0A D7  23 16 40 CC D4 2A FA 43",
                    "00000230  33 73 34 40 CC 7F 6A BC  74 93 58 26 40 CC 3B DF",
                    "00000240  4F 8D 97 EE 24 40 CC 4A  0C 02 2B 87 16 28 40 CC",
                    "00000250  62 10 58 39 B4 A8 35 40  CC 98 6E 12 83 C0 4A 3A",
                    "00000260  40 CC 09 FA 0B 3D 62 70  3A 40 CC 54 E3 A5 9B C4",
                    "00000270  40 31 40 CC A0 1A 2F DD  24 06 34 40 CC E0 9D 7C",
                    "00000280  7A 6C 27 3A 40 CC C3 F5  28 5C 8F 82 3D 40 CC 7B",
                    "00000290  14 AE 47 E1 FA 12 40 CC  A0 1A 2F DD 24 A6 34 40",
                    "000002A0  CC AC 1C 5A 64 3B 5F 2A  40 CC C7 4B 37 89 41 A0",
                    "000002B0  20 40 CC 4C 37 89 41 60  E5 11 40 CC 72 8B F9 B9",
                    "000002C0  A1 05 3C 40 CC F2 D2 4D  62 10 58 01 40 CC F0 A7",
                    "000002D0  C6 4B 37 09 2C 40 CC 57  EB C4 E5 78 C9 32 40 CC",
                    "000002E0  8F C2 F5 28 5C CF 23 40  CC 68 91 ED 7C 3F F5 31",
                    "000002F0  40 CC AA F1 D2 4D 62 90  1D 40 CC 81 95 43 8B 6C",
                    "00000300  67 24 40 CC 00 00 00 00  00 00 0D 40 CC 86 39 41",
                    "00000310  9B 1C 7A 3A 40 CC 0E 2D  B2 9D EF A7 11 40 CC 54",
                    "00000320  E3 A5 9B C4 A0 22 40 CC  CC EF 34 99 F1 D2 39 40",
                    "00000330  CC 2F DD 24 06 81 55 3D  40 CC DA AB 8F 87 BE DF",
                    "00000340  39 40 CC AC 1C 5A 64 3B  5F 38 40 CC 62 10 58 39",
                    "00000350  B4 28 34 40 CC D1 22 DB  F9 7E AA 27 40 CC 52 B8",
                    "00000360  1E 85 EB 11 3C 40 CC A6  9B C4 20 B0 F2 1D 40 CC",
                    "00000370  1A E1 ED 41 08 4C 3C 40  CC DF 4F 8D 97 6E 12 1E",
                    "00000380  40 CC B4 C8 76 BE 9F 5A  20 40"
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 F0 CE 64 60 E5 D0 22  DB F9 33 40 4E 62 10 58",
                    "00000010  39 54 3F 40 8B 6C E7 FB  A9 F1 02 40 5D DD B1 D8",
                    "00000020  26 B1 3C 40 2C D5 05 BC  CC 6C 35 40 5C 8F C2 F5",
                    "00000030  28 1C 25 40 46 B6 F3 FD  D4 78 0B 40 06 81 95 43",
                    "00000040  8B 8C 30 40 DD 24 06 81  95 C3 1D 40 A0 1A 2F DD",
                    "00000050  24 06 23 40 85 EB 51 B8  1E 25 3E 40 EF CA 2E 18",
                    "00000060  5C 0F 32 40 2B 87 16 D9  CE B7 35 40 A0 1A 2F DD",
                    "00000070  24 06 0E 40 DD 24 06 81  95 23 30 40 7F 6A BC 74",
                    "00000080  93 D8 28 40 E0 9D 7C 7A  6C 47 30 40 E5 D0 22 DB",
                    "00000090  F9 FE 39 40 1D 5A 64 3B  DF AF 30 40 02 2B 87 16",
                    "000000A0  D9 0E 29 40 06 81 95 43  8B 8C 36 40 1D 5A 64 3B",
                    "000000B0  DF 6F 31 40 63 5E 47 1C  B2 DD 33 40 D9 CE F7 53",
                    "000000C0  E3 E5 29 40 4E 62 10 58  39 B4 C8 3F 52 B8 1E 85",
                    "000000D0  EB 31 37 40 C7 4B 37 89  41 60 0A 40 D1 22 DB F9",
                    "000000E0  7E 6A 34 40 4F B0 FF 3A  37 49 3C 40 7B 14 AE 47",
                    "000000F0  E1 1A 30 40 51 6A 2F A2  ED 7C 38 40 D5 78 E9 26",
                    "00000100  31 48 30 40 29 5C 8F C2  F5 E8 28 40 F5 4B C4 5B",
                    "00000110  E7 BB 39 40 77 BE 9F 1A  2F 1D 32 40 F4 FD D4 78",
                    "00000120  E9 66 27 40 37 89 41 60  E5 50 3F 40 B2 9D EF A7",
                    "00000130  C6 AB 34 40 BE 9F 1A 2F  DD 24 17 40 2D B2 9D EF",
                    "00000140  A7 86 28 40 8D 97 6E 12  83 40 11 40 7B 14 AE 47",
                    "00000150  E1 5A 38 40 EF CA 2E 18  5C 2F 3F 40 80 B8 AB 57",
                    "00000160  91 0D 34 40 C9 76 BE 9F  1A 2F ED 3F 04 56 0E 2D",
                    "00000170  B2 9D 25 40 1D 5A 64 3B  DF 4F FD 3F 52 B8 1E 85",
                    "00000180  EB F1 34 40 8D 97 6E 12  83 40 11 40 49 BE 12 48",
                    "00000190  89 C1 3B 40 78 0C 8F FD  2C 52 3C 40 A3 22 4E 27",
                    "000001A0  D9 8E 33 40 4C 37 89 41  60 A5 37 40 08 AC 1C 5A",
                    "000001B0  64 FB 3B 40 62 10 58 39  B4 C8 DE 3F FA 7E 6A BC",
                    "000001C0  74 93 10 40 78 0C 8F FD  2C 32 37 40 46 B6 F3 FD",
                    "000001D0  D4 38 34 40 6A BC 74 93  18 84 1C 40 7D 3F 35 5E",
                    "000001E0  BA 49 01 40 A4 70 3D 0A  D7 23 16 40 D4 2A FA 43",
                    "000001F0  33 73 34 40 7F 6A BC 74  93 58 26 40 3B DF 4F 8D",
                    "00000200  97 EE 24 40 4A 0C 02 2B  87 16 28 40 62 10 58 39",
                    "00000210  B4 A8 35 40 98 6E 12 83  C0 4A 3A 40 09 FA 0B 3D",
                    "00000220  62 70 3A 40 54 E3 A5 9B  C4 40 31 40 A0 1A 2F DD",
                    "00000230  24 06 34 40 E0 9D 7C 7A  6C 27 3A 40 C3 F5 28 5C",
                    "00000240  8F 82 3D 40 7B 14 AE 47  E1 FA 12 40 A0 1A 2F DD",
                    "00000250  24 A6 34 40 AC 1C 5A 64  3B 5F 2A 40 C7 4B 37 89",
                    "00000260  41 A0 20 40 4C 37 89 41  60 E5 11 40 72 8B F9 B9",
                    "00000270  A1 05 3C 40 F2 D2 4D 62  10 58 01 40 F0 A7 C6 4B",
                    "00000280  37 09 2C 40 57 EB C4 E5  78 C9 32 40 8F C2 F5 28",
                    "00000290  5C CF 23 40 68 91 ED 7C  3F F5 31 40 AA F1 D2 4D",
                    "000002A0  62 90 1D 40 81 95 43 8B  6C 67 24 40 00 00 00 00",
                    "000002B0  00 00 0D 40 86 39 41 9B  1C 7A 3A 40 0E 2D B2 9D",
                    "000002C0  EF A7 11 40 54 E3 A5 9B  C4 A0 22 40 CC EF 34 99",
                    "000002D0  F1 D2 39 40 2F DD 24 06  81 55 3D 40 DA AB 8F 87",
                    "000002E0  BE DF 39 40 AC 1C 5A 64  3B 5F 38 40 62 10 58 39",
                    "000002F0  B4 28 34 40 D1 22 DB F9  7E AA 27 40 52 B8 1E 85",
                    "00000300  EB 11 3C 40 A6 9B C4 20  B0 F2 1D 40 1A E1 ED 41",
                    "00000310  08 4C 3C 40 DF 4F 8D 97  6E 12 1E 40 B4 C8 76 BE",
                    "00000320  9F 5A 20 40"
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }
        }

        [TestMethod]
        [Owner("sboshra")]
        public void UniformArrayOfNumberArraysTest1()
        {
            // -------------------------
            // Empty arrays
            // -------------------------
            {
                JsonToken[] tokensToWrite =
                {
                    JsonToken.ArrayStart(),
                        JsonToken.Int32NumberArray(Int32Array()),
                        JsonToken.Int32NumberArray(Int32Array()),
                        JsonToken.Int32NumberArray(Int32Array()),
                        JsonToken.Int32NumberArray(Int32Array()),
                        JsonToken.Int32NumberArray(Int32Array()),
                        JsonToken.Int32NumberArray(Int32Array()),
                        JsonToken.Int32NumberArray(Int32Array()),
                    JsonToken.ArrayEnd(),
                };

                string[] expectedText =
                {
                    @"[[],[],[],[],[],[],[]]"
                };

                string[] expectedBinary =
                {
                    "00000000  80 E2 07 E0 E0 E0 E0 E0  E0 E0"
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary, JsonWriteOptions.EnableNumberArrays);
            }

            // -------------------------
            // Int8 Single-item Arrays
            // -------------------------
            {
                JsonToken[] tokensToWrite =
                {
                    JsonToken.ArrayStart(),
                        JsonToken.Int8NumberArray(Int8Array(100)),
                        JsonToken.Int8NumberArray(Int8Array(102)),
                        JsonToken.Int8NumberArray(Int8Array(104)),
                        JsonToken.Int8NumberArray(Int8Array(106)),
                        JsonToken.Int8NumberArray(Int8Array(108)),
                        JsonToken.Int8NumberArray(Int8Array(110)),
                        JsonToken.Int8NumberArray(Int8Array(112)),
                    JsonToken.ArrayEnd(),
                };

                string[] expectedText =
                {
                    @"[[100],[102],[104],[106],[108],[110],[112]]"
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E2 15 E1 C8 64 E1 C8  66 E1 C8 68 E1 C8 6A E1",
                    "00000010  C8 6C E1 C8 6E E1 C8 70",
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 F2 F0 D8 01 07 64 66  68 6A 6C 6E 70",
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }

            // -------------------------
            // UInt8 Single-item Arrays
            // -------------------------
            {
                JsonToken[] tokensToWrite =
                {
                    JsonToken.ArrayStart(),
                        JsonToken.UInt8NumberArray(UInt8Array(200)),
                        JsonToken.UInt8NumberArray(UInt8Array(202)),
                        JsonToken.UInt8NumberArray(UInt8Array(204)),
                        JsonToken.UInt8NumberArray(UInt8Array(206)),
                        JsonToken.UInt8NumberArray(UInt8Array(208)),
                        JsonToken.UInt8NumberArray(UInt8Array(210)),
                        JsonToken.UInt8NumberArray(UInt8Array(212)),
                    JsonToken.ArrayEnd(),
                };

                string[] expectedText =
                {
                    @"[[200],[202],[204],[206],[208],[210],[212]]"
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E2 15 E1 C8 C8 E1 C8  CA E1 C8 CC E1 C8 CE E1",
                    "00000010  C8 D0 E1 C8 D2 E1 C8 D4",
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 F2 F0 D7 01 07 C8 CA  CC CE D0 D2 D4",
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }

            // -------------------------
            // Int16 Single-item Arrays
            // -------------------------
            {
                JsonToken[] tokensToWrite =
                {
                    JsonToken.ArrayStart(),
                        JsonToken.Int16NumberArray(Int16Array(5200)),
                        JsonToken.Int16NumberArray(Int16Array(5202)),
                        JsonToken.Int16NumberArray(Int16Array(5204)),
                        JsonToken.Int16NumberArray(Int16Array(5206)),
                        JsonToken.Int16NumberArray(Int16Array(5208)),
                        JsonToken.Int16NumberArray(Int16Array(5210)),
                        JsonToken.Int16NumberArray(Int16Array(5212)),
                    JsonToken.ArrayEnd(),
                };

                string[] expectedText =
                {
                    @"[[5200],[5202],[5204],[5206],[5208],[5210],[5212]]"
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E2 1C E1 C9 50 14 E1  C9 52 14 E1 C9 54 14 E1",
                    "00000010  C9 56 14 E1 C9 58 14 E1  C9 5A 14 E1 C9 5C 14"
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 F2 F0 D9 01 07 50 14  52 14 54 14 56 14 58 14",
                    "00000010  5A 14 5C 14",
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }

            // -------------------------
            // Int32 Single-item Arrays
            // -------------------------
            {
                JsonToken[] tokensToWrite =
                {
                    JsonToken.ArrayStart(),
                        JsonToken.Int32NumberArray(Int32Array(845200)),
                        JsonToken.Int32NumberArray(Int32Array(845202)),
                        JsonToken.Int32NumberArray(Int32Array(845204)),
                        JsonToken.Int32NumberArray(Int32Array(845206)),
                        JsonToken.Int32NumberArray(Int32Array(845208)),
                        JsonToken.Int32NumberArray(Int32Array(845210)),
                        JsonToken.Int32NumberArray(Int32Array(845212)),
                    JsonToken.ArrayEnd(),
                };

                string[] expectedText =
                {
                    @"[[845200],[845202],[845204],[845206],[845208],[845210],[845212]]"
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E2 2A E1 CA 90 E5 0C  00 E1 CA 92 E5 0C 00 E1",
                    "00000010  CA 94 E5 0C 00 E1 CA 96  E5 0C 00 E1 CA 98 E5 0C",
                    "00000020  00 E1 CA 9A E5 0C 00 E1  CA 9C E5 0C 00",
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 F2 F0 DA 01 07 90 E5  0C 00 92 E5 0C 00 94 E5",
                    "00000010  0C 00 96 E5 0C 00 98 E5  0C 00 9A E5 0C 00 9C E5",
                    "00000020  0C 00",
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }

            // -------------------------
            // Int64 Single-item Arrays
            // -------------------------
            {
                JsonToken[] tokensToWrite =
                {
                    JsonToken.ArrayStart(),
                        JsonToken.Int64NumberArray(Int64Array(2709488845200)),
                        JsonToken.Int64NumberArray(Int64Array(2709488845202)),
                        JsonToken.Int64NumberArray(Int64Array(2709488845204)),
                        JsonToken.Int64NumberArray(Int64Array(2709488845206)),
                        JsonToken.Int64NumberArray(Int64Array(2709488845208)),
                        JsonToken.Int64NumberArray(Int64Array(2709488845210)),
                        JsonToken.Int64NumberArray(Int64Array(2709488845212)),
                    JsonToken.ArrayEnd(),
                };

                string[] expectedText =
                {
                    "[[2709488845200],[2709488845202],[2709488845204],[2709488845206],[2709488845208],[2709488845210],[27",
                    "09488845212]]",
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E2 46 E1 CB 90 C1 1E  DA 76 02 00 00 E1 CB 92",
                    "00000010  C1 1E DA 76 02 00 00 E1  CB 94 C1 1E DA 76 02 00",
                    "00000020  00 E1 CB 96 C1 1E DA 76  02 00 00 E1 CB 98 C1 1E",
                    "00000030  DA 76 02 00 00 E1 CB 9A  C1 1E DA 76 02 00 00 E1",
                    "00000040  CB 9C C1 1E DA 76 02 00  00",
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 F2 F0 DB 01 07 90 C1  1E DA 76 02 00 00 92 C1",
                    "00000010  1E DA 76 02 00 00 94 C1  1E DA 76 02 00 00 96 C1",
                    "00000020  1E DA 76 02 00 00 98 C1  1E DA 76 02 00 00 9A C1",
                    "00000030  1E DA 76 02 00 00 9C C1  1E DA 76 02 00 00",
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }

            // -------------------------
            // Float32 Single-item Arrays
            // -------------------------
            {
                JsonToken[] tokensToWrite =
                {
                    JsonToken.ArrayStart(),
                        JsonToken.Float32NumberArray(Float32Array(1.1f)),
                        JsonToken.Float32NumberArray(Float32Array(1.2f)),
                        JsonToken.Float32NumberArray(Float32Array(1.3f)),
                        JsonToken.Float32NumberArray(Float32Array(1.4f)),
                        JsonToken.Float32NumberArray(Float32Array(1.5f)),
                        JsonToken.Float32NumberArray(Float32Array(1.6f)),
                        JsonToken.Float32NumberArray(Float32Array(1.7f)),
                    JsonToken.ArrayEnd(),
                };

                string[] expectedText =
                {
                    @"[[1.100000023841858],[1.2000000476837158],[1.2999999523162842],[1.399999976158142],[1.5],[1.60000002",
                    @"3841858],[1.7000000476837158]]",
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E2 46 E1 CC 00 00 00  A0 99 99 F1 3F E1 CC 00",
                    "00000010  00 00 40 33 33 F3 3F E1  CC 00 00 00 C0 CC CC F4",
                    "00000020  3F E1 CC 00 00 00 60 66  66 F6 3F E1 CC 00 00 00",
                    "00000030  00 00 00 F8 3F E1 CC 00  00 00 A0 99 99 F9 3F E1",
                    "00000040  CC 00 00 00 40 33 33 FB  3F",
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 F2 F0 CD 01 07 CD CC  8C 3F 9A 99 99 3F 66 66",
                    "00000010  A6 3F 33 33 B3 3F 00 00  C0 3F CD CC CC 3F 9A 99",
                    "00000020  D9 3F",
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }

            // -------------------------
            // Float64 Single-item Arrays
            // -------------------------
            {
                JsonToken[] tokensToWrite =
                {
                    JsonToken.ArrayStart(),
                        JsonToken.Float64NumberArray(Float64Array(1.1)),
                        JsonToken.Float64NumberArray(Float64Array(1.2)),
                        JsonToken.Float64NumberArray(Float64Array(1.3)),
                        JsonToken.Float64NumberArray(Float64Array(1.4)),
                        JsonToken.Float64NumberArray(Float64Array(1.5)),
                        JsonToken.Float64NumberArray(Float64Array(1.6)),
                        JsonToken.Float64NumberArray(Float64Array(1.7)),
                    JsonToken.ArrayEnd(),
                };

                string[] expectedText =
                {
                    @"[[1.1],[1.2],[1.3],[1.4],[1.5],[1.6],[1.7]]",
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E2 46 E1 CC 9A 99 99  99 99 99 F1 3F E1 CC 33",
                    "00000010  33 33 33 33 33 F3 3F E1  CC CD CC CC CC CC CC F4",
                    "00000020  3F E1 CC 66 66 66 66 66  66 F6 3F E1 CC 00 00 00",
                    "00000030  00 00 00 F8 3F E1 CC 9A  99 99 99 99 99 F9 3F E1",
                    "00000040  CC 33 33 33 33 33 33 FB  3F",
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 F2 F0 CE 01 07 9A 99  99 99 99 99 F1 3F 33 33",
                    "00000010  33 33 33 33 F3 3F CD CC  CC CC CC CC F4 3F 66 66",
                    "00000020  66 66 66 66 F6 3F 00 00  00 00 00 00 F8 3F 9A 99",
                    "00000030  99 99 99 99 F9 3F 33 33  33 33 33 33 FB 3F",
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }
        }

        [TestMethod]
        [Owner("sboshra")]
        public void UniformArrayOfNumberArraysTest2()
        {
            // -------------------------
            // Int8 Number Arrays
            // -------------------------

            sbyte[] values1 = { -3, -2, -1, 0, 1, 2, 3 };
            sbyte[] values2 = { -10, 0, 10, 20, 30, 40, 50 };
            sbyte[] values3 = { 10, 0, -20, -30, -40, -50, -60 };
            sbyte[] values4 = { -6, -4, -3, 0, 2, 4, 6 };

            sbyte[] valuesX1 = new sbyte[300];
            sbyte[] valuesX2 = new sbyte[300];
            sbyte[] valuesX3 = new sbyte[300];
            sbyte[] valuesX4 = new sbyte[300];

            valuesX1[0] = -1;
            valuesX2[0] = -2;
            valuesX3[0] = -3;
            valuesX4[0] = -4;

            // Case 1
            {
                JsonToken[] tokensToWrite =
                {
                    JsonToken.ArrayStart(),
                        JsonToken.Int8NumberArray(values1),
                    JsonToken.ArrayEnd(),
                };

                string[] expectedText =
                {
                    @"[[-3,-2,-1,0,1,2,3]]"
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E1 E2 0D C9 FD FF C9  FE FF C9 FF FF 00 01 02",
                    "00000010  03"
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 E1 F0 D8 07 FD FE FF  00 01 02 03"
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }

            // Case 2
            {
                JsonToken[] tokensToWrite =
                {
                    JsonToken.ArrayStart(),
                        JsonToken.Int8NumberArray(values1),
                        JsonToken.Int8NumberArray(values1),
                    JsonToken.ArrayEnd(),
                };

                string[] expectedText =
                {
                    @"[[-3,-2,-1,0,1,2,3],[-3,-2,-1,0,1,2,3]]"
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E2 1E E2 0D C9 FD FF  C9 FE FF C9 FF FF 00 01",
                    "00000010  02 03 E2 0D C9 FD FF C9  FE FF C9 FF FF 00 01 02",
                    "00000020  03"
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 F2 F0 D8 07 02 FD FE  FF 00 01 02 03 FD FE FF",
                    "00000010  00 01 02 03"
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }

            // Case 3
            {
                JsonToken[] tokensToWrite =
                {
                    JsonToken.ArrayStart(),
                        JsonToken.Int8NumberArray(values1),
                        JsonToken.Int8NumberArray(values2),
                        JsonToken.Int8NumberArray(values3),
                    JsonToken.ArrayEnd(),
                };

                string[] expectedText =
                {
                    @"[[-3,-2,-1,0,1,2,3],[-10,0,10,20,30,40,50],[10,0,-20,-30,-40,-50,-60]]"
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E2 2F E2 0D C9 FD FF  C9 FE FF C9 FF FF 00 01",
                    "00000010  02 03 E2 0B C9 F6 FF 00  0A 14 1E C8 28 C8 32 E2",
                    "00000020  11 0A 00 C9 EC FF C9 E2  FF C9 D8 FF C9 CE FF C9",
                    "00000030  C4 FF"
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 F2 F0 D8 07 03 FD FE  FF 00 01 02 03 F6 00 0A",
                    "00000010  14 1E 28 32 0A 00 EC E2  D8 CE C4"
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }

            // Case 4
            {
                JsonToken[] tokensToWrite =
                {
                    JsonToken.ArrayStart(),
                        JsonToken.Int8NumberArray(values1),
                        JsonToken.Int8NumberArray(values2),
                        JsonToken.Int8NumberArray(values3),
                        JsonToken.Int8NumberArray(values4),
                        JsonToken.Int8NumberArray(values4),
                        JsonToken.Int8NumberArray(values3),
                        JsonToken.Int8NumberArray(values2),
                        JsonToken.Int8NumberArray(values1),
                    JsonToken.ArrayEnd(),
                };

                string[] expectedText =
                {
                    @"[[-3,-2,-1,0,1,2,3],[-10,0,10,20,30,40,50],[10,0,-20,-30,-40,-50,-60],[-6,-4,-3,0,2,4,6],[-6,-4,-3,0",
                    @",2,4,6],[10,0,-20,-30,-40,-50,-60],[-10,0,10,20,30,40,50],[-3,-2,-1,0,1,2,3]]"
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E2 7C E2 0D C9 FD FF  C9 FE FF C9 FF FF 00 01",
                    "00000010  02 03 E2 0B C9 F6 FF 00  0A 14 1E C8 28 C8 32 E2",
                    "00000020  11 0A 00 C9 EC FF C9 E2  FF C9 D8 FF C9 CE FF C9",
                    "00000030  C4 FF E2 0D C9 FA FF C9  FC FF C9 FD FF 00 02 04",
                    "00000040  06 E2 0D C9 FA FF C9 FC  FF C9 FD FF 00 02 04 06",
                    "00000050  E2 11 0A 00 C9 EC FF C9  E2 FF C9 D8 FF C9 CE FF",
                    "00000060  C9 C4 FF E2 0B C9 F6 FF  00 0A 14 1E C8 28 C8 32",
                    "00000070  E2 0D C9 FD FF C9 FE FF  C9 FF FF 00 01 02 03"
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 F2 F0 D8 07 08 FD FE  FF 00 01 02 03 F6 00 0A",
                    "00000010  14 1E 28 32 0A 00 EC E2  D8 CE C4 FA FC FD 00 02",
                    "00000020  04 06 FA FC FD 00 02 04  06 0A 00 EC E2 D8 CE C4",
                    "00000030  F6 00 0A 14 1E 28 32 FD  FE FF 00 01 02 03"
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }

            // Case 5
            {
                sbyte[][] valueSets = { values1, values2, values3, values4 };

                JsonToken[] tokensToWrite = new JsonToken[1 + 260 + 1];

                tokensToWrite[0] = JsonToken.ArrayStart();
                for (int i = 0; i < tokensToWrite.Length - 2; i++)
                {
                    tokensToWrite[1 + i] = JsonToken.Int8NumberArray(valueSets[i % valueSets.Length]);
                }
                tokensToWrite[^1] = JsonToken.ArrayEnd();

                string[] expectedText =
                {
                    @"[[-3,-2,-1,0,1,2,3],[-10,0,10,20,30,40,50],[10,0,-20,-30,-40,-50,-60],[-6,-4,-3,0,2,4,6],[-3,-2,-1,0",
                    @",1,2,3],[-10,0,10,20,30,40,50],[10,0,-20,-30,-40,-50,-60],[-6,-4,-3,0,2,4,6],[-3,-2,-1,0,1,2,3],[-10",
                    @",0,10,20,30,40,50],[10,0,-20,-30,-40,-50,-60],[-6,-4,-3,0,2,4,6],[-3,-2,-1,0,1,2,3],[-10,0,10,20,30,",
                    @"40,50],[10,0,-20,-30,-40,-50,-60],[-6,-4,-3,0,2,4,6],[-3,-2,-1,0,1,2,3],[-10,0,10,20,30,40,50],[10,0",
                    @",-20,-30,-40,-50,-60],[-6,-4,-3,0,2,4,6],[-3,-2,-1,0,1,2,3],[-10,0,10,20,30,40,50],[10,0,-20,-30,-40",
                    @",-50,-60],[-6,-4,-3,0,2,4,6],[-3,-2,-1,0,1,2,3],[-10,0,10,20,30,40,50],[10,0,-20,-30,-40,-50,-60],[-",
                    @"6,-4,-3,0,2,4,6],[-3,-2,-1,0,1,2,3],[-10,0,10,20,30,40,50],[10,0,-20,-30,-40,-50,-60],[-6,-4,-3,0,2,",
                    @"4,6],[-3,-2,-1,0,1,2,3],[-10,0,10,20,30,40,50],[10,0,-20,-30,-40,-50,-60],[-6,-4,-3,0,2,4,6],[-3,-2,",
                    @"-1,0,1,2,3],[-10,0,10,20,30,40,50],[10,0,-20,-30,-40,-50,-60],[-6,-4,-3,0,2,4,6],[-3,-2,-1,0,1,2,3],",
                    @"[-10,0,10,20,30,40,50],[10,0,-20,-30,-40,-50,-60],[-6,-4,-3,0,2,4,6],[-3,-2,-1,0,1,2,3],[-10,0,10,20",
                    @",30,40,50],[10,0,-20,-30,-40,-50,-60],[-6,-4,-3,0,2,4,6],[-3,-2,-1,0,1,2,3],[-10,0,10,20,30,40,50],[",
                    @"10,0,-20,-30,-40,-50,-60],[-6,-4,-3,0,2,4,6],[-3,-2,-1,0,1,2,3],[-10,0,10,20,30,40,50],[10,0,-20,-30",
                    @",-40,-50,-60],[-6,-4,-3,0,2,4,6],[-3,-2,-1,0,1,2,3],[-10,0,10,20,30,40,50],[10,0,-20,-30,-40,-50,-60",
                    @"],[-6,-4,-3,0,2,4,6],[-3,-2,-1,0,1,2,3],[-10,0,10,20,30,40,50],[10,0,-20,-30,-40,-50,-60],[-6,-4,-3,",
                    @"0,2,4,6],[-3,-2,-1,0,1,2,3],[-10,0,10,20,30,40,50],[10,0,-20,-30,-40,-50,-60],[-6,-4,-3,0,2,4,6],[-3",
                    @",-2,-1,0,1,2,3],[-10,0,10,20,30,40,50],[10,0,-20,-30,-40,-50,-60],[-6,-4,-3,0,2,4,6],[-3,-2,-1,0,1,2",
                    @",3],[-10,0,10,20,30,40,50],[10,0,-20,-30,-40,-50,-60],[-6,-4,-3,0,2,4,6],[-3,-2,-1,0,1,2,3],[-10,0,1",
                    @"0,20,30,40,50],[10,0,-20,-30,-40,-50,-60],[-6,-4,-3,0,2,4,6],[-3,-2,-1,0,1,2,3],[-10,0,10,20,30,40,5",
                    @"0],[10,0,-20,-30,-40,-50,-60],[-6,-4,-3,0,2,4,6],[-3,-2,-1,0,1,2,3],[-10,0,10,20,30,40,50],[10,0,-20",
                    @",-30,-40,-50,-60],[-6,-4,-3,0,2,4,6],[-3,-2,-1,0,1,2,3],[-10,0,10,20,30,40,50],[10,0,-20,-30,-40,-50",
                    @",-60],[-6,-4,-3,0,2,4,6],[-3,-2,-1,0,1,2,3],[-10,0,10,20,30,40,50],[10,0,-20,-30,-40,-50,-60],[-6,-4",
                    @",-3,0,2,4,6],[-3,-2,-1,0,1,2,3],[-10,0,10,20,30,40,50],[10,0,-20,-30,-40,-50,-60],[-6,-4,-3,0,2,4,6]",
                    @",[-3,-2,-1,0,1,2,3],[-10,0,10,20,30,40,50],[10,0,-20,-30,-40,-50,-60],[-6,-4,-3,0,2,4,6],[-3,-2,-1,0",
                    @",1,2,3],[-10,0,10,20,30,40,50],[10,0,-20,-30,-40,-50,-60],[-6,-4,-3,0,2,4,6],[-3,-2,-1,0,1,2,3],[-10",
                    @",0,10,20,30,40,50],[10,0,-20,-30,-40,-50,-60],[-6,-4,-3,0,2,4,6],[-3,-2,-1,0,1,2,3],[-10,0,10,20,30,",
                    @"40,50],[10,0,-20,-30,-40,-50,-60],[-6,-4,-3,0,2,4,6],[-3,-2,-1,0,1,2,3],[-10,0,10,20,30,40,50],[10,0",
                    @",-20,-30,-40,-50,-60],[-6,-4,-3,0,2,4,6],[-3,-2,-1,0,1,2,3],[-10,0,10,20,30,40,50],[10,0,-20,-30,-40",
                    @",-50,-60],[-6,-4,-3,0,2,4,6],[-3,-2,-1,0,1,2,3],[-10,0,10,20,30,40,50],[10,0,-20,-30,-40,-50,-60],[-",
                    @"6,-4,-3,0,2,4,6],[-3,-2,-1,0,1,2,3],[-10,0,10,20,30,40,50],[10,0,-20,-30,-40,-50,-60],[-6,-4,-3,0,2,",
                    @"4,6],[-3,-2,-1,0,1,2,3],[-10,0,10,20,30,40,50],[10,0,-20,-30,-40,-50,-60],[-6,-4,-3,0,2,4,6],[-3,-2,",
                    @"-1,0,1,2,3],[-10,0,10,20,30,40,50],[10,0,-20,-30,-40,-50,-60],[-6,-4,-3,0,2,4,6],[-3,-2,-1,0,1,2,3],",
                    @"[-10,0,10,20,30,40,50],[10,0,-20,-30,-40,-50,-60],[-6,-4,-3,0,2,4,6],[-3,-2,-1,0,1,2,3],[-10,0,10,20",
                    @",30,40,50],[10,0,-20,-30,-40,-50,-60],[-6,-4,-3,0,2,4,6],[-3,-2,-1,0,1,2,3],[-10,0,10,20,30,40,50],[",
                    @"10,0,-20,-30,-40,-50,-60],[-6,-4,-3,0,2,4,6],[-3,-2,-1,0,1,2,3],[-10,0,10,20,30,40,50],[10,0,-20,-30",
                    @",-40,-50,-60],[-6,-4,-3,0,2,4,6],[-3,-2,-1,0,1,2,3],[-10,0,10,20,30,40,50],[10,0,-20,-30,-40,-50,-60",
                    @"],[-6,-4,-3,0,2,4,6],[-3,-2,-1,0,1,2,3],[-10,0,10,20,30,40,50],[10,0,-20,-30,-40,-50,-60],[-6,-4,-3,",
                    @"0,2,4,6],[-3,-2,-1,0,1,2,3],[-10,0,10,20,30,40,50],[10,0,-20,-30,-40,-50,-60],[-6,-4,-3,0,2,4,6],[-3",
                    @",-2,-1,0,1,2,3],[-10,0,10,20,30,40,50],[10,0,-20,-30,-40,-50,-60],[-6,-4,-3,0,2,4,6],[-3,-2,-1,0,1,2",
                    @",3],[-10,0,10,20,30,40,50],[10,0,-20,-30,-40,-50,-60],[-6,-4,-3,0,2,4,6],[-3,-2,-1,0,1,2,3],[-10,0,1",
                    @"0,20,30,40,50],[10,0,-20,-30,-40,-50,-60],[-6,-4,-3,0,2,4,6],[-3,-2,-1,0,1,2,3],[-10,0,10,20,30,40,5",
                    @"0],[10,0,-20,-30,-40,-50,-60],[-6,-4,-3,0,2,4,6],[-3,-2,-1,0,1,2,3],[-10,0,10,20,30,40,50],[10,0,-20",
                    @",-30,-40,-50,-60],[-6,-4,-3,0,2,4,6],[-3,-2,-1,0,1,2,3],[-10,0,10,20,30,40,50],[10,0,-20,-30,-40,-50",
                    @",-60],[-6,-4,-3,0,2,4,6],[-3,-2,-1,0,1,2,3],[-10,0,10,20,30,40,50],[10,0,-20,-30,-40,-50,-60],[-6,-4",
                    @",-3,0,2,4,6],[-3,-2,-1,0,1,2,3],[-10,0,10,20,30,40,50],[10,0,-20,-30,-40,-50,-60],[-6,-4,-3,0,2,4,6]",
                    @",[-3,-2,-1,0,1,2,3],[-10,0,10,20,30,40,50],[10,0,-20,-30,-40,-50,-60],[-6,-4,-3,0,2,4,6],[-3,-2,-1,0",
                    @",1,2,3],[-10,0,10,20,30,40,50],[10,0,-20,-30,-40,-50,-60],[-6,-4,-3,0,2,4,6],[-3,-2,-1,0,1,2,3],[-10",
                    @",0,10,20,30,40,50],[10,0,-20,-30,-40,-50,-60],[-6,-4,-3,0,2,4,6],[-3,-2,-1,0,1,2,3],[-10,0,10,20,30,",
                    @"40,50],[10,0,-20,-30,-40,-50,-60],[-6,-4,-3,0,2,4,6],[-3,-2,-1,0,1,2,3],[-10,0,10,20,30,40,50],[10,0",
                    @",-20,-30,-40,-50,-60],[-6,-4,-3,0,2,4,6],[-3,-2,-1,0,1,2,3],[-10,0,10,20,30,40,50],[10,0,-20,-30,-40",
                    @",-50,-60],[-6,-4,-3,0,2,4,6],[-3,-2,-1,0,1,2,3],[-10,0,10,20,30,40,50],[10,0,-20,-30,-40,-50,-60],[-",
                    @"6,-4,-3,0,2,4,6],[-3,-2,-1,0,1,2,3],[-10,0,10,20,30,40,50],[10,0,-20,-30,-40,-50,-60],[-6,-4,-3,0,2,",
                    @"4,6],[-3,-2,-1,0,1,2,3],[-10,0,10,20,30,40,50],[10,0,-20,-30,-40,-50,-60],[-6,-4,-3,0,2,4,6],[-3,-2,",
                    @"-1,0,1,2,3],[-10,0,10,20,30,40,50],[10,0,-20,-30,-40,-50,-60],[-6,-4,-3,0,2,4,6],[-3,-2,-1,0,1,2,3],",
                    @"[-10,0,10,20,30,40,50],[10,0,-20,-30,-40,-50,-60],[-6,-4,-3,0,2,4,6],[-3,-2,-1,0,1,2,3],[-10,0,10,20",
                    @",30,40,50],[10,0,-20,-30,-40,-50,-60],[-6,-4,-3,0,2,4,6],[-3,-2,-1,0,1,2,3],[-10,0,10,20,30,40,50],[",
                    @"10,0,-20,-30,-40,-50,-60],[-6,-4,-3,0,2,4,6],[-3,-2,-1,0,1,2,3],[-10,0,10,20,30,40,50],[10,0,-20,-30",
                    @",-40,-50,-60],[-6,-4,-3,0,2,4,6],[-3,-2,-1,0,1,2,3],[-10,0,10,20,30,40,50],[10,0,-20,-30,-40,-50,-60",
                    @"],[-6,-4,-3,0,2,4,6]]"
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E6 BE 0F 04 01 E2 0D  C9 FD FF C9 FE FF C9 FF",
                    "00000010  FF 00 01 02 03 E2 0B C9  F6 FF 00 0A 14 1E C8 28",
                    "00000020  C8 32 E2 11 0A 00 C9 EC  FF C9 E2 FF C9 D8 FF C9",
                    "00000030  CE FF C9 C4 FF E2 0D C9  FA FF C9 FC FF C9 FD FF",
                    "00000040  00 02 04 06 E2 0D C9 FD  FF C9 FE FF C9 FF FF 00",
                    "00000050  01 02 03 E2 0B C9 F6 FF  00 0A 14 1E C8 28 C8 32",
                    "00000060  E2 11 0A 00 C9 EC FF C9  E2 FF C9 D8 FF C9 CE FF",
                    "00000070  C9 C4 FF E2 0D C9 FA FF  C9 FC FF C9 FD FF 00 02",
                    "00000080  04 06 E2 0D C9 FD FF C9  FE FF C9 FF FF 00 01 02",
                    "00000090  03 E2 0B C9 F6 FF 00 0A  14 1E C8 28 C8 32 E2 11",
                    "000000A0  0A 00 C9 EC FF C9 E2 FF  C9 D8 FF C9 CE FF C9 C4",
                    "000000B0  FF E2 0D C9 FA FF C9 FC  FF C9 FD FF 00 02 04 06",
                    "000000C0  E2 0D C9 FD FF C9 FE FF  C9 FF FF 00 01 02 03 E2",
                    "000000D0  0B C9 F6 FF 00 0A 14 1E  C8 28 C8 32 E2 11 0A 00",
                    "000000E0  C9 EC FF C9 E2 FF C9 D8  FF C9 CE FF C9 C4 FF E2",
                    "000000F0  0D C9 FA FF C9 FC FF C9  FD FF 00 02 04 06 E2 0D",
                    "00000100  C9 FD FF C9 FE FF C9 FF  FF 00 01 02 03 E2 0B C9",
                    "00000110  F6 FF 00 0A 14 1E C8 28  C8 32 E2 11 0A 00 C9 EC",
                    "00000120  FF C9 E2 FF C9 D8 FF C9  CE FF C9 C4 FF E2 0D C9",
                    "00000130  FA FF C9 FC FF C9 FD FF  00 02 04 06 E2 0D C9 FD",
                    "00000140  FF C9 FE FF C9 FF FF 00  01 02 03 E2 0B C9 F6 FF",
                    "00000150  00 0A 14 1E C8 28 C8 32  E2 11 0A 00 C9 EC FF C9",
                    "00000160  E2 FF C9 D8 FF C9 CE FF  C9 C4 FF E2 0D C9 FA FF",
                    "00000170  C9 FC FF C9 FD FF 00 02  04 06 E2 0D C9 FD FF C9",
                    "00000180  FE FF C9 FF FF 00 01 02  03 E2 0B C9 F6 FF 00 0A",
                    "00000190  14 1E C8 28 C8 32 E2 11  0A 00 C9 EC FF C9 E2 FF",
                    "000001A0  C9 D8 FF C9 CE FF C9 C4  FF E2 0D C9 FA FF C9 FC",
                    "000001B0  FF C9 FD FF 00 02 04 06  E2 0D C9 FD FF C9 FE FF",
                    "000001C0  C9 FF FF 00 01 02 03 E2  0B C9 F6 FF 00 0A 14 1E",
                    "000001D0  C8 28 C8 32 E2 11 0A 00  C9 EC FF C9 E2 FF C9 D8",
                    "000001E0  FF C9 CE FF C9 C4 FF E2  0D C9 FA FF C9 FC FF C9",
                    "000001F0  FD FF 00 02 04 06 E2 0D  C9 FD FF C9 FE FF C9 FF",
                    "00000200  FF 00 01 02 03 E2 0B C9  F6 FF 00 0A 14 1E C8 28",
                    "00000210  C8 32 E2 11 0A 00 C9 EC  FF C9 E2 FF C9 D8 FF C9",
                    "00000220  CE FF C9 C4 FF E2 0D C9  FA FF C9 FC FF C9 FD FF",
                    "00000230  00 02 04 06 E2 0D C9 FD  FF C9 FE FF C9 FF FF 00",
                    "00000240  01 02 03 E2 0B C9 F6 FF  00 0A 14 1E C8 28 C8 32",
                    "00000250  E2 11 0A 00 C9 EC FF C9  E2 FF C9 D8 FF C9 CE FF",
                    "00000260  C9 C4 FF E2 0D C9 FA FF  C9 FC FF C9 FD FF 00 02",
                    "00000270  04 06 E2 0D C9 FD FF C9  FE FF C9 FF FF 00 01 02",
                    "00000280  03 E2 0B C9 F6 FF 00 0A  14 1E C8 28 C8 32 E2 11",
                    "00000290  0A 00 C9 EC FF C9 E2 FF  C9 D8 FF C9 CE FF C9 C4",
                    "000002A0  FF E2 0D C9 FA FF C9 FC  FF C9 FD FF 00 02 04 06",
                    "000002B0  E2 0D C9 FD FF C9 FE FF  C9 FF FF 00 01 02 03 E2",
                    "000002C0  0B C9 F6 FF 00 0A 14 1E  C8 28 C8 32 E2 11 0A 00",
                    "000002D0  C9 EC FF C9 E2 FF C9 D8  FF C9 CE FF C9 C4 FF E2",
                    "000002E0  0D C9 FA FF C9 FC FF C9  FD FF 00 02 04 06 E2 0D",
                    "000002F0  C9 FD FF C9 FE FF C9 FF  FF 00 01 02 03 E2 0B C9",
                    "00000300  F6 FF 00 0A 14 1E C8 28  C8 32 E2 11 0A 00 C9 EC",
                    "00000310  FF C9 E2 FF C9 D8 FF C9  CE FF C9 C4 FF E2 0D C9",
                    "00000320  FA FF C9 FC FF C9 FD FF  00 02 04 06 E2 0D C9 FD",
                    "00000330  FF C9 FE FF C9 FF FF 00  01 02 03 E2 0B C9 F6 FF",
                    "00000340  00 0A 14 1E C8 28 C8 32  E2 11 0A 00 C9 EC FF C9",
                    "00000350  E2 FF C9 D8 FF C9 CE FF  C9 C4 FF E2 0D C9 FA FF",
                    "00000360  C9 FC FF C9 FD FF 00 02  04 06 E2 0D C9 FD FF C9",
                    "00000370  FE FF C9 FF FF 00 01 02  03 E2 0B C9 F6 FF 00 0A",
                    "00000380  14 1E C8 28 C8 32 E2 11  0A 00 C9 EC FF C9 E2 FF",
                    "00000390  C9 D8 FF C9 CE FF C9 C4  FF E2 0D C9 FA FF C9 FC",
                    "000003A0  FF C9 FD FF 00 02 04 06  E2 0D C9 FD FF C9 FE FF",
                    "000003B0  C9 FF FF 00 01 02 03 E2  0B C9 F6 FF 00 0A 14 1E",
                    "000003C0  C8 28 C8 32 E2 11 0A 00  C9 EC FF C9 E2 FF C9 D8",
                    "000003D0  FF C9 CE FF C9 C4 FF E2  0D C9 FA FF C9 FC FF C9",
                    "000003E0  FD FF 00 02 04 06 E2 0D  C9 FD FF C9 FE FF C9 FF",
                    "000003F0  FF 00 01 02 03 E2 0B C9  F6 FF 00 0A 14 1E C8 28",
                    "00000400  C8 32 E2 11 0A 00 C9 EC  FF C9 E2 FF C9 D8 FF C9",
                    "00000410  CE FF C9 C4 FF E2 0D C9  FA FF C9 FC FF C9 FD FF",
                    "00000420  00 02 04 06 E2 0D C9 FD  FF C9 FE FF C9 FF FF 00",
                    "00000430  01 02 03 E2 0B C9 F6 FF  00 0A 14 1E C8 28 C8 32",
                    "00000440  E2 11 0A 00 C9 EC FF C9  E2 FF C9 D8 FF C9 CE FF",
                    "00000450  C9 C4 FF E2 0D C9 FA FF  C9 FC FF C9 FD FF 00 02",
                    "00000460  04 06 E2 0D C9 FD FF C9  FE FF C9 FF FF 00 01 02",
                    "00000470  03 E2 0B C9 F6 FF 00 0A  14 1E C8 28 C8 32 E2 11",
                    "00000480  0A 00 C9 EC FF C9 E2 FF  C9 D8 FF C9 CE FF C9 C4",
                    "00000490  FF E2 0D C9 FA FF C9 FC  FF C9 FD FF 00 02 04 06",
                    "000004A0  E2 0D C9 FD FF C9 FE FF  C9 FF FF 00 01 02 03 E2",
                    "000004B0  0B C9 F6 FF 00 0A 14 1E  C8 28 C8 32 E2 11 0A 00",
                    "000004C0  C9 EC FF C9 E2 FF C9 D8  FF C9 CE FF C9 C4 FF E2",
                    "000004D0  0D C9 FA FF C9 FC FF C9  FD FF 00 02 04 06 E2 0D",
                    "000004E0  C9 FD FF C9 FE FF C9 FF  FF 00 01 02 03 E2 0B C9",
                    "000004F0  F6 FF 00 0A 14 1E C8 28  C8 32 E2 11 0A 00 C9 EC",
                    "00000500  FF C9 E2 FF C9 D8 FF C9  CE FF C9 C4 FF E2 0D C9",
                    "00000510  FA FF C9 FC FF C9 FD FF  00 02 04 06 E2 0D C9 FD",
                    "00000520  FF C9 FE FF C9 FF FF 00  01 02 03 E2 0B C9 F6 FF",
                    "00000530  00 0A 14 1E C8 28 C8 32  E2 11 0A 00 C9 EC FF C9",
                    "00000540  E2 FF C9 D8 FF C9 CE FF  C9 C4 FF E2 0D C9 FA FF",
                    "00000550  C9 FC FF C9 FD FF 00 02  04 06 E2 0D C9 FD FF C9",
                    "00000560  FE FF C9 FF FF 00 01 02  03 E2 0B C9 F6 FF 00 0A",
                    "00000570  14 1E C8 28 C8 32 E2 11  0A 00 C9 EC FF C9 E2 FF",
                    "00000580  C9 D8 FF C9 CE FF C9 C4  FF E2 0D C9 FA FF C9 FC",
                    "00000590  FF C9 FD FF 00 02 04 06  E2 0D C9 FD FF C9 FE FF",
                    "000005A0  C9 FF FF 00 01 02 03 E2  0B C9 F6 FF 00 0A 14 1E",
                    "000005B0  C8 28 C8 32 E2 11 0A 00  C9 EC FF C9 E2 FF C9 D8",
                    "000005C0  FF C9 CE FF C9 C4 FF E2  0D C9 FA FF C9 FC FF C9",
                    "000005D0  FD FF 00 02 04 06 E2 0D  C9 FD FF C9 FE FF C9 FF",
                    "000005E0  FF 00 01 02 03 E2 0B C9  F6 FF 00 0A 14 1E C8 28",
                    "000005F0  C8 32 E2 11 0A 00 C9 EC  FF C9 E2 FF C9 D8 FF C9",
                    "00000600  CE FF C9 C4 FF E2 0D C9  FA FF C9 FC FF C9 FD FF",
                    "00000610  00 02 04 06 E2 0D C9 FD  FF C9 FE FF C9 FF FF 00",
                    "00000620  01 02 03 E2 0B C9 F6 FF  00 0A 14 1E C8 28 C8 32",
                    "00000630  E2 11 0A 00 C9 EC FF C9  E2 FF C9 D8 FF C9 CE FF",
                    "00000640  C9 C4 FF E2 0D C9 FA FF  C9 FC FF C9 FD FF 00 02",
                    "00000650  04 06 E2 0D C9 FD FF C9  FE FF C9 FF FF 00 01 02",
                    "00000660  03 E2 0B C9 F6 FF 00 0A  14 1E C8 28 C8 32 E2 11",
                    "00000670  0A 00 C9 EC FF C9 E2 FF  C9 D8 FF C9 CE FF C9 C4",
                    "00000680  FF E2 0D C9 FA FF C9 FC  FF C9 FD FF 00 02 04 06",
                    "00000690  E2 0D C9 FD FF C9 FE FF  C9 FF FF 00 01 02 03 E2",
                    "000006A0  0B C9 F6 FF 00 0A 14 1E  C8 28 C8 32 E2 11 0A 00",
                    "000006B0  C9 EC FF C9 E2 FF C9 D8  FF C9 CE FF C9 C4 FF E2",
                    "000006C0  0D C9 FA FF C9 FC FF C9  FD FF 00 02 04 06 E2 0D",
                    "000006D0  C9 FD FF C9 FE FF C9 FF  FF 00 01 02 03 E2 0B C9",
                    "000006E0  F6 FF 00 0A 14 1E C8 28  C8 32 E2 11 0A 00 C9 EC",
                    "000006F0  FF C9 E2 FF C9 D8 FF C9  CE FF C9 C4 FF E2 0D C9",
                    "00000700  FA FF C9 FC FF C9 FD FF  00 02 04 06 E2 0D C9 FD",
                    "00000710  FF C9 FE FF C9 FF FF 00  01 02 03 E2 0B C9 F6 FF",
                    "00000720  00 0A 14 1E C8 28 C8 32  E2 11 0A 00 C9 EC FF C9",
                    "00000730  E2 FF C9 D8 FF C9 CE FF  C9 C4 FF E2 0D C9 FA FF",
                    "00000740  C9 FC FF C9 FD FF 00 02  04 06 E2 0D C9 FD FF C9",
                    "00000750  FE FF C9 FF FF 00 01 02  03 E2 0B C9 F6 FF 00 0A",
                    "00000760  14 1E C8 28 C8 32 E2 11  0A 00 C9 EC FF C9 E2 FF",
                    "00000770  C9 D8 FF C9 CE FF C9 C4  FF E2 0D C9 FA FF C9 FC",
                    "00000780  FF C9 FD FF 00 02 04 06  E2 0D C9 FD FF C9 FE FF",
                    "00000790  C9 FF FF 00 01 02 03 E2  0B C9 F6 FF 00 0A 14 1E",
                    "000007A0  C8 28 C8 32 E2 11 0A 00  C9 EC FF C9 E2 FF C9 D8",
                    "000007B0  FF C9 CE FF C9 C4 FF E2  0D C9 FA FF C9 FC FF C9",
                    "000007C0  FD FF 00 02 04 06 E2 0D  C9 FD FF C9 FE FF C9 FF",
                    "000007D0  FF 00 01 02 03 E2 0B C9  F6 FF 00 0A 14 1E C8 28",
                    "000007E0  C8 32 E2 11 0A 00 C9 EC  FF C9 E2 FF C9 D8 FF C9",
                    "000007F0  CE FF C9 C4 FF E2 0D C9  FA FF C9 FC FF C9 FD FF",
                    "00000800  00 02 04 06 E2 0D C9 FD  FF C9 FE FF C9 FF FF 00",
                    "00000810  01 02 03 E2 0B C9 F6 FF  00 0A 14 1E C8 28 C8 32",
                    "00000820  E2 11 0A 00 C9 EC FF C9  E2 FF C9 D8 FF C9 CE FF",
                    "00000830  C9 C4 FF E2 0D C9 FA FF  C9 FC FF C9 FD FF 00 02",
                    "00000840  04 06 E2 0D C9 FD FF C9  FE FF C9 FF FF 00 01 02",
                    "00000850  03 E2 0B C9 F6 FF 00 0A  14 1E C8 28 C8 32 E2 11",
                    "00000860  0A 00 C9 EC FF C9 E2 FF  C9 D8 FF C9 CE FF C9 C4",
                    "00000870  FF E2 0D C9 FA FF C9 FC  FF C9 FD FF 00 02 04 06",
                    "00000880  E2 0D C9 FD FF C9 FE FF  C9 FF FF 00 01 02 03 E2",
                    "00000890  0B C9 F6 FF 00 0A 14 1E  C8 28 C8 32 E2 11 0A 00",
                    "000008A0  C9 EC FF C9 E2 FF C9 D8  FF C9 CE FF C9 C4 FF E2",
                    "000008B0  0D C9 FA FF C9 FC FF C9  FD FF 00 02 04 06 E2 0D",
                    "000008C0  C9 FD FF C9 FE FF C9 FF  FF 00 01 02 03 E2 0B C9",
                    "000008D0  F6 FF 00 0A 14 1E C8 28  C8 32 E2 11 0A 00 C9 EC",
                    "000008E0  FF C9 E2 FF C9 D8 FF C9  CE FF C9 C4 FF E2 0D C9",
                    "000008F0  FA FF C9 FC FF C9 FD FF  00 02 04 06 E2 0D C9 FD",
                    "00000900  FF C9 FE FF C9 FF FF 00  01 02 03 E2 0B C9 F6 FF",
                    "00000910  00 0A 14 1E C8 28 C8 32  E2 11 0A 00 C9 EC FF C9",
                    "00000920  E2 FF C9 D8 FF C9 CE FF  C9 C4 FF E2 0D C9 FA FF",
                    "00000930  C9 FC FF C9 FD FF 00 02  04 06 E2 0D C9 FD FF C9",
                    "00000940  FE FF C9 FF FF 00 01 02  03 E2 0B C9 F6 FF 00 0A",
                    "00000950  14 1E C8 28 C8 32 E2 11  0A 00 C9 EC FF C9 E2 FF",
                    "00000960  C9 D8 FF C9 CE FF C9 C4  FF E2 0D C9 FA FF C9 FC",
                    "00000970  FF C9 FD FF 00 02 04 06  E2 0D C9 FD FF C9 FE FF",
                    "00000980  C9 FF FF 00 01 02 03 E2  0B C9 F6 FF 00 0A 14 1E",
                    "00000990  C8 28 C8 32 E2 11 0A 00  C9 EC FF C9 E2 FF C9 D8",
                    "000009A0  FF C9 CE FF C9 C4 FF E2  0D C9 FA FF C9 FC FF C9",
                    "000009B0  FD FF 00 02 04 06 E2 0D  C9 FD FF C9 FE FF C9 FF",
                    "000009C0  FF 00 01 02 03 E2 0B C9  F6 FF 00 0A 14 1E C8 28",
                    "000009D0  C8 32 E2 11 0A 00 C9 EC  FF C9 E2 FF C9 D8 FF C9",
                    "000009E0  CE FF C9 C4 FF E2 0D C9  FA FF C9 FC FF C9 FD FF",
                    "000009F0  00 02 04 06 E2 0D C9 FD  FF C9 FE FF C9 FF FF 00",
                    "00000A00  01 02 03 E2 0B C9 F6 FF  00 0A 14 1E C8 28 C8 32",
                    "00000A10  E2 11 0A 00 C9 EC FF C9  E2 FF C9 D8 FF C9 CE FF",
                    "00000A20  C9 C4 FF E2 0D C9 FA FF  C9 FC FF C9 FD FF 00 02",
                    "00000A30  04 06 E2 0D C9 FD FF C9  FE FF C9 FF FF 00 01 02",
                    "00000A40  03 E2 0B C9 F6 FF 00 0A  14 1E C8 28 C8 32 E2 11",
                    "00000A50  0A 00 C9 EC FF C9 E2 FF  C9 D8 FF C9 CE FF C9 C4",
                    "00000A60  FF E2 0D C9 FA FF C9 FC  FF C9 FD FF 00 02 04 06",
                    "00000A70  E2 0D C9 FD FF C9 FE FF  C9 FF FF 00 01 02 03 E2",
                    "00000A80  0B C9 F6 FF 00 0A 14 1E  C8 28 C8 32 E2 11 0A 00",
                    "00000A90  C9 EC FF C9 E2 FF C9 D8  FF C9 CE FF C9 C4 FF E2",
                    "00000AA0  0D C9 FA FF C9 FC FF C9  FD FF 00 02 04 06 E2 0D",
                    "00000AB0  C9 FD FF C9 FE FF C9 FF  FF 00 01 02 03 E2 0B C9",
                    "00000AC0  F6 FF 00 0A 14 1E C8 28  C8 32 E2 11 0A 00 C9 EC",
                    "00000AD0  FF C9 E2 FF C9 D8 FF C9  CE FF C9 C4 FF E2 0D C9",
                    "00000AE0  FA FF C9 FC FF C9 FD FF  00 02 04 06 E2 0D C9 FD",
                    "00000AF0  FF C9 FE FF C9 FF FF 00  01 02 03 E2 0B C9 F6 FF",
                    "00000B00  00 0A 14 1E C8 28 C8 32  E2 11 0A 00 C9 EC FF C9",
                    "00000B10  E2 FF C9 D8 FF C9 CE FF  C9 C4 FF E2 0D C9 FA FF",
                    "00000B20  C9 FC FF C9 FD FF 00 02  04 06 E2 0D C9 FD FF C9",
                    "00000B30  FE FF C9 FF FF 00 01 02  03 E2 0B C9 F6 FF 00 0A",
                    "00000B40  14 1E C8 28 C8 32 E2 11  0A 00 C9 EC FF C9 E2 FF",
                    "00000B50  C9 D8 FF C9 CE FF C9 C4  FF E2 0D C9 FA FF C9 FC",
                    "00000B60  FF C9 FD FF 00 02 04 06  E2 0D C9 FD FF C9 FE FF",
                    "00000B70  C9 FF FF 00 01 02 03 E2  0B C9 F6 FF 00 0A 14 1E",
                    "00000B80  C8 28 C8 32 E2 11 0A 00  C9 EC FF C9 E2 FF C9 D8",
                    "00000B90  FF C9 CE FF C9 C4 FF E2  0D C9 FA FF C9 FC FF C9",
                    "00000BA0  FD FF 00 02 04 06 E2 0D  C9 FD FF C9 FE FF C9 FF",
                    "00000BB0  FF 00 01 02 03 E2 0B C9  F6 FF 00 0A 14 1E C8 28",
                    "00000BC0  C8 32 E2 11 0A 00 C9 EC  FF C9 E2 FF C9 D8 FF C9",
                    "00000BD0  CE FF C9 C4 FF E2 0D C9  FA FF C9 FC FF C9 FD FF",
                    "00000BE0  00 02 04 06 E2 0D C9 FD  FF C9 FE FF C9 FF FF 00",
                    "00000BF0  01 02 03 E2 0B C9 F6 FF  00 0A 14 1E C8 28 C8 32",
                    "00000C00  E2 11 0A 00 C9 EC FF C9  E2 FF C9 D8 FF C9 CE FF",
                    "00000C10  C9 C4 FF E2 0D C9 FA FF  C9 FC FF C9 FD FF 00 02",
                    "00000C20  04 06 E2 0D C9 FD FF C9  FE FF C9 FF FF 00 01 02",
                    "00000C30  03 E2 0B C9 F6 FF 00 0A  14 1E C8 28 C8 32 E2 11",
                    "00000C40  0A 00 C9 EC FF C9 E2 FF  C9 D8 FF C9 CE FF C9 C4",
                    "00000C50  FF E2 0D C9 FA FF C9 FC  FF C9 FD FF 00 02 04 06",
                    "00000C60  E2 0D C9 FD FF C9 FE FF  C9 FF FF 00 01 02 03 E2",
                    "00000C70  0B C9 F6 FF 00 0A 14 1E  C8 28 C8 32 E2 11 0A 00",
                    "00000C80  C9 EC FF C9 E2 FF C9 D8  FF C9 CE FF C9 C4 FF E2",
                    "00000C90  0D C9 FA FF C9 FC FF C9  FD FF 00 02 04 06 E2 0D",
                    "00000CA0  C9 FD FF C9 FE FF C9 FF  FF 00 01 02 03 E2 0B C9",
                    "00000CB0  F6 FF 00 0A 14 1E C8 28  C8 32 E2 11 0A 00 C9 EC",
                    "00000CC0  FF C9 E2 FF C9 D8 FF C9  CE FF C9 C4 FF E2 0D C9",
                    "00000CD0  FA FF C9 FC FF C9 FD FF  00 02 04 06 E2 0D C9 FD",
                    "00000CE0  FF C9 FE FF C9 FF FF 00  01 02 03 E2 0B C9 F6 FF",
                    "00000CF0  00 0A 14 1E C8 28 C8 32  E2 11 0A 00 C9 EC FF C9",
                    "00000D00  E2 FF C9 D8 FF C9 CE FF  C9 C4 FF E2 0D C9 FA FF",
                    "00000D10  C9 FC FF C9 FD FF 00 02  04 06 E2 0D C9 FD FF C9",
                    "00000D20  FE FF C9 FF FF 00 01 02  03 E2 0B C9 F6 FF 00 0A",
                    "00000D30  14 1E C8 28 C8 32 E2 11  0A 00 C9 EC FF C9 E2 FF",
                    "00000D40  C9 D8 FF C9 CE FF C9 C4  FF E2 0D C9 FA FF C9 FC",
                    "00000D50  FF C9 FD FF 00 02 04 06  E2 0D C9 FD FF C9 FE FF",
                    "00000D60  C9 FF FF 00 01 02 03 E2  0B C9 F6 FF 00 0A 14 1E",
                    "00000D70  C8 28 C8 32 E2 11 0A 00  C9 EC FF C9 E2 FF C9 D8",
                    "00000D80  FF C9 CE FF C9 C4 FF E2  0D C9 FA FF C9 FC FF C9",
                    "00000D90  FD FF 00 02 04 06 E2 0D  C9 FD FF C9 FE FF C9 FF",
                    "00000DA0  FF 00 01 02 03 E2 0B C9  F6 FF 00 0A 14 1E C8 28",
                    "00000DB0  C8 32 E2 11 0A 00 C9 EC  FF C9 E2 FF C9 D8 FF C9",
                    "00000DC0  CE FF C9 C4 FF E2 0D C9  FA FF C9 FC FF C9 FD FF",
                    "00000DD0  00 02 04 06 E2 0D C9 FD  FF C9 FE FF C9 FF FF 00",
                    "00000DE0  01 02 03 E2 0B C9 F6 FF  00 0A 14 1E C8 28 C8 32",
                    "00000DF0  E2 11 0A 00 C9 EC FF C9  E2 FF C9 D8 FF C9 CE FF",
                    "00000E00  C9 C4 FF E2 0D C9 FA FF  C9 FC FF C9 FD FF 00 02",
                    "00000E10  04 06 E2 0D C9 FD FF C9  FE FF C9 FF FF 00 01 02",
                    "00000E20  03 E2 0B C9 F6 FF 00 0A  14 1E C8 28 C8 32 E2 11",
                    "00000E30  0A 00 C9 EC FF C9 E2 FF  C9 D8 FF C9 CE FF C9 C4",
                    "00000E40  FF E2 0D C9 FA FF C9 FC  FF C9 FD FF 00 02 04 06",
                    "00000E50  E2 0D C9 FD FF C9 FE FF  C9 FF FF 00 01 02 03 E2",
                    "00000E60  0B C9 F6 FF 00 0A 14 1E  C8 28 C8 32 E2 11 0A 00",
                    "00000E70  C9 EC FF C9 E2 FF C9 D8  FF C9 CE FF C9 C4 FF E2",
                    "00000E80  0D C9 FA FF C9 FC FF C9  FD FF 00 02 04 06 E2 0D",
                    "00000E90  C9 FD FF C9 FE FF C9 FF  FF 00 01 02 03 E2 0B C9",
                    "00000EA0  F6 FF 00 0A 14 1E C8 28  C8 32 E2 11 0A 00 C9 EC",
                    "00000EB0  FF C9 E2 FF C9 D8 FF C9  CE FF C9 C4 FF E2 0D C9",
                    "00000EC0  FA FF C9 FC FF C9 FD FF  00 02 04 06 E2 0D C9 FD",
                    "00000ED0  FF C9 FE FF C9 FF FF 00  01 02 03 E2 0B C9 F6 FF",
                    "00000EE0  00 0A 14 1E C8 28 C8 32  E2 11 0A 00 C9 EC FF C9",
                    "00000EF0  E2 FF C9 D8 FF C9 CE FF  C9 C4 FF E2 0D C9 FA FF",
                    "00000F00  C9 FC FF C9 FD FF 00 02  04 06 E2 0D C9 FD FF C9",
                    "00000F10  FE FF C9 FF FF 00 01 02  03 E2 0B C9 F6 FF 00 0A",
                    "00000F20  14 1E C8 28 C8 32 E2 11  0A 00 C9 EC FF C9 E2 FF",
                    "00000F30  C9 D8 FF C9 CE FF C9 C4  FF E2 0D C9 FA FF C9 FC",
                    "00000F40  FF C9 FD FF 00 02 04 06  E2 0D C9 FD FF C9 FE FF",
                    "00000F50  C9 FF FF 00 01 02 03 E2  0B C9 F6 FF 00 0A 14 1E",
                    "00000F60  C8 28 C8 32 E2 11 0A 00  C9 EC FF C9 E2 FF C9 D8",
                    "00000F70  FF C9 CE FF C9 C4 FF E2  0D C9 FA FF C9 FC FF C9",
                    "00000F80  FD FF 00 02 04 06 E2 0D  C9 FD FF C9 FE FF C9 FF",
                    "00000F90  FF 00 01 02 03 E2 0B C9  F6 FF 00 0A 14 1E C8 28",
                    "00000FA0  C8 32 E2 11 0A 00 C9 EC  FF C9 E2 FF C9 D8 FF C9",
                    "00000FB0  CE FF C9 C4 FF E2 0D C9  FA FF C9 FC FF C9 FD FF",
                    "00000FC0  00 02 04 06"
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 F3 F1 D8 07 00 04 01  FD FE FF 00 01 02 03 F6",
                    "00000010  00 0A 14 1E 28 32 0A 00  EC E2 D8 CE C4 FA FC FD",
                    "00000020  00 02 04 06 FD FE FF 00  01 02 03 F6 00 0A 14 1E",
                    "00000030  28 32 0A 00 EC E2 D8 CE  C4 FA FC FD 00 02 04 06",
                    "00000040  FD FE FF 00 01 02 03 F6  00 0A 14 1E 28 32 0A 00",
                    "00000050  EC E2 D8 CE C4 FA FC FD  00 02 04 06 FD FE FF 00",
                    "00000060  01 02 03 F6 00 0A 14 1E  28 32 0A 00 EC E2 D8 CE",
                    "00000070  C4 FA FC FD 00 02 04 06  FD FE FF 00 01 02 03 F6",
                    "00000080  00 0A 14 1E 28 32 0A 00  EC E2 D8 CE C4 FA FC FD",
                    "00000090  00 02 04 06 FD FE FF 00  01 02 03 F6 00 0A 14 1E",
                    "000000A0  28 32 0A 00 EC E2 D8 CE  C4 FA FC FD 00 02 04 06",
                    "000000B0  FD FE FF 00 01 02 03 F6  00 0A 14 1E 28 32 0A 00",
                    "000000C0  EC E2 D8 CE C4 FA FC FD  00 02 04 06 FD FE FF 00",
                    "000000D0  01 02 03 F6 00 0A 14 1E  28 32 0A 00 EC E2 D8 CE",
                    "000000E0  C4 FA FC FD 00 02 04 06  FD FE FF 00 01 02 03 F6",
                    "000000F0  00 0A 14 1E 28 32 0A 00  EC E2 D8 CE C4 FA FC FD",
                    "00000100  00 02 04 06 FD FE FF 00  01 02 03 F6 00 0A 14 1E",
                    "00000110  28 32 0A 00 EC E2 D8 CE  C4 FA FC FD 00 02 04 06",
                    "00000120  FD FE FF 00 01 02 03 F6  00 0A 14 1E 28 32 0A 00",
                    "00000130  EC E2 D8 CE C4 FA FC FD  00 02 04 06 FD FE FF 00",
                    "00000140  01 02 03 F6 00 0A 14 1E  28 32 0A 00 EC E2 D8 CE",
                    "00000150  C4 FA FC FD 00 02 04 06  FD FE FF 00 01 02 03 F6",
                    "00000160  00 0A 14 1E 28 32 0A 00  EC E2 D8 CE C4 FA FC FD",
                    "00000170  00 02 04 06 FD FE FF 00  01 02 03 F6 00 0A 14 1E",
                    "00000180  28 32 0A 00 EC E2 D8 CE  C4 FA FC FD 00 02 04 06",
                    "00000190  FD FE FF 00 01 02 03 F6  00 0A 14 1E 28 32 0A 00",
                    "000001A0  EC E2 D8 CE C4 FA FC FD  00 02 04 06 FD FE FF 00",
                    "000001B0  01 02 03 F6 00 0A 14 1E  28 32 0A 00 EC E2 D8 CE",
                    "000001C0  C4 FA FC FD 00 02 04 06  FD FE FF 00 01 02 03 F6",
                    "000001D0  00 0A 14 1E 28 32 0A 00  EC E2 D8 CE C4 FA FC FD",
                    "000001E0  00 02 04 06 FD FE FF 00  01 02 03 F6 00 0A 14 1E",
                    "000001F0  28 32 0A 00 EC E2 D8 CE  C4 FA FC FD 00 02 04 06",
                    "00000200  FD FE FF 00 01 02 03 F6  00 0A 14 1E 28 32 0A 00",
                    "00000210  EC E2 D8 CE C4 FA FC FD  00 02 04 06 FD FE FF 00",
                    "00000220  01 02 03 F6 00 0A 14 1E  28 32 0A 00 EC E2 D8 CE",
                    "00000230  C4 FA FC FD 00 02 04 06  FD FE FF 00 01 02 03 F6",
                    "00000240  00 0A 14 1E 28 32 0A 00  EC E2 D8 CE C4 FA FC FD",
                    "00000250  00 02 04 06 FD FE FF 00  01 02 03 F6 00 0A 14 1E",
                    "00000260  28 32 0A 00 EC E2 D8 CE  C4 FA FC FD 00 02 04 06",
                    "00000270  FD FE FF 00 01 02 03 F6  00 0A 14 1E 28 32 0A 00",
                    "00000280  EC E2 D8 CE C4 FA FC FD  00 02 04 06 FD FE FF 00",
                    "00000290  01 02 03 F6 00 0A 14 1E  28 32 0A 00 EC E2 D8 CE",
                    "000002A0  C4 FA FC FD 00 02 04 06  FD FE FF 00 01 02 03 F6",
                    "000002B0  00 0A 14 1E 28 32 0A 00  EC E2 D8 CE C4 FA FC FD",
                    "000002C0  00 02 04 06 FD FE FF 00  01 02 03 F6 00 0A 14 1E",
                    "000002D0  28 32 0A 00 EC E2 D8 CE  C4 FA FC FD 00 02 04 06",
                    "000002E0  FD FE FF 00 01 02 03 F6  00 0A 14 1E 28 32 0A 00",
                    "000002F0  EC E2 D8 CE C4 FA FC FD  00 02 04 06 FD FE FF 00",
                    "00000300  01 02 03 F6 00 0A 14 1E  28 32 0A 00 EC E2 D8 CE",
                    "00000310  C4 FA FC FD 00 02 04 06  FD FE FF 00 01 02 03 F6",
                    "00000320  00 0A 14 1E 28 32 0A 00  EC E2 D8 CE C4 FA FC FD",
                    "00000330  00 02 04 06 FD FE FF 00  01 02 03 F6 00 0A 14 1E",
                    "00000340  28 32 0A 00 EC E2 D8 CE  C4 FA FC FD 00 02 04 06",
                    "00000350  FD FE FF 00 01 02 03 F6  00 0A 14 1E 28 32 0A 00",
                    "00000360  EC E2 D8 CE C4 FA FC FD  00 02 04 06 FD FE FF 00",
                    "00000370  01 02 03 F6 00 0A 14 1E  28 32 0A 00 EC E2 D8 CE",
                    "00000380  C4 FA FC FD 00 02 04 06  FD FE FF 00 01 02 03 F6",
                    "00000390  00 0A 14 1E 28 32 0A 00  EC E2 D8 CE C4 FA FC FD",
                    "000003A0  00 02 04 06 FD FE FF 00  01 02 03 F6 00 0A 14 1E",
                    "000003B0  28 32 0A 00 EC E2 D8 CE  C4 FA FC FD 00 02 04 06",
                    "000003C0  FD FE FF 00 01 02 03 F6  00 0A 14 1E 28 32 0A 00",
                    "000003D0  EC E2 D8 CE C4 FA FC FD  00 02 04 06 FD FE FF 00",
                    "000003E0  01 02 03 F6 00 0A 14 1E  28 32 0A 00 EC E2 D8 CE",
                    "000003F0  C4 FA FC FD 00 02 04 06  FD FE FF 00 01 02 03 F6",
                    "00000400  00 0A 14 1E 28 32 0A 00  EC E2 D8 CE C4 FA FC FD",
                    "00000410  00 02 04 06 FD FE FF 00  01 02 03 F6 00 0A 14 1E",
                    "00000420  28 32 0A 00 EC E2 D8 CE  C4 FA FC FD 00 02 04 06",
                    "00000430  FD FE FF 00 01 02 03 F6  00 0A 14 1E 28 32 0A 00",
                    "00000440  EC E2 D8 CE C4 FA FC FD  00 02 04 06 FD FE FF 00",
                    "00000450  01 02 03 F6 00 0A 14 1E  28 32 0A 00 EC E2 D8 CE",
                    "00000460  C4 FA FC FD 00 02 04 06  FD FE FF 00 01 02 03 F6",
                    "00000470  00 0A 14 1E 28 32 0A 00  EC E2 D8 CE C4 FA FC FD",
                    "00000480  00 02 04 06 FD FE FF 00  01 02 03 F6 00 0A 14 1E",
                    "00000490  28 32 0A 00 EC E2 D8 CE  C4 FA FC FD 00 02 04 06",
                    "000004A0  FD FE FF 00 01 02 03 F6  00 0A 14 1E 28 32 0A 00",
                    "000004B0  EC E2 D8 CE C4 FA FC FD  00 02 04 06 FD FE FF 00",
                    "000004C0  01 02 03 F6 00 0A 14 1E  28 32 0A 00 EC E2 D8 CE",
                    "000004D0  C4 FA FC FD 00 02 04 06  FD FE FF 00 01 02 03 F6",
                    "000004E0  00 0A 14 1E 28 32 0A 00  EC E2 D8 CE C4 FA FC FD",
                    "000004F0  00 02 04 06 FD FE FF 00  01 02 03 F6 00 0A 14 1E",
                    "00000500  28 32 0A 00 EC E2 D8 CE  C4 FA FC FD 00 02 04 06",
                    "00000510  FD FE FF 00 01 02 03 F6  00 0A 14 1E 28 32 0A 00",
                    "00000520  EC E2 D8 CE C4 FA FC FD  00 02 04 06 FD FE FF 00",
                    "00000530  01 02 03 F6 00 0A 14 1E  28 32 0A 00 EC E2 D8 CE",
                    "00000540  C4 FA FC FD 00 02 04 06  FD FE FF 00 01 02 03 F6",
                    "00000550  00 0A 14 1E 28 32 0A 00  EC E2 D8 CE C4 FA FC FD",
                    "00000560  00 02 04 06 FD FE FF 00  01 02 03 F6 00 0A 14 1E",
                    "00000570  28 32 0A 00 EC E2 D8 CE  C4 FA FC FD 00 02 04 06",
                    "00000580  FD FE FF 00 01 02 03 F6  00 0A 14 1E 28 32 0A 00",
                    "00000590  EC E2 D8 CE C4 FA FC FD  00 02 04 06 FD FE FF 00",
                    "000005A0  01 02 03 F6 00 0A 14 1E  28 32 0A 00 EC E2 D8 CE",
                    "000005B0  C4 FA FC FD 00 02 04 06  FD FE FF 00 01 02 03 F6",
                    "000005C0  00 0A 14 1E 28 32 0A 00  EC E2 D8 CE C4 FA FC FD",
                    "000005D0  00 02 04 06 FD FE FF 00  01 02 03 F6 00 0A 14 1E",
                    "000005E0  28 32 0A 00 EC E2 D8 CE  C4 FA FC FD 00 02 04 06",
                    "000005F0  FD FE FF 00 01 02 03 F6  00 0A 14 1E 28 32 0A 00",
                    "00000600  EC E2 D8 CE C4 FA FC FD  00 02 04 06 FD FE FF 00",
                    "00000610  01 02 03 F6 00 0A 14 1E  28 32 0A 00 EC E2 D8 CE",
                    "00000620  C4 FA FC FD 00 02 04 06  FD FE FF 00 01 02 03 F6",
                    "00000630  00 0A 14 1E 28 32 0A 00  EC E2 D8 CE C4 FA FC FD",
                    "00000640  00 02 04 06 FD FE FF 00  01 02 03 F6 00 0A 14 1E",
                    "00000650  28 32 0A 00 EC E2 D8 CE  C4 FA FC FD 00 02 04 06",
                    "00000660  FD FE FF 00 01 02 03 F6  00 0A 14 1E 28 32 0A 00",
                    "00000670  EC E2 D8 CE C4 FA FC FD  00 02 04 06 FD FE FF 00",
                    "00000680  01 02 03 F6 00 0A 14 1E  28 32 0A 00 EC E2 D8 CE",
                    "00000690  C4 FA FC FD 00 02 04 06  FD FE FF 00 01 02 03 F6",
                    "000006A0  00 0A 14 1E 28 32 0A 00  EC E2 D8 CE C4 FA FC FD",
                    "000006B0  00 02 04 06 FD FE FF 00  01 02 03 F6 00 0A 14 1E",
                    "000006C0  28 32 0A 00 EC E2 D8 CE  C4 FA FC FD 00 02 04 06",
                    "000006D0  FD FE FF 00 01 02 03 F6  00 0A 14 1E 28 32 0A 00",
                    "000006E0  EC E2 D8 CE C4 FA FC FD  00 02 04 06 FD FE FF 00",
                    "000006F0  01 02 03 F6 00 0A 14 1E  28 32 0A 00 EC E2 D8 CE",
                    "00000700  C4 FA FC FD 00 02 04 06  FD FE FF 00 01 02 03 F6",
                    "00000710  00 0A 14 1E 28 32 0A 00  EC E2 D8 CE C4 FA FC FD",
                    "00000720  00 02 04 06"
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }

            // Case 6
            {
                JsonToken[] tokensToWrite =
                {
                    JsonToken.ArrayStart(),
                        JsonToken.Int8NumberArray(valuesX1),
                        JsonToken.Int8NumberArray(valuesX1),
                    JsonToken.ArrayEnd(),
                };

                string[] expectedText =
                {
                    @"[[-1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0",
                    @",0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0",
                    @",0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0",
                    @",0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0",
                    @",0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0",
                    @",0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0",
                    @",0],[-1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,",
                    @"0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,",
                    @"0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,",
                    @"0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,",
                    @"0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,",
                    @"0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,",
                    @"0,0,0]]"
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E3 66 02 E6 2E 01 2C  01 C9 FF FF 00 00 00 00",
                    "00000010  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000020  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000030  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000040  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000050  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000060  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000070  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000080  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000090  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000000A0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000000B0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000000C0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000000D0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000000E0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000000F0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000100  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000110  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000120  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000130  00 00 00 00 00 00 00 E6  2E 01 2C 01 C9 FF FF 00",
                    "00000140  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000150  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000160  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000170  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000180  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000190  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000001A0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000001B0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000001C0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000001D0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000001E0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000001F0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000200  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000210  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000220  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000230  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000240  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000250  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000260  00 00 00 00 00 00 00 00  00 00"
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 F3 F1 D8 2C 01 02 00  FF 00 00 00 00 00 00 00",
                    "00000010  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000020  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000030  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000040  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000050  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000060  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000070  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000080  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000090  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000000A0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000000B0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000000C0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000000D0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000000E0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000000F0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000100  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000110  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000120  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000130  00 00 00 00 FF 00 00 00  00 00 00 00 00 00 00 00",
                    "00000140  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000150  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000160  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000170  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000180  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000190  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000001A0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000001B0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000001C0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000001D0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000001E0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000001F0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000200  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000210  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000220  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000230  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000240  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000250  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }

            // Case 7
            {
                JsonToken[] tokensToWrite =
                {
                    JsonToken.ArrayStart(),
                        JsonToken.Int8NumberArray(valuesX1),
                        JsonToken.Int8NumberArray(valuesX2),
                        JsonToken.Int8NumberArray(valuesX3),
                        JsonToken.Int8NumberArray(valuesX4),
                    JsonToken.ArrayEnd(),
                };

                string[] expectedText =
                {
                    @"[[-1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0",
                    @",0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0",
                    @",0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0",
                    @",0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0",
                    @",0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0",
                    @",0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0",
                    @",0],[-2,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,",
                    @"0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,",
                    @"0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,",
                    @"0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,",
                    @"0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,",
                    @"0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,",
                    @"0,0,0],[-3,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0",
                    @",0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0",
                    @",0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0",
                    @",0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0",
                    @",0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0",
                    @",0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0",
                    @",0,0,0,0],[-4,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,",
                    @"0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,",
                    @"0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,",
                    @"0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,",
                    @"0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,",
                    @"0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,",
                    @"0,0,0,0,0,0]]"
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E3 CC 04 E6 2E 01 2C  01 C9 FF FF 00 00 00 00",
                    "00000010  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000020  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000030  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000040  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000050  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000060  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000070  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000080  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000090  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000000A0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000000B0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000000C0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000000D0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000000E0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000000F0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000100  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000110  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000120  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000130  00 00 00 00 00 00 00 E6  2E 01 2C 01 C9 FE FF 00",
                    "00000140  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000150  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000160  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000170  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000180  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000190  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000001A0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000001B0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000001C0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000001D0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000001E0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000001F0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000200  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000210  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000220  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000230  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000240  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000250  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000260  00 00 00 00 00 00 00 00  00 00 E6 2E 01 2C 01 C9",
                    "00000270  FD FF 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000280  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000290  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000002A0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000002B0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000002C0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000002D0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000002E0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000002F0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000300  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000310  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000320  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000330  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000340  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000350  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000360  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000370  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000380  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000390  00 00 00 00 00 00 00 00  00 00 00 00 00 E6 2E 01",
                    "000003A0  2C 01 C9 FC FF 00 00 00  00 00 00 00 00 00 00 00",
                    "000003B0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000003C0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000003D0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000003E0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000003F0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000400  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000410  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000420  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000430  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000440  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000450  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000460  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000470  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000480  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000490  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000004A0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000004B0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000004C0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 F3 F1 D8 2C 01 04 00  FF 00 00 00 00 00 00 00",
                    "00000010  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000020  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000030  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000040  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000050  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000060  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000070  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000080  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000090  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000000A0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000000B0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000000C0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000000D0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000000E0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000000F0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000100  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000110  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000120  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000130  00 00 00 00 FE 00 00 00  00 00 00 00 00 00 00 00",
                    "00000140  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000150  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000160  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000170  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000180  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000190  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000001A0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000001B0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000001C0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000001D0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000001E0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000001F0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000200  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000210  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000220  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000230  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000240  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000250  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000260  FD 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000270  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000280  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000290  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000002A0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000002B0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000002C0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000002D0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000002E0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000002F0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000300  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000310  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000320  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000330  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000340  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000350  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000360  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000370  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000380  00 00 00 00 00 00 00 00  00 00 00 00 FC 00 00 00",
                    "00000390  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000003A0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000003B0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000003C0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000003D0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000003E0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000003F0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000400  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000410  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000420  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000430  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000440  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000450  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000460  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000470  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000480  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000490  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000004A0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000004B0  00 00 00 00 00 00 00 00"
            };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }
        }

        [TestMethod]
        [Owner("sboshra")]
        public void UniformArrayOfNumberArraysTest3()
        {
            // -------------------------
            // UInt8 Number Arrays
            // -------------------------

            byte[] values1 = { 0, 8, 16, 32, 40, 48, 56, 64 };
            byte[] values2 = { 0, 10, 20, 30, 40, 50, 60, 70 };
            byte[] values3 = { 100, 101, 102, 103, 104, 105, 106, 107 };
            byte[] values4 = { 255, 254, 253, 253, 251, 250, 249, 248 };

            byte[] valuesS1 = { 0, 1 };
            byte[] valuesS2 = { 1, 2 };
            byte[] valuesS3 = { 2, 3 };
            byte[] valuesS4 = { 3, 4 };

            // Case 1
            {
                JsonToken[] tokensToWrite =
                {
                    JsonToken.ArrayStart(),
                        JsonToken.UInt8NumberArray(values1),
                    JsonToken.ArrayEnd(),
                };

                string[] expectedText =
                {
                    @"[[0,8,16,32,40,48,56,64]]"
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E1 E2 0D 00 08 10 C8  20 C8 28 C8 30 C8 38 C8",
                    "00000010  40"
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 E1 F0 D7 08 00 08 10  20 28 30 38 40"
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }

            // Case 2
            {
                JsonToken[] tokensToWrite =
                {
                    JsonToken.ArrayStart(),
                        JsonToken.UInt8NumberArray(values1),
                        JsonToken.UInt8NumberArray(values1),
                    JsonToken.ArrayEnd(),
                };

                string[] expectedText =
                {
                    @"[[0,8,16,32,40,48,56,64],[0,8,16,32,40,48,56,64]]"
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E2 1E E2 0D 00 08 10  C8 20 C8 28 C8 30 C8 38",
                    "00000010  C8 40 E2 0D 00 08 10 C8  20 C8 28 C8 30 C8 38 C8",
                    "00000020  40"
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 F2 F0 D7 08 02 00 08  10 20 28 30 38 40 00 08",
                    "00000010  10 20 28 30 38 40"
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }

            // Case 3
            {
                JsonToken[] tokensToWrite =
                {
                    JsonToken.ArrayStart(),
                        JsonToken.UInt8NumberArray(values1),
                        JsonToken.UInt8NumberArray(values2),
                        JsonToken.UInt8NumberArray(values3),
                    JsonToken.ArrayEnd(),
                };

                string[] expectedText =
                {
                    @"[[0,8,16,32,40,48,56,64],[0,10,20,30,40,50,60,70],[100,101,102,103,104,105,106,107]]"
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E2 2F E2 0D 00 08 10  C8 20 C8 28 C8 30 C8 38",
                    "00000010  C8 40 E2 0C 00 0A 14 1E  C8 28 C8 32 C8 3C C8 46",
                    "00000020  E2 10 C8 64 C8 65 C8 66  C8 67 C8 68 C8 69 C8 6A",
                    "00000030  C8 6B"
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 F2 F0 D7 08 03 00 08  10 20 28 30 38 40 00 0A",
                    "00000010  14 1E 28 32 3C 46 64 65  66 67 68 69 6A 6B"
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }

            // Case 4
            {
                JsonToken[] tokensToWrite =
                {
                    JsonToken.ArrayStart(),
                        JsonToken.UInt8NumberArray(values1),
                        JsonToken.UInt8NumberArray(values2),
                        JsonToken.UInt8NumberArray(values3),
                        JsonToken.UInt8NumberArray(values4),
                        JsonToken.UInt8NumberArray(values4),
                        JsonToken.UInt8NumberArray(values3),
                        JsonToken.UInt8NumberArray(values2),
                        JsonToken.UInt8NumberArray(values1),
                    JsonToken.ArrayEnd(),
                };

                string[] expectedText =
                {
                    @"[[0,8,16,32,40,48,56,64],[0,10,20,30,40,50,60,70],[100,101,102,103,104,105,106,107],[255,254,253,253",
                    @",251,250,249,248],[255,254,253,253,251,250,249,248],[100,101,102,103,104,105,106,107],[0,10,20,30,40",
                    @",50,60,70],[0,8,16,32,40,48,56,64]]",
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E2 82 E2 0D 00 08 10  C8 20 C8 28 C8 30 C8 38",
                    "00000010  C8 40 E2 0C 00 0A 14 1E  C8 28 C8 32 C8 3C C8 46",
                    "00000020  E2 10 C8 64 C8 65 C8 66  C8 67 C8 68 C8 69 C8 6A",
                    "00000030  C8 6B E2 10 C8 FF C8 FE  C8 FD C8 FD C8 FB C8 FA",
                    "00000040  C8 F9 C8 F8 E2 10 C8 FF  C8 FE C8 FD C8 FD C8 FB",
                    "00000050  C8 FA C8 F9 C8 F8 E2 10  C8 64 C8 65 C8 66 C8 67",
                    "00000060  C8 68 C8 69 C8 6A C8 6B  E2 0C 00 0A 14 1E C8 28",
                    "00000070  C8 32 C8 3C C8 46 E2 0D  00 08 10 C8 20 C8 28 C8",
                    "00000080  30 C8 38 C8 40"
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 F2 F0 D7 08 08 00 08  10 20 28 30 38 40 00 0A",
                    "00000010  14 1E 28 32 3C 46 64 65  66 67 68 69 6A 6B FF FE",
                    "00000020  FD FD FB FA F9 F8 FF FE  FD FD FB FA F9 F8 64 65",
                    "00000030  66 67 68 69 6A 6B 00 0A  14 1E 28 32 3C 46 00 08",
                    "00000040  10 20 28 30 38 40"
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }

            // Case 5
            {
                byte[][] valueSets = { valuesS1, valuesS2, valuesS3, valuesS4 };

                JsonToken[] tokensToWrite = new JsonToken[1 + 260 + 1];

                tokensToWrite[0] = JsonToken.ArrayStart();
                for (int i = 0; i < tokensToWrite.Length - 2; i++)
                {
                    tokensToWrite[1 + i] = JsonToken.UInt8NumberArray(valueSets[i % valueSets.Length]);
                }
                tokensToWrite[^1] = JsonToken.ArrayEnd();

                string[] expectedText =
                {
                    @"[[0,1],[1,2],[2,3],[3,4],[0,1],[1,2],[2,3],[3,4],[0,1],[1,2],[2,3],[3,4],[0,1],[1,2],[2,3],[3,4],[0,",
                    @"1],[1,2],[2,3],[3,4],[0,1],[1,2],[2,3],[3,4],[0,1],[1,2],[2,3],[3,4],[0,1],[1,2],[2,3],[3,4],[0,1],[",
                    @"1,2],[2,3],[3,4],[0,1],[1,2],[2,3],[3,4],[0,1],[1,2],[2,3],[3,4],[0,1],[1,2],[2,3],[3,4],[0,1],[1,2]",
                    @",[2,3],[3,4],[0,1],[1,2],[2,3],[3,4],[0,1],[1,2],[2,3],[3,4],[0,1],[1,2],[2,3],[3,4],[0,1],[1,2],[2,",
                    @"3],[3,4],[0,1],[1,2],[2,3],[3,4],[0,1],[1,2],[2,3],[3,4],[0,1],[1,2],[2,3],[3,4],[0,1],[1,2],[2,3],[",
                    @"3,4],[0,1],[1,2],[2,3],[3,4],[0,1],[1,2],[2,3],[3,4],[0,1],[1,2],[2,3],[3,4],[0,1],[1,2],[2,3],[3,4]",
                    @",[0,1],[1,2],[2,3],[3,4],[0,1],[1,2],[2,3],[3,4],[0,1],[1,2],[2,3],[3,4],[0,1],[1,2],[2,3],[3,4],[0,",
                    @"1],[1,2],[2,3],[3,4],[0,1],[1,2],[2,3],[3,4],[0,1],[1,2],[2,3],[3,4],[0,1],[1,2],[2,3],[3,4],[0,1],[",
                    @"1,2],[2,3],[3,4],[0,1],[1,2],[2,3],[3,4],[0,1],[1,2],[2,3],[3,4],[0,1],[1,2],[2,3],[3,4],[0,1],[1,2]",
                    @",[2,3],[3,4],[0,1],[1,2],[2,3],[3,4],[0,1],[1,2],[2,3],[3,4],[0,1],[1,2],[2,3],[3,4],[0,1],[1,2],[2,",
                    @"3],[3,4],[0,1],[1,2],[2,3],[3,4],[0,1],[1,2],[2,3],[3,4],[0,1],[1,2],[2,3],[3,4],[0,1],[1,2],[2,3],[",
                    @"3,4],[0,1],[1,2],[2,3],[3,4],[0,1],[1,2],[2,3],[3,4],[0,1],[1,2],[2,3],[3,4],[0,1],[1,2],[2,3],[3,4]",
                    @",[0,1],[1,2],[2,3],[3,4],[0,1],[1,2],[2,3],[3,4],[0,1],[1,2],[2,3],[3,4],[0,1],[1,2],[2,3],[3,4],[0,",
                    @"1],[1,2],[2,3],[3,4],[0,1],[1,2],[2,3],[3,4],[0,1],[1,2],[2,3],[3,4],[0,1],[1,2],[2,3],[3,4],[0,1],[",
                    @"1,2],[2,3],[3,4],[0,1],[1,2],[2,3],[3,4],[0,1],[1,2],[2,3],[3,4],[0,1],[1,2],[2,3],[3,4],[0,1],[1,2]",
                    @",[2,3],[3,4],[0,1],[1,2],[2,3],[3,4],[0,1],[1,2],[2,3],[3,4]]",
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E6 10 04 04 01 E2 02  00 01 E2 02 01 02 E2 02",
                    "00000010  02 03 E2 02 03 04 E2 02  00 01 E2 02 01 02 E2 02",
                    "00000020  02 03 E2 02 03 04 E2 02  00 01 E2 02 01 02 E2 02",
                    "00000030  02 03 E2 02 03 04 E2 02  00 01 E2 02 01 02 E2 02",
                    "00000040  02 03 E2 02 03 04 E2 02  00 01 E2 02 01 02 E2 02",
                    "00000050  02 03 E2 02 03 04 E2 02  00 01 E2 02 01 02 E2 02",
                    "00000060  02 03 E2 02 03 04 E2 02  00 01 E2 02 01 02 E2 02",
                    "00000070  02 03 E2 02 03 04 E2 02  00 01 E2 02 01 02 E2 02",
                    "00000080  02 03 E2 02 03 04 E2 02  00 01 E2 02 01 02 E2 02",
                    "00000090  02 03 E2 02 03 04 E2 02  00 01 E2 02 01 02 E2 02",
                    "000000A0  02 03 E2 02 03 04 E2 02  00 01 E2 02 01 02 E2 02",
                    "000000B0  02 03 E2 02 03 04 E2 02  00 01 E2 02 01 02 E2 02",
                    "000000C0  02 03 E2 02 03 04 E2 02  00 01 E2 02 01 02 E2 02",
                    "000000D0  02 03 E2 02 03 04 E2 02  00 01 E2 02 01 02 E2 02",
                    "000000E0  02 03 E2 02 03 04 E2 02  00 01 E2 02 01 02 E2 02",
                    "000000F0  02 03 E2 02 03 04 E2 02  00 01 E2 02 01 02 E2 02",
                    "00000100  02 03 E2 02 03 04 E2 02  00 01 E2 02 01 02 E2 02",
                    "00000110  02 03 E2 02 03 04 E2 02  00 01 E2 02 01 02 E2 02",
                    "00000120  02 03 E2 02 03 04 E2 02  00 01 E2 02 01 02 E2 02",
                    "00000130  02 03 E2 02 03 04 E2 02  00 01 E2 02 01 02 E2 02",
                    "00000140  02 03 E2 02 03 04 E2 02  00 01 E2 02 01 02 E2 02",
                    "00000150  02 03 E2 02 03 04 E2 02  00 01 E2 02 01 02 E2 02",
                    "00000160  02 03 E2 02 03 04 E2 02  00 01 E2 02 01 02 E2 02",
                    "00000170  02 03 E2 02 03 04 E2 02  00 01 E2 02 01 02 E2 02",
                    "00000180  02 03 E2 02 03 04 E2 02  00 01 E2 02 01 02 E2 02",
                    "00000190  02 03 E2 02 03 04 E2 02  00 01 E2 02 01 02 E2 02",
                    "000001A0  02 03 E2 02 03 04 E2 02  00 01 E2 02 01 02 E2 02",
                    "000001B0  02 03 E2 02 03 04 E2 02  00 01 E2 02 01 02 E2 02",
                    "000001C0  02 03 E2 02 03 04 E2 02  00 01 E2 02 01 02 E2 02",
                    "000001D0  02 03 E2 02 03 04 E2 02  00 01 E2 02 01 02 E2 02",
                    "000001E0  02 03 E2 02 03 04 E2 02  00 01 E2 02 01 02 E2 02",
                    "000001F0  02 03 E2 02 03 04 E2 02  00 01 E2 02 01 02 E2 02",
                    "00000200  02 03 E2 02 03 04 E2 02  00 01 E2 02 01 02 E2 02",
                    "00000210  02 03 E2 02 03 04 E2 02  00 01 E2 02 01 02 E2 02",
                    "00000220  02 03 E2 02 03 04 E2 02  00 01 E2 02 01 02 E2 02",
                    "00000230  02 03 E2 02 03 04 E2 02  00 01 E2 02 01 02 E2 02",
                    "00000240  02 03 E2 02 03 04 E2 02  00 01 E2 02 01 02 E2 02",
                    "00000250  02 03 E2 02 03 04 E2 02  00 01 E2 02 01 02 E2 02",
                    "00000260  02 03 E2 02 03 04 E2 02  00 01 E2 02 01 02 E2 02",
                    "00000270  02 03 E2 02 03 04 E2 02  00 01 E2 02 01 02 E2 02",
                    "00000280  02 03 E2 02 03 04 E2 02  00 01 E2 02 01 02 E2 02",
                    "00000290  02 03 E2 02 03 04 E2 02  00 01 E2 02 01 02 E2 02",
                    "000002A0  02 03 E2 02 03 04 E2 02  00 01 E2 02 01 02 E2 02",
                    "000002B0  02 03 E2 02 03 04 E2 02  00 01 E2 02 01 02 E2 02",
                    "000002C0  02 03 E2 02 03 04 E2 02  00 01 E2 02 01 02 E2 02",
                    "000002D0  02 03 E2 02 03 04 E2 02  00 01 E2 02 01 02 E2 02",
                    "000002E0  02 03 E2 02 03 04 E2 02  00 01 E2 02 01 02 E2 02",
                    "000002F0  02 03 E2 02 03 04 E2 02  00 01 E2 02 01 02 E2 02",
                    "00000300  02 03 E2 02 03 04 E2 02  00 01 E2 02 01 02 E2 02",
                    "00000310  02 03 E2 02 03 04 E2 02  00 01 E2 02 01 02 E2 02",
                    "00000320  02 03 E2 02 03 04 E2 02  00 01 E2 02 01 02 E2 02",
                    "00000330  02 03 E2 02 03 04 E2 02  00 01 E2 02 01 02 E2 02",
                    "00000340  02 03 E2 02 03 04 E2 02  00 01 E2 02 01 02 E2 02",
                    "00000350  02 03 E2 02 03 04 E2 02  00 01 E2 02 01 02 E2 02",
                    "00000360  02 03 E2 02 03 04 E2 02  00 01 E2 02 01 02 E2 02",
                    "00000370  02 03 E2 02 03 04 E2 02  00 01 E2 02 01 02 E2 02",
                    "00000380  02 03 E2 02 03 04 E2 02  00 01 E2 02 01 02 E2 02",
                    "00000390  02 03 E2 02 03 04 E2 02  00 01 E2 02 01 02 E2 02",
                    "000003A0  02 03 E2 02 03 04 E2 02  00 01 E2 02 01 02 E2 02",
                    "000003B0  02 03 E2 02 03 04 E2 02  00 01 E2 02 01 02 E2 02",
                    "000003C0  02 03 E2 02 03 04 E2 02  00 01 E2 02 01 02 E2 02",
                    "000003D0  02 03 E2 02 03 04 E2 02  00 01 E2 02 01 02 E2 02",
                    "000003E0  02 03 E2 02 03 04 E2 02  00 01 E2 02 01 02 E2 02",
                    "000003F0  02 03 E2 02 03 04 E2 02  00 01 E2 02 01 02 E2 02",
                    "00000400  02 03 E2 02 03 04 E2 02  00 01 E2 02 01 02 E2 02",
                    "00000410  02 03 E2 02 03 04"
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 F3 F1 D7 02 00 04 01  00 01 01 02 02 03 03 04",
                    "00000010  00 01 01 02 02 03 03 04  00 01 01 02 02 03 03 04",
                    "00000020  00 01 01 02 02 03 03 04  00 01 01 02 02 03 03 04",
                    "00000030  00 01 01 02 02 03 03 04  00 01 01 02 02 03 03 04",
                    "00000040  00 01 01 02 02 03 03 04  00 01 01 02 02 03 03 04",
                    "00000050  00 01 01 02 02 03 03 04  00 01 01 02 02 03 03 04",
                    "00000060  00 01 01 02 02 03 03 04  00 01 01 02 02 03 03 04",
                    "00000070  00 01 01 02 02 03 03 04  00 01 01 02 02 03 03 04",
                    "00000080  00 01 01 02 02 03 03 04  00 01 01 02 02 03 03 04",
                    "00000090  00 01 01 02 02 03 03 04  00 01 01 02 02 03 03 04",
                    "000000A0  00 01 01 02 02 03 03 04  00 01 01 02 02 03 03 04",
                    "000000B0  00 01 01 02 02 03 03 04  00 01 01 02 02 03 03 04",
                    "000000C0  00 01 01 02 02 03 03 04  00 01 01 02 02 03 03 04",
                    "000000D0  00 01 01 02 02 03 03 04  00 01 01 02 02 03 03 04",
                    "000000E0  00 01 01 02 02 03 03 04  00 01 01 02 02 03 03 04",
                    "000000F0  00 01 01 02 02 03 03 04  00 01 01 02 02 03 03 04",
                    "00000100  00 01 01 02 02 03 03 04  00 01 01 02 02 03 03 04",
                    "00000110  00 01 01 02 02 03 03 04  00 01 01 02 02 03 03 04",
                    "00000120  00 01 01 02 02 03 03 04  00 01 01 02 02 03 03 04",
                    "00000130  00 01 01 02 02 03 03 04  00 01 01 02 02 03 03 04",
                    "00000140  00 01 01 02 02 03 03 04  00 01 01 02 02 03 03 04",
                    "00000150  00 01 01 02 02 03 03 04  00 01 01 02 02 03 03 04",
                    "00000160  00 01 01 02 02 03 03 04  00 01 01 02 02 03 03 04",
                    "00000170  00 01 01 02 02 03 03 04  00 01 01 02 02 03 03 04",
                    "00000180  00 01 01 02 02 03 03 04  00 01 01 02 02 03 03 04",
                    "00000190  00 01 01 02 02 03 03 04  00 01 01 02 02 03 03 04",
                    "000001A0  00 01 01 02 02 03 03 04  00 01 01 02 02 03 03 04",
                    "000001B0  00 01 01 02 02 03 03 04  00 01 01 02 02 03 03 04",
                    "000001C0  00 01 01 02 02 03 03 04  00 01 01 02 02 03 03 04",
                    "000001D0  00 01 01 02 02 03 03 04  00 01 01 02 02 03 03 04",
                    "000001E0  00 01 01 02 02 03 03 04  00 01 01 02 02 03 03 04",
                    "000001F0  00 01 01 02 02 03 03 04  00 01 01 02 02 03 03 04",
                    "00000200  00 01 01 02 02 03 03 04  00 01 01 02 02 03 03 04",
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }
        }

        [TestMethod]
        [Owner("sboshra")]
        public void UniformArrayOfNumberArraysTest4()
        {
            // -------------------------
            // Int16 Number Arrays
            // -------------------------

            short[] values1 = { -3000, -2000, -1000, 0, 1000, 2000, 3000 };
            short[] values2 = { 1000, 2000, 3000, 4000, 5000, 6000, 7000 };
            short[] values3 = { -1000, -2000, -3000, -4000, -5000, -6000, -7000 };
            short[] values4 = { 2000, 4000, 6000, 8000, 10000, 12000, 14000 };

            short[] valuesS1 = { 1024, 2048 };
            short[] valuesS2 = { 2048, 4096 };
            short[] valuesS3 = { 4096, 8192 };
            short[] valuesS4 = { 8192, 1024 };

            // Case 1
            {
                JsonToken[] tokensToWrite =
                {
                    JsonToken.ArrayStart(),
                        JsonToken.Int16NumberArray(values1),
                    JsonToken.ArrayEnd(),
                };

                string[] expectedText =
                {
                    @"[[-3000,-2000,-1000,0,1000,2000,3000]]"
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E1 E2 13 C9 48 F4 C9  30 F8 C9 18 FC 00 C9 E8",
                    "00000010  03 C9 D0 07 C9 B8 0B"
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 E1 F0 D9 07 48 F4 30  F8 18 FC 00 00 E8 03 D0",
                    "00000010  07 B8 0B"
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }

            // Case 2
            {
                JsonToken[] tokensToWrite =
                {
                    JsonToken.ArrayStart(),
                        JsonToken.Int16NumberArray(values1),
                        JsonToken.Int16NumberArray(values1),
                    JsonToken.ArrayEnd(),
                };

                string[] expectedText =
                {
                    @"[[-3000,-2000,-1000,0,1000,2000,3000],[-3000,-2000,-1000,0,1000,2000,3000]]"
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E2 2A E2 13 C9 48 F4  C9 30 F8 C9 18 FC 00 C9",
                    "00000010  E8 03 C9 D0 07 C9 B8 0B  E2 13 C9 48 F4 C9 30 F8",
                    "00000020  C9 18 FC 00 C9 E8 03 C9  D0 07 C9 B8 0B"
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 F2 F0 D9 07 02 48 F4  30 F8 18 FC 00 00 E8 03",
                    "00000010  D0 07 B8 0B 48 F4 30 F8  18 FC 00 00 E8 03 D0 07",
                    "00000020  B8 0B"
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }

            // Case 3
            {
                JsonToken[] tokensToWrite =
                {
                    JsonToken.ArrayStart(),
                        JsonToken.Int16NumberArray(values1),
                        JsonToken.Int16NumberArray(values2),
                        JsonToken.Int16NumberArray(values3),
                    JsonToken.ArrayEnd(),
                };

                string[] expectedText =
                {
                    @"[[-3000,-2000,-1000,0,1000,2000,3000],[1000,2000,3000,4000,5000,6000,7000],[-1000,-2000,-3000,-4000,",
                    @"-5000,-6000,-7000]]",
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E2 43 E2 13 C9 48 F4  C9 30 F8 C9 18 FC 00 C9",
                    "00000010  E8 03 C9 D0 07 C9 B8 0B  E2 15 C9 E8 03 C9 D0 07",
                    "00000020  C9 B8 0B C9 A0 0F C9 88  13 C9 70 17 C9 58 1B E2",
                    "00000030  15 C9 18 FC C9 30 F8 C9  48 F4 C9 60 F0 C9 78 EC",
                    "00000040  C9 90 E8 C9 A8 E4"
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 F2 F0 D9 07 03 48 F4  30 F8 18 FC 00 00 E8 03",
                    "00000010  D0 07 B8 0B E8 03 D0 07  B8 0B A0 0F 88 13 70 17",
                    "00000020  58 1B 18 FC 30 F8 48 F4  60 F0 78 EC 90 E8 A8 E4",
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }

            // Case 4
            {
                JsonToken[] tokensToWrite =
                {
                    JsonToken.ArrayStart(),
                        JsonToken.Int16NumberArray(values1),
                        JsonToken.Int16NumberArray(values2),
                        JsonToken.Int16NumberArray(values3),
                        JsonToken.Int16NumberArray(values4),
                        JsonToken.Int16NumberArray(values4),
                        JsonToken.Int16NumberArray(values3),
                        JsonToken.Int16NumberArray(values2),
                        JsonToken.Int16NumberArray(values1),
                    JsonToken.ArrayEnd(),
                };

                string[] expectedText =
                {
                    @"[[-3000,-2000,-1000,0,1000,2000,3000],[1000,2000,3000,4000,5000,6000,7000],[-1000,-2000,-3000,-4000,",
                    @"-5000,-6000,-7000],[2000,4000,6000,8000,10000,12000,14000],[2000,4000,6000,8000,10000,12000,14000],[",
                    @"-1000,-2000,-3000,-4000,-5000,-6000,-7000],[1000,2000,3000,4000,5000,6000,7000],[-3000,-2000,-1000,0",
                    @",1000,2000,3000]]",
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E2 B4 E2 13 C9 48 F4  C9 30 F8 C9 18 FC 00 C9",
                    "00000010  E8 03 C9 D0 07 C9 B8 0B  E2 15 C9 E8 03 C9 D0 07",
                    "00000020  C9 B8 0B C9 A0 0F C9 88  13 C9 70 17 C9 58 1B E2",
                    "00000030  15 C9 18 FC C9 30 F8 C9  48 F4 C9 60 F0 C9 78 EC",
                    "00000040  C9 90 E8 C9 A8 E4 E2 15  C9 D0 07 C9 A0 0F C9 70",
                    "00000050  17 C9 40 1F C9 10 27 C9  E0 2E C9 B0 36 E2 15 C9",
                    "00000060  D0 07 C9 A0 0F C9 70 17  C9 40 1F C9 10 27 C9 E0",
                    "00000070  2E C9 B0 36 E2 15 C9 18  FC C9 30 F8 C9 48 F4 C9",
                    "00000080  60 F0 C9 78 EC C9 90 E8  C9 A8 E4 E2 15 C9 E8 03",
                    "00000090  C9 D0 07 C9 B8 0B C9 A0  0F C9 88 13 C9 70 17 C9",
                    "000000A0  58 1B E2 13 C9 48 F4 C9  30 F8 C9 18 FC 00 C9 E8",
                    "000000B0  03 C9 D0 07 C9 B8 0B"
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 F2 F0 D9 07 08 48 F4  30 F8 18 FC 00 00 E8 03",
                    "00000010  D0 07 B8 0B E8 03 D0 07  B8 0B A0 0F 88 13 70 17",
                    "00000020  58 1B 18 FC 30 F8 48 F4  60 F0 78 EC 90 E8 A8 E4",
                    "00000030  D0 07 A0 0F 70 17 40 1F  10 27 E0 2E B0 36 D0 07",
                    "00000040  A0 0F 70 17 40 1F 10 27  E0 2E B0 36 18 FC 30 F8",
                    "00000050  48 F4 60 F0 78 EC 90 E8  A8 E4 E8 03 D0 07 B8 0B",
                    "00000060  A0 0F 88 13 70 17 58 1B  48 F4 30 F8 18 FC 00 00",
                    "00000070  E8 03 D0 07 B8 0B"
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }

            // Case 5
            {
                short[][] valueSets = { valuesS1, valuesS2, valuesS3, valuesS4 };

                JsonToken[] tokensToWrite = new JsonToken[1 + 260 + 1];

                tokensToWrite[0] = JsonToken.ArrayStart();
                for (int i = 0; i < tokensToWrite.Length - 2; i++)
                {
                    tokensToWrite[1 + i] = JsonToken.Int16NumberArray(valueSets[i % valueSets.Length]);
                }
                tokensToWrite[^1] = JsonToken.ArrayEnd();

                string[] expectedText =
                {
                    @"[[1024,2048],[2048,4096],[4096,8192],[8192,1024],[1024,2048],[2048,4096],[4096,8192],[8192,1024],[10",
                    @"24,2048],[2048,4096],[4096,8192],[8192,1024],[1024,2048],[2048,4096],[4096,8192],[8192,1024],[1024,2",
                    @"048],[2048,4096],[4096,8192],[8192,1024],[1024,2048],[2048,4096],[4096,8192],[8192,1024],[1024,2048]",
                    @",[2048,4096],[4096,8192],[8192,1024],[1024,2048],[2048,4096],[4096,8192],[8192,1024],[1024,2048],[20",
                    @"48,4096],[4096,8192],[8192,1024],[1024,2048],[2048,4096],[4096,8192],[8192,1024],[1024,2048],[2048,4",
                    @"096],[4096,8192],[8192,1024],[1024,2048],[2048,4096],[4096,8192],[8192,1024],[1024,2048],[2048,4096]",
                    @",[4096,8192],[8192,1024],[1024,2048],[2048,4096],[4096,8192],[8192,1024],[1024,2048],[2048,4096],[40",
                    @"96,8192],[8192,1024],[1024,2048],[2048,4096],[4096,8192],[8192,1024],[1024,2048],[2048,4096],[4096,8",
                    @"192],[8192,1024],[1024,2048],[2048,4096],[4096,8192],[8192,1024],[1024,2048],[2048,4096],[4096,8192]",
                    @",[8192,1024],[1024,2048],[2048,4096],[4096,8192],[8192,1024],[1024,2048],[2048,4096],[4096,8192],[81",
                    @"92,1024],[1024,2048],[2048,4096],[4096,8192],[8192,1024],[1024,2048],[2048,4096],[4096,8192],[8192,1",
                    @"024],[1024,2048],[2048,4096],[4096,8192],[8192,1024],[1024,2048],[2048,4096],[4096,8192],[8192,1024]",
                    @",[1024,2048],[2048,4096],[4096,8192],[8192,1024],[1024,2048],[2048,4096],[4096,8192],[8192,1024],[10",
                    @"24,2048],[2048,4096],[4096,8192],[8192,1024],[1024,2048],[2048,4096],[4096,8192],[8192,1024],[1024,2",
                    @"048],[2048,4096],[4096,8192],[8192,1024],[1024,2048],[2048,4096],[4096,8192],[8192,1024],[1024,2048]",
                    @",[2048,4096],[4096,8192],[8192,1024],[1024,2048],[2048,4096],[4096,8192],[8192,1024],[1024,2048],[20",
                    @"48,4096],[4096,8192],[8192,1024],[1024,2048],[2048,4096],[4096,8192],[8192,1024],[1024,2048],[2048,4",
                    @"096],[4096,8192],[8192,1024],[1024,2048],[2048,4096],[4096,8192],[8192,1024],[1024,2048],[2048,4096]",
                    @",[4096,8192],[8192,1024],[1024,2048],[2048,4096],[4096,8192],[8192,1024],[1024,2048],[2048,4096],[40",
                    @"96,8192],[8192,1024],[1024,2048],[2048,4096],[4096,8192],[8192,1024],[1024,2048],[2048,4096],[4096,8",
                    @"192],[8192,1024],[1024,2048],[2048,4096],[4096,8192],[8192,1024],[1024,2048],[2048,4096],[4096,8192]",
                    @",[8192,1024],[1024,2048],[2048,4096],[4096,8192],[8192,1024],[1024,2048],[2048,4096],[4096,8192],[81",
                    @"92,1024],[1024,2048],[2048,4096],[4096,8192],[8192,1024],[1024,2048],[2048,4096],[4096,8192],[8192,1",
                    @"024],[1024,2048],[2048,4096],[4096,8192],[8192,1024],[1024,2048],[2048,4096],[4096,8192],[8192,1024]",
                    @",[1024,2048],[2048,4096],[4096,8192],[8192,1024],[1024,2048],[2048,4096],[4096,8192],[8192,1024],[10",
                    @"24,2048],[2048,4096],[4096,8192],[8192,1024],[1024,2048],[2048,4096],[4096,8192],[8192,1024],[1024,2",
                    @"048],[2048,4096],[4096,8192],[8192,1024],[1024,2048],[2048,4096],[4096,8192],[8192,1024],[1024,2048]",
                    @",[2048,4096],[4096,8192],[8192,1024],[1024,2048],[2048,4096],[4096,8192],[8192,1024],[1024,2048],[20",
                    @"48,4096],[4096,8192],[8192,1024],[1024,2048],[2048,4096],[4096,8192],[8192,1024],[1024,2048],[2048,4",
                    @"096],[4096,8192],[8192,1024],[1024,2048],[2048,4096],[4096,8192],[8192,1024],[1024,2048],[2048,4096]",
                    @",[4096,8192],[8192,1024],[1024,2048],[2048,4096],[4096,8192],[8192,1024],[1024,2048],[2048,4096],[40",
                    @"96,8192],[8192,1024]]",
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E6 20 08 04 01 E2 06  C9 00 04 C9 00 08 E2 06",
                    "00000010  C9 00 08 C9 00 10 E2 06  C9 00 10 C9 00 20 E2 06",
                    "00000020  C9 00 20 C9 00 04 E2 06  C9 00 04 C9 00 08 E2 06",
                    "00000030  C9 00 08 C9 00 10 E2 06  C9 00 10 C9 00 20 E2 06",
                    "00000040  C9 00 20 C9 00 04 E2 06  C9 00 04 C9 00 08 E2 06",
                    "00000050  C9 00 08 C9 00 10 E2 06  C9 00 10 C9 00 20 E2 06",
                    "00000060  C9 00 20 C9 00 04 E2 06  C9 00 04 C9 00 08 E2 06",
                    "00000070  C9 00 08 C9 00 10 E2 06  C9 00 10 C9 00 20 E2 06",
                    "00000080  C9 00 20 C9 00 04 E2 06  C9 00 04 C9 00 08 E2 06",
                    "00000090  C9 00 08 C9 00 10 E2 06  C9 00 10 C9 00 20 E2 06",
                    "000000A0  C9 00 20 C9 00 04 E2 06  C9 00 04 C9 00 08 E2 06",
                    "000000B0  C9 00 08 C9 00 10 E2 06  C9 00 10 C9 00 20 E2 06",
                    "000000C0  C9 00 20 C9 00 04 E2 06  C9 00 04 C9 00 08 E2 06",
                    "000000D0  C9 00 08 C9 00 10 E2 06  C9 00 10 C9 00 20 E2 06",
                    "000000E0  C9 00 20 C9 00 04 E2 06  C9 00 04 C9 00 08 E2 06",
                    "000000F0  C9 00 08 C9 00 10 E2 06  C9 00 10 C9 00 20 E2 06",
                    "00000100  C9 00 20 C9 00 04 E2 06  C9 00 04 C9 00 08 E2 06",
                    "00000110  C9 00 08 C9 00 10 E2 06  C9 00 10 C9 00 20 E2 06",
                    "00000120  C9 00 20 C9 00 04 E2 06  C9 00 04 C9 00 08 E2 06",
                    "00000130  C9 00 08 C9 00 10 E2 06  C9 00 10 C9 00 20 E2 06",
                    "00000140  C9 00 20 C9 00 04 E2 06  C9 00 04 C9 00 08 E2 06",
                    "00000150  C9 00 08 C9 00 10 E2 06  C9 00 10 C9 00 20 E2 06",
                    "00000160  C9 00 20 C9 00 04 E2 06  C9 00 04 C9 00 08 E2 06",
                    "00000170  C9 00 08 C9 00 10 E2 06  C9 00 10 C9 00 20 E2 06",
                    "00000180  C9 00 20 C9 00 04 E2 06  C9 00 04 C9 00 08 E2 06",
                    "00000190  C9 00 08 C9 00 10 E2 06  C9 00 10 C9 00 20 E2 06",
                    "000001A0  C9 00 20 C9 00 04 E2 06  C9 00 04 C9 00 08 E2 06",
                    "000001B0  C9 00 08 C9 00 10 E2 06  C9 00 10 C9 00 20 E2 06",
                    "000001C0  C9 00 20 C9 00 04 E2 06  C9 00 04 C9 00 08 E2 06",
                    "000001D0  C9 00 08 C9 00 10 E2 06  C9 00 10 C9 00 20 E2 06",
                    "000001E0  C9 00 20 C9 00 04 E2 06  C9 00 04 C9 00 08 E2 06",
                    "000001F0  C9 00 08 C9 00 10 E2 06  C9 00 10 C9 00 20 E2 06",
                    "00000200  C9 00 20 C9 00 04 E2 06  C9 00 04 C9 00 08 E2 06",
                    "00000210  C9 00 08 C9 00 10 E2 06  C9 00 10 C9 00 20 E2 06",
                    "00000220  C9 00 20 C9 00 04 E2 06  C9 00 04 C9 00 08 E2 06",
                    "00000230  C9 00 08 C9 00 10 E2 06  C9 00 10 C9 00 20 E2 06",
                    "00000240  C9 00 20 C9 00 04 E2 06  C9 00 04 C9 00 08 E2 06",
                    "00000250  C9 00 08 C9 00 10 E2 06  C9 00 10 C9 00 20 E2 06",
                    "00000260  C9 00 20 C9 00 04 E2 06  C9 00 04 C9 00 08 E2 06",
                    "00000270  C9 00 08 C9 00 10 E2 06  C9 00 10 C9 00 20 E2 06",
                    "00000280  C9 00 20 C9 00 04 E2 06  C9 00 04 C9 00 08 E2 06",
                    "00000290  C9 00 08 C9 00 10 E2 06  C9 00 10 C9 00 20 E2 06",
                    "000002A0  C9 00 20 C9 00 04 E2 06  C9 00 04 C9 00 08 E2 06",
                    "000002B0  C9 00 08 C9 00 10 E2 06  C9 00 10 C9 00 20 E2 06",
                    "000002C0  C9 00 20 C9 00 04 E2 06  C9 00 04 C9 00 08 E2 06",
                    "000002D0  C9 00 08 C9 00 10 E2 06  C9 00 10 C9 00 20 E2 06",
                    "000002E0  C9 00 20 C9 00 04 E2 06  C9 00 04 C9 00 08 E2 06",
                    "000002F0  C9 00 08 C9 00 10 E2 06  C9 00 10 C9 00 20 E2 06",
                    "00000300  C9 00 20 C9 00 04 E2 06  C9 00 04 C9 00 08 E2 06",
                    "00000310  C9 00 08 C9 00 10 E2 06  C9 00 10 C9 00 20 E2 06",
                    "00000320  C9 00 20 C9 00 04 E2 06  C9 00 04 C9 00 08 E2 06",
                    "00000330  C9 00 08 C9 00 10 E2 06  C9 00 10 C9 00 20 E2 06",
                    "00000340  C9 00 20 C9 00 04 E2 06  C9 00 04 C9 00 08 E2 06",
                    "00000350  C9 00 08 C9 00 10 E2 06  C9 00 10 C9 00 20 E2 06",
                    "00000360  C9 00 20 C9 00 04 E2 06  C9 00 04 C9 00 08 E2 06",
                    "00000370  C9 00 08 C9 00 10 E2 06  C9 00 10 C9 00 20 E2 06",
                    "00000380  C9 00 20 C9 00 04 E2 06  C9 00 04 C9 00 08 E2 06",
                    "00000390  C9 00 08 C9 00 10 E2 06  C9 00 10 C9 00 20 E2 06",
                    "000003A0  C9 00 20 C9 00 04 E2 06  C9 00 04 C9 00 08 E2 06",
                    "000003B0  C9 00 08 C9 00 10 E2 06  C9 00 10 C9 00 20 E2 06",
                    "000003C0  C9 00 20 C9 00 04 E2 06  C9 00 04 C9 00 08 E2 06",
                    "000003D0  C9 00 08 C9 00 10 E2 06  C9 00 10 C9 00 20 E2 06",
                    "000003E0  C9 00 20 C9 00 04 E2 06  C9 00 04 C9 00 08 E2 06",
                    "000003F0  C9 00 08 C9 00 10 E2 06  C9 00 10 C9 00 20 E2 06",
                    "00000400  C9 00 20 C9 00 04 E2 06  C9 00 04 C9 00 08 E2 06",
                    "00000410  C9 00 08 C9 00 10 E2 06  C9 00 10 C9 00 20 E2 06",
                    "00000420  C9 00 20 C9 00 04 E2 06  C9 00 04 C9 00 08 E2 06",
                    "00000430  C9 00 08 C9 00 10 E2 06  C9 00 10 C9 00 20 E2 06",
                    "00000440  C9 00 20 C9 00 04 E2 06  C9 00 04 C9 00 08 E2 06",
                    "00000450  C9 00 08 C9 00 10 E2 06  C9 00 10 C9 00 20 E2 06",
                    "00000460  C9 00 20 C9 00 04 E2 06  C9 00 04 C9 00 08 E2 06",
                    "00000470  C9 00 08 C9 00 10 E2 06  C9 00 10 C9 00 20 E2 06",
                    "00000480  C9 00 20 C9 00 04 E2 06  C9 00 04 C9 00 08 E2 06",
                    "00000490  C9 00 08 C9 00 10 E2 06  C9 00 10 C9 00 20 E2 06",
                    "000004A0  C9 00 20 C9 00 04 E2 06  C9 00 04 C9 00 08 E2 06",
                    "000004B0  C9 00 08 C9 00 10 E2 06  C9 00 10 C9 00 20 E2 06",
                    "000004C0  C9 00 20 C9 00 04 E2 06  C9 00 04 C9 00 08 E2 06",
                    "000004D0  C9 00 08 C9 00 10 E2 06  C9 00 10 C9 00 20 E2 06",
                    "000004E0  C9 00 20 C9 00 04 E2 06  C9 00 04 C9 00 08 E2 06",
                    "000004F0  C9 00 08 C9 00 10 E2 06  C9 00 10 C9 00 20 E2 06",
                    "00000500  C9 00 20 C9 00 04 E2 06  C9 00 04 C9 00 08 E2 06",
                    "00000510  C9 00 08 C9 00 10 E2 06  C9 00 10 C9 00 20 E2 06",
                    "00000520  C9 00 20 C9 00 04 E2 06  C9 00 04 C9 00 08 E2 06",
                    "00000530  C9 00 08 C9 00 10 E2 06  C9 00 10 C9 00 20 E2 06",
                    "00000540  C9 00 20 C9 00 04 E2 06  C9 00 04 C9 00 08 E2 06",
                    "00000550  C9 00 08 C9 00 10 E2 06  C9 00 10 C9 00 20 E2 06",
                    "00000560  C9 00 20 C9 00 04 E2 06  C9 00 04 C9 00 08 E2 06",
                    "00000570  C9 00 08 C9 00 10 E2 06  C9 00 10 C9 00 20 E2 06",
                    "00000580  C9 00 20 C9 00 04 E2 06  C9 00 04 C9 00 08 E2 06",
                    "00000590  C9 00 08 C9 00 10 E2 06  C9 00 10 C9 00 20 E2 06",
                    "000005A0  C9 00 20 C9 00 04 E2 06  C9 00 04 C9 00 08 E2 06",
                    "000005B0  C9 00 08 C9 00 10 E2 06  C9 00 10 C9 00 20 E2 06",
                    "000005C0  C9 00 20 C9 00 04 E2 06  C9 00 04 C9 00 08 E2 06",
                    "000005D0  C9 00 08 C9 00 10 E2 06  C9 00 10 C9 00 20 E2 06",
                    "000005E0  C9 00 20 C9 00 04 E2 06  C9 00 04 C9 00 08 E2 06",
                    "000005F0  C9 00 08 C9 00 10 E2 06  C9 00 10 C9 00 20 E2 06",
                    "00000600  C9 00 20 C9 00 04 E2 06  C9 00 04 C9 00 08 E2 06",
                    "00000610  C9 00 08 C9 00 10 E2 06  C9 00 10 C9 00 20 E2 06",
                    "00000620  C9 00 20 C9 00 04 E2 06  C9 00 04 C9 00 08 E2 06",
                    "00000630  C9 00 08 C9 00 10 E2 06  C9 00 10 C9 00 20 E2 06",
                    "00000640  C9 00 20 C9 00 04 E2 06  C9 00 04 C9 00 08 E2 06",
                    "00000650  C9 00 08 C9 00 10 E2 06  C9 00 10 C9 00 20 E2 06",
                    "00000660  C9 00 20 C9 00 04 E2 06  C9 00 04 C9 00 08 E2 06",
                    "00000670  C9 00 08 C9 00 10 E2 06  C9 00 10 C9 00 20 E2 06",
                    "00000680  C9 00 20 C9 00 04 E2 06  C9 00 04 C9 00 08 E2 06",
                    "00000690  C9 00 08 C9 00 10 E2 06  C9 00 10 C9 00 20 E2 06",
                    "000006A0  C9 00 20 C9 00 04 E2 06  C9 00 04 C9 00 08 E2 06",
                    "000006B0  C9 00 08 C9 00 10 E2 06  C9 00 10 C9 00 20 E2 06",
                    "000006C0  C9 00 20 C9 00 04 E2 06  C9 00 04 C9 00 08 E2 06",
                    "000006D0  C9 00 08 C9 00 10 E2 06  C9 00 10 C9 00 20 E2 06",
                    "000006E0  C9 00 20 C9 00 04 E2 06  C9 00 04 C9 00 08 E2 06",
                    "000006F0  C9 00 08 C9 00 10 E2 06  C9 00 10 C9 00 20 E2 06",
                    "00000700  C9 00 20 C9 00 04 E2 06  C9 00 04 C9 00 08 E2 06",
                    "00000710  C9 00 08 C9 00 10 E2 06  C9 00 10 C9 00 20 E2 06",
                    "00000720  C9 00 20 C9 00 04 E2 06  C9 00 04 C9 00 08 E2 06",
                    "00000730  C9 00 08 C9 00 10 E2 06  C9 00 10 C9 00 20 E2 06",
                    "00000740  C9 00 20 C9 00 04 E2 06  C9 00 04 C9 00 08 E2 06",
                    "00000750  C9 00 08 C9 00 10 E2 06  C9 00 10 C9 00 20 E2 06",
                    "00000760  C9 00 20 C9 00 04 E2 06  C9 00 04 C9 00 08 E2 06",
                    "00000770  C9 00 08 C9 00 10 E2 06  C9 00 10 C9 00 20 E2 06",
                    "00000780  C9 00 20 C9 00 04 E2 06  C9 00 04 C9 00 08 E2 06",
                    "00000790  C9 00 08 C9 00 10 E2 06  C9 00 10 C9 00 20 E2 06",
                    "000007A0  C9 00 20 C9 00 04 E2 06  C9 00 04 C9 00 08 E2 06",
                    "000007B0  C9 00 08 C9 00 10 E2 06  C9 00 10 C9 00 20 E2 06",
                    "000007C0  C9 00 20 C9 00 04 E2 06  C9 00 04 C9 00 08 E2 06",
                    "000007D0  C9 00 08 C9 00 10 E2 06  C9 00 10 C9 00 20 E2 06",
                    "000007E0  C9 00 20 C9 00 04 E2 06  C9 00 04 C9 00 08 E2 06",
                    "000007F0  C9 00 08 C9 00 10 E2 06  C9 00 10 C9 00 20 E2 06",
                    "00000800  C9 00 20 C9 00 04 E2 06  C9 00 04 C9 00 08 E2 06",
                    "00000810  C9 00 08 C9 00 10 E2 06  C9 00 10 C9 00 20 E2 06",
                    "00000820  C9 00 20 C9 00 04"
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 F3 F1 D9 02 00 04 01  00 04 00 08 00 08 00 10",
                    "00000010  00 10 00 20 00 20 00 04  00 04 00 08 00 08 00 10",
                    "00000020  00 10 00 20 00 20 00 04  00 04 00 08 00 08 00 10",
                    "00000030  00 10 00 20 00 20 00 04  00 04 00 08 00 08 00 10",
                    "00000040  00 10 00 20 00 20 00 04  00 04 00 08 00 08 00 10",
                    "00000050  00 10 00 20 00 20 00 04  00 04 00 08 00 08 00 10",
                    "00000060  00 10 00 20 00 20 00 04  00 04 00 08 00 08 00 10",
                    "00000070  00 10 00 20 00 20 00 04  00 04 00 08 00 08 00 10",
                    "00000080  00 10 00 20 00 20 00 04  00 04 00 08 00 08 00 10",
                    "00000090  00 10 00 20 00 20 00 04  00 04 00 08 00 08 00 10",
                    "000000A0  00 10 00 20 00 20 00 04  00 04 00 08 00 08 00 10",
                    "000000B0  00 10 00 20 00 20 00 04  00 04 00 08 00 08 00 10",
                    "000000C0  00 10 00 20 00 20 00 04  00 04 00 08 00 08 00 10",
                    "000000D0  00 10 00 20 00 20 00 04  00 04 00 08 00 08 00 10",
                    "000000E0  00 10 00 20 00 20 00 04  00 04 00 08 00 08 00 10",
                    "000000F0  00 10 00 20 00 20 00 04  00 04 00 08 00 08 00 10",
                    "00000100  00 10 00 20 00 20 00 04  00 04 00 08 00 08 00 10",
                    "00000110  00 10 00 20 00 20 00 04  00 04 00 08 00 08 00 10",
                    "00000120  00 10 00 20 00 20 00 04  00 04 00 08 00 08 00 10",
                    "00000130  00 10 00 20 00 20 00 04  00 04 00 08 00 08 00 10",
                    "00000140  00 10 00 20 00 20 00 04  00 04 00 08 00 08 00 10",
                    "00000150  00 10 00 20 00 20 00 04  00 04 00 08 00 08 00 10",
                    "00000160  00 10 00 20 00 20 00 04  00 04 00 08 00 08 00 10",
                    "00000170  00 10 00 20 00 20 00 04  00 04 00 08 00 08 00 10",
                    "00000180  00 10 00 20 00 20 00 04  00 04 00 08 00 08 00 10",
                    "00000190  00 10 00 20 00 20 00 04  00 04 00 08 00 08 00 10",
                    "000001A0  00 10 00 20 00 20 00 04  00 04 00 08 00 08 00 10",
                    "000001B0  00 10 00 20 00 20 00 04  00 04 00 08 00 08 00 10",
                    "000001C0  00 10 00 20 00 20 00 04  00 04 00 08 00 08 00 10",
                    "000001D0  00 10 00 20 00 20 00 04  00 04 00 08 00 08 00 10",
                    "000001E0  00 10 00 20 00 20 00 04  00 04 00 08 00 08 00 10",
                    "000001F0  00 10 00 20 00 20 00 04  00 04 00 08 00 08 00 10",
                    "00000200  00 10 00 20 00 20 00 04  00 04 00 08 00 08 00 10",
                    "00000210  00 10 00 20 00 20 00 04  00 04 00 08 00 08 00 10",
                    "00000220  00 10 00 20 00 20 00 04  00 04 00 08 00 08 00 10",
                    "00000230  00 10 00 20 00 20 00 04  00 04 00 08 00 08 00 10",
                    "00000240  00 10 00 20 00 20 00 04  00 04 00 08 00 08 00 10",
                    "00000250  00 10 00 20 00 20 00 04  00 04 00 08 00 08 00 10",
                    "00000260  00 10 00 20 00 20 00 04  00 04 00 08 00 08 00 10",
                    "00000270  00 10 00 20 00 20 00 04  00 04 00 08 00 08 00 10",
                    "00000280  00 10 00 20 00 20 00 04  00 04 00 08 00 08 00 10",
                    "00000290  00 10 00 20 00 20 00 04  00 04 00 08 00 08 00 10",
                    "000002A0  00 10 00 20 00 20 00 04  00 04 00 08 00 08 00 10",
                    "000002B0  00 10 00 20 00 20 00 04  00 04 00 08 00 08 00 10",
                    "000002C0  00 10 00 20 00 20 00 04  00 04 00 08 00 08 00 10",
                    "000002D0  00 10 00 20 00 20 00 04  00 04 00 08 00 08 00 10",
                    "000002E0  00 10 00 20 00 20 00 04  00 04 00 08 00 08 00 10",
                    "000002F0  00 10 00 20 00 20 00 04  00 04 00 08 00 08 00 10",
                    "00000300  00 10 00 20 00 20 00 04  00 04 00 08 00 08 00 10",
                    "00000310  00 10 00 20 00 20 00 04  00 04 00 08 00 08 00 10",
                    "00000320  00 10 00 20 00 20 00 04  00 04 00 08 00 08 00 10",
                    "00000330  00 10 00 20 00 20 00 04  00 04 00 08 00 08 00 10",
                    "00000340  00 10 00 20 00 20 00 04  00 04 00 08 00 08 00 10",
                    "00000350  00 10 00 20 00 20 00 04  00 04 00 08 00 08 00 10",
                    "00000360  00 10 00 20 00 20 00 04  00 04 00 08 00 08 00 10",
                    "00000370  00 10 00 20 00 20 00 04  00 04 00 08 00 08 00 10",
                    "00000380  00 10 00 20 00 20 00 04  00 04 00 08 00 08 00 10",
                    "00000390  00 10 00 20 00 20 00 04  00 04 00 08 00 08 00 10",
                    "000003A0  00 10 00 20 00 20 00 04  00 04 00 08 00 08 00 10",
                    "000003B0  00 10 00 20 00 20 00 04  00 04 00 08 00 08 00 10",
                    "000003C0  00 10 00 20 00 20 00 04  00 04 00 08 00 08 00 10",
                    "000003D0  00 10 00 20 00 20 00 04  00 04 00 08 00 08 00 10",
                    "000003E0  00 10 00 20 00 20 00 04  00 04 00 08 00 08 00 10",
                    "000003F0  00 10 00 20 00 20 00 04  00 04 00 08 00 08 00 10",
                    "00000400  00 10 00 20 00 20 00 04  00 04 00 08 00 08 00 10",
                    "00000410  00 10 00 20 00 20 00 04"
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }
        }

        [TestMethod]
        [Owner("sboshra")]
        public void UniformArrayOfNumberArraysTest5()
        {
            // -------------------------
            // Int32 Number Arrays
            // -------------------------

            int[] values1 = { -300000, -200000, -100000, 0, 100000, 200000 };
            int[] values2 = { 10000, 200000, 300000, 400000, 500000, 600000 };
            int[] values3 = { -100000, -200000, -300000, -400000, -500000, -600000 };
            int[] values4 = { 200000, 400000, 600000, 800000, 1000000, 1200000 };

            int[] valuesX1 = new int[300];
            int[] valuesX2 = new int[300];

            valuesX1[0] = -1;
            valuesX2[0] = -2;

            // Case 1
            {
                JsonToken[] tokensToWrite =
                {
                    JsonToken.ArrayStart(),
                        JsonToken.Int32NumberArray(values1),
                    JsonToken.ArrayEnd(),
                };

                string[] expectedText =
                {
                    @"[[-300000,-200000,-100000,0,100000,200000]]"
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E1 E2 1A CA 20 6C FB  FF CA C0 F2 FC FF CA 60",
                    "00000010  79 FE FF 00 CA A0 86 01  00 CA 40 0D 03 00"
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 E1 F0 DA 06 20 6C FB  FF C0 F2 FC FF 60 79 FE",
                    "00000010  FF 00 00 00 00 A0 86 01  00 40 0D 03 00"
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }

            // Case 2
            {
                JsonToken[] tokensToWrite =
                {
                    JsonToken.ArrayStart(),
                        JsonToken.Int32NumberArray(values1),
                        JsonToken.Int32NumberArray(values1),
                    JsonToken.ArrayEnd(),
                };

                string[] expectedText =
                {
                    @"[[-300000,-200000,-100000,0,100000,200000],[-300000,-200000,-100000,0,100000,200000]]"
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E2 38 E2 1A CA 20 6C  FB FF CA C0 F2 FC FF CA",
                    "00000010  60 79 FE FF 00 CA A0 86  01 00 CA 40 0D 03 00 E2",
                    "00000020  1A CA 20 6C FB FF CA C0  F2 FC FF CA 60 79 FE FF",
                    "00000030  00 CA A0 86 01 00 CA 40  0D 03 00"
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 F2 F0 DA 06 02 20 6C  FB FF C0 F2 FC FF 60 79",
                    "00000010  FE FF 00 00 00 00 A0 86  01 00 40 0D 03 00 20 6C",
                    "00000020  FB FF C0 F2 FC FF 60 79  FE FF 00 00 00 00 A0 86",
                    "00000030  01 00 40 0D 03 00"
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }

            // Case 3
            {
                JsonToken[] tokensToWrite =
                {
                    JsonToken.ArrayStart(),
                        JsonToken.Int32NumberArray(values1),
                        JsonToken.Int32NumberArray(values2),
                        JsonToken.Int32NumberArray(values3),
                    JsonToken.ArrayEnd(),
                };

                string[] expectedText =
                {
                    @"[[-300000,-200000,-100000,0,100000,200000],[10000,200000,300000,400000,500000,600000],[-100000,-2000",
                    @"00,-300000,-400000,-500000,-600000]]",
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E2 5A E2 1A CA 20 6C  FB FF CA C0 F2 FC FF CA",
                    "00000010  60 79 FE FF 00 CA A0 86  01 00 CA 40 0D 03 00 E2",
                    "00000020  1C C9 10 27 CA 40 0D 03  00 CA E0 93 04 00 CA 80",
                    "00000030  1A 06 00 CA 20 A1 07 00  CA C0 27 09 00 E2 1E CA",
                    "00000040  60 79 FE FF CA C0 F2 FC  FF CA 20 6C FB FF CA 80",
                    "00000050  E5 F9 FF CA E0 5E F8 FF  CA 40 D8 F6 FF"
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 F2 F0 DA 06 03 20 6C  FB FF C0 F2 FC FF 60 79",
                    "00000010  FE FF 00 00 00 00 A0 86  01 00 40 0D 03 00 10 27",
                    "00000020  00 00 40 0D 03 00 E0 93  04 00 80 1A 06 00 20 A1",
                    "00000030  07 00 C0 27 09 00 60 79  FE FF C0 F2 FC FF 20 6C",
                    "00000040  FB FF 80 E5 F9 FF E0 5E  F8 FF 40 D8 F6 FF"
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }

            // Case 4
            {
                JsonToken[] tokensToWrite =
                {
                    JsonToken.ArrayStart(),
                        JsonToken.Int32NumberArray(values1),
                        JsonToken.Int32NumberArray(values2),
                        JsonToken.Int32NumberArray(values3),
                        JsonToken.Int32NumberArray(values4),
                        JsonToken.Int32NumberArray(values4),
                        JsonToken.Int32NumberArray(values3),
                        JsonToken.Int32NumberArray(values2),
                        JsonToken.Int32NumberArray(values1),
                    JsonToken.ArrayEnd(),
                };

                string[] expectedText =
                {
                    @"[[-300000,-200000,-100000,0,100000,200000],[10000,200000,300000,400000,500000,600000],[-100000,-2000",
                    @"00,-300000,-400000,-500000,-600000],[200000,400000,600000,800000,1000000,1200000],[200000,400000,600",
                    @"000,800000,1000000,1200000],[-100000,-200000,-300000,-400000,-500000,-600000],[10000,200000,300000,4",
                    @"00000,500000,600000],[-300000,-200000,-100000,0,100000,200000]]",
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E2 F4 E2 1A CA 20 6C  FB FF CA C0 F2 FC FF CA",
                    "00000010  60 79 FE FF 00 CA A0 86  01 00 CA 40 0D 03 00 E2",
                    "00000020  1C C9 10 27 CA 40 0D 03  00 CA E0 93 04 00 CA 80",
                    "00000030  1A 06 00 CA 20 A1 07 00  CA C0 27 09 00 E2 1E CA",
                    "00000040  60 79 FE FF CA C0 F2 FC  FF CA 20 6C FB FF CA 80",
                    "00000050  E5 F9 FF CA E0 5E F8 FF  CA 40 D8 F6 FF E2 1E CA",
                    "00000060  40 0D 03 00 CA 80 1A 06  00 CA C0 27 09 00 CA 00",
                    "00000070  35 0C 00 CA 40 42 0F 00  CA 80 4F 12 00 E2 1E CA",
                    "00000080  40 0D 03 00 CA 80 1A 06  00 CA C0 27 09 00 CA 00",
                    "00000090  35 0C 00 CA 40 42 0F 00  CA 80 4F 12 00 E2 1E CA",
                    "000000A0  60 79 FE FF CA C0 F2 FC  FF CA 20 6C FB FF CA 80",
                    "000000B0  E5 F9 FF CA E0 5E F8 FF  CA 40 D8 F6 FF E2 1C C9",
                    "000000C0  10 27 CA 40 0D 03 00 CA  E0 93 04 00 CA 80 1A 06",
                    "000000D0  00 CA 20 A1 07 00 CA C0  27 09 00 E2 1A CA 20 6C",
                    "000000E0  FB FF CA C0 F2 FC FF CA  60 79 FE FF 00 CA A0 86",
                    "000000F0  01 00 CA 40 0D 03 00"
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 F2 F0 DA 06 08 20 6C  FB FF C0 F2 FC FF 60 79",
                    "00000010  FE FF 00 00 00 00 A0 86  01 00 40 0D 03 00 10 27",
                    "00000020  00 00 40 0D 03 00 E0 93  04 00 80 1A 06 00 20 A1",
                    "00000030  07 00 C0 27 09 00 60 79  FE FF C0 F2 FC FF 20 6C",
                    "00000040  FB FF 80 E5 F9 FF E0 5E  F8 FF 40 D8 F6 FF 40 0D",
                    "00000050  03 00 80 1A 06 00 C0 27  09 00 00 35 0C 00 40 42",
                    "00000060  0F 00 80 4F 12 00 40 0D  03 00 80 1A 06 00 C0 27",
                    "00000070  09 00 00 35 0C 00 40 42  0F 00 80 4F 12 00 60 79",
                    "00000080  FE FF C0 F2 FC FF 20 6C  FB FF 80 E5 F9 FF E0 5E",
                    "00000090  F8 FF 40 D8 F6 FF 10 27  00 00 40 0D 03 00 E0 93",
                    "000000A0  04 00 80 1A 06 00 20 A1  07 00 C0 27 09 00 20 6C",
                    "000000B0  FB FF C0 F2 FC FF 60 79  FE FF 00 00 00 00 A0 86",
                    "000000C0  01 00 40 0D 03 00"
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }

            // Case 5
            {
                JsonToken[] tokensToWrite =
                {
                    JsonToken.ArrayStart(),
                        JsonToken.Int32NumberArray(valuesX1),
                        JsonToken.Int32NumberArray(valuesX1),
                    JsonToken.ArrayEnd(),
                };

                string[] expectedText =
                {
                    @"[[-1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0",
                    @",0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0",
                    @",0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0",
                    @",0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0",
                    @",0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0",
                    @",0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0",
                    @",0],[-1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,",
                    @"0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,",
                    @"0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,",
                    @"0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,",
                    @"0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,",
                    @"0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,",
                    @"0,0,0]]",
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E3 66 02 E6 2E 01 2C  01 C9 FF FF 00 00 00 00",
                    "00000010  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000020  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000030  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000040  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000050  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000060  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000070  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000080  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000090  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000000A0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000000B0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000000C0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000000D0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000000E0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000000F0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000100  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000110  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000120  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000130  00 00 00 00 00 00 00 E6  2E 01 2C 01 C9 FF FF 00",
                    "00000140  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000150  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000160  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000170  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000180  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000190  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000001A0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000001B0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000001C0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000001D0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000001E0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000001F0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000200  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000210  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000220  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000230  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000240  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000250  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000260  00 00 00 00 00 00 00 00  00 00"
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 F3 F1 DA 2C 01 02 00  FF FF FF FF 00 00 00 00",
                    "00000010  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000020  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000030  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000040  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000050  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000060  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000070  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000080  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000090  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000000A0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000000B0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000000C0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000000D0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000000E0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000000F0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000100  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000110  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000120  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000130  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000140  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000150  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000160  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000170  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000180  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000190  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000001A0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000001B0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000001C0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000001D0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000001E0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000001F0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000200  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000210  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000220  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000230  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000240  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000250  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000260  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000270  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000280  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000290  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000002A0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000002B0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000002C0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000002D0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000002E0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000002F0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000300  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000310  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000320  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000330  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000340  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000350  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000360  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000370  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000380  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000390  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000003A0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000003B0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000003C0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000003D0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000003E0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000003F0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000400  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000410  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000420  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000430  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000440  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000450  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000460  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000470  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000480  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000490  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000004A0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000004B0  00 00 00 00 00 00 00 00  FF FF FF FF 00 00 00 00",
                    "000004C0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000004D0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000004E0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000004F0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000500  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000510  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000520  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000530  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000540  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000550  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000560  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000570  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000580  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000590  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000005A0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000005B0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000005C0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000005D0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000005E0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000005F0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000600  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000610  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000620  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000630  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000640  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000650  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000660  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000670  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000680  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000690  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000006A0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000006B0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000006C0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000006D0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000006E0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000006F0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000700  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000710  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000720  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000730  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000740  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000750  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000760  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000770  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000780  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000790  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000007A0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000007B0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000007C0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000007D0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000007E0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000007F0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000800  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000810  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000820  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000830  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000840  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000850  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000860  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000870  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000880  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000890  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000008A0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000008B0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000008C0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000008D0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000008E0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "000008F0  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000900  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000910  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000920  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000930  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000940  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000950  00 00 00 00 00 00 00 00  00 00 00 00 00 00 00 00",
                    "00000960  00 00 00 00 00 00 00 00"
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }
        }

        [TestMethod]
        [Owner("sboshra")]
        public void UniformArrayOfNumberArraysTest6()
        {
            // -------------------------
            // Int64 Number Arrays
            // -------------------------

            long[] values1 = { -30000000000, -20000000000, -10000000000, 10000000000 };
            long[] values2 = { 10000000000, 20000000000, 30000000000, 40000000000 };
            long[] values3 = { -10000000000, -20000000000, -30000000000, -40000000000 };
            long[] values4 = { 80000000000, 60000000000, 40000000000, 20000000000 };

            // Case 1
            {
                JsonToken[] tokensToWrite =
                {
                    JsonToken.ArrayStart(),
                        JsonToken.Int64NumberArray(values1),
                    JsonToken.ArrayEnd(),
                };

                string[] expectedText =
                {
                    @"[[-30000000000,-20000000000,-10000000000,10000000000]]"
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E1 E2 24 CB 00 54 DC  03 F9 FF FF FF CB 00 38",
                    "00000010  E8 57 FB FF FF FF CB 00  1C F4 AB FD FF FF FF CB",
                    "00000020  00 E4 0B 54 02 00 00 00",
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 E1 F0 DB 04 00 54 DC  03 F9 FF FF FF 00 38 E8",
                    "00000010  57 FB FF FF FF 00 1C F4  AB FD FF FF FF 00 E4 0B",
                    "00000020  54 02 00 00 00",
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }

            // Case 2
            {
                JsonToken[] tokensToWrite =
                {
                    JsonToken.ArrayStart(),
                        JsonToken.Int64NumberArray(values1),
                        JsonToken.Int64NumberArray(values1),
                    JsonToken.ArrayEnd(),
                };

                string[] expectedText =
                {
                    @"[[-30000000000,-20000000000,-10000000000,10000000000],[-30000000000,-20000000000,-10000000000,100000",
                    @"00000]]",
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E2 4C E2 24 CB 00 54  DC 03 F9 FF FF FF CB 00",
                    "00000010  38 E8 57 FB FF FF FF CB  00 1C F4 AB FD FF FF FF",
                    "00000020  CB 00 E4 0B 54 02 00 00  00 E2 24 CB 00 54 DC 03",
                    "00000030  F9 FF FF FF CB 00 38 E8  57 FB FF FF FF CB 00 1C",
                    "00000040  F4 AB FD FF FF FF CB 00  E4 0B 54 02 00 00 00",
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 F2 F0 DB 04 02 00 54  DC 03 F9 FF FF FF 00 38",
                    "00000010  E8 57 FB FF FF FF 00 1C  F4 AB FD FF FF FF 00 E4",
                    "00000020  0B 54 02 00 00 00 00 54  DC 03 F9 FF FF FF 00 38",
                    "00000030  E8 57 FB FF FF FF 00 1C  F4 AB FD FF FF FF 00 E4",
                    "00000040  0B 54 02 00 00 00"
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }

            // Case 3
            {
                JsonToken[] tokensToWrite =
                {
                    JsonToken.ArrayStart(),
                        JsonToken.Int64NumberArray(values1),
                        JsonToken.Int64NumberArray(values2),
                        JsonToken.Int64NumberArray(values3),
                    JsonToken.ArrayEnd(),
                };

                string[] expectedText =
                {
                    @"[[-30000000000,-20000000000,-10000000000,10000000000],[10000000000,20000000000,30000000000,400000000",
                    @"00],[-10000000000,-20000000000,-30000000000,-40000000000]]",
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E2 72 E2 24 CB 00 54  DC 03 F9 FF FF FF CB 00",
                    "00000010  38 E8 57 FB FF FF FF CB  00 1C F4 AB FD FF FF FF",
                    "00000020  CB 00 E4 0B 54 02 00 00  00 E2 24 CB 00 E4 0B 54",
                    "00000030  02 00 00 00 CB 00 C8 17  A8 04 00 00 00 CB 00 AC",
                    "00000040  23 FC 06 00 00 00 CB 00  90 2F 50 09 00 00 00 E2",
                    "00000050  24 CB 00 1C F4 AB FD FF  FF FF CB 00 38 E8 57 FB",
                    "00000060  FF FF FF CB 00 54 DC 03  F9 FF FF FF CB 00 70 D0",
                    "00000070  AF F6 FF FF FF"
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 F2 F0 DB 04 03 00 54  DC 03 F9 FF FF FF 00 38",
                    "00000010  E8 57 FB FF FF FF 00 1C  F4 AB FD FF FF FF 00 E4",
                    "00000020  0B 54 02 00 00 00 00 E4  0B 54 02 00 00 00 00 C8",
                    "00000030  17 A8 04 00 00 00 00 AC  23 FC 06 00 00 00 00 90",
                    "00000040  2F 50 09 00 00 00 00 1C  F4 AB FD FF FF FF 00 38",
                    "00000050  E8 57 FB FF FF FF 00 54  DC 03 F9 FF FF FF 00 70",
                    "00000060  D0 AF F6 FF FF FF"
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }

            // Case 4
            {
                JsonToken[] tokensToWrite =
                {
                    JsonToken.ArrayStart(),
                        JsonToken.Int64NumberArray(values1),
                        JsonToken.Int64NumberArray(values2),
                        JsonToken.Int64NumberArray(values3),
                        JsonToken.Int64NumberArray(values4),
                        JsonToken.Int64NumberArray(values4),
                        JsonToken.Int64NumberArray(values3),
                        JsonToken.Int64NumberArray(values2),
                        JsonToken.Int64NumberArray(values1),
                    JsonToken.ArrayEnd(),
                };

                string[] expectedText =
                {
                    @"[[-30000000000,-20000000000,-10000000000,10000000000],[10000000000,20000000000,30000000000,400000000",
                    @"00],[-10000000000,-20000000000,-30000000000,-40000000000],[80000000000,60000000000,40000000000,20000",
                    @"000000],[80000000000,60000000000,40000000000,20000000000],[-10000000000,-20000000000,-30000000000,-4",
                    @"0000000000],[10000000000,20000000000,30000000000,40000000000],[-30000000000,-20000000000,-1000000000",
                    @"0,10000000000]]",
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E3 30 01 E2 24 CB 00  54 DC 03 F9 FF FF FF CB",
                    "00000010  00 38 E8 57 FB FF FF FF  CB 00 1C F4 AB FD FF FF",
                    "00000020  FF CB 00 E4 0B 54 02 00  00 00 E2 24 CB 00 E4 0B",
                    "00000030  54 02 00 00 00 CB 00 C8  17 A8 04 00 00 00 CB 00",
                    "00000040  AC 23 FC 06 00 00 00 CB  00 90 2F 50 09 00 00 00",
                    "00000050  E2 24 CB 00 1C F4 AB FD  FF FF FF CB 00 38 E8 57",
                    "00000060  FB FF FF FF CB 00 54 DC  03 F9 FF FF FF CB 00 70",
                    "00000070  D0 AF F6 FF FF FF E2 24  CB 00 20 5F A0 12 00 00",
                    "00000080  00 CB 00 58 47 F8 0D 00  00 00 CB 00 90 2F 50 09",
                    "00000090  00 00 00 CB 00 C8 17 A8  04 00 00 00 E2 24 CB 00",
                    "000000A0  20 5F A0 12 00 00 00 CB  00 58 47 F8 0D 00 00 00",
                    "000000B0  CB 00 90 2F 50 09 00 00  00 CB 00 C8 17 A8 04 00",
                    "000000C0  00 00 E2 24 CB 00 1C F4  AB FD FF FF FF CB 00 38",
                    "000000D0  E8 57 FB FF FF FF CB 00  54 DC 03 F9 FF FF FF CB",
                    "000000E0  00 70 D0 AF F6 FF FF FF  E2 24 CB 00 E4 0B 54 02",
                    "000000F0  00 00 00 CB 00 C8 17 A8  04 00 00 00 CB 00 AC 23",
                    "00000100  FC 06 00 00 00 CB 00 90  2F 50 09 00 00 00 E2 24",
                    "00000110  CB 00 54 DC 03 F9 FF FF  FF CB 00 38 E8 57 FB FF",
                    "00000120  FF FF CB 00 1C F4 AB FD  FF FF FF CB 00 E4 0B 54",
                    "00000130  02 00 00 00"
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 F2 F0 DB 04 08 00 54  DC 03 F9 FF FF FF 00 38",
                    "00000010  E8 57 FB FF FF FF 00 1C  F4 AB FD FF FF FF 00 E4",
                    "00000020  0B 54 02 00 00 00 00 E4  0B 54 02 00 00 00 00 C8",
                    "00000030  17 A8 04 00 00 00 00 AC  23 FC 06 00 00 00 00 90",
                    "00000040  2F 50 09 00 00 00 00 1C  F4 AB FD FF FF FF 00 38",
                    "00000050  E8 57 FB FF FF FF 00 54  DC 03 F9 FF FF FF 00 70",
                    "00000060  D0 AF F6 FF FF FF 00 20  5F A0 12 00 00 00 00 58",
                    "00000070  47 F8 0D 00 00 00 00 90  2F 50 09 00 00 00 00 C8",
                    "00000080  17 A8 04 00 00 00 00 20  5F A0 12 00 00 00 00 58",
                    "00000090  47 F8 0D 00 00 00 00 90  2F 50 09 00 00 00 00 C8",
                    "000000A0  17 A8 04 00 00 00 00 1C  F4 AB FD FF FF FF 00 38",
                    "000000B0  E8 57 FB FF FF FF 00 54  DC 03 F9 FF FF FF 00 70",
                    "000000C0  D0 AF F6 FF FF FF 00 E4  0B 54 02 00 00 00 00 C8",
                    "000000D0  17 A8 04 00 00 00 00 AC  23 FC 06 00 00 00 00 90",
                    "000000E0  2F 50 09 00 00 00 00 54  DC 03 F9 FF FF FF 00 38",
                    "000000F0  E8 57 FB FF FF FF 00 1C  F4 AB FD FF FF FF 00 E4",
                    "00000100  0B 54 02 00 00 00"
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }
        }

        [TestMethod]
        [Owner("sboshra")]
        public void UniformArrayOfNumberArraysTest7()
        {
            // -------------------------
            // Float32 Number Arrays
            // -------------------------

            float[] values1 = { -3.1f, -2.1f, -1.1f, 1.1f };
            float[] values2 = { 1.01f, 2.01f, 3.01f, 4.01f };
            float[] values3 = { -1.11f, -2.22f, -3.33f, -4.44f };
            float[] values4 = { 8.811f, 6.611f, 4.411f, 2.211f };

            float[] valuesS1 = { 1.11f, 2.22f };
            float[] valuesS2 = { 2.22f, 3.33f };

            // Case 1
            {
                JsonToken[] tokensToWrite =
                {
                    JsonToken.ArrayStart(),
                        JsonToken.Float32NumberArray(values1),
                    JsonToken.ArrayEnd(),
                };

                string[] expectedText =
                {
                    @"[[-3.0999999046325684,-2.0999999046325684,-1.100000023841858,1.100000023841858]]",
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E1 E2 24 CC 00 00 00  C0 CC CC 08 C0 CC 00 00",
                    "00000010  00 C0 CC CC 00 C0 CC 00  00 00 A0 99 99 F1 BF CC",
                    "00000020  00 00 00 A0 99 99 F1 3F"
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 E1 F0 CD 04 66 66 46  C0 66 66 06 C0 CD CC 8C",
                    "00000010  BF CD CC 8C 3F"
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }

            // Case 2
            {
                JsonToken[] tokensToWrite =
                {
                    JsonToken.ArrayStart(),
                        JsonToken.Float32NumberArray(values1),
                        JsonToken.Float32NumberArray(values1),
                    JsonToken.ArrayEnd(),
                };

                string[] expectedText =
                {
                    @"[[-3.0999999046325684,-2.0999999046325684,-1.100000023841858,1.100000023841858],[-3.0999999046325684",
                    @",-2.0999999046325684,-1.100000023841858,1.100000023841858]]",
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E2 4C E2 24 CC 00 00  00 C0 CC CC 08 C0 CC 00",
                    "00000010  00 00 C0 CC CC 00 C0 CC  00 00 00 A0 99 99 F1 BF",
                    "00000020  CC 00 00 00 A0 99 99 F1  3F E2 24 CC 00 00 00 C0",
                    "00000030  CC CC 08 C0 CC 00 00 00  C0 CC CC 00 C0 CC 00 00",
                    "00000040  00 A0 99 99 F1 BF CC 00  00 00 A0 99 99 F1 3F"
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 F2 F0 CD 04 02 66 66  46 C0 66 66 06 C0 CD CC",
                    "00000010  8C BF CD CC 8C 3F 66 66  46 C0 66 66 06 C0 CD CC",
                    "00000020  8C BF CD CC 8C 3F"
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }

            // Case 3
            {
                JsonToken[] tokensToWrite =
                {
                    JsonToken.ArrayStart(),
                        JsonToken.Float32NumberArray(values1),
                        JsonToken.Float32NumberArray(values1),
                        JsonToken.Float32NumberArray(valuesS1),
                        JsonToken.Float32NumberArray(valuesS1),
                    JsonToken.ArrayEnd(),
                };

                string[] expectedText =
                {
                    @"[[-3.0999999046325684,-2.0999999046325684,-1.100000023841858,1.100000023841858],[-3.0999999046325684",
                    @",-2.0999999046325684,-1.100000023841858,1.100000023841858],[1.1100000143051147,2.2200000286102295],[",
                    @"1.1100000143051147,2.2200000286102295]]",
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E2 74 E2 24 CC 00 00  00 C0 CC CC 08 C0 CC 00",
                    "00000010  00 00 C0 CC CC 00 C0 CC  00 00 00 A0 99 99 F1 BF",
                    "00000020  CC 00 00 00 A0 99 99 F1  3F E2 24 CC 00 00 00 C0",
                    "00000030  CC CC 08 C0 CC 00 00 00  C0 CC CC 00 C0 CC 00 00",
                    "00000040  00 A0 99 99 F1 BF CC 00  00 00 A0 99 99 F1 3F E2",
                    "00000050  12 CC 00 00 00 60 8F C2  F1 3F CC 00 00 00 60 8F",
                    "00000060  C2 01 40 E2 12 CC 00 00  00 60 8F C2 F1 3F CC 00",
                    "00000070  00 00 60 8F C2 01 40"
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 E2 3C F0 CD 04 66 66  46 C0 66 66 06 C0 CD CC",
                    "00000010  8C BF CD CC 8C 3F F0 CD  04 66 66 46 C0 66 66 06",
                    "00000020  C0 CD CC 8C BF CD CC 8C  3F F0 CD 02 7B 14 8E 3F",
                    "00000030  7B 14 0E 40 F0 CD 02 7B  14 8E 3F 7B 14 0E 40"
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }

            // Case 4
            {
                JsonToken[] tokensToWrite =
                {
                    JsonToken.ArrayStart(),
                        JsonToken.Float32NumberArray(values1),
                        JsonToken.Float32NumberArray(values2),
                        JsonToken.Float32NumberArray(values3),
                    JsonToken.ArrayEnd(),
                };

                string[] expectedText =
                {
                    @"[[-3.0999999046325684,-2.0999999046325684,-1.100000023841858,1.100000023841858],[1.0099999904632568,",
                    @"2.009999990463257,3.009999990463257,4.010000228881836],[-1.1100000143051147,-2.2200000286102295,-3.3",
                    @"299999237060547,-4.440000057220459]]",
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E2 72 E2 24 CC 00 00  00 C0 CC CC 08 C0 CC 00",
                    "00000010  00 00 C0 CC CC 00 C0 CC  00 00 00 A0 99 99 F1 BF",
                    "00000020  CC 00 00 00 A0 99 99 F1  3F E2 24 CC 00 00 00 C0",
                    "00000030  F5 28 F0 3F CC 00 00 00  E0 7A 14 00 40 CC 00 00",
                    "00000040  00 E0 7A 14 08 40 CC 00  00 00 80 3D 0A 10 40 E2",
                    "00000050  24 CC 00 00 00 60 8F C2  F1 BF CC 00 00 00 60 8F",
                    "00000060  C2 01 C0 CC 00 00 00 00  D7 A3 0A C0 CC 00 00 00",
                    "00000070  60 8F C2 11 C0"
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 F2 F0 CD 04 03 66 66  46 C0 66 66 06 C0 CD CC",
                    "00000010  8C BF CD CC 8C 3F AE 47  81 3F D7 A3 00 40 D7 A3",
                    "00000020  40 40 EC 51 80 40 7B 14  8E BF 7B 14 0E C0 B8 1E",
                    "00000030  55 C0 7B 14 8E C0"
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }

            // Case 5
            {
                JsonToken[] tokensToWrite =
                {
                    JsonToken.ArrayStart(),
                        JsonToken.Float32NumberArray(values1),
                        JsonToken.Float32NumberArray(values2),
                        JsonToken.Float32NumberArray(values3),
                        JsonToken.Float32NumberArray(values4),
                        JsonToken.Float32NumberArray(values4),
                        JsonToken.Float32NumberArray(values3),
                        JsonToken.Float32NumberArray(values2),
                        JsonToken.Float32NumberArray(values1),
                    JsonToken.ArrayEnd(),
                };

                string[] expectedText =
                {
                    @"[[-3.0999999046325684,-2.0999999046325684,-1.100000023841858,1.100000023841858],[1.0099999904632568,",
                    @"2.009999990463257,3.009999990463257,4.010000228881836],[-1.1100000143051147,-2.2200000286102295,-3.3",
                    @"299999237060547,-4.440000057220459],[8.810999870300293,6.611000061035156,4.410999774932861,2.2109999",
                    @"656677246],[8.810999870300293,6.611000061035156,4.410999774932861,2.2109999656677246],[-1.1100000143",
                    @"051147,-2.2200000286102295,-3.3299999237060547,-4.440000057220459],[1.0099999904632568,2.00999999046",
                    @"3257,3.009999990463257,4.010000228881836],[-3.0999999046325684,-2.0999999046325684,-1.10000002384185",
                    @"8,1.100000023841858]]",
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E3 30 01 E2 24 CC 00  00 00 C0 CC CC 08 C0 CC",
                    "00000010  00 00 00 C0 CC CC 00 C0  CC 00 00 00 A0 99 99 F1",
                    "00000020  BF CC 00 00 00 A0 99 99  F1 3F E2 24 CC 00 00 00",
                    "00000030  C0 F5 28 F0 3F CC 00 00  00 E0 7A 14 00 40 CC 00",
                    "00000040  00 00 E0 7A 14 08 40 CC  00 00 00 80 3D 0A 10 40",
                    "00000050  E2 24 CC 00 00 00 60 8F  C2 F1 BF CC 00 00 00 60",
                    "00000060  8F C2 01 C0 CC 00 00 00  00 D7 A3 0A C0 CC 00 00",
                    "00000070  00 60 8F C2 11 C0 E2 24  CC 00 00 00 60 3B 9F 21",
                    "00000080  40 CC 00 00 00 00 AA 71  1A 40 CC 00 00 00 20 DD",
                    "00000090  A4 11 40 CC 00 00 00 C0  20 B0 01 40 E2 24 CC 00",
                    "000000A0  00 00 60 3B 9F 21 40 CC  00 00 00 00 AA 71 1A 40",
                    "000000B0  CC 00 00 00 20 DD A4 11  40 CC 00 00 00 C0 20 B0",
                    "000000C0  01 40 E2 24 CC 00 00 00  60 8F C2 F1 BF CC 00 00",
                    "000000D0  00 60 8F C2 01 C0 CC 00  00 00 00 D7 A3 0A C0 CC",
                    "000000E0  00 00 00 60 8F C2 11 C0  E2 24 CC 00 00 00 C0 F5",
                    "000000F0  28 F0 3F CC 00 00 00 E0  7A 14 00 40 CC 00 00 00",
                    "00000100  E0 7A 14 08 40 CC 00 00  00 80 3D 0A 10 40 E2 24",
                    "00000110  CC 00 00 00 C0 CC CC 08  C0 CC 00 00 00 C0 CC CC",
                    "00000120  00 C0 CC 00 00 00 A0 99  99 F1 BF CC 00 00 00 A0",
                    "00000130  99 99 F1 3F"
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 F2 F0 CD 04 08 66 66  46 C0 66 66 06 C0 CD CC",
                    "00000010  8C BF CD CC 8C 3F AE 47  81 3F D7 A3 00 40 D7 A3",
                    "00000020  40 40 EC 51 80 40 7B 14  8E BF 7B 14 0E C0 B8 1E",
                    "00000030  55 C0 7B 14 8E C0 DB F9  0C 41 50 8D D3 40 E9 26",
                    "00000040  8D 40 06 81 0D 40 DB F9  0C 41 50 8D D3 40 E9 26",
                    "00000050  8D 40 06 81 0D 40 7B 14  8E BF 7B 14 0E C0 B8 1E",
                    "00000060  55 C0 7B 14 8E C0 AE 47  81 3F D7 A3 00 40 D7 A3",
                    "00000070  40 40 EC 51 80 40 66 66  46 C0 66 66 06 C0 CD CC",
                    "00000080  8C BF CD CC 8C 3F"
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }

            // Case 6
            {
                JsonToken[] tokensToWrite =
                {
                    JsonToken.ArrayStart(),
                        JsonToken.Float32NumberArray(values1),
                        JsonToken.Float32NumberArray(valuesS1),
                        JsonToken.Float32NumberArray(valuesS2),
                        JsonToken.Float32NumberArray(values2),
                        JsonToken.Float32NumberArray(values1),
                        JsonToken.Float32NumberArray(values2),
                        JsonToken.Float32NumberArray(valuesS1),
                        JsonToken.Float32NumberArray(valuesS2),
                    JsonToken.ArrayEnd(),
                };

                string[] expectedText =
                {
                    @"[[-3.0999999046325684,-2.0999999046325684,-1.100000023841858,1.100000023841858],[1.1100000143051147,",
                    @"2.2200000286102295],[2.2200000286102295,3.3299999237060547],[1.0099999904632568,2.009999990463257,3.",
                    @"009999990463257,4.010000228881836],[-3.0999999046325684,-2.0999999046325684,-1.100000023841858,1.100",
                    @"000023841858],[1.0099999904632568,2.009999990463257,3.009999990463257,4.010000228881836],[1.11000001",
                    @"43051147,2.2200000286102295],[2.2200000286102295,3.3299999237060547]]",
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E2 E8 E2 24 CC 00 00  00 C0 CC CC 08 C0 CC 00",
                    "00000010  00 00 C0 CC CC 00 C0 CC  00 00 00 A0 99 99 F1 BF",
                    "00000020  CC 00 00 00 A0 99 99 F1  3F E2 12 CC 00 00 00 60",
                    "00000030  8F C2 F1 3F CC 00 00 00  60 8F C2 01 40 E2 12 CC",
                    "00000040  00 00 00 60 8F C2 01 40  CC 00 00 00 00 D7 A3 0A",
                    "00000050  40 E2 24 CC 00 00 00 C0  F5 28 F0 3F CC 00 00 00",
                    "00000060  E0 7A 14 00 40 CC 00 00  00 E0 7A 14 08 40 CC 00",
                    "00000070  00 00 80 3D 0A 10 40 E2  24 CC 00 00 00 C0 CC CC",
                    "00000080  08 C0 CC 00 00 00 C0 CC  CC 00 C0 CC 00 00 00 A0",
                    "00000090  99 99 F1 BF CC 00 00 00  A0 99 99 F1 3F E2 24 CC",
                    "000000A0  00 00 00 C0 F5 28 F0 3F  CC 00 00 00 E0 7A 14 00",
                    "000000B0  40 CC 00 00 00 E0 7A 14  08 40 CC 00 00 00 80 3D",
                    "000000C0  0A 10 40 E2 12 CC 00 00  00 60 8F C2 F1 3F CC 00",
                    "000000D0  00 00 60 8F C2 01 40 E2  12 CC 00 00 00 60 8F C2",
                    "000000E0  01 40 CC 00 00 00 00 D7  A3 0A 40",
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 E2 78 F0 CD 04 66 66  46 C0 66 66 06 C0 CD CC",
                    "00000010  8C BF CD CC 8C 3F F0 CD  02 7B 14 8E 3F 7B 14 0E",
                    "00000020  40 F0 CD 02 7B 14 0E 40  B8 1E 55 40 F0 CD 04 AE",
                    "00000030  47 81 3F D7 A3 00 40 D7  A3 40 40 EC 51 80 40 F0",
                    "00000040  CD 04 66 66 46 C0 66 66  06 C0 CD CC 8C BF CD CC",
                    "00000050  8C 3F F0 CD 04 AE 47 81  3F D7 A3 00 40 D7 A3 40",
                    "00000060  40 EC 51 80 40 F0 CD 02  7B 14 8E 3F 7B 14 0E 40",
                    "00000070  F0 CD 02 7B 14 0E 40 B8  1E 55 40",
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }
        }

        [TestMethod]
        [Owner("sboshra")]
        public void UniformArrayOfNumberArraysTest8()
        {
            // -------------------------
            // Float64 Number Arrays
            // -------------------------

            double[] values1 = { -3.1, -2.1, -1.1, 1.1 };
            double[] values2 = { 1.01, 2.01, 3.01, 4.01 };
            double[] values3 = { -1.11, -2.22, -3.33, -4.44 };
            double[] values4 = { 8.81, 6.61, 4.41, 2.21 };

            double[] valuesS1 = { 1.11, 2.22 };
            double[] valuesS2 = { 2.22, 3.33 };
            double[] valuesS3 = { 3.33, 4.44 };
            double[] valuesS4 = { 4.44, 5.55 };

            // Case 1
            {
                JsonToken[] tokensToWrite =
                {
                    JsonToken.ArrayStart(),
                        JsonToken.Float64NumberArray(values1),
                    JsonToken.ArrayEnd(),
                };

                string[] expectedText =
                {
                    @"[[-3.1,-2.1,-1.1,1.1]]"
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E1 E2 24 CC CD CC CC  CC CC CC 08 C0 CC CD CC",
                    "00000010  CC CC CC CC 00 C0 CC 9A  99 99 99 99 99 F1 BF CC",
                    "00000020  9A 99 99 99 99 99 F1 3F"
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 E1 F0 CE 04 CD CC CC  CC CC CC 08 C0 CD CC CC",
                    "00000010  CC CC CC 00 C0 9A 99 99  99 99 99 F1 BF 9A 99 99",
                    "00000020  99 99 99 F1 3F"
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }

            // Case 2
            {
                JsonToken[] tokensToWrite =
                {
                    JsonToken.ArrayStart(),
                        JsonToken.Float64NumberArray(values1),
                        JsonToken.Float64NumberArray(values1),
                    JsonToken.ArrayEnd(),
                };

                string[] expectedText =
                {
                    @"[[-3.1,-2.1,-1.1,1.1],[-3.1,-2.1,-1.1,1.1]]"
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E2 4C E2 24 CC CD CC  CC CC CC CC 08 C0 CC CD",
                    "00000010  CC CC CC CC CC 00 C0 CC  9A 99 99 99 99 99 F1 BF",
                    "00000020  CC 9A 99 99 99 99 99 F1  3F E2 24 CC CD CC CC CC",
                    "00000030  CC CC 08 C0 CC CD CC CC  CC CC CC 00 C0 CC 9A 99",
                    "00000040  99 99 99 99 F1 BF CC 9A  99 99 99 99 99 F1 3F"
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 F2 F0 CE 04 02 CD CC  CC CC CC CC 08 C0 CD CC",
                    "00000010  CC CC CC CC 00 C0 9A 99  99 99 99 99 F1 BF 9A 99",
                    "00000020  99 99 99 99 F1 3F CD CC  CC CC CC CC 08 C0 CD CC",
                    "00000030  CC CC CC CC 00 C0 9A 99  99 99 99 99 F1 BF 9A 99",
                    "00000040  99 99 99 99 F1 3F"
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }

            // Case 3
            {
                JsonToken[] tokensToWrite =
                {
                    JsonToken.ArrayStart(),
                        JsonToken.Float64NumberArray(values1),
                        JsonToken.Float64NumberArray(values1),
                        JsonToken.Float64NumberArray(valuesS1),
                        JsonToken.Float64NumberArray(valuesS1),
                    JsonToken.ArrayEnd(),
                };

                string[] expectedText =
                {
                    @"[[-3.1,-2.1,-1.1,1.1],[-3.1,-2.1,-1.1,1.1],[1.11,2.22],[1.11,2.22]]"
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E2 74 E2 24 CC CD CC  CC CC CC CC 08 C0 CC CD",
                    "00000010  CC CC CC CC CC 00 C0 CC  9A 99 99 99 99 99 F1 BF",
                    "00000020  CC 9A 99 99 99 99 99 F1  3F E2 24 CC CD CC CC CC",
                    "00000030  CC CC 08 C0 CC CD CC CC  CC CC CC 00 C0 CC 9A 99",
                    "00000040  99 99 99 99 F1 BF CC 9A  99 99 99 99 99 F1 3F E2",
                    "00000050  12 CC C3 F5 28 5C 8F C2  F1 3F CC C3 F5 28 5C 8F",
                    "00000060  C2 01 40 E2 12 CC C3 F5  28 5C 8F C2 F1 3F CC C3",
                    "00000070  F5 28 5C 8F C2 01 40"
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 E2 6C F0 CE 04 CD CC  CC CC CC CC 08 C0 CD CC",
                    "00000010  CC CC CC CC 00 C0 9A 99  99 99 99 99 F1 BF 9A 99",
                    "00000020  99 99 99 99 F1 3F F0 CE  04 CD CC CC CC CC CC 08",
                    "00000030  C0 CD CC CC CC CC CC 00  C0 9A 99 99 99 99 99 F1",
                    "00000040  BF 9A 99 99 99 99 99 F1  3F F0 CE 02 C3 F5 28 5C",
                    "00000050  8F C2 F1 3F C3 F5 28 5C  8F C2 01 40 F0 CE 02 C3",
                    "00000060  F5 28 5C 8F C2 F1 3F C3  F5 28 5C 8F C2 01 40"
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }

            // Case 4
            {
                JsonToken[] tokensToWrite =
                {
                    JsonToken.ArrayStart(),
                        JsonToken.Float64NumberArray(values1),
                        JsonToken.Float64NumberArray(values2),
                        JsonToken.Float64NumberArray(values3),
                    JsonToken.ArrayEnd(),
                };

                string[] expectedText =
                {
                    @"[[-3.1,-2.1,-1.1,1.1],[1.01,2.01,3.01,4.01],[-1.11,-2.22,-3.33,-4.44]]"
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E2 72 E2 24 CC CD CC  CC CC CC CC 08 C0 CC CD",
                    "00000010  CC CC CC CC CC 00 C0 CC  9A 99 99 99 99 99 F1 BF",
                    "00000020  CC 9A 99 99 99 99 99 F1  3F E2 24 CC 29 5C 8F C2",
                    "00000030  F5 28 F0 3F CC 14 AE 47  E1 7A 14 00 40 CC 14 AE",
                    "00000040  47 E1 7A 14 08 40 CC 0A  D7 A3 70 3D 0A 10 40 E2",
                    "00000050  24 CC C3 F5 28 5C 8F C2  F1 BF CC C3 F5 28 5C 8F",
                    "00000060  C2 01 C0 CC A4 70 3D 0A  D7 A3 0A C0 CC C3 F5 28",
                    "00000070  5C 8F C2 11 C0"
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 F2 F0 CE 04 03 CD CC  CC CC CC CC 08 C0 CD CC",
                    "00000010  CC CC CC CC 00 C0 9A 99  99 99 99 99 F1 BF 9A 99",
                    "00000020  99 99 99 99 F1 3F 29 5C  8F C2 F5 28 F0 3F 14 AE",
                    "00000030  47 E1 7A 14 00 40 14 AE  47 E1 7A 14 08 40 0A D7",
                    "00000040  A3 70 3D 0A 10 40 C3 F5  28 5C 8F C2 F1 BF C3 F5",
                    "00000050  28 5C 8F C2 01 C0 A4 70  3D 0A D7 A3 0A C0 C3 F5",
                    "00000060  28 5C 8F C2 11 C0"
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }

            // Case 5
            {
                JsonToken[] tokensToWrite =
                {
                    JsonToken.ArrayStart(),
                        JsonToken.Float64NumberArray(values1),
                        JsonToken.Float64NumberArray(values2),
                        JsonToken.Float64NumberArray(values3),
                        JsonToken.Float64NumberArray(values4),
                        JsonToken.Float64NumberArray(values4),
                        JsonToken.Float64NumberArray(values3),
                        JsonToken.Float64NumberArray(values2),
                        JsonToken.Float64NumberArray(values1),
                    JsonToken.ArrayEnd(),
                };

                string[] expectedText =
                {
                    @"[[-3.1,-2.1,-1.1,1.1],[1.01,2.01,3.01,4.01],[-1.11,-2.22,-3.33,-4.44],[8.81,6.61,4.41,2.21],[8.81,6.",
                    @"61,4.41,2.21],[-1.11,-2.22,-3.33,-4.44],[1.01,2.01,3.01,4.01],[-3.1,-2.1,-1.1,1.1]]",
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E3 30 01 E2 24 CC CD  CC CC CC CC CC 08 C0 CC",
                    "00000010  CD CC CC CC CC CC 00 C0  CC 9A 99 99 99 99 99 F1",
                    "00000020  BF CC 9A 99 99 99 99 99  F1 3F E2 24 CC 29 5C 8F",
                    "00000030  C2 F5 28 F0 3F CC 14 AE  47 E1 7A 14 00 40 CC 14",
                    "00000040  AE 47 E1 7A 14 08 40 CC  0A D7 A3 70 3D 0A 10 40",
                    "00000050  E2 24 CC C3 F5 28 5C 8F  C2 F1 BF CC C3 F5 28 5C",
                    "00000060  8F C2 01 C0 CC A4 70 3D  0A D7 A3 0A C0 CC C3 F5",
                    "00000070  28 5C 8F C2 11 C0 E2 24  CC 1F 85 EB 51 B8 9E 21",
                    "00000080  40 CC 71 3D 0A D7 A3 70  1A 40 CC A4 70 3D 0A D7",
                    "00000090  A3 11 40 CC AE 47 E1 7A  14 AE 01 40 E2 24 CC 1F",
                    "000000A0  85 EB 51 B8 9E 21 40 CC  71 3D 0A D7 A3 70 1A 40",
                    "000000B0  CC A4 70 3D 0A D7 A3 11  40 CC AE 47 E1 7A 14 AE",
                    "000000C0  01 40 E2 24 CC C3 F5 28  5C 8F C2 F1 BF CC C3 F5",
                    "000000D0  28 5C 8F C2 01 C0 CC A4  70 3D 0A D7 A3 0A C0 CC",
                    "000000E0  C3 F5 28 5C 8F C2 11 C0  E2 24 CC 29 5C 8F C2 F5",
                    "000000F0  28 F0 3F CC 14 AE 47 E1  7A 14 00 40 CC 14 AE 47",
                    "00000100  E1 7A 14 08 40 CC 0A D7  A3 70 3D 0A 10 40 E2 24",
                    "00000110  CC CD CC CC CC CC CC 08  C0 CC CD CC CC CC CC CC",
                    "00000120  00 C0 CC 9A 99 99 99 99  99 F1 BF CC 9A 99 99 99",
                    "00000130  99 99 F1 3F"
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 F2 F0 CE 04 08 CD CC  CC CC CC CC 08 C0 CD CC",
                    "00000010  CC CC CC CC 00 C0 9A 99  99 99 99 99 F1 BF 9A 99",
                    "00000020  99 99 99 99 F1 3F 29 5C  8F C2 F5 28 F0 3F 14 AE",
                    "00000030  47 E1 7A 14 00 40 14 AE  47 E1 7A 14 08 40 0A D7",
                    "00000040  A3 70 3D 0A 10 40 C3 F5  28 5C 8F C2 F1 BF C3 F5",
                    "00000050  28 5C 8F C2 01 C0 A4 70  3D 0A D7 A3 0A C0 C3 F5",
                    "00000060  28 5C 8F C2 11 C0 1F 85  EB 51 B8 9E 21 40 71 3D",
                    "00000070  0A D7 A3 70 1A 40 A4 70  3D 0A D7 A3 11 40 AE 47",
                    "00000080  E1 7A 14 AE 01 40 1F 85  EB 51 B8 9E 21 40 71 3D",
                    "00000090  0A D7 A3 70 1A 40 A4 70  3D 0A D7 A3 11 40 AE 47",
                    "000000A0  E1 7A 14 AE 01 40 C3 F5  28 5C 8F C2 F1 BF C3 F5",
                    "000000B0  28 5C 8F C2 01 C0 A4 70  3D 0A D7 A3 0A C0 C3 F5",
                    "000000C0  28 5C 8F C2 11 C0 29 5C  8F C2 F5 28 F0 3F 14 AE",
                    "000000D0  47 E1 7A 14 00 40 14 AE  47 E1 7A 14 08 40 0A D7",
                    "000000E0  A3 70 3D 0A 10 40 CD CC  CC CC CC CC 08 C0 CD CC",
                    "000000F0  CC CC CC CC 00 C0 9A 99  99 99 99 99 F1 BF 9A 99",
                    "00000100  99 99 99 99 F1 3F"
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }

            // Case 6
            {
                double[][] valueSets = { valuesS1, valuesS2, valuesS3, valuesS4 };

                JsonToken[] tokensToWrite = new JsonToken[1 + 260 + 1];

                tokensToWrite[0] = JsonToken.ArrayStart();
                for (int i = 0; i < tokensToWrite.Length - 2; i++)
                {
                    tokensToWrite[1 + i] = JsonToken.Float64NumberArray(valueSets[i % valueSets.Length]);
                }
                tokensToWrite[^1] = JsonToken.ArrayEnd();

                string[] expectedText =
                {
                    @"[[1.11,2.22],[2.22,3.33],[3.33,4.44],[4.44,5.55],[1.11,2.22],[2.22,3.33],[3.33,4.44],[4.44,5.55],[1.",
                    @"11,2.22],[2.22,3.33],[3.33,4.44],[4.44,5.55],[1.11,2.22],[2.22,3.33],[3.33,4.44],[4.44,5.55],[1.11,2",
                    @".22],[2.22,3.33],[3.33,4.44],[4.44,5.55],[1.11,2.22],[2.22,3.33],[3.33,4.44],[4.44,5.55],[1.11,2.22]",
                    @",[2.22,3.33],[3.33,4.44],[4.44,5.55],[1.11,2.22],[2.22,3.33],[3.33,4.44],[4.44,5.55],[1.11,2.22],[2.",
                    @"22,3.33],[3.33,4.44],[4.44,5.55],[1.11,2.22],[2.22,3.33],[3.33,4.44],[4.44,5.55],[1.11,2.22],[2.22,3",
                    @".33],[3.33,4.44],[4.44,5.55],[1.11,2.22],[2.22,3.33],[3.33,4.44],[4.44,5.55],[1.11,2.22],[2.22,3.33]",
                    @",[3.33,4.44],[4.44,5.55],[1.11,2.22],[2.22,3.33],[3.33,4.44],[4.44,5.55],[1.11,2.22],[2.22,3.33],[3.",
                    @"33,4.44],[4.44,5.55],[1.11,2.22],[2.22,3.33],[3.33,4.44],[4.44,5.55],[1.11,2.22],[2.22,3.33],[3.33,4",
                    @".44],[4.44,5.55],[1.11,2.22],[2.22,3.33],[3.33,4.44],[4.44,5.55],[1.11,2.22],[2.22,3.33],[3.33,4.44]",
                    @",[4.44,5.55],[1.11,2.22],[2.22,3.33],[3.33,4.44],[4.44,5.55],[1.11,2.22],[2.22,3.33],[3.33,4.44],[4.",
                    @"44,5.55],[1.11,2.22],[2.22,3.33],[3.33,4.44],[4.44,5.55],[1.11,2.22],[2.22,3.33],[3.33,4.44],[4.44,5",
                    @".55],[1.11,2.22],[2.22,3.33],[3.33,4.44],[4.44,5.55],[1.11,2.22],[2.22,3.33],[3.33,4.44],[4.44,5.55]",
                    @",[1.11,2.22],[2.22,3.33],[3.33,4.44],[4.44,5.55],[1.11,2.22],[2.22,3.33],[3.33,4.44],[4.44,5.55],[1.",
                    @"11,2.22],[2.22,3.33],[3.33,4.44],[4.44,5.55],[1.11,2.22],[2.22,3.33],[3.33,4.44],[4.44,5.55],[1.11,2",
                    @".22],[2.22,3.33],[3.33,4.44],[4.44,5.55],[1.11,2.22],[2.22,3.33],[3.33,4.44],[4.44,5.55],[1.11,2.22]",
                    @",[2.22,3.33],[3.33,4.44],[4.44,5.55],[1.11,2.22],[2.22,3.33],[3.33,4.44],[4.44,5.55],[1.11,2.22],[2.",
                    @"22,3.33],[3.33,4.44],[4.44,5.55],[1.11,2.22],[2.22,3.33],[3.33,4.44],[4.44,5.55],[1.11,2.22],[2.22,3",
                    @".33],[3.33,4.44],[4.44,5.55],[1.11,2.22],[2.22,3.33],[3.33,4.44],[4.44,5.55],[1.11,2.22],[2.22,3.33]",
                    @",[3.33,4.44],[4.44,5.55],[1.11,2.22],[2.22,3.33],[3.33,4.44],[4.44,5.55],[1.11,2.22],[2.22,3.33],[3.",
                    @"33,4.44],[4.44,5.55],[1.11,2.22],[2.22,3.33],[3.33,4.44],[4.44,5.55],[1.11,2.22],[2.22,3.33],[3.33,4",
                    @".44],[4.44,5.55],[1.11,2.22],[2.22,3.33],[3.33,4.44],[4.44,5.55],[1.11,2.22],[2.22,3.33],[3.33,4.44]",
                    @",[4.44,5.55],[1.11,2.22],[2.22,3.33],[3.33,4.44],[4.44,5.55],[1.11,2.22],[2.22,3.33],[3.33,4.44],[4.",
                    @"44,5.55],[1.11,2.22],[2.22,3.33],[3.33,4.44],[4.44,5.55],[1.11,2.22],[2.22,3.33],[3.33,4.44],[4.44,5",
                    @".55],[1.11,2.22],[2.22,3.33],[3.33,4.44],[4.44,5.55],[1.11,2.22],[2.22,3.33],[3.33,4.44],[4.44,5.55]",
                    @",[1.11,2.22],[2.22,3.33],[3.33,4.44],[4.44,5.55],[1.11,2.22],[2.22,3.33],[3.33,4.44],[4.44,5.55],[1.",
                    @"11,2.22],[2.22,3.33],[3.33,4.44],[4.44,5.55],[1.11,2.22],[2.22,3.33],[3.33,4.44],[4.44,5.55],[1.11,2",
                    @".22],[2.22,3.33],[3.33,4.44],[4.44,5.55],[1.11,2.22],[2.22,3.33],[3.33,4.44],[4.44,5.55],[1.11,2.22]",
                    @",[2.22,3.33],[3.33,4.44],[4.44,5.55],[1.11,2.22],[2.22,3.33],[3.33,4.44],[4.44,5.55],[1.11,2.22],[2.",
                    @"22,3.33],[3.33,4.44],[4.44,5.55],[1.11,2.22],[2.22,3.33],[3.33,4.44],[4.44,5.55],[1.11,2.22],[2.22,3",
                    @".33],[3.33,4.44],[4.44,5.55],[1.11,2.22],[2.22,3.33],[3.33,4.44],[4.44,5.55],[1.11,2.22],[2.22,3.33]",
                    @",[3.33,4.44],[4.44,5.55],[1.11,2.22],[2.22,3.33],[3.33,4.44],[4.44,5.55],[1.11,2.22],[2.22,3.33],[3.",
                    @"33,4.44],[4.44,5.55]]",
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E6 50 14 04 01 E2 12  CC C3 F5 28 5C 8F C2 F1",
                    "00000010  3F CC C3 F5 28 5C 8F C2  01 40 E2 12 CC C3 F5 28",
                    "00000020  5C 8F C2 01 40 CC A4 70  3D 0A D7 A3 0A 40 E2 12",
                    "00000030  CC A4 70 3D 0A D7 A3 0A  40 CC C3 F5 28 5C 8F C2",
                    "00000040  11 40 E2 12 CC C3 F5 28  5C 8F C2 11 40 CC 33 33",
                    "00000050  33 33 33 33 16 40 E2 12  CC C3 F5 28 5C 8F C2 F1",
                    "00000060  3F CC C3 F5 28 5C 8F C2  01 40 E2 12 CC C3 F5 28",
                    "00000070  5C 8F C2 01 40 CC A4 70  3D 0A D7 A3 0A 40 E2 12",
                    "00000080  CC A4 70 3D 0A D7 A3 0A  40 CC C3 F5 28 5C 8F C2",
                    "00000090  11 40 E2 12 CC C3 F5 28  5C 8F C2 11 40 CC 33 33",
                    "000000A0  33 33 33 33 16 40 E2 12  CC C3 F5 28 5C 8F C2 F1",
                    "000000B0  3F CC C3 F5 28 5C 8F C2  01 40 E2 12 CC C3 F5 28",
                    "000000C0  5C 8F C2 01 40 CC A4 70  3D 0A D7 A3 0A 40 E2 12",
                    "000000D0  CC A4 70 3D 0A D7 A3 0A  40 CC C3 F5 28 5C 8F C2",
                    "000000E0  11 40 E2 12 CC C3 F5 28  5C 8F C2 11 40 CC 33 33",
                    "000000F0  33 33 33 33 16 40 E2 12  CC C3 F5 28 5C 8F C2 F1",
                    "00000100  3F CC C3 F5 28 5C 8F C2  01 40 E2 12 CC C3 F5 28",
                    "00000110  5C 8F C2 01 40 CC A4 70  3D 0A D7 A3 0A 40 E2 12",
                    "00000120  CC A4 70 3D 0A D7 A3 0A  40 CC C3 F5 28 5C 8F C2",
                    "00000130  11 40 E2 12 CC C3 F5 28  5C 8F C2 11 40 CC 33 33",
                    "00000140  33 33 33 33 16 40 E2 12  CC C3 F5 28 5C 8F C2 F1",
                    "00000150  3F CC C3 F5 28 5C 8F C2  01 40 E2 12 CC C3 F5 28",
                    "00000160  5C 8F C2 01 40 CC A4 70  3D 0A D7 A3 0A 40 E2 12",
                    "00000170  CC A4 70 3D 0A D7 A3 0A  40 CC C3 F5 28 5C 8F C2",
                    "00000180  11 40 E2 12 CC C3 F5 28  5C 8F C2 11 40 CC 33 33",
                    "00000190  33 33 33 33 16 40 E2 12  CC C3 F5 28 5C 8F C2 F1",
                    "000001A0  3F CC C3 F5 28 5C 8F C2  01 40 E2 12 CC C3 F5 28",
                    "000001B0  5C 8F C2 01 40 CC A4 70  3D 0A D7 A3 0A 40 E2 12",
                    "000001C0  CC A4 70 3D 0A D7 A3 0A  40 CC C3 F5 28 5C 8F C2",
                    "000001D0  11 40 E2 12 CC C3 F5 28  5C 8F C2 11 40 CC 33 33",
                    "000001E0  33 33 33 33 16 40 E2 12  CC C3 F5 28 5C 8F C2 F1",
                    "000001F0  3F CC C3 F5 28 5C 8F C2  01 40 E2 12 CC C3 F5 28",
                    "00000200  5C 8F C2 01 40 CC A4 70  3D 0A D7 A3 0A 40 E2 12",
                    "00000210  CC A4 70 3D 0A D7 A3 0A  40 CC C3 F5 28 5C 8F C2",
                    "00000220  11 40 E2 12 CC C3 F5 28  5C 8F C2 11 40 CC 33 33",
                    "00000230  33 33 33 33 16 40 E2 12  CC C3 F5 28 5C 8F C2 F1",
                    "00000240  3F CC C3 F5 28 5C 8F C2  01 40 E2 12 CC C3 F5 28",
                    "00000250  5C 8F C2 01 40 CC A4 70  3D 0A D7 A3 0A 40 E2 12",
                    "00000260  CC A4 70 3D 0A D7 A3 0A  40 CC C3 F5 28 5C 8F C2",
                    "00000270  11 40 E2 12 CC C3 F5 28  5C 8F C2 11 40 CC 33 33",
                    "00000280  33 33 33 33 16 40 E2 12  CC C3 F5 28 5C 8F C2 F1",
                    "00000290  3F CC C3 F5 28 5C 8F C2  01 40 E2 12 CC C3 F5 28",
                    "000002A0  5C 8F C2 01 40 CC A4 70  3D 0A D7 A3 0A 40 E2 12",
                    "000002B0  CC A4 70 3D 0A D7 A3 0A  40 CC C3 F5 28 5C 8F C2",
                    "000002C0  11 40 E2 12 CC C3 F5 28  5C 8F C2 11 40 CC 33 33",
                    "000002D0  33 33 33 33 16 40 E2 12  CC C3 F5 28 5C 8F C2 F1",
                    "000002E0  3F CC C3 F5 28 5C 8F C2  01 40 E2 12 CC C3 F5 28",
                    "000002F0  5C 8F C2 01 40 CC A4 70  3D 0A D7 A3 0A 40 E2 12",
                    "00000300  CC A4 70 3D 0A D7 A3 0A  40 CC C3 F5 28 5C 8F C2",
                    "00000310  11 40 E2 12 CC C3 F5 28  5C 8F C2 11 40 CC 33 33",
                    "00000320  33 33 33 33 16 40 E2 12  CC C3 F5 28 5C 8F C2 F1",
                    "00000330  3F CC C3 F5 28 5C 8F C2  01 40 E2 12 CC C3 F5 28",
                    "00000340  5C 8F C2 01 40 CC A4 70  3D 0A D7 A3 0A 40 E2 12",
                    "00000350  CC A4 70 3D 0A D7 A3 0A  40 CC C3 F5 28 5C 8F C2",
                    "00000360  11 40 E2 12 CC C3 F5 28  5C 8F C2 11 40 CC 33 33",
                    "00000370  33 33 33 33 16 40 E2 12  CC C3 F5 28 5C 8F C2 F1",
                    "00000380  3F CC C3 F5 28 5C 8F C2  01 40 E2 12 CC C3 F5 28",
                    "00000390  5C 8F C2 01 40 CC A4 70  3D 0A D7 A3 0A 40 E2 12",
                    "000003A0  CC A4 70 3D 0A D7 A3 0A  40 CC C3 F5 28 5C 8F C2",
                    "000003B0  11 40 E2 12 CC C3 F5 28  5C 8F C2 11 40 CC 33 33",
                    "000003C0  33 33 33 33 16 40 E2 12  CC C3 F5 28 5C 8F C2 F1",
                    "000003D0  3F CC C3 F5 28 5C 8F C2  01 40 E2 12 CC C3 F5 28",
                    "000003E0  5C 8F C2 01 40 CC A4 70  3D 0A D7 A3 0A 40 E2 12",
                    "000003F0  CC A4 70 3D 0A D7 A3 0A  40 CC C3 F5 28 5C 8F C2",
                    "00000400  11 40 E2 12 CC C3 F5 28  5C 8F C2 11 40 CC 33 33",
                    "00000410  33 33 33 33 16 40 E2 12  CC C3 F5 28 5C 8F C2 F1",
                    "00000420  3F CC C3 F5 28 5C 8F C2  01 40 E2 12 CC C3 F5 28",
                    "00000430  5C 8F C2 01 40 CC A4 70  3D 0A D7 A3 0A 40 E2 12",
                    "00000440  CC A4 70 3D 0A D7 A3 0A  40 CC C3 F5 28 5C 8F C2",
                    "00000450  11 40 E2 12 CC C3 F5 28  5C 8F C2 11 40 CC 33 33",
                    "00000460  33 33 33 33 16 40 E2 12  CC C3 F5 28 5C 8F C2 F1",
                    "00000470  3F CC C3 F5 28 5C 8F C2  01 40 E2 12 CC C3 F5 28",
                    "00000480  5C 8F C2 01 40 CC A4 70  3D 0A D7 A3 0A 40 E2 12",
                    "00000490  CC A4 70 3D 0A D7 A3 0A  40 CC C3 F5 28 5C 8F C2",
                    "000004A0  11 40 E2 12 CC C3 F5 28  5C 8F C2 11 40 CC 33 33",
                    "000004B0  33 33 33 33 16 40 E2 12  CC C3 F5 28 5C 8F C2 F1",
                    "000004C0  3F CC C3 F5 28 5C 8F C2  01 40 E2 12 CC C3 F5 28",
                    "000004D0  5C 8F C2 01 40 CC A4 70  3D 0A D7 A3 0A 40 E2 12",
                    "000004E0  CC A4 70 3D 0A D7 A3 0A  40 CC C3 F5 28 5C 8F C2",
                    "000004F0  11 40 E2 12 CC C3 F5 28  5C 8F C2 11 40 CC 33 33",
                    "00000500  33 33 33 33 16 40 E2 12  CC C3 F5 28 5C 8F C2 F1",
                    "00000510  3F CC C3 F5 28 5C 8F C2  01 40 E2 12 CC C3 F5 28",
                    "00000520  5C 8F C2 01 40 CC A4 70  3D 0A D7 A3 0A 40 E2 12",
                    "00000530  CC A4 70 3D 0A D7 A3 0A  40 CC C3 F5 28 5C 8F C2",
                    "00000540  11 40 E2 12 CC C3 F5 28  5C 8F C2 11 40 CC 33 33",
                    "00000550  33 33 33 33 16 40 E2 12  CC C3 F5 28 5C 8F C2 F1",
                    "00000560  3F CC C3 F5 28 5C 8F C2  01 40 E2 12 CC C3 F5 28",
                    "00000570  5C 8F C2 01 40 CC A4 70  3D 0A D7 A3 0A 40 E2 12",
                    "00000580  CC A4 70 3D 0A D7 A3 0A  40 CC C3 F5 28 5C 8F C2",
                    "00000590  11 40 E2 12 CC C3 F5 28  5C 8F C2 11 40 CC 33 33",
                    "000005A0  33 33 33 33 16 40 E2 12  CC C3 F5 28 5C 8F C2 F1",
                    "000005B0  3F CC C3 F5 28 5C 8F C2  01 40 E2 12 CC C3 F5 28",
                    "000005C0  5C 8F C2 01 40 CC A4 70  3D 0A D7 A3 0A 40 E2 12",
                    "000005D0  CC A4 70 3D 0A D7 A3 0A  40 CC C3 F5 28 5C 8F C2",
                    "000005E0  11 40 E2 12 CC C3 F5 28  5C 8F C2 11 40 CC 33 33",
                    "000005F0  33 33 33 33 16 40 E2 12  CC C3 F5 28 5C 8F C2 F1",
                    "00000600  3F CC C3 F5 28 5C 8F C2  01 40 E2 12 CC C3 F5 28",
                    "00000610  5C 8F C2 01 40 CC A4 70  3D 0A D7 A3 0A 40 E2 12",
                    "00000620  CC A4 70 3D 0A D7 A3 0A  40 CC C3 F5 28 5C 8F C2",
                    "00000630  11 40 E2 12 CC C3 F5 28  5C 8F C2 11 40 CC 33 33",
                    "00000640  33 33 33 33 16 40 E2 12  CC C3 F5 28 5C 8F C2 F1",
                    "00000650  3F CC C3 F5 28 5C 8F C2  01 40 E2 12 CC C3 F5 28",
                    "00000660  5C 8F C2 01 40 CC A4 70  3D 0A D7 A3 0A 40 E2 12",
                    "00000670  CC A4 70 3D 0A D7 A3 0A  40 CC C3 F5 28 5C 8F C2",
                    "00000680  11 40 E2 12 CC C3 F5 28  5C 8F C2 11 40 CC 33 33",
                    "00000690  33 33 33 33 16 40 E2 12  CC C3 F5 28 5C 8F C2 F1",
                    "000006A0  3F CC C3 F5 28 5C 8F C2  01 40 E2 12 CC C3 F5 28",
                    "000006B0  5C 8F C2 01 40 CC A4 70  3D 0A D7 A3 0A 40 E2 12",
                    "000006C0  CC A4 70 3D 0A D7 A3 0A  40 CC C3 F5 28 5C 8F C2",
                    "000006D0  11 40 E2 12 CC C3 F5 28  5C 8F C2 11 40 CC 33 33",
                    "000006E0  33 33 33 33 16 40 E2 12  CC C3 F5 28 5C 8F C2 F1",
                    "000006F0  3F CC C3 F5 28 5C 8F C2  01 40 E2 12 CC C3 F5 28",
                    "00000700  5C 8F C2 01 40 CC A4 70  3D 0A D7 A3 0A 40 E2 12",
                    "00000710  CC A4 70 3D 0A D7 A3 0A  40 CC C3 F5 28 5C 8F C2",
                    "00000720  11 40 E2 12 CC C3 F5 28  5C 8F C2 11 40 CC 33 33",
                    "00000730  33 33 33 33 16 40 E2 12  CC C3 F5 28 5C 8F C2 F1",
                    "00000740  3F CC C3 F5 28 5C 8F C2  01 40 E2 12 CC C3 F5 28",
                    "00000750  5C 8F C2 01 40 CC A4 70  3D 0A D7 A3 0A 40 E2 12",
                    "00000760  CC A4 70 3D 0A D7 A3 0A  40 CC C3 F5 28 5C 8F C2",
                    "00000770  11 40 E2 12 CC C3 F5 28  5C 8F C2 11 40 CC 33 33",
                    "00000780  33 33 33 33 16 40 E2 12  CC C3 F5 28 5C 8F C2 F1",
                    "00000790  3F CC C3 F5 28 5C 8F C2  01 40 E2 12 CC C3 F5 28",
                    "000007A0  5C 8F C2 01 40 CC A4 70  3D 0A D7 A3 0A 40 E2 12",
                    "000007B0  CC A4 70 3D 0A D7 A3 0A  40 CC C3 F5 28 5C 8F C2",
                    "000007C0  11 40 E2 12 CC C3 F5 28  5C 8F C2 11 40 CC 33 33",
                    "000007D0  33 33 33 33 16 40 E2 12  CC C3 F5 28 5C 8F C2 F1",
                    "000007E0  3F CC C3 F5 28 5C 8F C2  01 40 E2 12 CC C3 F5 28",
                    "000007F0  5C 8F C2 01 40 CC A4 70  3D 0A D7 A3 0A 40 E2 12",
                    "00000800  CC A4 70 3D 0A D7 A3 0A  40 CC C3 F5 28 5C 8F C2",
                    "00000810  11 40 E2 12 CC C3 F5 28  5C 8F C2 11 40 CC 33 33",
                    "00000820  33 33 33 33 16 40 E2 12  CC C3 F5 28 5C 8F C2 F1",
                    "00000830  3F CC C3 F5 28 5C 8F C2  01 40 E2 12 CC C3 F5 28",
                    "00000840  5C 8F C2 01 40 CC A4 70  3D 0A D7 A3 0A 40 E2 12",
                    "00000850  CC A4 70 3D 0A D7 A3 0A  40 CC C3 F5 28 5C 8F C2",
                    "00000860  11 40 E2 12 CC C3 F5 28  5C 8F C2 11 40 CC 33 33",
                    "00000870  33 33 33 33 16 40 E2 12  CC C3 F5 28 5C 8F C2 F1",
                    "00000880  3F CC C3 F5 28 5C 8F C2  01 40 E2 12 CC C3 F5 28",
                    "00000890  5C 8F C2 01 40 CC A4 70  3D 0A D7 A3 0A 40 E2 12",
                    "000008A0  CC A4 70 3D 0A D7 A3 0A  40 CC C3 F5 28 5C 8F C2",
                    "000008B0  11 40 E2 12 CC C3 F5 28  5C 8F C2 11 40 CC 33 33",
                    "000008C0  33 33 33 33 16 40 E2 12  CC C3 F5 28 5C 8F C2 F1",
                    "000008D0  3F CC C3 F5 28 5C 8F C2  01 40 E2 12 CC C3 F5 28",
                    "000008E0  5C 8F C2 01 40 CC A4 70  3D 0A D7 A3 0A 40 E2 12",
                    "000008F0  CC A4 70 3D 0A D7 A3 0A  40 CC C3 F5 28 5C 8F C2",
                    "00000900  11 40 E2 12 CC C3 F5 28  5C 8F C2 11 40 CC 33 33",
                    "00000910  33 33 33 33 16 40 E2 12  CC C3 F5 28 5C 8F C2 F1",
                    "00000920  3F CC C3 F5 28 5C 8F C2  01 40 E2 12 CC C3 F5 28",
                    "00000930  5C 8F C2 01 40 CC A4 70  3D 0A D7 A3 0A 40 E2 12",
                    "00000940  CC A4 70 3D 0A D7 A3 0A  40 CC C3 F5 28 5C 8F C2",
                    "00000950  11 40 E2 12 CC C3 F5 28  5C 8F C2 11 40 CC 33 33",
                    "00000960  33 33 33 33 16 40 E2 12  CC C3 F5 28 5C 8F C2 F1",
                    "00000970  3F CC C3 F5 28 5C 8F C2  01 40 E2 12 CC C3 F5 28",
                    "00000980  5C 8F C2 01 40 CC A4 70  3D 0A D7 A3 0A 40 E2 12",
                    "00000990  CC A4 70 3D 0A D7 A3 0A  40 CC C3 F5 28 5C 8F C2",
                    "000009A0  11 40 E2 12 CC C3 F5 28  5C 8F C2 11 40 CC 33 33",
                    "000009B0  33 33 33 33 16 40 E2 12  CC C3 F5 28 5C 8F C2 F1",
                    "000009C0  3F CC C3 F5 28 5C 8F C2  01 40 E2 12 CC C3 F5 28",
                    "000009D0  5C 8F C2 01 40 CC A4 70  3D 0A D7 A3 0A 40 E2 12",
                    "000009E0  CC A4 70 3D 0A D7 A3 0A  40 CC C3 F5 28 5C 8F C2",
                    "000009F0  11 40 E2 12 CC C3 F5 28  5C 8F C2 11 40 CC 33 33",
                    "00000A00  33 33 33 33 16 40 E2 12  CC C3 F5 28 5C 8F C2 F1",
                    "00000A10  3F CC C3 F5 28 5C 8F C2  01 40 E2 12 CC C3 F5 28",
                    "00000A20  5C 8F C2 01 40 CC A4 70  3D 0A D7 A3 0A 40 E2 12",
                    "00000A30  CC A4 70 3D 0A D7 A3 0A  40 CC C3 F5 28 5C 8F C2",
                    "00000A40  11 40 E2 12 CC C3 F5 28  5C 8F C2 11 40 CC 33 33",
                    "00000A50  33 33 33 33 16 40 E2 12  CC C3 F5 28 5C 8F C2 F1",
                    "00000A60  3F CC C3 F5 28 5C 8F C2  01 40 E2 12 CC C3 F5 28",
                    "00000A70  5C 8F C2 01 40 CC A4 70  3D 0A D7 A3 0A 40 E2 12",
                    "00000A80  CC A4 70 3D 0A D7 A3 0A  40 CC C3 F5 28 5C 8F C2",
                    "00000A90  11 40 E2 12 CC C3 F5 28  5C 8F C2 11 40 CC 33 33",
                    "00000AA0  33 33 33 33 16 40 E2 12  CC C3 F5 28 5C 8F C2 F1",
                    "00000AB0  3F CC C3 F5 28 5C 8F C2  01 40 E2 12 CC C3 F5 28",
                    "00000AC0  5C 8F C2 01 40 CC A4 70  3D 0A D7 A3 0A 40 E2 12",
                    "00000AD0  CC A4 70 3D 0A D7 A3 0A  40 CC C3 F5 28 5C 8F C2",
                    "00000AE0  11 40 E2 12 CC C3 F5 28  5C 8F C2 11 40 CC 33 33",
                    "00000AF0  33 33 33 33 16 40 E2 12  CC C3 F5 28 5C 8F C2 F1",
                    "00000B00  3F CC C3 F5 28 5C 8F C2  01 40 E2 12 CC C3 F5 28",
                    "00000B10  5C 8F C2 01 40 CC A4 70  3D 0A D7 A3 0A 40 E2 12",
                    "00000B20  CC A4 70 3D 0A D7 A3 0A  40 CC C3 F5 28 5C 8F C2",
                    "00000B30  11 40 E2 12 CC C3 F5 28  5C 8F C2 11 40 CC 33 33",
                    "00000B40  33 33 33 33 16 40 E2 12  CC C3 F5 28 5C 8F C2 F1",
                    "00000B50  3F CC C3 F5 28 5C 8F C2  01 40 E2 12 CC C3 F5 28",
                    "00000B60  5C 8F C2 01 40 CC A4 70  3D 0A D7 A3 0A 40 E2 12",
                    "00000B70  CC A4 70 3D 0A D7 A3 0A  40 CC C3 F5 28 5C 8F C2",
                    "00000B80  11 40 E2 12 CC C3 F5 28  5C 8F C2 11 40 CC 33 33",
                    "00000B90  33 33 33 33 16 40 E2 12  CC C3 F5 28 5C 8F C2 F1",
                    "00000BA0  3F CC C3 F5 28 5C 8F C2  01 40 E2 12 CC C3 F5 28",
                    "00000BB0  5C 8F C2 01 40 CC A4 70  3D 0A D7 A3 0A 40 E2 12",
                    "00000BC0  CC A4 70 3D 0A D7 A3 0A  40 CC C3 F5 28 5C 8F C2",
                    "00000BD0  11 40 E2 12 CC C3 F5 28  5C 8F C2 11 40 CC 33 33",
                    "00000BE0  33 33 33 33 16 40 E2 12  CC C3 F5 28 5C 8F C2 F1",
                    "00000BF0  3F CC C3 F5 28 5C 8F C2  01 40 E2 12 CC C3 F5 28",
                    "00000C00  5C 8F C2 01 40 CC A4 70  3D 0A D7 A3 0A 40 E2 12",
                    "00000C10  CC A4 70 3D 0A D7 A3 0A  40 CC C3 F5 28 5C 8F C2",
                    "00000C20  11 40 E2 12 CC C3 F5 28  5C 8F C2 11 40 CC 33 33",
                    "00000C30  33 33 33 33 16 40 E2 12  CC C3 F5 28 5C 8F C2 F1",
                    "00000C40  3F CC C3 F5 28 5C 8F C2  01 40 E2 12 CC C3 F5 28",
                    "00000C50  5C 8F C2 01 40 CC A4 70  3D 0A D7 A3 0A 40 E2 12",
                    "00000C60  CC A4 70 3D 0A D7 A3 0A  40 CC C3 F5 28 5C 8F C2",
                    "00000C70  11 40 E2 12 CC C3 F5 28  5C 8F C2 11 40 CC 33 33",
                    "00000C80  33 33 33 33 16 40 E2 12  CC C3 F5 28 5C 8F C2 F1",
                    "00000C90  3F CC C3 F5 28 5C 8F C2  01 40 E2 12 CC C3 F5 28",
                    "00000CA0  5C 8F C2 01 40 CC A4 70  3D 0A D7 A3 0A 40 E2 12",
                    "00000CB0  CC A4 70 3D 0A D7 A3 0A  40 CC C3 F5 28 5C 8F C2",
                    "00000CC0  11 40 E2 12 CC C3 F5 28  5C 8F C2 11 40 CC 33 33",
                    "00000CD0  33 33 33 33 16 40 E2 12  CC C3 F5 28 5C 8F C2 F1",
                    "00000CE0  3F CC C3 F5 28 5C 8F C2  01 40 E2 12 CC C3 F5 28",
                    "00000CF0  5C 8F C2 01 40 CC A4 70  3D 0A D7 A3 0A 40 E2 12",
                    "00000D00  CC A4 70 3D 0A D7 A3 0A  40 CC C3 F5 28 5C 8F C2",
                    "00000D10  11 40 E2 12 CC C3 F5 28  5C 8F C2 11 40 CC 33 33",
                    "00000D20  33 33 33 33 16 40 E2 12  CC C3 F5 28 5C 8F C2 F1",
                    "00000D30  3F CC C3 F5 28 5C 8F C2  01 40 E2 12 CC C3 F5 28",
                    "00000D40  5C 8F C2 01 40 CC A4 70  3D 0A D7 A3 0A 40 E2 12",
                    "00000D50  CC A4 70 3D 0A D7 A3 0A  40 CC C3 F5 28 5C 8F C2",
                    "00000D60  11 40 E2 12 CC C3 F5 28  5C 8F C2 11 40 CC 33 33",
                    "00000D70  33 33 33 33 16 40 E2 12  CC C3 F5 28 5C 8F C2 F1",
                    "00000D80  3F CC C3 F5 28 5C 8F C2  01 40 E2 12 CC C3 F5 28",
                    "00000D90  5C 8F C2 01 40 CC A4 70  3D 0A D7 A3 0A 40 E2 12",
                    "00000DA0  CC A4 70 3D 0A D7 A3 0A  40 CC C3 F5 28 5C 8F C2",
                    "00000DB0  11 40 E2 12 CC C3 F5 28  5C 8F C2 11 40 CC 33 33",
                    "00000DC0  33 33 33 33 16 40 E2 12  CC C3 F5 28 5C 8F C2 F1",
                    "00000DD0  3F CC C3 F5 28 5C 8F C2  01 40 E2 12 CC C3 F5 28",
                    "00000DE0  5C 8F C2 01 40 CC A4 70  3D 0A D7 A3 0A 40 E2 12",
                    "00000DF0  CC A4 70 3D 0A D7 A3 0A  40 CC C3 F5 28 5C 8F C2",
                    "00000E00  11 40 E2 12 CC C3 F5 28  5C 8F C2 11 40 CC 33 33",
                    "00000E10  33 33 33 33 16 40 E2 12  CC C3 F5 28 5C 8F C2 F1",
                    "00000E20  3F CC C3 F5 28 5C 8F C2  01 40 E2 12 CC C3 F5 28",
                    "00000E30  5C 8F C2 01 40 CC A4 70  3D 0A D7 A3 0A 40 E2 12",
                    "00000E40  CC A4 70 3D 0A D7 A3 0A  40 CC C3 F5 28 5C 8F C2",
                    "00000E50  11 40 E2 12 CC C3 F5 28  5C 8F C2 11 40 CC 33 33",
                    "00000E60  33 33 33 33 16 40 E2 12  CC C3 F5 28 5C 8F C2 F1",
                    "00000E70  3F CC C3 F5 28 5C 8F C2  01 40 E2 12 CC C3 F5 28",
                    "00000E80  5C 8F C2 01 40 CC A4 70  3D 0A D7 A3 0A 40 E2 12",
                    "00000E90  CC A4 70 3D 0A D7 A3 0A  40 CC C3 F5 28 5C 8F C2",
                    "00000EA0  11 40 E2 12 CC C3 F5 28  5C 8F C2 11 40 CC 33 33",
                    "00000EB0  33 33 33 33 16 40 E2 12  CC C3 F5 28 5C 8F C2 F1",
                    "00000EC0  3F CC C3 F5 28 5C 8F C2  01 40 E2 12 CC C3 F5 28",
                    "00000ED0  5C 8F C2 01 40 CC A4 70  3D 0A D7 A3 0A 40 E2 12",
                    "00000EE0  CC A4 70 3D 0A D7 A3 0A  40 CC C3 F5 28 5C 8F C2",
                    "00000EF0  11 40 E2 12 CC C3 F5 28  5C 8F C2 11 40 CC 33 33",
                    "00000F00  33 33 33 33 16 40 E2 12  CC C3 F5 28 5C 8F C2 F1",
                    "00000F10  3F CC C3 F5 28 5C 8F C2  01 40 E2 12 CC C3 F5 28",
                    "00000F20  5C 8F C2 01 40 CC A4 70  3D 0A D7 A3 0A 40 E2 12",
                    "00000F30  CC A4 70 3D 0A D7 A3 0A  40 CC C3 F5 28 5C 8F C2",
                    "00000F40  11 40 E2 12 CC C3 F5 28  5C 8F C2 11 40 CC 33 33",
                    "00000F50  33 33 33 33 16 40 E2 12  CC C3 F5 28 5C 8F C2 F1",
                    "00000F60  3F CC C3 F5 28 5C 8F C2  01 40 E2 12 CC C3 F5 28",
                    "00000F70  5C 8F C2 01 40 CC A4 70  3D 0A D7 A3 0A 40 E2 12",
                    "00000F80  CC A4 70 3D 0A D7 A3 0A  40 CC C3 F5 28 5C 8F C2",
                    "00000F90  11 40 E2 12 CC C3 F5 28  5C 8F C2 11 40 CC 33 33",
                    "00000FA0  33 33 33 33 16 40 E2 12  CC C3 F5 28 5C 8F C2 F1",
                    "00000FB0  3F CC C3 F5 28 5C 8F C2  01 40 E2 12 CC C3 F5 28",
                    "00000FC0  5C 8F C2 01 40 CC A4 70  3D 0A D7 A3 0A 40 E2 12",
                    "00000FD0  CC A4 70 3D 0A D7 A3 0A  40 CC C3 F5 28 5C 8F C2",
                    "00000FE0  11 40 E2 12 CC C3 F5 28  5C 8F C2 11 40 CC 33 33",
                    "00000FF0  33 33 33 33 16 40 E2 12  CC C3 F5 28 5C 8F C2 F1",
                    "00001000  3F CC C3 F5 28 5C 8F C2  01 40 E2 12 CC C3 F5 28",
                    "00001010  5C 8F C2 01 40 CC A4 70  3D 0A D7 A3 0A 40 E2 12",
                    "00001020  CC A4 70 3D 0A D7 A3 0A  40 CC C3 F5 28 5C 8F C2",
                    "00001030  11 40 E2 12 CC C3 F5 28  5C 8F C2 11 40 CC 33 33",
                    "00001040  33 33 33 33 16 40 E2 12  CC C3 F5 28 5C 8F C2 F1",
                    "00001050  3F CC C3 F5 28 5C 8F C2  01 40 E2 12 CC C3 F5 28",
                    "00001060  5C 8F C2 01 40 CC A4 70  3D 0A D7 A3 0A 40 E2 12",
                    "00001070  CC A4 70 3D 0A D7 A3 0A  40 CC C3 F5 28 5C 8F C2",
                    "00001080  11 40 E2 12 CC C3 F5 28  5C 8F C2 11 40 CC 33 33",
                    "00001090  33 33 33 33 16 40 E2 12  CC C3 F5 28 5C 8F C2 F1",
                    "000010A0  3F CC C3 F5 28 5C 8F C2  01 40 E2 12 CC C3 F5 28",
                    "000010B0  5C 8F C2 01 40 CC A4 70  3D 0A D7 A3 0A 40 E2 12",
                    "000010C0  CC A4 70 3D 0A D7 A3 0A  40 CC C3 F5 28 5C 8F C2",
                    "000010D0  11 40 E2 12 CC C3 F5 28  5C 8F C2 11 40 CC 33 33",
                    "000010E0  33 33 33 33 16 40 E2 12  CC C3 F5 28 5C 8F C2 F1",
                    "000010F0  3F CC C3 F5 28 5C 8F C2  01 40 E2 12 CC C3 F5 28",
                    "00001100  5C 8F C2 01 40 CC A4 70  3D 0A D7 A3 0A 40 E2 12",
                    "00001110  CC A4 70 3D 0A D7 A3 0A  40 CC C3 F5 28 5C 8F C2",
                    "00001120  11 40 E2 12 CC C3 F5 28  5C 8F C2 11 40 CC 33 33",
                    "00001130  33 33 33 33 16 40 E2 12  CC C3 F5 28 5C 8F C2 F1",
                    "00001140  3F CC C3 F5 28 5C 8F C2  01 40 E2 12 CC C3 F5 28",
                    "00001150  5C 8F C2 01 40 CC A4 70  3D 0A D7 A3 0A 40 E2 12",
                    "00001160  CC A4 70 3D 0A D7 A3 0A  40 CC C3 F5 28 5C 8F C2",
                    "00001170  11 40 E2 12 CC C3 F5 28  5C 8F C2 11 40 CC 33 33",
                    "00001180  33 33 33 33 16 40 E2 12  CC C3 F5 28 5C 8F C2 F1",
                    "00001190  3F CC C3 F5 28 5C 8F C2  01 40 E2 12 CC C3 F5 28",
                    "000011A0  5C 8F C2 01 40 CC A4 70  3D 0A D7 A3 0A 40 E2 12",
                    "000011B0  CC A4 70 3D 0A D7 A3 0A  40 CC C3 F5 28 5C 8F C2",
                    "000011C0  11 40 E2 12 CC C3 F5 28  5C 8F C2 11 40 CC 33 33",
                    "000011D0  33 33 33 33 16 40 E2 12  CC C3 F5 28 5C 8F C2 F1",
                    "000011E0  3F CC C3 F5 28 5C 8F C2  01 40 E2 12 CC C3 F5 28",
                    "000011F0  5C 8F C2 01 40 CC A4 70  3D 0A D7 A3 0A 40 E2 12",
                    "00001200  CC A4 70 3D 0A D7 A3 0A  40 CC C3 F5 28 5C 8F C2",
                    "00001210  11 40 E2 12 CC C3 F5 28  5C 8F C2 11 40 CC 33 33",
                    "00001220  33 33 33 33 16 40 E2 12  CC C3 F5 28 5C 8F C2 F1",
                    "00001230  3F CC C3 F5 28 5C 8F C2  01 40 E2 12 CC C3 F5 28",
                    "00001240  5C 8F C2 01 40 CC A4 70  3D 0A D7 A3 0A 40 E2 12",
                    "00001250  CC A4 70 3D 0A D7 A3 0A  40 CC C3 F5 28 5C 8F C2",
                    "00001260  11 40 E2 12 CC C3 F5 28  5C 8F C2 11 40 CC 33 33",
                    "00001270  33 33 33 33 16 40 E2 12  CC C3 F5 28 5C 8F C2 F1",
                    "00001280  3F CC C3 F5 28 5C 8F C2  01 40 E2 12 CC C3 F5 28",
                    "00001290  5C 8F C2 01 40 CC A4 70  3D 0A D7 A3 0A 40 E2 12",
                    "000012A0  CC A4 70 3D 0A D7 A3 0A  40 CC C3 F5 28 5C 8F C2",
                    "000012B0  11 40 E2 12 CC C3 F5 28  5C 8F C2 11 40 CC 33 33",
                    "000012C0  33 33 33 33 16 40 E2 12  CC C3 F5 28 5C 8F C2 F1",
                    "000012D0  3F CC C3 F5 28 5C 8F C2  01 40 E2 12 CC C3 F5 28",
                    "000012E0  5C 8F C2 01 40 CC A4 70  3D 0A D7 A3 0A 40 E2 12",
                    "000012F0  CC A4 70 3D 0A D7 A3 0A  40 CC C3 F5 28 5C 8F C2",
                    "00001300  11 40 E2 12 CC C3 F5 28  5C 8F C2 11 40 CC 33 33",
                    "00001310  33 33 33 33 16 40 E2 12  CC C3 F5 28 5C 8F C2 F1",
                    "00001320  3F CC C3 F5 28 5C 8F C2  01 40 E2 12 CC C3 F5 28",
                    "00001330  5C 8F C2 01 40 CC A4 70  3D 0A D7 A3 0A 40 E2 12",
                    "00001340  CC A4 70 3D 0A D7 A3 0A  40 CC C3 F5 28 5C 8F C2",
                    "00001350  11 40 E2 12 CC C3 F5 28  5C 8F C2 11 40 CC 33 33",
                    "00001360  33 33 33 33 16 40 E2 12  CC C3 F5 28 5C 8F C2 F1",
                    "00001370  3F CC C3 F5 28 5C 8F C2  01 40 E2 12 CC C3 F5 28",
                    "00001380  5C 8F C2 01 40 CC A4 70  3D 0A D7 A3 0A 40 E2 12",
                    "00001390  CC A4 70 3D 0A D7 A3 0A  40 CC C3 F5 28 5C 8F C2",
                    "000013A0  11 40 E2 12 CC C3 F5 28  5C 8F C2 11 40 CC 33 33",
                    "000013B0  33 33 33 33 16 40 E2 12  CC C3 F5 28 5C 8F C2 F1",
                    "000013C0  3F CC C3 F5 28 5C 8F C2  01 40 E2 12 CC C3 F5 28",
                    "000013D0  5C 8F C2 01 40 CC A4 70  3D 0A D7 A3 0A 40 E2 12",
                    "000013E0  CC A4 70 3D 0A D7 A3 0A  40 CC C3 F5 28 5C 8F C2",
                    "000013F0  11 40 E2 12 CC C3 F5 28  5C 8F C2 11 40 CC 33 33",
                    "00001400  33 33 33 33 16 40 E2 12  CC C3 F5 28 5C 8F C2 F1",
                    "00001410  3F CC C3 F5 28 5C 8F C2  01 40 E2 12 CC C3 F5 28",
                    "00001420  5C 8F C2 01 40 CC A4 70  3D 0A D7 A3 0A 40 E2 12",
                    "00001430  CC A4 70 3D 0A D7 A3 0A  40 CC C3 F5 28 5C 8F C2",
                    "00001440  11 40 E2 12 CC C3 F5 28  5C 8F C2 11 40 CC 33 33",
                    "00001450  33 33 33 33 16 40"
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 F3 F1 CE 02 00 04 01  C3 F5 28 5C 8F C2 F1 3F",
                    "00000010  C3 F5 28 5C 8F C2 01 40  C3 F5 28 5C 8F C2 01 40",
                    "00000020  A4 70 3D 0A D7 A3 0A 40  A4 70 3D 0A D7 A3 0A 40",
                    "00000030  C3 F5 28 5C 8F C2 11 40  C3 F5 28 5C 8F C2 11 40",
                    "00000040  33 33 33 33 33 33 16 40  C3 F5 28 5C 8F C2 F1 3F",
                    "00000050  C3 F5 28 5C 8F C2 01 40  C3 F5 28 5C 8F C2 01 40",
                    "00000060  A4 70 3D 0A D7 A3 0A 40  A4 70 3D 0A D7 A3 0A 40",
                    "00000070  C3 F5 28 5C 8F C2 11 40  C3 F5 28 5C 8F C2 11 40",
                    "00000080  33 33 33 33 33 33 16 40  C3 F5 28 5C 8F C2 F1 3F",
                    "00000090  C3 F5 28 5C 8F C2 01 40  C3 F5 28 5C 8F C2 01 40",
                    "000000A0  A4 70 3D 0A D7 A3 0A 40  A4 70 3D 0A D7 A3 0A 40",
                    "000000B0  C3 F5 28 5C 8F C2 11 40  C3 F5 28 5C 8F C2 11 40",
                    "000000C0  33 33 33 33 33 33 16 40  C3 F5 28 5C 8F C2 F1 3F",
                    "000000D0  C3 F5 28 5C 8F C2 01 40  C3 F5 28 5C 8F C2 01 40",
                    "000000E0  A4 70 3D 0A D7 A3 0A 40  A4 70 3D 0A D7 A3 0A 40",
                    "000000F0  C3 F5 28 5C 8F C2 11 40  C3 F5 28 5C 8F C2 11 40",
                    "00000100  33 33 33 33 33 33 16 40  C3 F5 28 5C 8F C2 F1 3F",
                    "00000110  C3 F5 28 5C 8F C2 01 40  C3 F5 28 5C 8F C2 01 40",
                    "00000120  A4 70 3D 0A D7 A3 0A 40  A4 70 3D 0A D7 A3 0A 40",
                    "00000130  C3 F5 28 5C 8F C2 11 40  C3 F5 28 5C 8F C2 11 40",
                    "00000140  33 33 33 33 33 33 16 40  C3 F5 28 5C 8F C2 F1 3F",
                    "00000150  C3 F5 28 5C 8F C2 01 40  C3 F5 28 5C 8F C2 01 40",
                    "00000160  A4 70 3D 0A D7 A3 0A 40  A4 70 3D 0A D7 A3 0A 40",
                    "00000170  C3 F5 28 5C 8F C2 11 40  C3 F5 28 5C 8F C2 11 40",
                    "00000180  33 33 33 33 33 33 16 40  C3 F5 28 5C 8F C2 F1 3F",
                    "00000190  C3 F5 28 5C 8F C2 01 40  C3 F5 28 5C 8F C2 01 40",
                    "000001A0  A4 70 3D 0A D7 A3 0A 40  A4 70 3D 0A D7 A3 0A 40",
                    "000001B0  C3 F5 28 5C 8F C2 11 40  C3 F5 28 5C 8F C2 11 40",
                    "000001C0  33 33 33 33 33 33 16 40  C3 F5 28 5C 8F C2 F1 3F",
                    "000001D0  C3 F5 28 5C 8F C2 01 40  C3 F5 28 5C 8F C2 01 40",
                    "000001E0  A4 70 3D 0A D7 A3 0A 40  A4 70 3D 0A D7 A3 0A 40",
                    "000001F0  C3 F5 28 5C 8F C2 11 40  C3 F5 28 5C 8F C2 11 40",
                    "00000200  33 33 33 33 33 33 16 40  C3 F5 28 5C 8F C2 F1 3F",
                    "00000210  C3 F5 28 5C 8F C2 01 40  C3 F5 28 5C 8F C2 01 40",
                    "00000220  A4 70 3D 0A D7 A3 0A 40  A4 70 3D 0A D7 A3 0A 40",
                    "00000230  C3 F5 28 5C 8F C2 11 40  C3 F5 28 5C 8F C2 11 40",
                    "00000240  33 33 33 33 33 33 16 40  C3 F5 28 5C 8F C2 F1 3F",
                    "00000250  C3 F5 28 5C 8F C2 01 40  C3 F5 28 5C 8F C2 01 40",
                    "00000260  A4 70 3D 0A D7 A3 0A 40  A4 70 3D 0A D7 A3 0A 40",
                    "00000270  C3 F5 28 5C 8F C2 11 40  C3 F5 28 5C 8F C2 11 40",
                    "00000280  33 33 33 33 33 33 16 40  C3 F5 28 5C 8F C2 F1 3F",
                    "00000290  C3 F5 28 5C 8F C2 01 40  C3 F5 28 5C 8F C2 01 40",
                    "000002A0  A4 70 3D 0A D7 A3 0A 40  A4 70 3D 0A D7 A3 0A 40",
                    "000002B0  C3 F5 28 5C 8F C2 11 40  C3 F5 28 5C 8F C2 11 40",
                    "000002C0  33 33 33 33 33 33 16 40  C3 F5 28 5C 8F C2 F1 3F",
                    "000002D0  C3 F5 28 5C 8F C2 01 40  C3 F5 28 5C 8F C2 01 40",
                    "000002E0  A4 70 3D 0A D7 A3 0A 40  A4 70 3D 0A D7 A3 0A 40",
                    "000002F0  C3 F5 28 5C 8F C2 11 40  C3 F5 28 5C 8F C2 11 40",
                    "00000300  33 33 33 33 33 33 16 40  C3 F5 28 5C 8F C2 F1 3F",
                    "00000310  C3 F5 28 5C 8F C2 01 40  C3 F5 28 5C 8F C2 01 40",
                    "00000320  A4 70 3D 0A D7 A3 0A 40  A4 70 3D 0A D7 A3 0A 40",
                    "00000330  C3 F5 28 5C 8F C2 11 40  C3 F5 28 5C 8F C2 11 40",
                    "00000340  33 33 33 33 33 33 16 40  C3 F5 28 5C 8F C2 F1 3F",
                    "00000350  C3 F5 28 5C 8F C2 01 40  C3 F5 28 5C 8F C2 01 40",
                    "00000360  A4 70 3D 0A D7 A3 0A 40  A4 70 3D 0A D7 A3 0A 40",
                    "00000370  C3 F5 28 5C 8F C2 11 40  C3 F5 28 5C 8F C2 11 40",
                    "00000380  33 33 33 33 33 33 16 40  C3 F5 28 5C 8F C2 F1 3F",
                    "00000390  C3 F5 28 5C 8F C2 01 40  C3 F5 28 5C 8F C2 01 40",
                    "000003A0  A4 70 3D 0A D7 A3 0A 40  A4 70 3D 0A D7 A3 0A 40",
                    "000003B0  C3 F5 28 5C 8F C2 11 40  C3 F5 28 5C 8F C2 11 40",
                    "000003C0  33 33 33 33 33 33 16 40  C3 F5 28 5C 8F C2 F1 3F",
                    "000003D0  C3 F5 28 5C 8F C2 01 40  C3 F5 28 5C 8F C2 01 40",
                    "000003E0  A4 70 3D 0A D7 A3 0A 40  A4 70 3D 0A D7 A3 0A 40",
                    "000003F0  C3 F5 28 5C 8F C2 11 40  C3 F5 28 5C 8F C2 11 40",
                    "00000400  33 33 33 33 33 33 16 40  C3 F5 28 5C 8F C2 F1 3F",
                    "00000410  C3 F5 28 5C 8F C2 01 40  C3 F5 28 5C 8F C2 01 40",
                    "00000420  A4 70 3D 0A D7 A3 0A 40  A4 70 3D 0A D7 A3 0A 40",
                    "00000430  C3 F5 28 5C 8F C2 11 40  C3 F5 28 5C 8F C2 11 40",
                    "00000440  33 33 33 33 33 33 16 40  C3 F5 28 5C 8F C2 F1 3F",
                    "00000450  C3 F5 28 5C 8F C2 01 40  C3 F5 28 5C 8F C2 01 40",
                    "00000460  A4 70 3D 0A D7 A3 0A 40  A4 70 3D 0A D7 A3 0A 40",
                    "00000470  C3 F5 28 5C 8F C2 11 40  C3 F5 28 5C 8F C2 11 40",
                    "00000480  33 33 33 33 33 33 16 40  C3 F5 28 5C 8F C2 F1 3F",
                    "00000490  C3 F5 28 5C 8F C2 01 40  C3 F5 28 5C 8F C2 01 40",
                    "000004A0  A4 70 3D 0A D7 A3 0A 40  A4 70 3D 0A D7 A3 0A 40",
                    "000004B0  C3 F5 28 5C 8F C2 11 40  C3 F5 28 5C 8F C2 11 40",
                    "000004C0  33 33 33 33 33 33 16 40  C3 F5 28 5C 8F C2 F1 3F",
                    "000004D0  C3 F5 28 5C 8F C2 01 40  C3 F5 28 5C 8F C2 01 40",
                    "000004E0  A4 70 3D 0A D7 A3 0A 40  A4 70 3D 0A D7 A3 0A 40",
                    "000004F0  C3 F5 28 5C 8F C2 11 40  C3 F5 28 5C 8F C2 11 40",
                    "00000500  33 33 33 33 33 33 16 40  C3 F5 28 5C 8F C2 F1 3F",
                    "00000510  C3 F5 28 5C 8F C2 01 40  C3 F5 28 5C 8F C2 01 40",
                    "00000520  A4 70 3D 0A D7 A3 0A 40  A4 70 3D 0A D7 A3 0A 40",
                    "00000530  C3 F5 28 5C 8F C2 11 40  C3 F5 28 5C 8F C2 11 40",
                    "00000540  33 33 33 33 33 33 16 40  C3 F5 28 5C 8F C2 F1 3F",
                    "00000550  C3 F5 28 5C 8F C2 01 40  C3 F5 28 5C 8F C2 01 40",
                    "00000560  A4 70 3D 0A D7 A3 0A 40  A4 70 3D 0A D7 A3 0A 40",
                    "00000570  C3 F5 28 5C 8F C2 11 40  C3 F5 28 5C 8F C2 11 40",
                    "00000580  33 33 33 33 33 33 16 40  C3 F5 28 5C 8F C2 F1 3F",
                    "00000590  C3 F5 28 5C 8F C2 01 40  C3 F5 28 5C 8F C2 01 40",
                    "000005A0  A4 70 3D 0A D7 A3 0A 40  A4 70 3D 0A D7 A3 0A 40",
                    "000005B0  C3 F5 28 5C 8F C2 11 40  C3 F5 28 5C 8F C2 11 40",
                    "000005C0  33 33 33 33 33 33 16 40  C3 F5 28 5C 8F C2 F1 3F",
                    "000005D0  C3 F5 28 5C 8F C2 01 40  C3 F5 28 5C 8F C2 01 40",
                    "000005E0  A4 70 3D 0A D7 A3 0A 40  A4 70 3D 0A D7 A3 0A 40",
                    "000005F0  C3 F5 28 5C 8F C2 11 40  C3 F5 28 5C 8F C2 11 40",
                    "00000600  33 33 33 33 33 33 16 40  C3 F5 28 5C 8F C2 F1 3F",
                    "00000610  C3 F5 28 5C 8F C2 01 40  C3 F5 28 5C 8F C2 01 40",
                    "00000620  A4 70 3D 0A D7 A3 0A 40  A4 70 3D 0A D7 A3 0A 40",
                    "00000630  C3 F5 28 5C 8F C2 11 40  C3 F5 28 5C 8F C2 11 40",
                    "00000640  33 33 33 33 33 33 16 40  C3 F5 28 5C 8F C2 F1 3F",
                    "00000650  C3 F5 28 5C 8F C2 01 40  C3 F5 28 5C 8F C2 01 40",
                    "00000660  A4 70 3D 0A D7 A3 0A 40  A4 70 3D 0A D7 A3 0A 40",
                    "00000670  C3 F5 28 5C 8F C2 11 40  C3 F5 28 5C 8F C2 11 40",
                    "00000680  33 33 33 33 33 33 16 40  C3 F5 28 5C 8F C2 F1 3F",
                    "00000690  C3 F5 28 5C 8F C2 01 40  C3 F5 28 5C 8F C2 01 40",
                    "000006A0  A4 70 3D 0A D7 A3 0A 40  A4 70 3D 0A D7 A3 0A 40",
                    "000006B0  C3 F5 28 5C 8F C2 11 40  C3 F5 28 5C 8F C2 11 40",
                    "000006C0  33 33 33 33 33 33 16 40  C3 F5 28 5C 8F C2 F1 3F",
                    "000006D0  C3 F5 28 5C 8F C2 01 40  C3 F5 28 5C 8F C2 01 40",
                    "000006E0  A4 70 3D 0A D7 A3 0A 40  A4 70 3D 0A D7 A3 0A 40",
                    "000006F0  C3 F5 28 5C 8F C2 11 40  C3 F5 28 5C 8F C2 11 40",
                    "00000700  33 33 33 33 33 33 16 40  C3 F5 28 5C 8F C2 F1 3F",
                    "00000710  C3 F5 28 5C 8F C2 01 40  C3 F5 28 5C 8F C2 01 40",
                    "00000720  A4 70 3D 0A D7 A3 0A 40  A4 70 3D 0A D7 A3 0A 40",
                    "00000730  C3 F5 28 5C 8F C2 11 40  C3 F5 28 5C 8F C2 11 40",
                    "00000740  33 33 33 33 33 33 16 40  C3 F5 28 5C 8F C2 F1 3F",
                    "00000750  C3 F5 28 5C 8F C2 01 40  C3 F5 28 5C 8F C2 01 40",
                    "00000760  A4 70 3D 0A D7 A3 0A 40  A4 70 3D 0A D7 A3 0A 40",
                    "00000770  C3 F5 28 5C 8F C2 11 40  C3 F5 28 5C 8F C2 11 40",
                    "00000780  33 33 33 33 33 33 16 40  C3 F5 28 5C 8F C2 F1 3F",
                    "00000790  C3 F5 28 5C 8F C2 01 40  C3 F5 28 5C 8F C2 01 40",
                    "000007A0  A4 70 3D 0A D7 A3 0A 40  A4 70 3D 0A D7 A3 0A 40",
                    "000007B0  C3 F5 28 5C 8F C2 11 40  C3 F5 28 5C 8F C2 11 40",
                    "000007C0  33 33 33 33 33 33 16 40  C3 F5 28 5C 8F C2 F1 3F",
                    "000007D0  C3 F5 28 5C 8F C2 01 40  C3 F5 28 5C 8F C2 01 40",
                    "000007E0  A4 70 3D 0A D7 A3 0A 40  A4 70 3D 0A D7 A3 0A 40",
                    "000007F0  C3 F5 28 5C 8F C2 11 40  C3 F5 28 5C 8F C2 11 40",
                    "00000800  33 33 33 33 33 33 16 40  C3 F5 28 5C 8F C2 F1 3F",
                    "00000810  C3 F5 28 5C 8F C2 01 40  C3 F5 28 5C 8F C2 01 40",
                    "00000820  A4 70 3D 0A D7 A3 0A 40  A4 70 3D 0A D7 A3 0A 40",
                    "00000830  C3 F5 28 5C 8F C2 11 40  C3 F5 28 5C 8F C2 11 40",
                    "00000840  33 33 33 33 33 33 16 40  C3 F5 28 5C 8F C2 F1 3F",
                    "00000850  C3 F5 28 5C 8F C2 01 40  C3 F5 28 5C 8F C2 01 40",
                    "00000860  A4 70 3D 0A D7 A3 0A 40  A4 70 3D 0A D7 A3 0A 40",
                    "00000870  C3 F5 28 5C 8F C2 11 40  C3 F5 28 5C 8F C2 11 40",
                    "00000880  33 33 33 33 33 33 16 40  C3 F5 28 5C 8F C2 F1 3F",
                    "00000890  C3 F5 28 5C 8F C2 01 40  C3 F5 28 5C 8F C2 01 40",
                    "000008A0  A4 70 3D 0A D7 A3 0A 40  A4 70 3D 0A D7 A3 0A 40",
                    "000008B0  C3 F5 28 5C 8F C2 11 40  C3 F5 28 5C 8F C2 11 40",
                    "000008C0  33 33 33 33 33 33 16 40  C3 F5 28 5C 8F C2 F1 3F",
                    "000008D0  C3 F5 28 5C 8F C2 01 40  C3 F5 28 5C 8F C2 01 40",
                    "000008E0  A4 70 3D 0A D7 A3 0A 40  A4 70 3D 0A D7 A3 0A 40",
                    "000008F0  C3 F5 28 5C 8F C2 11 40  C3 F5 28 5C 8F C2 11 40",
                    "00000900  33 33 33 33 33 33 16 40  C3 F5 28 5C 8F C2 F1 3F",
                    "00000910  C3 F5 28 5C 8F C2 01 40  C3 F5 28 5C 8F C2 01 40",
                    "00000920  A4 70 3D 0A D7 A3 0A 40  A4 70 3D 0A D7 A3 0A 40",
                    "00000930  C3 F5 28 5C 8F C2 11 40  C3 F5 28 5C 8F C2 11 40",
                    "00000940  33 33 33 33 33 33 16 40  C3 F5 28 5C 8F C2 F1 3F",
                    "00000950  C3 F5 28 5C 8F C2 01 40  C3 F5 28 5C 8F C2 01 40",
                    "00000960  A4 70 3D 0A D7 A3 0A 40  A4 70 3D 0A D7 A3 0A 40",
                    "00000970  C3 F5 28 5C 8F C2 11 40  C3 F5 28 5C 8F C2 11 40",
                    "00000980  33 33 33 33 33 33 16 40  C3 F5 28 5C 8F C2 F1 3F",
                    "00000990  C3 F5 28 5C 8F C2 01 40  C3 F5 28 5C 8F C2 01 40",
                    "000009A0  A4 70 3D 0A D7 A3 0A 40  A4 70 3D 0A D7 A3 0A 40",
                    "000009B0  C3 F5 28 5C 8F C2 11 40  C3 F5 28 5C 8F C2 11 40",
                    "000009C0  33 33 33 33 33 33 16 40  C3 F5 28 5C 8F C2 F1 3F",
                    "000009D0  C3 F5 28 5C 8F C2 01 40  C3 F5 28 5C 8F C2 01 40",
                    "000009E0  A4 70 3D 0A D7 A3 0A 40  A4 70 3D 0A D7 A3 0A 40",
                    "000009F0  C3 F5 28 5C 8F C2 11 40  C3 F5 28 5C 8F C2 11 40",
                    "00000A00  33 33 33 33 33 33 16 40  C3 F5 28 5C 8F C2 F1 3F",
                    "00000A10  C3 F5 28 5C 8F C2 01 40  C3 F5 28 5C 8F C2 01 40",
                    "00000A20  A4 70 3D 0A D7 A3 0A 40  A4 70 3D 0A D7 A3 0A 40",
                    "00000A30  C3 F5 28 5C 8F C2 11 40  C3 F5 28 5C 8F C2 11 40",
                    "00000A40  33 33 33 33 33 33 16 40  C3 F5 28 5C 8F C2 F1 3F",
                    "00000A50  C3 F5 28 5C 8F C2 01 40  C3 F5 28 5C 8F C2 01 40",
                    "00000A60  A4 70 3D 0A D7 A3 0A 40  A4 70 3D 0A D7 A3 0A 40",
                    "00000A70  C3 F5 28 5C 8F C2 11 40  C3 F5 28 5C 8F C2 11 40",
                    "00000A80  33 33 33 33 33 33 16 40  C3 F5 28 5C 8F C2 F1 3F",
                    "00000A90  C3 F5 28 5C 8F C2 01 40  C3 F5 28 5C 8F C2 01 40",
                    "00000AA0  A4 70 3D 0A D7 A3 0A 40  A4 70 3D 0A D7 A3 0A 40",
                    "00000AB0  C3 F5 28 5C 8F C2 11 40  C3 F5 28 5C 8F C2 11 40",
                    "00000AC0  33 33 33 33 33 33 16 40  C3 F5 28 5C 8F C2 F1 3F",
                    "00000AD0  C3 F5 28 5C 8F C2 01 40  C3 F5 28 5C 8F C2 01 40",
                    "00000AE0  A4 70 3D 0A D7 A3 0A 40  A4 70 3D 0A D7 A3 0A 40",
                    "00000AF0  C3 F5 28 5C 8F C2 11 40  C3 F5 28 5C 8F C2 11 40",
                    "00000B00  33 33 33 33 33 33 16 40  C3 F5 28 5C 8F C2 F1 3F",
                    "00000B10  C3 F5 28 5C 8F C2 01 40  C3 F5 28 5C 8F C2 01 40",
                    "00000B20  A4 70 3D 0A D7 A3 0A 40  A4 70 3D 0A D7 A3 0A 40",
                    "00000B30  C3 F5 28 5C 8F C2 11 40  C3 F5 28 5C 8F C2 11 40",
                    "00000B40  33 33 33 33 33 33 16 40  C3 F5 28 5C 8F C2 F1 3F",
                    "00000B50  C3 F5 28 5C 8F C2 01 40  C3 F5 28 5C 8F C2 01 40",
                    "00000B60  A4 70 3D 0A D7 A3 0A 40  A4 70 3D 0A D7 A3 0A 40",
                    "00000B70  C3 F5 28 5C 8F C2 11 40  C3 F5 28 5C 8F C2 11 40",
                    "00000B80  33 33 33 33 33 33 16 40  C3 F5 28 5C 8F C2 F1 3F",
                    "00000B90  C3 F5 28 5C 8F C2 01 40  C3 F5 28 5C 8F C2 01 40",
                    "00000BA0  A4 70 3D 0A D7 A3 0A 40  A4 70 3D 0A D7 A3 0A 40",
                    "00000BB0  C3 F5 28 5C 8F C2 11 40  C3 F5 28 5C 8F C2 11 40",
                    "00000BC0  33 33 33 33 33 33 16 40  C3 F5 28 5C 8F C2 F1 3F",
                    "00000BD0  C3 F5 28 5C 8F C2 01 40  C3 F5 28 5C 8F C2 01 40",
                    "00000BE0  A4 70 3D 0A D7 A3 0A 40  A4 70 3D 0A D7 A3 0A 40",
                    "00000BF0  C3 F5 28 5C 8F C2 11 40  C3 F5 28 5C 8F C2 11 40",
                    "00000C00  33 33 33 33 33 33 16 40  C3 F5 28 5C 8F C2 F1 3F",
                    "00000C10  C3 F5 28 5C 8F C2 01 40  C3 F5 28 5C 8F C2 01 40",
                    "00000C20  A4 70 3D 0A D7 A3 0A 40  A4 70 3D 0A D7 A3 0A 40",
                    "00000C30  C3 F5 28 5C 8F C2 11 40  C3 F5 28 5C 8F C2 11 40",
                    "00000C40  33 33 33 33 33 33 16 40  C3 F5 28 5C 8F C2 F1 3F",
                    "00000C50  C3 F5 28 5C 8F C2 01 40  C3 F5 28 5C 8F C2 01 40",
                    "00000C60  A4 70 3D 0A D7 A3 0A 40  A4 70 3D 0A D7 A3 0A 40",
                    "00000C70  C3 F5 28 5C 8F C2 11 40  C3 F5 28 5C 8F C2 11 40",
                    "00000C80  33 33 33 33 33 33 16 40  C3 F5 28 5C 8F C2 F1 3F",
                    "00000C90  C3 F5 28 5C 8F C2 01 40  C3 F5 28 5C 8F C2 01 40",
                    "00000CA0  A4 70 3D 0A D7 A3 0A 40  A4 70 3D 0A D7 A3 0A 40",
                    "00000CB0  C3 F5 28 5C 8F C2 11 40  C3 F5 28 5C 8F C2 11 40",
                    "00000CC0  33 33 33 33 33 33 16 40  C3 F5 28 5C 8F C2 F1 3F",
                    "00000CD0  C3 F5 28 5C 8F C2 01 40  C3 F5 28 5C 8F C2 01 40",
                    "00000CE0  A4 70 3D 0A D7 A3 0A 40  A4 70 3D 0A D7 A3 0A 40",
                    "00000CF0  C3 F5 28 5C 8F C2 11 40  C3 F5 28 5C 8F C2 11 40",
                    "00000D00  33 33 33 33 33 33 16 40  C3 F5 28 5C 8F C2 F1 3F",
                    "00000D10  C3 F5 28 5C 8F C2 01 40  C3 F5 28 5C 8F C2 01 40",
                    "00000D20  A4 70 3D 0A D7 A3 0A 40  A4 70 3D 0A D7 A3 0A 40",
                    "00000D30  C3 F5 28 5C 8F C2 11 40  C3 F5 28 5C 8F C2 11 40",
                    "00000D40  33 33 33 33 33 33 16 40  C3 F5 28 5C 8F C2 F1 3F",
                    "00000D50  C3 F5 28 5C 8F C2 01 40  C3 F5 28 5C 8F C2 01 40",
                    "00000D60  A4 70 3D 0A D7 A3 0A 40  A4 70 3D 0A D7 A3 0A 40",
                    "00000D70  C3 F5 28 5C 8F C2 11 40  C3 F5 28 5C 8F C2 11 40",
                    "00000D80  33 33 33 33 33 33 16 40  C3 F5 28 5C 8F C2 F1 3F",
                    "00000D90  C3 F5 28 5C 8F C2 01 40  C3 F5 28 5C 8F C2 01 40",
                    "00000DA0  A4 70 3D 0A D7 A3 0A 40  A4 70 3D 0A D7 A3 0A 40",
                    "00000DB0  C3 F5 28 5C 8F C2 11 40  C3 F5 28 5C 8F C2 11 40",
                    "00000DC0  33 33 33 33 33 33 16 40  C3 F5 28 5C 8F C2 F1 3F",
                    "00000DD0  C3 F5 28 5C 8F C2 01 40  C3 F5 28 5C 8F C2 01 40",
                    "00000DE0  A4 70 3D 0A D7 A3 0A 40  A4 70 3D 0A D7 A3 0A 40",
                    "00000DF0  C3 F5 28 5C 8F C2 11 40  C3 F5 28 5C 8F C2 11 40",
                    "00000E00  33 33 33 33 33 33 16 40  C3 F5 28 5C 8F C2 F1 3F",
                    "00000E10  C3 F5 28 5C 8F C2 01 40  C3 F5 28 5C 8F C2 01 40",
                    "00000E20  A4 70 3D 0A D7 A3 0A 40  A4 70 3D 0A D7 A3 0A 40",
                    "00000E30  C3 F5 28 5C 8F C2 11 40  C3 F5 28 5C 8F C2 11 40",
                    "00000E40  33 33 33 33 33 33 16 40  C3 F5 28 5C 8F C2 F1 3F",
                    "00000E50  C3 F5 28 5C 8F C2 01 40  C3 F5 28 5C 8F C2 01 40",
                    "00000E60  A4 70 3D 0A D7 A3 0A 40  A4 70 3D 0A D7 A3 0A 40",
                    "00000E70  C3 F5 28 5C 8F C2 11 40  C3 F5 28 5C 8F C2 11 40",
                    "00000E80  33 33 33 33 33 33 16 40  C3 F5 28 5C 8F C2 F1 3F",
                    "00000E90  C3 F5 28 5C 8F C2 01 40  C3 F5 28 5C 8F C2 01 40",
                    "00000EA0  A4 70 3D 0A D7 A3 0A 40  A4 70 3D 0A D7 A3 0A 40",
                    "00000EB0  C3 F5 28 5C 8F C2 11 40  C3 F5 28 5C 8F C2 11 40",
                    "00000EC0  33 33 33 33 33 33 16 40  C3 F5 28 5C 8F C2 F1 3F",
                    "00000ED0  C3 F5 28 5C 8F C2 01 40  C3 F5 28 5C 8F C2 01 40",
                    "00000EE0  A4 70 3D 0A D7 A3 0A 40  A4 70 3D 0A D7 A3 0A 40",
                    "00000EF0  C3 F5 28 5C 8F C2 11 40  C3 F5 28 5C 8F C2 11 40",
                    "00000F00  33 33 33 33 33 33 16 40  C3 F5 28 5C 8F C2 F1 3F",
                    "00000F10  C3 F5 28 5C 8F C2 01 40  C3 F5 28 5C 8F C2 01 40",
                    "00000F20  A4 70 3D 0A D7 A3 0A 40  A4 70 3D 0A D7 A3 0A 40",
                    "00000F30  C3 F5 28 5C 8F C2 11 40  C3 F5 28 5C 8F C2 11 40",
                    "00000F40  33 33 33 33 33 33 16 40  C3 F5 28 5C 8F C2 F1 3F",
                    "00000F50  C3 F5 28 5C 8F C2 01 40  C3 F5 28 5C 8F C2 01 40",
                    "00000F60  A4 70 3D 0A D7 A3 0A 40  A4 70 3D 0A D7 A3 0A 40",
                    "00000F70  C3 F5 28 5C 8F C2 11 40  C3 F5 28 5C 8F C2 11 40",
                    "00000F80  33 33 33 33 33 33 16 40  C3 F5 28 5C 8F C2 F1 3F",
                    "00000F90  C3 F5 28 5C 8F C2 01 40  C3 F5 28 5C 8F C2 01 40",
                    "00000FA0  A4 70 3D 0A D7 A3 0A 40  A4 70 3D 0A D7 A3 0A 40",
                    "00000FB0  C3 F5 28 5C 8F C2 11 40  C3 F5 28 5C 8F C2 11 40",
                    "00000FC0  33 33 33 33 33 33 16 40  C3 F5 28 5C 8F C2 F1 3F",
                    "00000FD0  C3 F5 28 5C 8F C2 01 40  C3 F5 28 5C 8F C2 01 40",
                    "00000FE0  A4 70 3D 0A D7 A3 0A 40  A4 70 3D 0A D7 A3 0A 40",
                    "00000FF0  C3 F5 28 5C 8F C2 11 40  C3 F5 28 5C 8F C2 11 40",
                    "00001000  33 33 33 33 33 33 16 40  C3 F5 28 5C 8F C2 F1 3F",
                    "00001010  C3 F5 28 5C 8F C2 01 40  C3 F5 28 5C 8F C2 01 40",
                    "00001020  A4 70 3D 0A D7 A3 0A 40  A4 70 3D 0A D7 A3 0A 40",
                    "00001030  C3 F5 28 5C 8F C2 11 40  C3 F5 28 5C 8F C2 11 40",
                    "00001040  33 33 33 33 33 33 16 40"
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }
        }

        [TestMethod]
        [Owner("sboshra")]
        public void UniformArrayOfNumberArraysTest9()
        {
            // -------------------------
            // Same numeric type/Different sizes
            // -------------------------

            // Common set of number arrays for the tests to follow
            sbyte[] i8Values1 = { -100, -50, 50, 100 };
            sbyte[] i8Values2 = { -125, -100, -50, 50, 100, 125 };

            short[] i16Values1 = { -2000, -1000, 1000, 2000 };
            short[] i16Values2 = { -2500, -2000, -1000, 1000, 2000, 2500 };

            int[] i32Values1 = { -200000, -100000, 100000, 200000 };
            int[] i32Values2 = { -250000, -200000, -100000, 100000, 200000, 250000 };

            long[] i64Values1 = { -20000000000, -10000000000, 10000000000, 20000000000 };
            long[] i64Values2 = { -25000000000, -20000000000, -10000000000, 10000000000, 20000000000, 25000000000 };

            double[] f64Values1 = { -2.1, -1.1, 1.1, 2.1 };
            double[] f64Values2 = { -3.1, -2.1, -1.1, 1.1, 2.1, 3.1 };

            // Case 1
            {
                JsonToken[] tokensToWrite =
                {
                    JsonToken.ArrayStart(),
                        JsonToken.Int8NumberArray(i8Values1),
                        JsonToken.Int8NumberArray(i8Values1),
                        JsonToken.Int8NumberArray(i8Values1),
                        JsonToken.Int8NumberArray(i8Values2),
                    JsonToken.ArrayEnd(),
                };

                string[] expectedText =
                {
                    @"[[-100,-50,50,100],[-100,-50,50,100],[-100,-50,50,100],[-125,-100,-50,50,100,125]]"
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E2 35 E2 0A C9 9C FF  C9 CE FF C8 32 C8 64 E2",
                    "00000010  0A C9 9C FF C9 CE FF C8  32 C8 64 E2 0A C9 9C FF",
                    "00000020  C9 CE FF C8 32 C8 64 E2  0F C9 83 FF C9 9C FF C9",
                    "00000030  CE FF C8 32 C8 64 C8 7D"
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 E2 1E F0 D8 04 9C CE  32 64 F0 D8 04 9C CE 32",
                    "00000010  64 F0 D8 04 9C CE 32 64  F0 D8 06 83 9C CE 32 64",
                    "00000020  7D"
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }

            // Case 2
            {
                JsonToken[] tokensToWrite =
                {
                    JsonToken.ArrayStart(),
                        JsonToken.Int16NumberArray(i16Values1),
                        JsonToken.Int16NumberArray(i16Values2),
                        JsonToken.Int16NumberArray(i16Values2),
                        JsonToken.Int16NumberArray(i16Values2),
                    JsonToken.ArrayEnd(),
                };

                string[] expectedText =
                {
                    @"[[-2000,-1000,1000,2000],[-2500,-2000,-1000,1000,2000,2500],[-2500,-2000,-1000,1000,2000,2500],[-250",
                    @"0,-2000,-1000,1000,2000,2500]]",
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E2 4A E2 0C C9 30 F8  C9 18 FC C9 E8 03 C9 D0",
                    "00000010  07 E2 12 C9 3C F6 C9 30  F8 C9 18 FC C9 E8 03 C9",
                    "00000020  D0 07 C9 C4 09 E2 12 C9  3C F6 C9 30 F8 C9 18 FC",
                    "00000030  C9 E8 03 C9 D0 07 C9 C4  09 E2 12 C9 3C F6 C9 30",
                    "00000040  F8 C9 18 FC C9 E8 03 C9  D0 07 C9 C4 09"
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 E2 38 F0 D9 04 30 F8  18 FC E8 03 D0 07 F0 D9",
                    "00000010  06 3C F6 30 F8 18 FC E8  03 D0 07 C4 09 F0 D9 06",
                    "00000020  3C F6 30 F8 18 FC E8 03  D0 07 C4 09 F0 D9 06 3C",
                    "00000030  F6 30 F8 18 FC E8 03 D0  07 C4 09"
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }

            // Case 3
            {
                JsonToken[] tokensToWrite =
                {
                    JsonToken.ArrayStart(),
                        JsonToken.Int32NumberArray(i32Values1),
                        JsonToken.Int32NumberArray(i32Values2),
                        JsonToken.Int32NumberArray(i32Values1),
                        JsonToken.Int32NumberArray(i32Values2),
                    JsonToken.ArrayEnd(),
                };

                string[] expectedText =
                {
                    @"[[-200000,-100000,100000,200000],[-250000,-200000,-100000,100000,200000,250000],[-200000,-100000,100",
                    @"000,200000],[-250000,-200000,-100000,100000,200000,250000]]",
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E2 6C E2 14 CA C0 F2  FC FF CA 60 79 FE FF CA",
                    "00000010  A0 86 01 00 CA 40 0D 03  00 E2 1E CA 70 2F FC FF",
                    "00000020  CA C0 F2 FC FF CA 60 79  FE FF CA A0 86 01 00 CA",
                    "00000030  40 0D 03 00 CA 90 D0 03  00 E2 14 CA C0 F2 FC FF",
                    "00000040  CA 60 79 FE FF CA A0 86  01 00 CA 40 0D 03 00 E2",
                    "00000050  1E CA 70 2F FC FF CA C0  F2 FC FF CA 60 79 FE FF",
                    "00000060  CA A0 86 01 00 CA 40 0D  03 00 CA 90 D0 03 00"
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 E2 5C F0 DA 04 C0 F2  FC FF 60 79 FE FF A0 86",
                    "00000010  01 00 40 0D 03 00 F0 DA  06 70 2F FC FF C0 F2 FC",
                    "00000020  FF 60 79 FE FF A0 86 01  00 40 0D 03 00 90 D0 03",
                    "00000030  00 F0 DA 04 C0 F2 FC FF  60 79 FE FF A0 86 01 00",
                    "00000040  40 0D 03 00 F0 DA 06 70  2F FC FF C0 F2 FC FF 60",
                    "00000050  79 FE FF A0 86 01 00 40  0D 03 00 90 D0 03 00"
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }

            // Case 4
            {
                JsonToken[] tokensToWrite =
                {
                    JsonToken.ArrayStart(),
                        JsonToken.Int64NumberArray(i64Values1),
                        JsonToken.Int64NumberArray(i64Values2),
                        JsonToken.Int64NumberArray(i64Values2),
                        JsonToken.Int64NumberArray(i64Values2),
                    JsonToken.ArrayEnd(),
                };

                string[] expectedText =
                {
                    @"[[-20000000000,-10000000000,10000000000,20000000000],[-25000000000,-20000000000,-10000000000,1000000",
                    @"0000,20000000000,25000000000],[-25000000000,-20000000000,-10000000000,10000000000,20000000000,250000",
                    @"00000],[-25000000000,-20000000000,-10000000000,10000000000,20000000000,25000000000]]",
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E2 CE E2 24 CB 00 38  E8 57 FB FF FF FF CB 00",
                    "00000010  1C F4 AB FD FF FF FF CB  00 E4 0B 54 02 00 00 00",
                    "00000020  CB 00 C8 17 A8 04 00 00  00 E2 36 CB 00 46 E2 2D",
                    "00000030  FA FF FF FF CB 00 38 E8  57 FB FF FF FF CB 00 1C",
                    "00000040  F4 AB FD FF FF FF CB 00  E4 0B 54 02 00 00 00 CB",
                    "00000050  00 C8 17 A8 04 00 00 00  CB 00 BA 1D D2 05 00 00",
                    "00000060  00 E2 36 CB 00 46 E2 2D  FA FF FF FF CB 00 38 E8",
                    "00000070  57 FB FF FF FF CB 00 1C  F4 AB FD FF FF FF CB 00",
                    "00000080  E4 0B 54 02 00 00 00 CB  00 C8 17 A8 04 00 00 00",
                    "00000090  CB 00 BA 1D D2 05 00 00  00 E2 36 CB 00 46 E2 2D",
                    "000000A0  FA FF FF FF CB 00 38 E8  57 FB FF FF FF CB 00 1C",
                    "000000B0  F4 AB FD FF FF FF CB 00  E4 0B 54 02 00 00 00 CB",
                    "000000C0  00 C8 17 A8 04 00 00 00  CB 00 BA 1D D2 05 00 00",
                    "000000D0  00"
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 E2 BC F0 DB 04 00 38  E8 57 FB FF FF FF 00 1C",
                    "00000010  F4 AB FD FF FF FF 00 E4  0B 54 02 00 00 00 00 C8",
                    "00000020  17 A8 04 00 00 00 F0 DB  06 00 46 E2 2D FA FF FF",
                    "00000030  FF 00 38 E8 57 FB FF FF  FF 00 1C F4 AB FD FF FF",
                    "00000040  FF 00 E4 0B 54 02 00 00  00 00 C8 17 A8 04 00 00",
                    "00000050  00 00 BA 1D D2 05 00 00  00 F0 DB 06 00 46 E2 2D",
                    "00000060  FA FF FF FF 00 38 E8 57  FB FF FF FF 00 1C F4 AB",
                    "00000070  FD FF FF FF 00 E4 0B 54  02 00 00 00 00 C8 17 A8",
                    "00000080  04 00 00 00 00 BA 1D D2  05 00 00 00 F0 DB 06 00",
                    "00000090  46 E2 2D FA FF FF FF 00  38 E8 57 FB FF FF FF 00",
                    "000000A0  1C F4 AB FD FF FF FF 00  E4 0B 54 02 00 00 00 00",
                    "000000B0  C8 17 A8 04 00 00 00 00  BA 1D D2 05 00 00 00"
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }

            // Case 5
            {
                JsonToken[] tokensToWrite =
                {
                    JsonToken.ArrayStart(),
                        JsonToken.Float64NumberArray(f64Values1),
                        JsonToken.Float64NumberArray(f64Values1),
                        JsonToken.Float64NumberArray(f64Values2),
                        JsonToken.Float64NumberArray(f64Values1),
                    JsonToken.ArrayEnd(),
                };

                string[] expectedText =
                {
                    @"[[-2.1,-1.1,1.1,2.1],[-2.1,-1.1,1.1,2.1],[-3.1,-2.1,-1.1,1.1,2.1,3.1],[-2.1,-1.1,1.1,2.1]]",
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E2 AA E2 24 CC CD CC  CC CC CC CC 00 C0 CC 9A",
                    "00000010  99 99 99 99 99 F1 BF CC  9A 99 99 99 99 99 F1 3F",
                    "00000020  CC CD CC CC CC CC CC 00  40 E2 24 CC CD CC CC CC",
                    "00000030  CC CC 00 C0 CC 9A 99 99  99 99 99 F1 BF CC 9A 99",
                    "00000040  99 99 99 99 F1 3F CC CD  CC CC CC CC CC 00 40 E2",
                    "00000050  36 CC CD CC CC CC CC CC  08 C0 CC CD CC CC CC CC",
                    "00000060  CC 00 C0 CC 9A 99 99 99  99 99 F1 BF CC 9A 99 99",
                    "00000070  99 99 99 F1 3F CC CD CC  CC CC CC CC 00 40 CC CD",
                    "00000080  CC CC CC CC CC 08 40 E2  24 CC CD CC CC CC CC CC",
                    "00000090  00 C0 CC 9A 99 99 99 99  99 F1 BF CC 9A 99 99 99",
                    "000000A0  99 99 F1 3F CC CD CC CC  CC CC CC 00 40"
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 E2 9C F0 CE 04 CD CC  CC CC CC CC 00 C0 9A 99",
                    "00000010  99 99 99 99 F1 BF 9A 99  99 99 99 99 F1 3F CD CC",
                    "00000020  CC CC CC CC 00 40 F0 CE  04 CD CC CC CC CC CC 00",
                    "00000030  C0 9A 99 99 99 99 99 F1  BF 9A 99 99 99 99 99 F1",
                    "00000040  3F CD CC CC CC CC CC 00  40 F0 CE 06 CD CC CC CC",
                    "00000050  CC CC 08 C0 CD CC CC CC  CC CC 00 C0 9A 99 99 99",
                    "00000060  99 99 F1 BF 9A 99 99 99  99 99 F1 3F CD CC CC CC",
                    "00000070  CC CC 00 40 CD CC CC CC  CC CC 08 40 F0 CE 04 CD",
                    "00000080  CC CC CC CC CC 00 C0 9A  99 99 99 99 99 F1 BF 9A",
                    "00000090  99 99 99 99 99 F1 3F CD  CC CC CC CC CC 00 40"
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }
        }

        [TestMethod]
        [Owner("sboshra")]
        public void UniformArrayOfNumberArraysTest10()
        {
            // -------------------------
            // Different numeric types/Same size
            // -------------------------

            // Common set of number arrays for the tests to follow
            sbyte[] i8Values1 = { -100, -50, 50, 100 };
            sbyte[] i8Values2 = { -125, -100, -50, 50, 100, 125 };

            short[] i16Values1 = { -2000, -1000, 1000, 2000 };
            short[] i16Values2 = { -2500, -2000, -1000, 1000, 2000, 2500 };

            int[] i32Values1 = { -200000, -100000, 100000, 200000 };
            int[] i32Values2 = { -250000, -200000, -100000, 100000, 200000, 250000 };

            long[] i64Values1 = { -20000000000, -10000000000, 10000000000, 20000000000 };
            long[] i64Values2 = { -25000000000, -20000000000, -10000000000, 10000000000, 20000000000, 25000000000 };

            double[] f64Values1 = { -2.1, -1.1, 1.1, 2.1 };
            double[] f64Values2 = { -3.1, -2.1, -1.1, 1.1, 2.1, 3.1 };

            // Case 1
            {
                JsonToken[] tokensToWrite =
                {
                    JsonToken.ArrayStart(),
                        JsonToken.Int8NumberArray(i8Values1),
                        JsonToken.Int8NumberArray(i8Values1),
                        JsonToken.Int8NumberArray(i8Values1),
                        JsonToken.Int16NumberArray(i16Values1),
                    JsonToken.ArrayEnd(),
                };

                string[] expectedText =
                {
                    @"[[-100,-50,50,100],[-100,-50,50,100],[-100,-50,50,100],[-2000,-1000,1000,2000]]",
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E2 32 E2 0A C9 9C FF  C9 CE FF C8 32 C8 64 E2",
                    "00000010  0A C9 9C FF C9 CE FF C8  32 C8 64 E2 0A C9 9C FF",
                    "00000020  C9 CE FF C8 32 C8 64 E2  0C C9 30 F8 C9 18 FC C9",
                    "00000030  E8 03 C9 D0 07"
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 E2 20 F0 D8 04 9C CE  32 64 F0 D8 04 9C CE 32",
                    "00000010  64 F0 D8 04 9C CE 32 64  F0 D9 04 30 F8 18 FC E8",
                    "00000020  03 D0 07"
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }

            // Case 2
            {
                JsonToken[] tokensToWrite =
                {
                    JsonToken.ArrayStart(),
                        JsonToken.Int16NumberArray(i16Values2),
                        JsonToken.Int16NumberArray(i16Values2),
                        JsonToken.Int32NumberArray(i32Values2),
                        JsonToken.Int32NumberArray(i32Values2),
                    JsonToken.ArrayEnd(),
                };

                string[] expectedText =
                {
                    @"[[-2500,-2000,-1000,1000,2000,2500],[-2500,-2000,-1000,1000,2000,2500],[-250000,-200000,-100000,1000",
                    @"00,200000,250000],[-250000,-200000,-100000,100000,200000,250000]]",
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E2 68 E2 12 C9 3C F6  C9 30 F8 C9 18 FC C9 E8",
                    "00000010  03 C9 D0 07 C9 C4 09 E2  12 C9 3C F6 C9 30 F8 C9",
                    "00000020  18 FC C9 E8 03 C9 D0 07  C9 C4 09 E2 1E CA 70 2F",
                    "00000030  FC FF CA C0 F2 FC FF CA  60 79 FE FF CA A0 86 01",
                    "00000040  00 CA 40 0D 03 00 CA 90  D0 03 00 E2 1E CA 70 2F",
                    "00000050  FC FF CA C0 F2 FC FF CA  60 79 FE FF CA A0 86 01",
                    "00000060  00 CA 40 0D 03 00 CA 90  D0 03 00"
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 E2 54 F0 D9 06 3C F6  30 F8 18 FC E8 03 D0 07",
                    "00000010  C4 09 F0 D9 06 3C F6 30  F8 18 FC E8 03 D0 07 C4",
                    "00000020  09 F0 DA 06 70 2F FC FF  C0 F2 FC FF 60 79 FE FF",
                    "00000030  A0 86 01 00 40 0D 03 00  90 D0 03 00 F0 DA 06 70",
                    "00000040  2F FC FF C0 F2 FC FF 60  79 FE FF A0 86 01 00 40",
                    "00000050  0D 03 00 90 D0 03 00"
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }

            // Case 3
            {
                JsonToken[] tokensToWrite =
                {
                    JsonToken.ArrayStart(),
                        JsonToken.Float64NumberArray(f64Values2),
                        JsonToken.Float64NumberArray(f64Values2),
                        JsonToken.Int64NumberArray(i64Values2),
                        JsonToken.Float64NumberArray(f64Values2),
                    JsonToken.ArrayEnd(),
                };

                string[] expectedText =
                {
                    @"[[-3.1,-2.1,-1.1,1.1,2.1,3.1],[-3.1,-2.1,-1.1,1.1,2.1,3.1],[-25000000000,-20000000000,-10000000000,1",
                    @"0000000000,20000000000,25000000000],[-3.1,-2.1,-1.1,1.1,2.1,3.1]]",
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E2 E0 E2 36 CC CD CC  CC CC CC CC 08 C0 CC CD",
                    "00000010  CC CC CC CC CC 00 C0 CC  9A 99 99 99 99 99 F1 BF",
                    "00000020  CC 9A 99 99 99 99 99 F1  3F CC CD CC CC CC CC CC",
                    "00000030  00 40 CC CD CC CC CC CC  CC 08 40 E2 36 CC CD CC",
                    "00000040  CC CC CC CC 08 C0 CC CD  CC CC CC CC CC 00 C0 CC",
                    "00000050  9A 99 99 99 99 99 F1 BF  CC 9A 99 99 99 99 99 F1",
                    "00000060  3F CC CD CC CC CC CC CC  00 40 CC CD CC CC CC CC",
                    "00000070  CC 08 40 E2 36 CB 00 46  E2 2D FA FF FF FF CB 00",
                    "00000080  38 E8 57 FB FF FF FF CB  00 1C F4 AB FD FF FF FF",
                    "00000090  CB 00 E4 0B 54 02 00 00  00 CB 00 C8 17 A8 04 00",
                    "000000A0  00 00 CB 00 BA 1D D2 05  00 00 00 E2 36 CC CD CC",
                    "000000B0  CC CC CC CC 08 C0 CC CD  CC CC CC CC CC 00 C0 CC",
                    "000000C0  9A 99 99 99 99 99 F1 BF  CC 9A 99 99 99 99 99 F1",
                    "000000D0  3F CC CD CC CC CC CC CC  00 40 CC CD CC CC CC CC",
                    "000000E0  CC 08 40"
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 E2 CC F0 CE 06 CD CC  CC CC CC CC 08 C0 CD CC",
                    "00000010  CC CC CC CC 00 C0 9A 99  99 99 99 99 F1 BF 9A 99",
                    "00000020  99 99 99 99 F1 3F CD CC  CC CC CC CC 00 40 CD CC",
                    "00000030  CC CC CC CC 08 40 F0 CE  06 CD CC CC CC CC CC 08",
                    "00000040  C0 CD CC CC CC CC CC 00  C0 9A 99 99 99 99 99 F1",
                    "00000050  BF 9A 99 99 99 99 99 F1  3F CD CC CC CC CC CC 00",
                    "00000060  40 CD CC CC CC CC CC 08  40 F0 DB 06 00 46 E2 2D",
                    "00000070  FA FF FF FF 00 38 E8 57  FB FF FF FF 00 1C F4 AB",
                    "00000080  FD FF FF FF 00 E4 0B 54  02 00 00 00 00 C8 17 A8",
                    "00000090  04 00 00 00 00 BA 1D D2  05 00 00 00 F0 CE 06 CD",
                    "000000A0  CC CC CC CC CC 08 C0 CD  CC CC CC CC CC 00 C0 9A",
                    "000000B0  99 99 99 99 99 F1 BF 9A  99 99 99 99 99 F1 3F CD",
                    "000000C0  CC CC CC CC CC 00 40 CD  CC CC CC CC CC 08 40"
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }

            // Case 4
            {
                JsonToken[] tokensToWrite =
                {
                    JsonToken.ArrayStart(),
                        JsonToken.Float64NumberArray(f64Values1),
                        JsonToken.Float64NumberArray(f64Values1),
                        JsonToken.Int64NumberArray(i64Values1),
                        JsonToken.Int64NumberArray(i64Values1),
                        JsonToken.Int32NumberArray(i32Values1),
                        JsonToken.Int32NumberArray(i32Values1),
                    JsonToken.ArrayEnd(),
                };

                string[] expectedText =
                {
                    @"[[-2.1,-1.1,1.1,2.1],[-2.1,-1.1,1.1,2.1],[-20000000000,-10000000000,10000000000,20000000000],[-20000",
                    @"000000,-10000000000,10000000000,20000000000],[-200000,-100000,100000,200000],[-200000,-100000,100000",
                    @",200000]]",
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E2 C4 E2 24 CC CD CC  CC CC CC CC 00 C0 CC 9A",
                    "00000010  99 99 99 99 99 F1 BF CC  9A 99 99 99 99 99 F1 3F",
                    "00000020  CC CD CC CC CC CC CC 00  40 E2 24 CC CD CC CC CC",
                    "00000030  CC CC 00 C0 CC 9A 99 99  99 99 99 F1 BF CC 9A 99",
                    "00000040  99 99 99 99 F1 3F CC CD  CC CC CC CC CC 00 40 E2",
                    "00000050  24 CB 00 38 E8 57 FB FF  FF FF CB 00 1C F4 AB FD",
                    "00000060  FF FF FF CB 00 E4 0B 54  02 00 00 00 CB 00 C8 17",
                    "00000070  A8 04 00 00 00 E2 24 CB  00 38 E8 57 FB FF FF FF",
                    "00000080  CB 00 1C F4 AB FD FF FF  FF CB 00 E4 0B 54 02 00",
                    "00000090  00 00 CB 00 C8 17 A8 04  00 00 00 E2 14 CA C0 F2",
                    "000000A0  FC FF CA 60 79 FE FF CA  A0 86 01 00 CA 40 0D 03",
                    "000000B0  00 E2 14 CA C0 F2 FC FF  CA 60 79 FE FF CA A0 86",
                    "000000C0  01 00 CA 40 0D 03 00"
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 E2 B2 F0 CE 04 CD CC  CC CC CC CC 00 C0 9A 99",
                    "00000010  99 99 99 99 F1 BF 9A 99  99 99 99 99 F1 3F CD CC",
                    "00000020  CC CC CC CC 00 40 F0 CE  04 CD CC CC CC CC CC 00",
                    "00000030  C0 9A 99 99 99 99 99 F1  BF 9A 99 99 99 99 99 F1",
                    "00000040  3F CD CC CC CC CC CC 00  40 F0 DB 04 00 38 E8 57",
                    "00000050  FB FF FF FF 00 1C F4 AB  FD FF FF FF 00 E4 0B 54",
                    "00000060  02 00 00 00 00 C8 17 A8  04 00 00 00 F0 DB 04 00",
                    "00000070  38 E8 57 FB FF FF FF 00  1C F4 AB FD FF FF FF 00",
                    "00000080  E4 0B 54 02 00 00 00 00  C8 17 A8 04 00 00 00 F0",
                    "00000090  DA 04 C0 F2 FC FF 60 79  FE FF A0 86 01 00 40 0D",
                    "000000A0  03 00 F0 DA 04 C0 F2 FC  FF 60 79 FE FF A0 86 01",
                    "000000B0  00 40 0D 03 00"
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }
        }

        [TestMethod]
        [Owner("sboshra")]
        public void UniformArrayOfNumberArraysTest11()
        {
            // -------------------------
            // Mixed w/ non-numeric arrays
            // -------------------------

            // Common set of number arrays for the tests to follow
            sbyte[] i8Values1 = { -100, -50, 50, 100 };
            sbyte[] i8Values2 = { -125, -100, -50, 50, 100, 125 };

            short[] i16Values1 = { -2000, -1000, 1000, 2000 };
            short[] i16Values2 = { -2500, -2000, -1000, 1000, 2000, 2500 };

            int[] i32Values1 = { -200000, -100000, 100000, 200000 };
            int[] i32Values2 = { -250000, -200000, -100000, 100000, 200000, 250000 };

            long[] i64Values1 = { -20000000000, -10000000000, 10000000000, 20000000000 };
            long[] i64Values2 = { -25000000000, -20000000000, -10000000000, 10000000000, 20000000000, 25000000000 };

            double[] f64Values1 = { -2.1, -1.1, 1.1, 2.1 };
            double[] f64Values2 = { -3.1, -2.1, -1.1, 1.1, 2.1, 3.1 };

            // Case 1
            {
                JsonToken[] tokensToWrite =
                {
                    JsonToken.ArrayStart(),
                        JsonToken.Int8NumberArray(i8Values1),
                        JsonToken.Int8NumberArray(i8Values1),
                        JsonToken.Int8NumberArray(i8Values1),
                        JsonToken.ArrayStart(),
                            JsonToken.String("Atlanta"),
                            JsonToken.String("Dallas"),
                            JsonToken.String("Seattle"),
                        JsonToken.ArrayEnd(),
                        JsonToken.Int16NumberArray(i16Values1),
                    JsonToken.ArrayEnd(),
                };

                string[] expectedText =
                {
                    @"[[-100,-50,50,100],[-100,-50,50,100],[-100,-50,50,100],[""Atlanta"",""Dallas"",""Seattle""],[-2000,-1000,1",
                    @"000,2000]]",
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E2 4B E2 0A C9 9C FF  C9 CE FF C8 32 C8 64 E2",
                    "00000010  0A C9 9C FF C9 CE FF C8  32 C8 64 E2 0A C9 9C FF",
                    "00000020  C9 CE FF C8 32 C8 64 E2  17 87 41 74 6C 61 6E 74",
                    "00000030  61 86 44 61 6C 6C 61 73  87 53 65 61 74 74 6C 65",
                    "00000040  E2 0C C9 30 F8 C9 18 FC  C9 E8 03 C9 D0 07"
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 E2 39 F0 D8 04 9C CE  32 64 F0 D8 04 9C CE 32",
                    "00000010  64 F0 D8 04 9C CE 32 64  E2 17 87 41 74 6C 61 6E",
                    "00000020  74 61 86 44 61 6C 6C 61  73 87 53 65 61 74 74 6C",
                    "00000030  65 F0 D9 04 30 F8 18 FC  E8 03 D0 07"
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }

            // Case 2
            {
                JsonToken[] tokensToWrite =
                {
                    JsonToken.ArrayStart(),
                        JsonToken.Int16NumberArray(i16Values2),
                        JsonToken.Int16NumberArray(i16Values2),
                        JsonToken.Int16NumberArray(i16Values2),
                        JsonToken.Int16NumberArray(i16Values2),
                        JsonToken.ArrayStart(),
                            JsonToken.Boolean(false),
                            JsonToken.Boolean(true),
                        JsonToken.ArrayEnd(),
                    JsonToken.ArrayEnd(),
                };

                string[] expectedText =
                {
                    @"[[-2500,-2000,-1000,1000,2000,2500],[-2500,-2000,-1000,1000,2000,2500],[-2500,-2000,-1000,1000,2000,",
                    @"2500],[-2500,-2000,-1000,1000,2000,2500],[false,true]]",
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E2 54 E2 12 C9 3C F6  C9 30 F8 C9 18 FC C9 E8",
                    "00000010  03 C9 D0 07 C9 C4 09 E2  12 C9 3C F6 C9 30 F8 C9",
                    "00000020  18 FC C9 E8 03 C9 D0 07  C9 C4 09 E2 12 C9 3C F6",
                    "00000030  C9 30 F8 C9 18 FC C9 E8  03 C9 D0 07 C9 C4 09 E2",
                    "00000040  12 C9 3C F6 C9 30 F8 C9  18 FC C9 E8 03 C9 D0 07",
                    "00000050  C9 C4 09 E2 02 D1 D2"
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 E2 40 F0 D9 06 3C F6  30 F8 18 FC E8 03 D0 07",
                    "00000010  C4 09 F0 D9 06 3C F6 30  F8 18 FC E8 03 D0 07 C4",
                    "00000020  09 F0 D9 06 3C F6 30 F8  18 FC E8 03 D0 07 C4 09",
                    "00000030  F0 D9 06 3C F6 30 F8 18  FC E8 03 D0 07 C4 09 E2",
                    "00000040  02 D1 D2"
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }

            // Case 3
            {
                JsonToken[] tokensToWrite =
                {
                    JsonToken.ArrayStart(),
                        JsonToken.Int64NumberArray(i64Values2),
                        JsonToken.Int64NumberArray(i64Values2),
                        JsonToken.Int64NumberArray(i64Values2),
                        JsonToken.Int64NumberArray(i64Values2),
                        JsonToken.ArrayStart(),
                        JsonToken.ArrayEnd(),
                    JsonToken.ArrayEnd(),
                };

                string[] expectedText =
                {
                    @"[[-25000000000,-20000000000,-10000000000,10000000000,20000000000,25000000000],[-25000000000,-2000000",
                    @"0000,-10000000000,10000000000,20000000000,25000000000],[-25000000000,-20000000000,-10000000000,10000",
                    @"000000,20000000000,25000000000],[-25000000000,-20000000000,-10000000000,10000000000,20000000000,2500",
                    @"0000000],[]]",
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E2 E1 E2 36 CB 00 46  E2 2D FA FF FF FF CB 00",
                    "00000010  38 E8 57 FB FF FF FF CB  00 1C F4 AB FD FF FF FF",
                    "00000020  CB 00 E4 0B 54 02 00 00  00 CB 00 C8 17 A8 04 00",
                    "00000030  00 00 CB 00 BA 1D D2 05  00 00 00 E2 36 CB 00 46",
                    "00000040  E2 2D FA FF FF FF CB 00  38 E8 57 FB FF FF FF CB",
                    "00000050  00 1C F4 AB FD FF FF FF  CB 00 E4 0B 54 02 00 00",
                    "00000060  00 CB 00 C8 17 A8 04 00  00 00 CB 00 BA 1D D2 05",
                    "00000070  00 00 00 E2 36 CB 00 46  E2 2D FA FF FF FF CB 00",
                    "00000080  38 E8 57 FB FF FF FF CB  00 1C F4 AB FD FF FF FF",
                    "00000090  CB 00 E4 0B 54 02 00 00  00 CB 00 C8 17 A8 04 00",
                    "000000A0  00 00 CB 00 BA 1D D2 05  00 00 00 E2 36 CB 00 46",
                    "000000B0  E2 2D FA FF FF FF CB 00  38 E8 57 FB FF FF FF CB",
                    "000000C0  00 1C F4 AB FD FF FF FF  CB 00 E4 0B 54 02 00 00",
                    "000000D0  00 CB 00 C8 17 A8 04 00  00 00 CB 00 BA 1D D2 05",
                    "000000E0  00 00 00 E0"
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 E2 CD F0 DB 06 00 46  E2 2D FA FF FF FF 00 38",
                    "00000010  E8 57 FB FF FF FF 00 1C  F4 AB FD FF FF FF 00 E4",
                    "00000020  0B 54 02 00 00 00 00 C8  17 A8 04 00 00 00 00 BA",
                    "00000030  1D D2 05 00 00 00 F0 DB  06 00 46 E2 2D FA FF FF",
                    "00000040  FF 00 38 E8 57 FB FF FF  FF 00 1C F4 AB FD FF FF",
                    "00000050  FF 00 E4 0B 54 02 00 00  00 00 C8 17 A8 04 00 00",
                    "00000060  00 00 BA 1D D2 05 00 00  00 F0 DB 06 00 46 E2 2D",
                    "00000070  FA FF FF FF 00 38 E8 57  FB FF FF FF 00 1C F4 AB",
                    "00000080  FD FF FF FF 00 E4 0B 54  02 00 00 00 00 C8 17 A8",
                    "00000090  04 00 00 00 00 BA 1D D2  05 00 00 00 F0 DB 06 00",
                    "000000A0  46 E2 2D FA FF FF FF 00  38 E8 57 FB FF FF FF 00",
                    "000000B0  1C F4 AB FD FF FF FF 00  E4 0B 54 02 00 00 00 00",
                    "000000C0  C8 17 A8 04 00 00 00 00  BA 1D D2 05 00 00 00 E0",
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }

            // Case 4
            {
                JsonToken[] tokensToWrite =
                {
                    JsonToken.ArrayStart(),
                        JsonToken.Float64NumberArray(f64Values1),
                        JsonToken.Float64NumberArray(f64Values1),
                        JsonToken.Float64NumberArray(f64Values1),
                        JsonToken.ArrayStart(),
                            JsonToken.Int32NumberArray(i32Values1),
                            JsonToken.Float64NumberArray(f64Values1),
                            JsonToken.Float64NumberArray(f64Values1),
                        JsonToken.ArrayEnd(),
                    JsonToken.ArrayEnd(),
                };

                string[] expectedText =
                {
                    @"[[-2.1,-1.1,1.1,2.1],[-2.1,-1.1,1.1,2.1],[-2.1,-1.1,1.1,2.1],[[-200000,-100000,100000,200000],[-2.1,",
                    @"-1.1,1.1,2.1],[-2.1,-1.1,1.1,2.1]]]",
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 E2 D6 E2 24 CC CD CC  CC CC CC CC 00 C0 CC 9A",
                    "00000010  99 99 99 99 99 F1 BF CC  9A 99 99 99 99 99 F1 3F",
                    "00000020  CC CD CC CC CC CC CC 00  40 E2 24 CC CD CC CC CC",
                    "00000030  CC CC 00 C0 CC 9A 99 99  99 99 99 F1 BF CC 9A 99",
                    "00000040  99 99 99 99 F1 3F CC CD  CC CC CC CC CC 00 40 E2",
                    "00000050  24 CC CD CC CC CC CC CC  00 C0 CC 9A 99 99 99 99",
                    "00000060  99 F1 BF CC 9A 99 99 99  99 99 F1 3F CC CD CC CC",
                    "00000070  CC CC CC 00 40 E2 62 E2  14 CA C0 F2 FC FF CA 60",
                    "00000080  79 FE FF CA A0 86 01 00  CA 40 0D 03 00 E2 24 CC",
                    "00000090  CD CC CC CC CC CC 00 C0  CC 9A 99 99 99 99 99 F1",
                    "000000A0  BF CC 9A 99 99 99 99 99  F1 3F CC CD CC CC CC CC",
                    "000000B0  CC 00 40 E2 24 CC CD CC  CC CC CC CC 00 C0 CC 9A",
                    "000000C0  99 99 99 99 99 F1 BF CC  9A 99 99 99 99 99 F1 3F",
                    "000000D0  CC CD CC CC CC CC CC 00  40"
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 E2 C4 F0 CE 04 CD CC  CC CC CC CC 00 C0 9A 99",
                    "00000010  99 99 99 99 F1 BF 9A 99  99 99 99 99 F1 3F CD CC",
                    "00000020  CC CC CC CC 00 40 F0 CE  04 CD CC CC CC CC CC 00",
                    "00000030  C0 9A 99 99 99 99 99 F1  BF 9A 99 99 99 99 99 F1",
                    "00000040  3F CD CC CC CC CC CC 00  40 F0 CE 04 CD CC CC CC",
                    "00000050  CC CC 00 C0 9A 99 99 99  99 99 F1 BF 9A 99 99 99",
                    "00000060  99 99 F1 3F CD CC CC CC  CC CC 00 40 E2 59 F0 DA",
                    "00000070  04 C0 F2 FC FF 60 79 FE  FF A0 86 01 00 40 0D 03",
                    "00000080  00 F0 CE 04 CD CC CC CC  CC CC 00 C0 9A 99 99 99",
                    "00000090  99 99 F1 BF 9A 99 99 99  99 99 F1 3F CD CC CC CC",
                    "000000A0  CC CC 00 40 F0 CE 04 CD  CC CC CC CC CC 00 C0 9A",
                    "000000B0  99 99 99 99 99 F1 BF 9A  99 99 99 99 99 F1 3F CD",
                    "000000C0  CC CC CC CC CC 00 40"
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }
        }

        [TestMethod]
        [Owner("sboshra")]
        public void UniformArrayOfNumberArraysTest12()
        {
            // -------------------------
            // Misc. Reported Issues
            // -------------------------

            // Case 1
            {
                JsonToken[] tokensToWrite =
                {
                    JsonToken.ObjectStart(),
                        JsonToken.FieldName("id"),
                        JsonToken.String("1da1ac75-b910-4383-927f-c8d09be18500"),
                        JsonToken.FieldName("mypk"),
                        JsonToken.String("ea3a2e12-570e-49da-b262-a6b5341fea0c"),
                        JsonToken.FieldName("sgmts"),
                        JsonToken.ArrayStart(),
                            JsonToken.ArrayStart(),
                                JsonToken.Number(6519456),
                                JsonToken.Number(1471916863),
                            JsonToken.ArrayEnd(),
                            JsonToken.ArrayStart(),
                                JsonToken.Number(2498434),
                                JsonToken.Number(1455671440),
                            JsonToken.ArrayEnd(),
                        JsonToken.ArrayEnd(),
                    JsonToken.ObjectEnd(),
                };

                string[] expectedText =
                {
                    @"{""id"":""1da1ac75-b910-4383-927f-c8d09be18500"",""mypk"":""ea3a2e12-570e-49da-b262-a6b5341fea0c"",""sgmts"":[",
                    @"[6519456,1471916863],[2498434,1455671440]]}",
                };

                string[] expectedBinary1 =
                {
                    "00000000  80 EA 48 2C 75 D1 1A CA  57 9B 01 34 38 29 F7 8C",
                    "00000010  0D B9 1E 58 00 84 6D 79  70 6B 75 AE A3 E2 21 75",
                    "00000020  E0 94 AD 2B 26 6A 5B 43  F1 AE C0 85 73 67 6D 74",
                    "00000030  73 E2 18 E2 0A CA A0 7A  63 00 CA 3F AB BB 57 E2",
                    "00000040  0A CA 82 1F 26 00 CA 90  C8 C3 56"
                };

                string[] expectedBinary2 =
                {
                    "00000000  80 EA 43 2C 75 D1 1A CA  57 9B 01 34 38 29 F7 8C",
                    "00000010  0D B9 1E 58 00 84 6D 79  70 6B 75 AE A3 E2 21 75",
                    "00000020  E0 94 AD 2B 26 6A 5B 43  F1 AE C0 85 73 67 6D 74",
                    "00000030  73 F2 F0 DA 02 02 A0 7A  63 00 3F AB BB 57 82 1F",
                    "00000040  26 00 90 C8 C3 56"
                };

                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary1, JsonWriteOptions.None);
                ExecuteAndValidate(tokensToWrite, expectedText, expectedBinary2, JsonWriteOptions.EnableNumberArrays);
            }
        }
        #endregion Array

        #region Escaping
        [TestMethod]
        [Owner("mayapainter")]
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
        [Owner("mayapainter")]
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
        [Owner("mayapainter")]
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
        [Owner("mayapainter")]
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
        [Owner("mayapainter")]
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
        [Owner("mayapainter")]
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
        [Owner("mayapainter")]
        public void EmptyObjectTest()
        {
            string expectedString = "{}";
            byte[] binaryOutput =
            {
                BinaryFormat,
                JsonBinaryEncoding.TypeMarker.Obj0,
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
        [Owner("mayapainter")]
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

                binaryOutputBuilder.Add(new byte[] { (byte)elementsBytes.Length });
                binaryOutputBuilder.Add(elementsBytes);
                binaryOutput = binaryOutputBuilder.SelectMany(x => x).ToArray();
            }

            byte[] binaryOutputWithEncoding;
            {
                List<byte[]> binaryOutputBuilder = new List<byte[]>
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
        [Owner("mayapainter")]
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
                    new byte[] { BinaryFormat, JsonBinaryEncoding.TypeMarker.ObjL2 }
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

                innerObjectElements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.ArrL1, (byte)innerArrayElementsBytes.Length });
                innerObjectElements.Add(innerArrayElementsBytes);

                byte[] innerObjectElementsBytes = innerObjectElements.SelectMany(x => x).ToArray();
                elements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.ObjL1, (byte)innerObjectElementsBytes.Length });
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
                    new byte[] { BinaryFormat, JsonBinaryEncoding.TypeMarker.ObjL1 }
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

                innerObjectElements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.ArrL1, (byte)innerArrayElementsBytes.Length });
                innerObjectElements.Add(innerArrayElementsBytes);

                byte[] innerObjectElementsBytes = innerObjectElements.SelectMany(x => x).ToArray();
                elements.Add(new byte[] { JsonBinaryEncoding.TypeMarker.ObjL1, (byte)innerObjectElementsBytes.Length });
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
        [Owner("mayapainter")]
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
        [Owner("mayapainter")]
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
        [Owner("mayapainter")]
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
        [Owner("mayapainter")]
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
        [Owner("mayapainter")]
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
        [Owner("mayapainter")]
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
        [Owner("mayapainter")]
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
        [Owner("mayapainter")]
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
        [Owner("mayapainter")]
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
        [Owner("mayapainter")]
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
        [Owner("mayapainter")]
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
        [Owner("mayapainter")]
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
        [Owner("mayapainter")]
        public void GuidTest()
        {
            Guid[] values = new Guid[] { Guid.Empty, Guid.NewGuid() };
            foreach (Guid value in values)
            {
                string expectedStringOutput = $"G{value}";
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
        [Owner("mayapainter")]
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

                        // Create through serialization API
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
                IJsonWriter jsonWriter = JsonWriter.Create(JsonSerializationFormat.Binary, JsonWriteOptions.None, 256);
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
                JsonTestUtils.WriteTokens(tokensToWrite, jsonWriter, writeAsUtf8String);
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

        private static void ExecuteAndValidate(
            JsonToken[] inputTokens,
            string[] expectedTextResult,
            string[] expectedBinaryResult,
            JsonWriteOptions writeOptions,
            bool skipRoundTripTest = false)
        {
            Assert.IsNotNull(inputTokens);
            Assert.IsNotNull(expectedTextResult);
            Assert.IsNotNull(expectedBinaryResult);

            JsonSerializationFormat[] serializationFormats = new JsonSerializationFormat[]
            {
                JsonSerializationFormat.Text,
                JsonSerializationFormat.Binary
            };

            string[][] exptectedResults = new string[][]
            {
                expectedTextResult,
                expectedBinaryResult
            };

            Assert.AreEqual(serializationFormats.Length, exptectedResults.Length);

            for (int i = 0; i < serializationFormats.Length; i++)
            {
                JsonSerializationFormat serializationFormat = serializationFormats[i];
                string[] expectedResult = exptectedResults[i];

                foreach (bool writeAsUtf8String in new bool[] { false, true })
                {
                    IJsonWriter jsonWriter = JsonWriter.Create(serializationFormat, writeOptions);
                    JsonTestUtils.WriteTokens(inputTokens, jsonWriter, writeAsUtf8String);
                    ReadOnlyMemory<byte> result = jsonWriter.GetResult();

                    string[] actualResult = JsonTestUtils.SerializeResultBuffer(result.ToArray(), serializationFormat);
                    if (!VerifyResults(expectedResult, actualResult))
                    {
                        Assert.Fail($"JsonWriter validation failed for format '{serializationFormat}' and write options '{writeOptions}'.");
                    }
                }
            }

            if(!skipRoundTripTest)
            {
                SerializationSpec[] serializationSpecs = new SerializationSpec[]
                {
                    SerializationSpec.Text(JsonWriteOptions.None),
                    SerializationSpec.Binary(JsonWriteOptions.None),
                    SerializationSpec.Binary(JsonWriteOptions.EnableNumberArrays),
                    SerializationSpec.Binary(JsonWriteOptions.EnableUInt64),
                };

                RewriteScenario[] rewriteScenarios = new RewriteScenario[]
                {
                    RewriteScenario.NavigatorRoot,
                    RewriteScenario.NavigatorNode,
                    RewriteScenario.ReaderAll,
                    RewriteScenario.ReaderToken,
                };

                foreach (SerializationSpec inputSpec in serializationSpecs)
                {
                    IJsonWriter inputWriter = JsonWriter.Create(inputSpec.SerializationFormat, inputSpec.WriteOptions);
                    WriteTokens(inputTokens, inputWriter, writeAsUtf8String: true);
                    ReadOnlyMemory<byte> inputResult = inputWriter.GetResult();

                    foreach (SerializationSpec outputSpec in serializationSpecs)
                    {
                        foreach (RewriteScenario rewriteScenario in rewriteScenarios)
                        {
                            VerifyJsonRoundTrip(
                                inputResult,
                                inputJson: null,
                                inputSpec,
                                outputSpec,
                                rewriteScenario);
                        }
                    }
                }
            }
        }

        private static bool VerifyResults(
            string[] expectedStringResults,
            string[] actualStringResults)
        {
            Assert.IsNotNull(expectedStringResults);
            Assert.IsNotNull(actualStringResults);

            if (expectedStringResults.Length != actualStringResults.Length)
            {
                DumpExpectedActualResults(expectedStringResults, actualStringResults);

                return false;
            }

            for (int i = 0; i < expectedStringResults.Length; i++)
            {
                if (expectedStringResults[i] != actualStringResults[i])
                {
                    Console.WriteLine($"Incorrect actual result at line number {i}.");
                    Console.WriteLine();
                    Console.WriteLine("Expected: " + expectedStringResults[i]);
                    Console.WriteLine("Actual  : " + actualStringResults[i]);

                    DumpExpectedActualResults(expectedStringResults, actualStringResults);

                    return false;
                }
            }

            return true;
        }

        private static void DumpExpectedActualResults(
            string[] expectedStringResults,
            string[] actualStringResults)
        {
            Assert.IsNotNull(expectedStringResults);
            Assert.IsNotNull(actualStringResults);

            Console.WriteLine();
            Console.WriteLine("Expected Result:");
            foreach (string item in expectedStringResults)
            {
                Console.WriteLine(item);
            }

            Console.WriteLine();
            Console.WriteLine("Actual Result:");
            foreach (string item in actualStringResults)
            {
                Console.WriteLine(item);
            }
        }

        static private sbyte[] Int8Array(params sbyte[] values) => values;
        static private byte[] UInt8Array(params byte[] values) => values;
        static private short[] Int16Array(params short[] values) => values;
        static private int[] Int32Array(params int[] values) => values;
        static private long[] Int64Array(params long[] values) => values;
        static private float[] Float32Array(params float[] values) => values;
        static private double[] Float64Array(params double[] values) => values;
    }
}