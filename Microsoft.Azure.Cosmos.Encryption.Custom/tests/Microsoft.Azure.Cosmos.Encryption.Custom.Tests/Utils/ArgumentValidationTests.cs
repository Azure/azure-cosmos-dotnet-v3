//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom.Tests.Utils
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ArgumentValidationTests
    {
        #region ThrowIfNull Tests

        [TestMethod]
        public void ThrowIfNull_WithNullArgument_ThrowsArgumentNullException()
        {
            // Arrange
            object nullObject = null;

            // Act & Assert
            ArgumentNullException ex = Assert.ThrowsException<ArgumentNullException>(() =>
                ArgumentValidation.ThrowIfNull(nullObject, "testParam"));

            Assert.AreEqual("testParam", ex.ParamName);
        }

        [TestMethod]
        public void ThrowIfNull_WithNonNullArgument_DoesNotThrow()
        {
            // Arrange
            object nonNullObject = new object();

            // Act & Assert - should not throw
            ArgumentValidation.ThrowIfNull(nonNullObject, "testParam");
        }

        [TestMethod]
        public void ThrowIfNull_WithNullArgumentAndNoParamName_ThrowsArgumentNullException()
        {
            // Arrange
            object nullObject = null;

            // Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() =>
                ArgumentValidation.ThrowIfNull(nullObject));
        }

        #endregion

        #region ThrowIfNullOrEmpty Tests

        [TestMethod]
        public void ThrowIfNullOrEmpty_WithNullString_ThrowsArgumentNullException()
        {
            // Arrange
            string nullString = null;

            // Act & Assert
            ArgumentNullException ex = Assert.ThrowsException<ArgumentNullException>(() =>
                ArgumentValidation.ThrowIfNullOrEmpty(nullString, "testParam"));

            Assert.AreEqual("testParam", ex.ParamName);
        }

        [TestMethod]
        public void ThrowIfNullOrEmpty_WithEmptyString_ThrowsArgumentException()
        {
            // Arrange
            string emptyString = string.Empty;

            // Act & Assert
            ArgumentException ex = Assert.ThrowsException<ArgumentException>(() =>
                ArgumentValidation.ThrowIfNullOrEmpty(emptyString, "testParam"));

            Assert.AreEqual("testParam", ex.ParamName);
            Assert.IsTrue(ex.Message.Contains("cannot be an empty string") || ex.Message.Contains("empty"));
        }

        [TestMethod]
        public void ThrowIfNullOrEmpty_WithValidString_DoesNotThrow()
        {
            // Arrange
            string validString = "validValue";

            // Act & Assert - should not throw
            ArgumentValidation.ThrowIfNullOrEmpty(validString, "testParam");
        }

        [TestMethod]
        public void ThrowIfNullOrEmpty_WithWhitespaceString_DoesNotThrow()
        {
            // Arrange
            string whitespaceString = "   ";

            // Act & Assert - should not throw (ThrowIfNullOrEmpty allows whitespace)
            ArgumentValidation.ThrowIfNullOrEmpty(whitespaceString, "testParam");
        }

        #endregion

        #region ThrowIfNullOrWhiteSpace Tests

        [TestMethod]
        public void ThrowIfNullOrWhiteSpace_WithNullString_ThrowsArgumentNullException()
        {
            // Arrange
            string nullString = null;

            // Act & Assert
            ArgumentNullException ex = Assert.ThrowsException<ArgumentNullException>(() =>
                ArgumentValidation.ThrowIfNullOrWhiteSpace(nullString, "testParam"));

            Assert.AreEqual("testParam", ex.ParamName);
        }

        [TestMethod]
        public void ThrowIfNullOrWhiteSpace_WithEmptyString_ThrowsArgumentException()
        {
            // Arrange
            string emptyString = string.Empty;

            // Act & Assert
            ArgumentException ex = Assert.ThrowsException<ArgumentException>(() =>
                ArgumentValidation.ThrowIfNullOrWhiteSpace(emptyString, "testParam"));

            Assert.AreEqual("testParam", ex.ParamName);
        }

        [TestMethod]
        public void ThrowIfNullOrWhiteSpace_WithWhitespaceString_ThrowsArgumentException()
        {
            // Arrange
            string whitespaceString = "   ";

            // Act & Assert
            ArgumentException ex = Assert.ThrowsException<ArgumentException>(() =>
                ArgumentValidation.ThrowIfNullOrWhiteSpace(whitespaceString, "testParam"));

            Assert.AreEqual("testParam", ex.ParamName);
            Assert.IsTrue(ex.Message.Contains("whitespace") || ex.Message.Contains("empty"));
        }

        [TestMethod]
        public void ThrowIfNullOrWhiteSpace_WithTabAndNewlineString_ThrowsArgumentException()
        {
            // Arrange
            string whitespaceString = "\t\n\r";

            // Act & Assert
            ArgumentException ex = Assert.ThrowsException<ArgumentException>(() =>
                ArgumentValidation.ThrowIfNullOrWhiteSpace(whitespaceString, "testParam"));

            Assert.AreEqual("testParam", ex.ParamName);
        }

        [TestMethod]
        public void ThrowIfNullOrWhiteSpace_WithValidString_DoesNotThrow()
        {
            // Arrange
            string validString = "validValue";

            // Act & Assert - should not throw
            ArgumentValidation.ThrowIfNullOrWhiteSpace(validString, "testParam");
        }

        [TestMethod]
        public void ThrowIfNullOrWhiteSpace_WithStringContainingWhitespace_DoesNotThrow()
        {
            // Arrange
            string validString = "  valid  ";

            // Act & Assert - should not throw (contains non-whitespace characters)
            ArgumentValidation.ThrowIfNullOrWhiteSpace(validString, "testParam");
        }

        #endregion

        #region ThrowIfNegative Tests

        [TestMethod]
        public void ThrowIfNegative_WithNegativeValue_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            int negativeValue = -1;

            // Act & Assert
            ArgumentOutOfRangeException ex = Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                ArgumentValidation.ThrowIfNegative(negativeValue, "testParam"));

            Assert.AreEqual("testParam", ex.ParamName);
            Assert.IsTrue(ex.Message.Contains("non-negative") || ex.Message.Contains("negative"));
        }

        [TestMethod]
        public void ThrowIfNegative_WithLargeNegativeValue_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            int negativeValue = int.MinValue;

            // Act & Assert
            ArgumentOutOfRangeException ex = Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                ArgumentValidation.ThrowIfNegative(negativeValue, "testParam"));

            Assert.AreEqual("testParam", ex.ParamName);
        }

        [TestMethod]
        public void ThrowIfNegative_WithZero_DoesNotThrow()
        {
            // Arrange
            int zeroValue = 0;

            // Act & Assert - should not throw
            ArgumentValidation.ThrowIfNegative(zeroValue, "testParam");
        }

        [TestMethod]
        public void ThrowIfNegative_WithPositiveValue_DoesNotThrow()
        {
            // Arrange
            int positiveValue = 42;

            // Act & Assert - should not throw
            ArgumentValidation.ThrowIfNegative(positiveValue, "testParam");
        }

        [TestMethod]
        public void ThrowIfNegative_WithMaxValue_DoesNotThrow()
        {
            // Arrange
            int maxValue = int.MaxValue;

            // Act & Assert - should not throw
            ArgumentValidation.ThrowIfNegative(maxValue, "testParam");
        }

        #endregion

        #region ThrowIfGreaterThan Tests

        [TestMethod]
        public void ThrowIfGreaterThan_WithValueGreaterThanOther_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            int value = 10;
            int other = 5;

            // Act & Assert
            ArgumentOutOfRangeException ex = Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                ArgumentValidation.ThrowIfGreaterThan(value, other, "testParam"));

            Assert.AreEqual("testParam", ex.ParamName);
            Assert.IsTrue(ex.Message.Contains("less than or equal") || ex.Message.Contains("greater"));
        }

        [TestMethod]
        public void ThrowIfGreaterThan_WithValueEqualToOther_DoesNotThrow()
        {
            // Arrange
            int value = 5;
            int other = 5;

            // Act & Assert - should not throw
            ArgumentValidation.ThrowIfGreaterThan(value, other, "testParam");
        }

        [TestMethod]
        public void ThrowIfGreaterThan_WithValueLessThanOther_DoesNotThrow()
        {
            // Arrange
            int value = 3;
            int other = 5;

            // Act & Assert - should not throw
            ArgumentValidation.ThrowIfGreaterThan(value, other, "testParam");
        }

        [TestMethod]
        public void ThrowIfGreaterThan_WithNegativeValues_WorksCorrectly()
        {
            // Arrange & Act & Assert - should throw
            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                ArgumentValidation.ThrowIfGreaterThan(-1, -5, "testParam"));

            // Should not throw
            ArgumentValidation.ThrowIfGreaterThan(-10, -5, "testParam");
            ArgumentValidation.ThrowIfGreaterThan(-5, -5, "testParam");
        }

        [TestMethod]
        public void ThrowIfGreaterThan_WithBoundaryValues_WorksCorrectly()
        {
            // Arrange & Act & Assert
            // Should throw when value > other
            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                ArgumentValidation.ThrowIfGreaterThan(int.MaxValue, int.MaxValue - 1, "testParam"));

            // Should not throw when value <= other
            ArgumentValidation.ThrowIfGreaterThan(int.MinValue, int.MaxValue, "testParam");
            ArgumentValidation.ThrowIfGreaterThan(int.MaxValue, int.MaxValue, "testParam");
        }

        #endregion

        #region Edge Cases and Integration Tests

        [TestMethod]
        public void AllMethods_WithNullParamName_DoNotThrow()
        {
            // Test that all methods work without param name
            ArgumentValidation.ThrowIfNull(new object());
            ArgumentValidation.ThrowIfNullOrEmpty("valid");
            ArgumentValidation.ThrowIfNullOrWhiteSpace("valid");
            ArgumentValidation.ThrowIfNegative(0);
            ArgumentValidation.ThrowIfGreaterThan(5, 10);
        }

        [TestMethod]
        public void ThrowIfNull_WithDifferentTypes_WorksCorrectly()
        {
            // Test with various reference types
            ArgumentValidation.ThrowIfNull("string", "param1");
            ArgumentValidation.ThrowIfNull(new int[] { 1, 2, 3 }, "param2");
            ArgumentValidation.ThrowIfNull(new ArgumentException(), "param3");

            // Test that null of different types throws
            Assert.ThrowsException<ArgumentNullException>(() =>
                ArgumentValidation.ThrowIfNull((string)null, "param1"));
            Assert.ThrowsException<ArgumentNullException>(() =>
                ArgumentValidation.ThrowIfNull((int[])null, "param2"));
            Assert.ThrowsException<ArgumentNullException>(() =>
                ArgumentValidation.ThrowIfNull((Exception)null, "param3"));
        }

        [TestMethod]
        public void StringValidations_WithUnicodeCharacters_WorkCorrectly()
        {
            // Valid Unicode string should not throw
            ArgumentValidation.ThrowIfNullOrEmpty("Hello 世界");
            ArgumentValidation.ThrowIfNullOrWhiteSpace("こんにちは");

            // Unicode whitespace should be caught
            string unicodeWhitespace = "\u00A0\u2003"; // Non-breaking space and em space
            Assert.ThrowsException<ArgumentException>(() =>
                ArgumentValidation.ThrowIfNullOrWhiteSpace(unicodeWhitespace, "testParam"));
        }

        #endregion

        #region Behavioral Verification Tests - Proves Validation Actually Works

        [TestMethod]
        public void ThrowIfNull_ActuallyPreventsNullFromBeingUsed()
        {
            // This test proves that the validation actually prevents null usage
            // not just that it throws an exception
            
            string testValue = null;
            bool validationWorked = false;

            try
            {
                ArgumentValidation.ThrowIfNull(testValue, nameof(testValue));
                // If we get here, validation failed - null was not caught
                Assert.Fail("Validation should have thrown ArgumentNullException for null value");
            }
            catch (ArgumentNullException ex)
            {
                // Validation worked - verify the exception details are correct
                Assert.AreEqual(nameof(testValue), ex.ParamName);
                validationWorked = true;
            }

            Assert.IsTrue(validationWorked, "Validation must throw ArgumentNullException for null values");
        }

        [TestMethod]
        public void ThrowIfNullOrEmpty_ActuallyDistinguishesBetweenNullAndEmpty()
        {
            // Verify that null throws ArgumentNullException (not ArgumentException)
            string nullValue = null;
            try
            {
                ArgumentValidation.ThrowIfNullOrEmpty(nullValue, "nullValue");
                Assert.Fail("Should have thrown ArgumentNullException for null");
            }
            catch (ArgumentNullException ex)
            {
                // Correct - null should throw ArgumentNullException
                Assert.AreEqual("nullValue", ex.ParamName);
                Assert.IsFalse(ex is ArgumentException && ex is not ArgumentNullException, 
                    "Null should throw ArgumentNullException, not ArgumentException");
            }

            // Verify that empty throws ArgumentException (not ArgumentNullException)
            string emptyValue = string.Empty;
            try
            {
                ArgumentValidation.ThrowIfNullOrEmpty(emptyValue, "emptyValue");
                Assert.Fail("Should have thrown ArgumentException for empty string");
            }
            catch (ArgumentException ex) when (ex is not ArgumentNullException)
            {
                // Correct - empty should throw ArgumentException but not ArgumentNullException
                Assert.AreEqual("emptyValue", ex.ParamName);
            }
            catch (ArgumentNullException)
            {
                Assert.Fail("Empty string should throw ArgumentException, not ArgumentNullException");
            }
        }

        [TestMethod]
        public void ThrowIfNullOrWhiteSpace_ActuallyValidatesWhitespace()
        {
            // Verify various whitespace characters are detected
            string[] whitespaceStrings = new[]
            {
                " ",           // Single space
                "  ",          // Multiple spaces
                "\t",          // Tab
                "\n",          // Newline
                "\r",          // Carriage return
                "\r\n",        // Windows line ending
                " \t\n\r ",    // Mixed whitespace
                "\u00A0",      // Non-breaking space
                "\u2003"       // Em space
            };

            foreach (string whitespace in whitespaceStrings)
            {
                try
                {
                    ArgumentValidation.ThrowIfNullOrWhiteSpace(whitespace, "whitespace");
                    Assert.Fail($"Whitespace '{whitespace.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t")}' should have been caught");
                }
                catch (ArgumentException ex)
                {
                    // Correct - whitespace was caught
                    Assert.AreEqual("whitespace", ex.ParamName);
                }
            }

            // Verify that strings with actual content pass
            string[] validStrings = new[]
            {
                "a",
                " a ",         // Content with surrounding whitespace
                "hello",
                "hello world",
                "123"
            };

            foreach (string valid in validStrings)
            {
                // Should not throw
                ArgumentValidation.ThrowIfNullOrWhiteSpace(valid, "valid");
            }
        }

        [TestMethod]
        public void ThrowIfNegative_ActuallyChecksSign()
        {
            // Test boundary between negative and non-negative
            int justNegative = -1;
            int zero = 0;
            int justPositive = 1;

            // -1 should throw
            try
            {
                ArgumentValidation.ThrowIfNegative(justNegative, "negative");
                Assert.Fail("-1 should be considered negative");
            }
            catch (ArgumentOutOfRangeException ex)
            {
                Assert.AreEqual("negative", ex.ParamName);
                Assert.AreEqual(justNegative, ex.ActualValue);
            }

            // 0 should NOT throw (zero is non-negative)
            ArgumentValidation.ThrowIfNegative(zero, "zero");

            // 1 should NOT throw
            ArgumentValidation.ThrowIfNegative(justPositive, "positive");

            // Test extreme values
            try
            {
                ArgumentValidation.ThrowIfNegative(int.MinValue, "minValue");
                Assert.Fail("int.MinValue should be considered negative");
            }
            catch (ArgumentOutOfRangeException ex)
            {
                Assert.AreEqual("minValue", ex.ParamName);
            }

            ArgumentValidation.ThrowIfNegative(int.MaxValue, "maxValue");
        }

        [TestMethod]
        public void ThrowIfGreaterThan_ActuallyComparesValues()
        {
            // Test exact boundary conditions
            int value = 10;
            int lowerBound = 5;
            int equalBound = 10;
            int higherBound = 15;

            // value > lowerBound should throw (10 > 5)
            try
            {
                ArgumentValidation.ThrowIfGreaterThan(value, lowerBound, "value");
                Assert.Fail("10 > 5 should throw");
            }
            catch (ArgumentOutOfRangeException ex)
            {
                Assert.AreEqual("value", ex.ParamName);
                Assert.AreEqual(value, ex.ActualValue);
                Assert.IsTrue(ex.Message.Contains(lowerBound.ToString()), 
                    "Exception message should mention the comparison value");
            }

            // value == equalBound should NOT throw (10 <= 10)
            ArgumentValidation.ThrowIfGreaterThan(value, equalBound, "value");

            // value < higherBound should NOT throw (10 < 15)
            ArgumentValidation.ThrowIfGreaterThan(value, higherBound, "value");
        }

        [TestMethod]
        public void ValidationMethods_ProveTheyDontAcceptInvalidValues()
        {
            // This test proves that validation methods reject invalid inputs
            // and accept valid inputs - testing the actual contract
            
            int invalidCallsBlocked = 0;
            int validCallsAllowed = 0;

            // ThrowIfNull - should block null (1 invalid)
            try 
            { 
                ArgumentValidation.ThrowIfNull(null, "test"); 
                Assert.Fail("ThrowIfNull should have thrown for null");
            }
            catch (ArgumentNullException) 
            { 
                invalidCallsBlocked++; 
            }
            
            ArgumentValidation.ThrowIfNull("not null", "test");
            validCallsAllowed++;

            // ThrowIfNullOrEmpty - should block null and empty (2 invalid)
            try 
            { 
                ArgumentValidation.ThrowIfNullOrEmpty(null, "test"); 
                Assert.Fail("ThrowIfNullOrEmpty should have thrown for null");
            }
            catch (ArgumentNullException) 
            { 
                invalidCallsBlocked++; 
            }
            
            try 
            { 
                ArgumentValidation.ThrowIfNullOrEmpty("", "test"); 
                Assert.Fail("ThrowIfNullOrEmpty should have thrown for empty");
            }
            catch (ArgumentException ex) when (ex.GetType() == typeof(ArgumentException))
            { 
                invalidCallsBlocked++; 
            }
            catch (ArgumentNullException)
            {
                Assert.Fail("Empty string should throw ArgumentException, not ArgumentNullException");
            }
            
            ArgumentValidation.ThrowIfNullOrEmpty("valid", "test");
            validCallsAllowed++;

            // ThrowIfNullOrWhiteSpace - should block null, empty, and whitespace (3 invalid)
            try 
            { 
                ArgumentValidation.ThrowIfNullOrWhiteSpace(null, "test"); 
                Assert.Fail("ThrowIfNullOrWhiteSpace should have thrown for null");
            }
            catch (ArgumentNullException) 
            { 
                invalidCallsBlocked++; 
            }
            
            try 
            { 
                ArgumentValidation.ThrowIfNullOrWhiteSpace("", "test"); 
                Assert.Fail("ThrowIfNullOrWhiteSpace should have thrown for empty");
            }
            catch (ArgumentException ex) when (ex.GetType() == typeof(ArgumentException))
            { 
                invalidCallsBlocked++; 
            }
            catch (ArgumentNullException)
            {
                Assert.Fail("Empty string should throw ArgumentException, not ArgumentNullException");
            }
            
            try 
            { 
                ArgumentValidation.ThrowIfNullOrWhiteSpace("   ", "test"); 
                Assert.Fail("ThrowIfNullOrWhiteSpace should have thrown for whitespace");
            }
            catch (ArgumentException ex) when (ex.GetType() == typeof(ArgumentException))
            { 
                invalidCallsBlocked++; 
            }
            catch (ArgumentNullException)
            {
                Assert.Fail("Whitespace string should throw ArgumentException, not ArgumentNullException");
            }
            
            ArgumentValidation.ThrowIfNullOrWhiteSpace("valid", "test");
            validCallsAllowed++;

            // ThrowIfNegative - should block negative (1 invalid)
            try 
            { 
                ArgumentValidation.ThrowIfNegative(-1, "test"); 
                Assert.Fail("ThrowIfNegative should have thrown for -1");
            }
            catch (ArgumentOutOfRangeException) 
            { 
                invalidCallsBlocked++; 
            }
            
            ArgumentValidation.ThrowIfNegative(0, "test");
            ArgumentValidation.ThrowIfNegative(1, "test");
            validCallsAllowed += 2;

            // ThrowIfGreaterThan - should block when value > other (1 invalid)
            try 
            { 
                ArgumentValidation.ThrowIfGreaterThan(10, 5, "test"); 
                Assert.Fail("ThrowIfGreaterThan should have thrown for 10 > 5");
            }
            catch (ArgumentOutOfRangeException) 
            { 
                invalidCallsBlocked++; 
            }
            
            ArgumentValidation.ThrowIfGreaterThan(5, 5, "test");
            ArgumentValidation.ThrowIfGreaterThan(5, 10, "test");
            validCallsAllowed += 2;

            // Verify all invalid calls were blocked and all valid calls were allowed
            // Expected: 1 (null) + 2 (null+empty) + 3 (null+empty+whitespace) + 1 (negative) + 1 (greater) = 8 total invalid
            Assert.AreEqual(8, invalidCallsBlocked, "All invalid calls must be blocked");
            Assert.AreEqual(7, validCallsAllowed, "All valid calls must be allowed");
        }

        #endregion

        #region ThrowIfNegative TimeSpan Tests

        [TestMethod]
        public void ThrowIfNegative_TimeSpan_WithNegativeValue_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            TimeSpan negativeValue = TimeSpan.FromMinutes(-5);

            // Act & Assert
            ArgumentOutOfRangeException ex = Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                ArgumentValidation.ThrowIfNegative(negativeValue, "testParam"));

            Assert.AreEqual("testParam", ex.ParamName);
            Assert.IsTrue(ex.Message.Contains("non-negative") || ex.Message.Contains("negative"));
        }

        [TestMethod]
        public void ThrowIfNegative_TimeSpan_WithMinValue_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            TimeSpan minValue = TimeSpan.MinValue;

            // Act & Assert
            ArgumentOutOfRangeException ex = Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                ArgumentValidation.ThrowIfNegative(minValue, "testParam"));

            Assert.AreEqual("testParam", ex.ParamName);
        }

        [TestMethod]
        public void ThrowIfNegative_TimeSpan_WithZero_DoesNotThrow()
        {
            // Arrange
            TimeSpan zeroValue = TimeSpan.Zero;

            // Act & Assert - should not throw
            ArgumentValidation.ThrowIfNegative(zeroValue, "testParam");
        }

        [TestMethod]
        public void ThrowIfNegative_TimeSpan_WithPositiveValue_DoesNotThrow()
        {
            // Arrange
            TimeSpan positiveValue = TimeSpan.FromMinutes(30);

            // Act & Assert - should not throw
            ArgumentValidation.ThrowIfNegative(positiveValue, "testParam");
        }

        [TestMethod]
        public void ThrowIfNegative_TimeSpan_WithMaxValue_DoesNotThrow()
        {
            // Arrange
            TimeSpan maxValue = TimeSpan.MaxValue;

            // Act & Assert - should not throw
            ArgumentValidation.ThrowIfNegative(maxValue, "testParam");
        }

        [TestMethod]
        public void ThrowIfNegative_TimeSpan_WithVariousNegativeValues_ThrowsConsistently()
        {
            // Test various negative TimeSpan values
            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                ArgumentValidation.ThrowIfNegative(TimeSpan.FromMilliseconds(-1), "test"));
            
            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                ArgumentValidation.ThrowIfNegative(TimeSpan.FromSeconds(-1), "test"));
            
            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                ArgumentValidation.ThrowIfNegative(TimeSpan.FromHours(-1), "test"));
            
            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                ArgumentValidation.ThrowIfNegative(TimeSpan.FromDays(-1), "test"));
        }

        #endregion

        #region ThrowIfGreaterThanOrEqual TimeSpan Tests

        [TestMethod]
        public void ThrowIfGreaterThanOrEqual_TimeSpan_WithValueGreaterThanOther_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            TimeSpan value = TimeSpan.FromMinutes(60);
            TimeSpan other = TimeSpan.FromMinutes(30);

            // Act & Assert
            ArgumentOutOfRangeException ex = Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                ArgumentValidation.ThrowIfGreaterThanOrEqual(value, other, "testParam"));

            Assert.AreEqual("testParam", ex.ParamName);
            Assert.IsTrue(ex.Message.Contains("must be less than") || ex.Message.Contains("greater"));
            Assert.IsTrue(ex.Message.Contains("00:60:00") || ex.Message.Contains("01:00:00")); // Value in message
        }

        [TestMethod]
        public void ThrowIfGreaterThanOrEqual_TimeSpan_WithValueEqualToOther_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            TimeSpan value = TimeSpan.FromMinutes(30);
            TimeSpan other = TimeSpan.FromMinutes(30);

            // Act & Assert - equal values should throw (GreaterThanOrEqual)
            ArgumentOutOfRangeException ex = Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                ArgumentValidation.ThrowIfGreaterThanOrEqual(value, other, "testParam"));

            Assert.AreEqual("testParam", ex.ParamName);
        }

        [TestMethod]
        public void ThrowIfGreaterThanOrEqual_TimeSpan_WithValueLessThanOther_DoesNotThrow()
        {
            // Arrange
            TimeSpan value = TimeSpan.FromMinutes(25);
            TimeSpan other = TimeSpan.FromMinutes(30);

            // Act & Assert - should not throw
            ArgumentValidation.ThrowIfGreaterThanOrEqual(value, other, "testParam");
        }

        [TestMethod]
        public void ThrowIfGreaterThanOrEqual_TimeSpan_WithZeroValues_WorksCorrectly()
        {
            // Zero equals Zero - should throw
            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                ArgumentValidation.ThrowIfGreaterThanOrEqual(TimeSpan.Zero, TimeSpan.Zero, "test"));

            // Negative less than Zero - should not throw
            ArgumentValidation.ThrowIfGreaterThanOrEqual(TimeSpan.FromMinutes(-1), TimeSpan.Zero, "test");

            // Positive greater than Zero - should throw
            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                ArgumentValidation.ThrowIfGreaterThanOrEqual(TimeSpan.FromMinutes(1), TimeSpan.Zero, "test"));
        }

        [TestMethod]
        public void ThrowIfGreaterThanOrEqual_TimeSpan_WithBoundaryValues_WorksCorrectly()
        {
            // Max >= Max - should throw
            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                ArgumentValidation.ThrowIfGreaterThanOrEqual(TimeSpan.MaxValue, TimeSpan.MaxValue, "test"));

            // Min >= Max - should not throw (Min < Max)
            ArgumentValidation.ThrowIfGreaterThanOrEqual(TimeSpan.MinValue, TimeSpan.MaxValue, "test");

            // Max-1 < Max - should not throw
            ArgumentValidation.ThrowIfGreaterThanOrEqual(
                TimeSpan.MaxValue - TimeSpan.FromTicks(1), 
                TimeSpan.MaxValue, 
                "test");
        }

        [TestMethod]
        public void ThrowIfGreaterThanOrEqual_TimeSpan_RealWorldScenario_ProactiveRefreshValidation()
        {
            // Simulate real-world scenario: proactiveRefreshThreshold < dekPropertiesTimeToLive
            TimeSpan dekTtl = TimeSpan.FromMinutes(120);

            // Valid: 119 minutes < 120 minutes
            ArgumentValidation.ThrowIfGreaterThanOrEqual(TimeSpan.FromMinutes(119), dekTtl, "proactiveRefreshThreshold");

            // Invalid: 120 minutes >= 120 minutes
            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                ArgumentValidation.ThrowIfGreaterThanOrEqual(TimeSpan.FromMinutes(120), dekTtl, "proactiveRefreshThreshold"));

            // Invalid: 121 minutes >= 120 minutes
            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                ArgumentValidation.ThrowIfGreaterThanOrEqual(TimeSpan.FromMinutes(121), dekTtl, "proactiveRefreshThreshold"));
        }

        [TestMethod]
        public void ThrowIfGreaterThanOrEqual_TimeSpan_WithDifferentUnits_ComparesCorrectly()
        {
            // Test that comparison works correctly across different time units
            TimeSpan oneHour = TimeSpan.FromHours(1);
            TimeSpan sixtyMinutes = TimeSpan.FromMinutes(60);
            TimeSpan thirtyMinutes = TimeSpan.FromMinutes(30);
            TimeSpan ninetyMinutes = TimeSpan.FromMinutes(90);

            // Equal values (1 hour = 60 minutes) - should throw
            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                ArgumentValidation.ThrowIfGreaterThanOrEqual(oneHour, sixtyMinutes, "test"));

            // 30 minutes < 1 hour - should not throw
            ArgumentValidation.ThrowIfGreaterThanOrEqual(thirtyMinutes, oneHour, "test");

            // 90 minutes > 1 hour - should throw
            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                ArgumentValidation.ThrowIfGreaterThanOrEqual(ninetyMinutes, oneHour, "test"));
        }

        #endregion

        #region TimeSpan Methods Integration Tests

        [TestMethod]
        public void TimeSpanValidations_WithNullParamName_DoNotThrow()
        {
            // Test that TimeSpan methods work without param name
            ArgumentValidation.ThrowIfNegative(TimeSpan.Zero);
            ArgumentValidation.ThrowIfNegative(TimeSpan.FromMinutes(1));
            ArgumentValidation.ThrowIfGreaterThanOrEqual(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10));
        }

        [TestMethod]
        public void TimeSpanValidations_AllInvalidCasesThrow()
        {
            int thrownCount = 0;

            // ThrowIfNegative should throw
            try
            {
                ArgumentValidation.ThrowIfNegative(TimeSpan.FromMinutes(-1), "test");
                Assert.Fail("Should have thrown for negative TimeSpan");
            }
            catch (ArgumentOutOfRangeException)
            {
                thrownCount++;
            }

            // ThrowIfGreaterThanOrEqual should throw for equal
            try
            {
                ArgumentValidation.ThrowIfGreaterThanOrEqual(
                    TimeSpan.FromMinutes(30), 
                    TimeSpan.FromMinutes(30), 
                    "test");
                Assert.Fail("Should have thrown for equal TimeSpan values");
            }
            catch (ArgumentOutOfRangeException)
            {
                thrownCount++;
            }

            // ThrowIfGreaterThanOrEqual should throw for greater
            try
            {
                ArgumentValidation.ThrowIfGreaterThanOrEqual(
                    TimeSpan.FromMinutes(60), 
                    TimeSpan.FromMinutes(30), 
                    "test");
                Assert.Fail("Should have thrown for greater TimeSpan value");
            }
            catch (ArgumentOutOfRangeException)
            {
                thrownCount++;
            }

            Assert.AreEqual(3, thrownCount, "All invalid TimeSpan validations should throw");
        }

        #endregion
    }
}
