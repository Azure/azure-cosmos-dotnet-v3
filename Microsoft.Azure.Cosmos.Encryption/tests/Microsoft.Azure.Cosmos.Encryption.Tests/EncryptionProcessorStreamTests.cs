//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.IO;
    using System.Runtime.Serialization;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class EncryptionProcessorStreamTests
    {
        private static EncryptionSettings CreateSettingsWithNoProperties()
        {
            // Create an EncryptionSettings instance without invoking its private constructor
            // and set PropertiesToEncrypt to an empty enumerable so no work is performed.
            object settings = FormatterServices.GetUninitializedObject(typeof(EncryptionSettings));

            var prop = typeof(EncryptionSettings).GetField("<PropertiesToEncrypt>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            prop.SetValue(settings, Array.Empty<string>());

            return (EncryptionSettings)settings;
        }

        private static MemoryStream ToStream(string json)
        {
            return new MemoryStream(Encoding.UTF8.GetBytes(json), writable: false);
        }

        private static string ReadToEnd(Stream s)
        {
            using var sr = new StreamReader(s, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
            return sr.ReadToEnd();
        }

        [TestMethod]
        public async Task EncryptAsync_Disposes_Input_And_Returns_New_Stream()
        {
            // Arrange
            var input = ToStream("{\"id\":\"abc\",\"p\":1}");
            var settings = CreateSettingsWithNoProperties();

            // Act
            Stream result = await EncryptionProcessor.EncryptAsync(input, settings, operationDiagnostics: null, cancellationToken: CancellationToken.None);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreNotSame(input, result, "EncryptAsync should return a different stream instance.");

            // Input should be disposed
            Assert.ThrowsException<ObjectDisposedException>(() => input.ReadByte());

            // Result should be readable and contain the same JSON (no properties to encrypt)
            string output = ReadToEnd(result);
            Assert.IsTrue(output.Contains("\"id\":\"abc\""), "Output JSON should contain original content when no properties are encrypted.");
        }

        [TestMethod]
        public async Task DecryptAsync_Disposes_Input_And_Returns_New_Stream_When_Not_Null()
        {
            // Arrange
            var input = ToStream("{\"id\":\"abc\",\"p\":1}");
            var settings = CreateSettingsWithNoProperties();

            // Act
            Stream result = await EncryptionProcessor.DecryptAsync(input, settings, operationDiagnostics: null, cancellationToken: CancellationToken.None);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreNotSame(input, result, "DecryptAsync should return a different stream instance.");
            Assert.ThrowsException<ObjectDisposedException>(() => input.ReadByte());

            string output = ReadToEnd(result);
            Assert.IsTrue(output.Contains("\"id\":\"abc\""), "Output JSON should be preserved when no properties are decrypted.");
        }

        [TestMethod]
        public async Task DecryptAsync_Returns_Null_When_Input_Null()
        {
            // Act
            Stream result = await EncryptionProcessor.DecryptAsync(input: null, encryptionSettings: null, operationDiagnostics: null, cancellationToken: CancellationToken.None);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task DecryptAsync_Input_Stream_Is_Seekable()
        {
            // Arrange: MemoryStream is seekable by default, satisfying the DEBUG assert in DecryptAsync.
            using var input = new MemoryStream(Encoding.UTF8.GetBytes("{\"id\":\"abc\"}"));
            Assert.IsTrue(input.CanSeek, "Test precondition: input stream must be seekable.");

            var settings = CreateSettingsWithNoProperties();

            // Act
            using Stream result = await EncryptionProcessor.DecryptAsync(input, settings, operationDiagnostics: null, cancellationToken: CancellationToken.None);

            // Assert
            Assert.IsNotNull(result);
        }

    // Note: DecryptAsync asserts that input.CanSeek is true in DEBUG builds.
    // We avoid invoking it with a non-seekable stream as that would terminate the test host.
    }
}
