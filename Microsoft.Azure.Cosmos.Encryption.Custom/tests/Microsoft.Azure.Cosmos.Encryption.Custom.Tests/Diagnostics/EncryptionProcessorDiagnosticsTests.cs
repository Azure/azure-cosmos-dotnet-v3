namespace Microsoft.Azure.Cosmos.Encryption.Custom.Tests.Diagnostics
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class EncryptionProcessorDiagnosticsTests
    {
        private sealed class NoopEncryptor : Encryptor
        {
            public override Task<DataEncryptionKey> GetEncryptionKeyAsync(string dataEncryptionKeyId, string encryptionAlgorithm, CancellationToken cancellationToken = default) => Task.FromResult<DataEncryptionKey>(null);
            public override Task<byte[]> EncryptAsync(byte[] plainText, string dataEncryptionKeyId, string encryptionAlgorithm, CancellationToken cancellationToken = default) => Task.FromResult(plainText);
            public override Task<byte[]> DecryptAsync(byte[] cipherText, string dataEncryptionKeyId, string encryptionAlgorithm, CancellationToken cancellationToken = default) => Task.FromResult(cipherText);
        }

        private static Encryptor CreateNoopEncryptor() => new NoopEncryptor();

        [TestMethod]
        public async Task DecryptAsync_SelectsNewtonsoftScopeByDefault()
        {
            // _ei present but null so encryption properties are treated as absent. Ensures early return without decrypt.
            string json = "{\"_ei\":null}";
            using MemoryStream input = new MemoryStream(Encoding.UTF8.GetBytes(json));
            Encryptor encryptor = CreateNoopEncryptor();
            CosmosDiagnosticsContext ctx = CosmosDiagnosticsContext.Create(null);
            try
            {
                await EncryptionProcessor.DecryptAsync(input, encryptor, ctx, requestOptions: null, CancellationToken.None);
            }
            catch (NotSupportedException)
            {
            }
            AssertScopePresent(ctx, "EncryptionProcessor.Decrypt.Newtonsoft");
            AssertScopeAbsent(ctx, "EncryptionProcessor.Decrypt.Stream");
            AssertScopeAbsent(ctx, "EncryptionProcessor.DecryptStreamImpl.Mde");
        }

    #if NET8_0_OR_GREATER
    [TestMethod]
        public async Task DecryptAsync_ProvidedOutput_NewtonsoftScope()
        {
            string json = "{\"_ei\":null}";
            using MemoryStream input = new MemoryStream(Encoding.UTF8.GetBytes(json));
            MemoryStream output = new MemoryStream();
            Encryptor encryptor = CreateNoopEncryptor();
            CosmosDiagnosticsContext ctx = CosmosDiagnosticsContext.Create(null);
            try
            {
                _ = await EncryptionProcessor.DecryptAsync(input, output, encryptor, ctx, null, CancellationToken.None);
            }
            catch (NotSupportedException)
            {
            }
            AssertScopePresent(ctx, "EncryptionProcessor.Decrypt.Newtonsoft");
            AssertScopeAbsent(ctx, "EncryptionProcessor.Decrypt.Stream");
            AssertScopeAbsent(ctx, "EncryptionProcessor.DecryptStreamImpl.Mde"); // Newtonsoft path shouldn't include impl scope
        }

        [TestMethod]
        public async Task DecryptAsync_StreamOverride_SelectsStreamScope()
        {
            string json = "{\"_ei\":null}";
            using MemoryStream input = new MemoryStream(Encoding.UTF8.GetBytes(json));
            Encryptor encryptor = CreateNoopEncryptor();
            CosmosDiagnosticsContext ctx = CosmosDiagnosticsContext.Create(null);
            ItemRequestOptions options = new ItemRequestOptions { Properties = new System.Collections.Generic.Dictionary<string, object>{{"encryption-json-processor", JsonProcessor.Stream}}};
            try
            {
                await EncryptionProcessor.DecryptAsync(input, encryptor, ctx, options, CancellationToken.None);
            }
            catch (NotSupportedException)
            {
            }
            AssertScopePresent(ctx, "EncryptionProcessor.Decrypt.Stream");
            AssertScopeAbsent(ctx, "EncryptionProcessor.Decrypt.Newtonsoft");
            AssertScopePresent(ctx, "EncryptionProcessor.DecryptStreamImpl.Mde");
        }

        [TestMethod]
        public async Task DecryptAsync_ProvidedOutput_StreamScope()
        {
            string json = "{\"_ei\":null}";
            using MemoryStream input = new MemoryStream(Encoding.UTF8.GetBytes(json));
            MemoryStream output = new MemoryStream();
            Encryptor encryptor = CreateNoopEncryptor();
            CosmosDiagnosticsContext ctx = CosmosDiagnosticsContext.Create(null);
            ItemRequestOptions options = new ItemRequestOptions { Properties = new System.Collections.Generic.Dictionary<string, object>{{"encryption-json-processor", JsonProcessor.Stream}}};
            try
            {
                _ = await EncryptionProcessor.DecryptAsync(input, output, encryptor, ctx, options, CancellationToken.None);
            }
            catch (NotSupportedException)
            {
            }
            AssertScopePresent(ctx, "EncryptionProcessor.Decrypt.Stream");
            AssertScopeAbsent(ctx, "EncryptionProcessor.Decrypt.Newtonsoft");
            AssertScopePresent(ctx, "EncryptionProcessor.DecryptStreamImpl.Mde");
        }
    #endif

        private static void AssertScopePresent(CosmosDiagnosticsContext ctx, string prefix)
        {
            Assert.IsTrue(ctx.Scopes.Any(s => s.StartsWith(prefix, StringComparison.Ordinal)), $"Expected scope prefix '{prefix}' not found. Scopes: {string.Join(';', ctx.Scopes)}");
        }

        private static void AssertScopeAbsent(CosmosDiagnosticsContext ctx, string prefix)
        {
            Assert.IsFalse(ctx.Scopes.Any(s => s.StartsWith(prefix, StringComparison.Ordinal)), $"Did not expect scope prefix '{prefix}'. Scopes: {string.Join(';', ctx.Scopes)}");
        }
    }
}
