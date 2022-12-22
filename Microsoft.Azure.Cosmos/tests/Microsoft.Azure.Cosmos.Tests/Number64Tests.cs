//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class Number64Tests
    {
        [TestMethod]
        public void ToString_Decimal_FormatsWithMonetaryPrecision()
        {
            List<(Number64, string)> tupleList = new List<(Number64, string)>
            {
                (decimal.MaxValue, "79228162514264337593543950335"),
                (decimal.MinValue, "-79228162514264337593543950335"),
                (104.37644171779141m, "104.37644171779141"),
                (0m, "0"),
            };

            foreach ((Number64 number64, string expectedString) in tupleList)
            {
                Assert.AreEqual(expectedString, number64.ToString("G"));
            }
        }

        [TestMethod]
        public void ToString_Double_FormatsWithScientificPrecision()
        {
            List<(Number64, string)> tupleList = new List<(Number64, string)>
            {
                (double.MaxValue, "1.7976931348623157E+308"),
                (double.MinValue, "-1.7976931348623157E+308"),
                (0.0000000012, "1.2E-09"),
                (0m, "0"),
            };

            foreach ((Number64 number64, string expectedString) in tupleList)
            {
                Assert.AreEqual(expectedString, number64.ToString("G17"));
            }
        }

        [TestMethod]
        public void ToString_Long_Formats()
        {
            List<(Number64, string)> tupleList = new List<(Number64, string)>
            {
                (long.MaxValue, "9223372036854775807"),
                (long.MinValue, "-9223372036854775808"),
                (123, "123"),
                (0m, "0"),
            };

            foreach ((Number64 number64, string expectedString) in tupleList)
            {
                Assert.AreEqual(expectedString, number64.ToString("G"));
            }
        }

        [TestMethod]
        public void ToDecimal_DifferentTypes_ReturnsDecimal()
        {
            List<(Number64, decimal)> tupleList = new List<(Number64, decimal)>
            {
                (1234.5678m, 1234.5678m),
                (1234.5678d, 1234.5678m),
                (1234, 1234),
            };

            foreach ((Number64 number64, decimal decimalNumber) in tupleList)
            {
                Number64 castedNumber64 = Number64.ToDecimal(number64);

                Assert.IsTrue(castedNumber64.IsDecimal);
                Assert.AreEqual(castedNumber64, decimalNumber);
            }
        }

        [TestMethod]
        public void ToDouble_DifferentTypes_ReturnsDouble()
        {
            List<(Number64, double)> tupleList = new List<(Number64, double)>
            {
                (1234.5678d, 1234.5678),
                (1234.5678m, 1234.5678),
                (79349367648374345, 79349367648374345),
            };

            foreach ((Number64 number64, double doubleNumber) in tupleList)
            {
                Number64 castedNumber64 = Number64.ToDouble(number64);

                Assert.IsTrue(castedNumber64.IsDouble);
                Assert.AreEqual(castedNumber64, doubleNumber);
            }
        }

        [TestMethod]
        public void ToLong_DifferentTypes_ReturnsLong()
        {
            List<(Number64, int)> tupleList = new List<(Number64, int)>
            {
                (1234, 1234),
                (1234.5678m, 1234),
                (1234.5678d, 1234),
            };

            foreach ((Number64 number64, int intNumber) in tupleList)
            {
                Number64 castedNumber64 = Number64.ToLong(number64);

                Assert.IsTrue(castedNumber64.IsInteger);
                Assert.AreEqual(castedNumber64, intNumber);
            }
        }

        [TestMethod]
        public void ToDoubleEx_DifferentTypes_ReturnsDoubleEx()
        {
            List<(Number64, double)> tupleList = new List<(Number64, double)>
            {
                (0, 0),
                (1234, 1234),
                (1234.5678m, 1234.5678),
                (1234.5678d, 1234.5678),
            };

            foreach ((Number64 number64, double doubleNumber) in tupleList)
            {
                Number64.DoubleEx doubleEx = Number64.ToDoubleEx(number64);

                Assert.AreEqual(doubleNumber, doubleEx.DoubleValue);
            }
        }

        [TestMethod]
        public void IsDecimal_Decimal_ReturnsTrue()
        {
            Number64 number1 = 123.111m;
            Assert.IsTrue(number1.IsDecimal);
        }

        [TestMethod]
        public void IsDecimal_Double_ReturnsFalse()
        {
            Number64 number1 = 123.111d;
            Assert.IsFalse(number1.IsDecimal);
        }

        [TestMethod]
        public void IsDecimal_Integer_ReturnsFalse()
        {
            Number64 number1 = 123;
            Assert.IsFalse(number1.IsDecimal);
        }

        [TestMethod]
        public void IsDouble_Double_ReturnsTrue()
        {
            Number64 number1 = 123.111d;
            Assert.IsTrue(number1.IsDouble);
        }

        [TestMethod]
        public void IsDouble_Integer_ReturnsTrue()
        {
            Number64 number1 = 123;
            Assert.IsFalse(number1.IsDouble);
        }

        [TestMethod]
        public void IsDouble_Decimal_ReturnsTrue()
        {
            Number64 number1 = 123.111m;
            Assert.IsFalse(number1.IsDouble);
        }

        [TestMethod]
        public void IsInteger_Decimal_ReturnsTrue()
        {
            Number64 number1 = 123.111m;
            Assert.IsFalse(number1.IsInteger);
        }

        [TestMethod]
        public void IsInteger_Double_ReturnsFalse()
        {
            Number64 number1 = 123.111d;
            Assert.IsFalse(number1.IsInteger);
        }

        [TestMethod]
        public void IsInteger_Integer_ReturnsFalse()
        {
            Number64 number1 = 123;
            Assert.IsTrue(number1.IsInteger);
        }

        [TestMethod]
        public void GetHashCode_Decimal_ReturnsHashCode()
        {
            {
                Number64 number64 = decimal.MaxValue;
                int expectedHashCode = decimal.MaxValue.GetHashCode();
                Assert.AreEqual(expectedHashCode, number64.GetHashCode());
            }

            {
                Number64 number64 = decimal.MinValue;
                int expectedHashCode = decimal.MinValue.GetHashCode();
                Assert.AreEqual(expectedHashCode, number64.GetHashCode());
            }

            {
                Number64 number64 = 1234m;
                int expectedHashCode = 1234m.GetHashCode();
                Assert.AreEqual(expectedHashCode, number64.GetHashCode());
            }
        }

        [TestMethod]
        public void GetHashCode_Double_ReturnsHashCode()
        {
            {
                Number64 number64 = double.MaxValue;
                int expectedHashCode = double.MaxValue.GetHashCode();
                Assert.AreEqual(expectedHashCode, number64.GetHashCode());
            }

            {
                Number64 number64 = double.MinValue;
                int expectedHashCode = double.MinValue.GetHashCode();
                Assert.AreEqual(expectedHashCode, number64.GetHashCode());
            }

            {
                Number64 number64 = 1234d;
                int expectedHashCode = 1234d.GetHashCode();
                Assert.AreEqual(expectedHashCode, number64.GetHashCode());
            }
        }

        [TestMethod]
        public void LessThanOperator_SameType_AssertComparisonOperators()
        {
            {
                Number64 number1 = 123.111m;
                Number64 number2 = 123.123m;
                this.AssertLessThanOperator(number1, number2);
            }

            {
                Number64 number1 = 123.111d;
                Number64 number2 = 123.123d;
                this.AssertLessThanOperator(number1, number2);
            }

            {
                Number64 number1 = 123;
                Number64 number2 = 124;
                this.AssertLessThanOperator(number1, number2);
            }
        }

        [TestMethod]
        public void LessThanOperator_DifferentTypes_AssertComparisonOperators()
        {
            // decimal less than other types
            {
                Number64 number1 = 123.111m;
                Number64 number2 = 123.123d;
                this.AssertLessThanOperator(number1, number2);
            }

            {
                Number64 number1 = 123.111m;
                Number64 number2 = 124;
                this.AssertLessThanOperator(number1, number2);
            }

            // double less than other types
            {
                Number64 number1 = 123.111d;
                Number64 number2 = 123.123m;
                this.AssertLessThanOperator(number1, number2);
            }

            {
                Number64 number1 = 123.111d;
                Number64 number2 = 124;
                this.AssertLessThanOperator(number1, number2);
            }

            // integer less than other types
            {
                Number64 number1 = 123;
                Number64 number2 = 123.123m;
                this.AssertLessThanOperator(number1, number2);
            }

            {
                Number64 number1 = 123;
                Number64 number2 = 123.123d;
                this.AssertLessThanOperator(number1, number2);
            }
        }

        [TestMethod]
        public void GreaterThanOperator_DifferentTypes_AssertComparisonOperators()
        {
            // decimal greater than other types
            {
                Number64 number1 = 123.2m;
                Number64 number2 = 123.1d;
                this.AssertGreaterThanOperator(number1, number2);
            }

            {
                Number64 number1 = 123.1m;
                Number64 number2 = 123;
                this.AssertGreaterThanOperator(number1, number2);
            }

            // double greater than other types
            {
                Number64 number1 = 123.2d;
                Number64 number2 = 123.1m;
                this.AssertGreaterThanOperator(number1, number2);
            }

            {
                Number64 number1 = 123.1d;
                Number64 number2 = 123;
                this.AssertGreaterThanOperator(number1, number2);
            }

            // long greater than other types
            {
                Number64 number1 = 124;
                Number64 number2 = 123.1m;
                this.AssertGreaterThanOperator(number1, number2);
            }

            {
                Number64 number1 = 124;
                Number64 number2 = 123.1d;
                this.AssertGreaterThanOperator(number1, number2);
            }
        }

        [TestMethod]
        public void GreaterThanOperator_SameType_AssertComparisonOperators()
        {
            {
                Number64 number1 = 123.123m;
                Number64 number2 = 123.111m;
                this.AssertGreaterThanOperator(number1, number2);
            }

            {
                Number64 number1 = 123.123d;
                Number64 number2 = 123.111d;
                this.AssertGreaterThanOperator(number1, number2);
            }

            {
                Number64 number1 = 124;
                Number64 number2 = 123;
                this.AssertGreaterThanOperator(number1, number2);
            }
        }

        [TestMethod]
        public void EqualOperator_DifferentTypes_AssertComparisonOperators()
        {
            {
                Number64 number1 = 123.1m;
                Number64 number2 = 123.1d;
                this.AssertEqualOperator(number1, number2);
            }

            {
                Number64 number1 = 123m;
                Number64 number2 = 123;
                this.AssertEqualOperator(number1, number2);
            }

            {
                Number64 number1 = 123.1d;
                Number64 number2 = 123.1m;
                this.AssertEqualOperator(number1, number2);
            }

            {
                Number64 number1 = 123d;
                Number64 number2 = 123;
                this.AssertEqualOperator(number1, number2);
            }

            {
                Number64 number1 = 123;
                Number64 number2 = 123m;
                this.AssertEqualOperator(number1, number2);
            }

            {
                Number64 number1 = 123;
                Number64 number2 = 123d;
                this.AssertEqualOperator(number1, number2);
            }
        }

        [TestMethod]
        public void EqualOperator_SameTypes_AssertComparisonOperators()
        {
            {
                Number64 number1 = double.MaxValue;
                Number64 number2 = double.MaxValue;
                this.AssertEqualOperator(number1, number2);
            }

            {
                Number64 number1 = int.MaxValue;
                Number64 number2 = int.MaxValue;
                this.AssertEqualOperator(number1, number2);
            }

            {
                Number64 number1 = long.MaxValue;
                Number64 number2 = long.MaxValue;
                this.AssertEqualOperator(number1, number2);
            }

            {
                Number64 number1 = decimal.MaxValue;
                Number64 number2 = decimal.MaxValue;
                this.AssertEqualOperator(number1, number2);
            }

            {
                Number64 number1 = 123.1d;
                Number64 number2 = 123.1d;
                this.AssertEqualOperator(number1, number2);
            }

            {
                Number64 number1 = 123.1m;
                Number64 number2 = 123.1m;
                this.AssertEqualOperator(number1, number2);
            }

            {
                Number64 number1 = 123.1m;
                Number64 number2 = 123.1m;
                this.AssertEqualOperator(number1, number2);
            }

            {
                Number64 number1 = 123;
                Number64 number2 = 123;
                this.AssertEqualOperator(number1, number2);
            }
        }

        private void AssertLessThanOperator(Number64 number1, Number64 number2)
        {
            Assert.IsTrue(number1 < number2);
            Assert.IsTrue(number1 <= number2);

            Assert.IsFalse(number1 > number2);
            Assert.IsFalse(number1 >= number2);
            Assert.IsFalse(number1 == number2);
        }

        private void AssertGreaterThanOperator(Number64 number1, Number64 number2)
        {
            Assert.IsTrue(number1 > number2);
            Assert.IsTrue(number1 >= number2);

            Assert.IsFalse(number1 < number2);
            Assert.IsFalse(number1 <= number2);
            Assert.IsFalse(number1 == number2);

        }

        private void AssertEqualOperator(Number64 number1, Number64 number2)
        {
            Assert.IsTrue(number1 == number2);
            Assert.IsTrue(number1 <= number2);
            Assert.IsTrue(number1 >= number2);

            Assert.IsFalse(number1 < number2);
            Assert.IsFalse(number1 > number2);
        }
    }
}
