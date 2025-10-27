//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
#if NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Tests.Transformation
{
    using System.Collections.Generic;
    using System.Text;
    using System.Text.Json;
    using Microsoft.Azure.Cosmos.Encryption.Custom.Transformation;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CandidatePathsTests
    {
        [TestMethod]
        public void Build_WithEmptyCollection_ReturnsEmptyCandidatePaths()
        {
            // Arrange
            List<string> emptyPaths = new List<string>();

            // Act
            CandidatePaths candidates = CandidatePaths.Build(emptyPaths);

            // Assert - Try matching a property, should not match
            string json = "{\"test\":\"value\"}";
            byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
            Utf8JsonReader reader = new Utf8JsonReader(jsonBytes);
            
            // Read to property name
            reader.Read(); // StartObject
            reader.Read(); // PropertyName "test"
            
            bool matched = candidates.TryMatch(ref reader, Encoding.UTF8.GetByteCount("test"), out string matchedPath);
            Assert.IsFalse(matched);
            Assert.IsNull(matchedPath);
        }

        [TestMethod]
        public void Build_WithNullCollection_ReturnsEmptyCandidatePaths()
        {
            // Act
            CandidatePaths candidates = CandidatePaths.Build(null);

            // Assert - Try matching a property, should not match
            string json = "{\"test\":\"value\"}";
            byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
            Utf8JsonReader reader = new Utf8JsonReader(jsonBytes);
            
            // Read to property name
            reader.Read(); // StartObject
            reader.Read(); // PropertyName "test"
            
            bool matched = candidates.TryMatch(ref reader, Encoding.UTF8.GetByteCount("test"), out string matchedPath);
            Assert.IsFalse(matched);
            Assert.IsNull(matchedPath);
        }

        [TestMethod]
        public void Build_FiltersOutInvalidPaths()
        {
            // Arrange - include various invalid paths
            List<string> paths = new List<string>
            {
                null,               // null path
                "",                 // empty path
                "noSlash",          // missing leading slash
                "/",                // root only (special case, treated separately)
                "/nested/path",     // nested path (contains second slash)
                "/validPath"        // valid path
            };

            // Act
            CandidatePaths candidates = CandidatePaths.Build(paths);

            // Assert - only "/validPath" should be in candidates
            string json = "{\"validPath\":1,\"noSlash\":2,\"nested\":3}";
            byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
            Utf8JsonReader reader = new Utf8JsonReader(jsonBytes);
            
            reader.Read(); // StartObject
            
            // Check "validPath" matches
            reader.Read(); // PropertyName "validPath"
            bool matched = candidates.TryMatch(ref reader, Encoding.UTF8.GetByteCount("validPath"), out string matchedPath);
            Assert.IsTrue(matched);
            Assert.AreEqual("/validPath", matchedPath);
            
            reader.Read(); // Number value
            
            // Check "noSlash" doesn't match (was filtered)
            reader.Read(); // PropertyName "noSlash"
            matched = candidates.TryMatch(ref reader, Encoding.UTF8.GetByteCount("noSlash"), out matchedPath);
            Assert.IsFalse(matched);
            
            reader.Read(); // Number value
            
            // Check "nested" doesn't match (nested path was filtered)
            reader.Read(); // PropertyName "nested"
            matched = candidates.TryMatch(ref reader, Encoding.UTF8.GetByteCount("nested"), out matchedPath);
            Assert.IsFalse(matched);
        }

        [TestMethod]
        public void TryMatch_WithRootPath_ReturnsIncludesEmptyFullPath()
        {
            // Arrange - include root path "/"
            List<string> paths = new List<string> { "/" };
            CandidatePaths candidates = CandidatePaths.Build(paths);

            // Act - try matching with nameLen = 0 (root path case)
            string json = "{\"\":\"emptyPropertyName\"}";
            byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
            Utf8JsonReader reader = new Utf8JsonReader(jsonBytes);
            
            reader.Read(); // StartObject
            reader.Read(); // PropertyName "" (empty string)
            
            bool matched = candidates.TryMatch(ref reader, 0, out string matchedPath);

            // Assert
            Assert.IsTrue(matched);
            Assert.AreEqual("/", matchedPath);
        }

        [TestMethod]
        public void TryMatch_WithValidPath_ReturnsTrue()
        {
            // Arrange
            List<string> paths = new List<string> { "/secret", "/data" };
            CandidatePaths candidates = CandidatePaths.Build(paths);

            string json = "{\"secret\":\"value\",\"notSecret\":123}";
            byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
            Utf8JsonReader reader = new Utf8JsonReader(jsonBytes);
            
            reader.Read(); // StartObject
            
            // Act - check "secret" matches
            reader.Read(); // PropertyName "secret"
            bool matched = candidates.TryMatch(ref reader, Encoding.UTF8.GetByteCount("secret"), out string matchedPath);
            
            // Assert
            Assert.IsTrue(matched);
            Assert.AreEqual("/secret", matchedPath);
            
            reader.Read(); // String value
            
            // Act - check "notSecret" doesn't match
            reader.Read(); // PropertyName "notSecret"
            matched = candidates.TryMatch(ref reader, Encoding.UTF8.GetByteCount("notSecret"), out matchedPath);
            
            // Assert
            Assert.IsFalse(matched);
            Assert.IsNull(matchedPath);
        }

        [TestMethod]
        public void Build_DeduplicatesPaths()
        {
            // Arrange - include duplicate paths
            List<string> paths = new List<string> { "/path1", "/path2", "/path1", "/path2" };
            
            // Act
            CandidatePaths candidates = CandidatePaths.Build(paths);
            
            // Assert - both unique paths should still match
            string json = "{\"path1\":1,\"path2\":2}";
            byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
            Utf8JsonReader reader = new Utf8JsonReader(jsonBytes);
            
            reader.Read(); // StartObject
            reader.Read(); // PropertyName "path1"
            
            bool matched = candidates.TryMatch(ref reader, Encoding.UTF8.GetByteCount("path1"), out string matchedPath);
            Assert.IsTrue(matched);
            Assert.AreEqual("/path1", matchedPath);
        }
    }
}
#endif
