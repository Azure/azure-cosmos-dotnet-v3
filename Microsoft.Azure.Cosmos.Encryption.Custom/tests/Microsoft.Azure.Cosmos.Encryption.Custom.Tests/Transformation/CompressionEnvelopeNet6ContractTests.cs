//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

#if !NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Tests.Transformation
{
    using System.Reflection;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CompressionEnvelopeNet6ContractTests
    {
        [TestMethod]
        public void CompressionEnvelopePrimitives_AreAbsent()
        {
            Assembly assembly = typeof(EncryptionOptions).Assembly;
            string[] typeNames =
            {
                "Microsoft.Azure.Cosmos.Encryption.Custom.Transformation.BrotliCompressorAdapter",
                "Microsoft.Azure.Cosmos.Encryption.Custom.Transformation.BufferWriterCopyingStream",
                "Microsoft.Azure.Cosmos.Encryption.Custom.Transformation.CompressionCodecId",
                "Microsoft.Azure.Cosmos.Encryption.Custom.Transformation.CompressionHelper",
                "Microsoft.Azure.Cosmos.Encryption.Custom.Transformation.CompressionLevelSetting",
                "Microsoft.Azure.Cosmos.Encryption.Custom.Transformation.CompressionSettings",
                "Microsoft.Azure.Cosmos.Encryption.Custom.Transformation.DecodedEnvelope",
                "Microsoft.Azure.Cosmos.Encryption.Custom.Transformation.DecompressionBudget",
                "Microsoft.Azure.Cosmos.Encryption.Custom.Transformation.DeflateCompressorAdapter",
                "Microsoft.Azure.Cosmos.Encryption.Custom.Transformation.EnvelopeHeader",
                "Microsoft.Azure.Cosmos.Encryption.Custom.Transformation.EnvelopeHeaderConstants",
                "Microsoft.Azure.Cosmos.Encryption.Custom.Transformation.EnvelopeHeaderReader",
                "Microsoft.Azure.Cosmos.Encryption.Custom.Transformation.EnvelopeHeaderWriter",
                "Microsoft.Azure.Cosmos.Encryption.Custom.Transformation.ICompressionCodecAdapter",
                "Microsoft.Azure.Cosmos.Encryption.Custom.Transformation.UleB128",
                "Microsoft.Azure.Cosmos.Encryption.Custom.Transformation.V4CompressionEnvelope",
            };

            foreach (string typeName in typeNames)
            {
                Assert.IsNull(assembly.GetType(typeName), typeName);
            }
        }
    }
}
#endif
