using System;
using System.Collections.Generic;
using Microsoft.Azure.Cosmos;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Azure.Cosmos.Tests
{
    /// <summary>
    /// Tests for the fix to handle long numeric strings in patch operation paths
    /// </summary>
    [TestClass]
    public class PatchOperationLongNumericPathTests
    {
        [TestMethod]
        public void TestLongNumericStringInPath_EscapedCorrectly()
        {
            // Test the exact scenario from the issue
            var longNumericString = "12345678901234567890"; // 20 characters - should be escaped
            var processedPath = PatchPathHelper.ProcessPath($"/strings/{longNumericString}");
            
            Assert.AreEqual($"/strings/[\"{longNumericString}\"]", processedPath);
        }

        [TestMethod]
        public void TestVeryLongNumericStringInPath_EscapedCorrectly()
        {
            // Test with an even longer numeric string
            var veryLongNumericString = "123456789012345678901234567890"; // 30 characters
            var processedPath = PatchPathHelper.ProcessPath($"/strings/{veryLongNumericString}");
            
            Assert.AreEqual($"/strings/[\"{veryLongNumericString}\"]", processedPath);
        }

        [TestMethod]
        public void TestShortNumericStringInPath_NotEscaped()
        {
            // Test with a short numeric string (should not be escaped)
            var shortNumericString = "123456789"; // 9 characters
            var processedPath = PatchPathHelper.ProcessPath($"/strings/{shortNumericString}");
            
            Assert.AreEqual($"/strings/{shortNumericString}", processedPath);
        }

        [TestMethod]
        public void TestBoundaryNumericStringInPath_NotEscaped()
        {
            // Test with a numeric string at the boundary (19 characters - should not be escaped)
            var boundaryNumericString = "1234567890123456789"; // 19 characters
            var processedPath = PatchPathHelper.ProcessPath($"/strings/{boundaryNumericString}");
            
            Assert.AreEqual($"/strings/{boundaryNumericString}", processedPath);
        }

        [TestMethod]
        public void TestMixedAlphaNumericStringInPath_NotEscaped()
        {
            // Test with mixed alphanumeric string (should not be escaped regardless of length)
            var mixedString = "abc123456789012345678901234567890def";
            var processedPath = PatchPathHelper.ProcessPath($"/strings/{mixedString}");
            
            Assert.AreEqual($"/strings/{mixedString}", processedPath);
        }

        [TestMethod]
        public void TestMultipleSegmentsWithLongNumeric_OnlyLongNumericEscaped()
        {
            // Test with multiple segments where only the long numeric one should be escaped
            var path = "/strings/12345678901234567890/nested/123456789";
            var processedPath = PatchPathHelper.ProcessPath(path);
            
            Assert.AreEqual("/strings/[\"12345678901234567890\"]/nested/123456789", processedPath);
        }

        [TestMethod]
        public void TestMultipleLongNumericSegments_AllEscaped()
        {
            // Test with multiple long numeric segments
            var path = "/data/12345678901234567890/items/987654321098765432109876543210";
            var processedPath = PatchPathHelper.ProcessPath(path);
            
            Assert.AreEqual("/data/[\"12345678901234567890\"]/items/[\"987654321098765432109876543210\"]", processedPath);
        }

        [TestMethod]
        public void TestPatchOperationsWithLongNumericPaths_CreatedSuccessfully()
        {
            // Test that patch operations can be created with long numeric paths
            var longNumericString = "12345678901234567890";
            
            var addOperation = PatchOperation.Add($"/strings/{longNumericString}", "test_value");
            var replaceOperation = PatchOperation.Replace($"/data/{longNumericString}", "new_value");
            var removeOperation = PatchOperation.Remove($"/items/{longNumericString}");
            
            Assert.AreEqual($"/strings/{longNumericString}", addOperation.Path);
            Assert.AreEqual($"/data/{longNumericString}", replaceOperation.Path);
            Assert.AreEqual($"/items/{longNumericString}", removeOperation.Path);
        }

        [TestMethod]
        public void TestEdgeCases_HandledCorrectly()
        {
            // Test edge cases
            Assert.AreEqual("", PatchPathHelper.ProcessPath(""));
            Assert.AreEqual("/", PatchPathHelper.ProcessPath("/"));
            Assert.AreEqual(null, PatchPathHelper.ProcessPath(null));
            Assert.AreEqual("/[\"12345678901234567890\"]", PatchPathHelper.ProcessPath("/12345678901234567890"));
        }

        [TestMethod]
        public void TestMoveOperationWithLongNumericPaths_BothPathsEscaped()
        {
            // Test that move operations escape both 'from' and 'path' when they contain long numeric strings
            var longNumericString1 = "12345678901234567890";
            var longNumericString2 = "98765432109876543210";
            
            var moveOperation = PatchOperation.Move($"/source/{longNumericString1}", $"/target/{longNumericString2}");
            
            Assert.AreEqual($"/target/{longNumericString2}", moveOperation.Path);
            Assert.AreEqual($"/source/{longNumericString1}", moveOperation.From);
            
            // Test that the helper processes both paths correctly
            var processedPath = PatchPathHelper.ProcessPath(moveOperation.Path);
            var processedFrom = PatchPathHelper.ProcessPath(moveOperation.From);
            
            Assert.AreEqual($"/target/[\"{longNumericString2}\"]", processedPath);
            Assert.AreEqual($"/source/[\"{longNumericString1}\"]", processedFrom);
        }
    }
}