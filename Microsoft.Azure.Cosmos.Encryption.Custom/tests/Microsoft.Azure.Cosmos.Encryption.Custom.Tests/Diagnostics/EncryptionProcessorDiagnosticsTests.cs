#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Custom.Tests.Diagnostics
{
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System;
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
            Assert.IsTrue(System.Linq.Enumerable.Any(ctx.Scopes, s => s.StartsWith("EncryptionProcessor.Decrypt.SelectProcessor.Newtonsoft")), string.Join(";", ctx.Scopes));
        }

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
            Assert.IsTrue(System.Linq.Enumerable.Any(ctx.Scopes, s => s.StartsWith("EncryptionProcessor.Decrypt.StreamingProvidedOutput.SelectProcessor.Newtonsoft")), string.Join(";", ctx.Scopes));
        }

        [TestMethod]
        public async Task DecryptAsync_StreamOverride_SelectsStreamScope()
        {
#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
            // Empty object: no encryption properties => streaming decrypt returns original input but scope emitted.
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
            Assert.IsTrue(System.Linq.Enumerable.Any(ctx.Scopes, s => s.StartsWith("EncryptionProcessor.Decrypt.SelectProcessor.Stream")), string.Join(";", ctx.Scopes));
#else
            Assert.Inconclusive("Stream override not compiled");
#endif
        }

    [TestMethod]
    public async Task DecryptAsync_ProvidedOutput_StreamScope()
    {
#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
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
        Assert.IsTrue(System.Linq.Enumerable.Any(ctx.Scopes, s => s.StartsWith("EncryptionProcessor.Decrypt.StreamingProvidedOutput.SelectProcessor.Stream")), string.Join(";", ctx.Scopes));
#else
        Assert.Inconclusive("Stream override not compiled");
#endif
    }
    }
}
#endif
