using System;
using System.Collections.Generic;
using Microsoft.Azure.Cosmos;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Azure.Cosmos.Tests
{
    [TestClass]
    public class PatchOperationNumericPathTests
    {
        [TestMethod]
        public void TestLongNumericStringInPath()
        {
            // Test that creating a patch operation with a long numeric-looking string works
            var longNumericString = "12345678901234567890"; // 20 characters
            var operation = PatchOperation.Add($"/strings/{longNumericString}", "value");
            
            Assert.AreEqual($"/strings/{longNumericString}", operation.Path);
            Assert.AreEqual(PatchOperationType.Add, operation.OperationType);
        }

        [TestMethod]
        public void TestVeryLongNumericStringInPath()
        {
            // Test with an even longer numeric string
            var veryLongNumericString = "123456789012345678901234567890"; // 30 characters
            var operation = PatchOperation.Add($"/strings/{veryLongNumericString}", "value");
            
            Assert.AreEqual($"/strings/{veryLongNumericString}", operation.Path);
            Assert.AreEqual(PatchOperationType.Add, operation.OperationType);
        }

        [TestMethod]
        public void TestShortNumericStringInPath()
        {
            // Test with a short numeric string (should work fine)
            var shortNumericString = "123456789"; // 9 characters
            var operation = PatchOperation.Add($"/strings/{shortNumericString}", "value");
            
            Assert.AreEqual($"/strings/{shortNumericString}", operation.Path);
            Assert.AreEqual(PatchOperationType.Add, operation.OperationType);
        }

        [TestMethod]
        public void TestMixedAlphaNumericStringInPath()
        {
            // Test with mixed alphanumeric string (should work fine)
            var mixedString = "abc123456789012345678901234567890def";
            var operation = PatchOperation.Add($"/strings/{mixedString}", "value");
            
            Assert.AreEqual($"/strings/{mixedString}", operation.Path);
            Assert.AreEqual(PatchOperationType.Add, operation.OperationType);
        }
    }
}