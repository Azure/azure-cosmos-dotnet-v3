using System;
using Microsoft.Azure.Cosmos;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Azure.Cosmos.Tests
{
    [TestClass]
    public class PatchPathHelperTests
    {
        [TestMethod]
        public void TestProcessPath_ShortNumericString_NoChange()
        {
            // Arrange
            var path = "/strings/123456789";
            
            // Act
            var result = PatchPathHelper.ProcessPath(path);
            
            // Assert
            Assert.AreEqual("/strings/123456789", result);
        }

        [TestMethod]
        public void TestProcessPath_LongNumericString_Escaped()
        {
            // Arrange
            var path = "/strings/12345678901234567890";
            
            // Act
            var result = PatchPathHelper.ProcessPath(path);
            
            // Assert
            Assert.AreEqual("/strings/[\"12345678901234567890\"]", result);
        }

        [TestMethod]
        public void TestProcessPath_VeryLongNumericString_Escaped()
        {
            // Arrange
            var path = "/strings/123456789012345678901234567890";
            
            // Act
            var result = PatchPathHelper.ProcessPath(path);
            
            // Assert
            Assert.AreEqual("/strings/[\"123456789012345678901234567890\"]", result);
        }

        [TestMethod]
        public void TestProcessPath_MixedAlphaNumeric_NoChange()
        {
            // Arrange
            var path = "/strings/abc123456789012345678901234567890def";
            
            // Act
            var result = PatchPathHelper.ProcessPath(path);
            
            // Assert
            Assert.AreEqual("/strings/abc123456789012345678901234567890def", result);
        }

        [TestMethod]
        public void TestProcessPath_MultipleSegments_OnlyNumericEscaped()
        {
            // Arrange
            var path = "/strings/12345678901234567890/nested/123456789";
            
            // Act
            var result = PatchPathHelper.ProcessPath(path);
            
            // Assert
            Assert.AreEqual("/strings/[\"12345678901234567890\"]/nested/123456789", result);
        }

        [TestMethod]
        public void TestProcessPath_EmptyPath_NoChange()
        {
            // Arrange
            var path = "";
            
            // Act
            var result = PatchPathHelper.ProcessPath(path);
            
            // Assert
            Assert.AreEqual("", result);
        }

        [TestMethod]
        public void TestProcessPath_NullPath_NoChange()
        {
            // Arrange
            string path = null;
            
            // Act
            var result = PatchPathHelper.ProcessPath(path);
            
            // Assert
            Assert.AreEqual(null, result);
        }

        [TestMethod]
        public void TestProcessPath_RootPath_NoChange()
        {
            // Arrange
            var path = "/";
            
            // Act
            var result = PatchPathHelper.ProcessPath(path);
            
            // Assert
            Assert.AreEqual("/", result);
        }

        [TestMethod]
        public void TestProcessPath_SingleSegment_Escaped()
        {
            // Arrange
            var path = "/12345678901234567890";
            
            // Act
            var result = PatchPathHelper.ProcessPath(path);
            
            // Assert
            Assert.AreEqual("/[\"12345678901234567890\"]", result);
        }
    }
}