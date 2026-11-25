//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Test class for <see cref="SerializationFormatUtil"/>
    /// </summary>
    [TestClass]
    public class SerializationFormatUtilTests
    {
        [TestMethod]
        public void GetSupportedSerializationFormatsAndFeatures_AllowsOmittedFormats()
        {
            Assert.AreEqual(
                "JsonText,CosmosBinary+Base64Strings+NumberArrays+UInt64",
                SerializationFormatUtil.GetSupportedSerializationFormatsAndFeatures());
            Assert.AreEqual(
                "JsonText, CosmosBinary+UInt64",
                SerializationFormatUtil.GetSupportedSerializationFormatsAndFeatures(features: SupportedSerializationFeatures.UInt64));
            Assert.AreEqual(
                "JsonText, CosmosBinary",
                SerializationFormatUtil.GetSupportedSerializationFormatsAndFeatures(features: SupportedSerializationFeatures.None));
        }

        [TestMethod]
        [DataRow((int)SupportedSerializationFormats.JsonText, "JsonText")]
        [DataRow((int)SupportedSerializationFormats.HybridRow, "HybridRow")]
        [DataRow((int)SupportedSerializationFormats.CosmosBinary, "CosmosBinary+Base64Strings+NumberArrays+UInt64")]
        [DataRow((int)(SupportedSerializationFormats.JsonText | SupportedSerializationFormats.CosmosBinary), "JsonText, CosmosBinary+Base64Strings+NumberArrays+UInt64")]
        [DataRow((int)(SupportedSerializationFormats.CosmosBinary | SupportedSerializationFormats.HybridRow), "CosmosBinary+Base64Strings+NumberArrays+UInt64, HybridRow")]
        [DataRow((int)(SupportedSerializationFormats.JsonText | SupportedSerializationFormats.CosmosBinary | SupportedSerializationFormats.HybridRow), "JsonText, CosmosBinary+Base64Strings+NumberArrays+UInt64, HybridRow")]
        public void GetSupportedSerializationFormatsAndFeatures_AdvertisesBinaryFeaturesOnlyForBinary(
            int formats,
            string expected)
        {
            Assert.AreEqual(expected, SerializationFormatUtil.GetSupportedSerializationFormatsAndFeatures((SupportedSerializationFormats)formats));
        }

        [TestMethod]
        [DataRow(null, null, "JsonText,CosmosBinary+Base64Strings+NumberArrays+UInt64")]
        [DataRow(null, (int)SupportedSerializationFeatures.None, "JsonText, CosmosBinary")]
        [DataRow(null, (int)SupportedSerializationFeatures.NumberArrays, "JsonText, CosmosBinary+NumberArrays")]
        [DataRow((int)SupportedSerializationFormats.CosmosBinary, (int)SupportedSerializationFeatures.None, "CosmosBinary")]
        [DataRow((int)SupportedSerializationFormats.CosmosBinary, (int)SupportedSerializationFeatures.Base64Strings, "CosmosBinary+Base64Strings")]
        [DataRow((int)SupportedSerializationFormats.CosmosBinary, (int)SupportedSerializationFeatures.NumberArrays, "CosmosBinary+NumberArrays")]
        [DataRow((int)SupportedSerializationFormats.CosmosBinary, (int)SupportedSerializationFeatures.UInt64, "CosmosBinary+UInt64")]
        [DataRow((int)SupportedSerializationFormats.CosmosBinary, (int)(SupportedSerializationFeatures.Base64Strings | SupportedSerializationFeatures.NumberArrays), "CosmosBinary+Base64Strings+NumberArrays")]
        [DataRow((int)SupportedSerializationFormats.CosmosBinary, (int)(SupportedSerializationFeatures.Base64Strings | SupportedSerializationFeatures.UInt64), "CosmosBinary+Base64Strings+UInt64")]
        [DataRow((int)SupportedSerializationFormats.CosmosBinary, (int)(SupportedSerializationFeatures.NumberArrays | SupportedSerializationFeatures.UInt64), "CosmosBinary+NumberArrays+UInt64")]
        [DataRow((int)SupportedSerializationFormats.CosmosBinary, (int)(SupportedSerializationFeatures.Base64Strings | SupportedSerializationFeatures.NumberArrays | SupportedSerializationFeatures.UInt64), "CosmosBinary+Base64Strings+NumberArrays+UInt64")]
        [DataRow((int)(SupportedSerializationFormats.CosmosBinary | SupportedSerializationFormats.HybridRow), (int)SupportedSerializationFeatures.None, "CosmosBinary, HybridRow")]
        [DataRow((int)SupportedSerializationFormats.JsonText, (int)SupportedSerializationFeatures.UInt64, "JsonText")]
        [DataRow((int)SupportedSerializationFormats.HybridRow, (int)SupportedSerializationFeatures.Base64Strings, "HybridRow")]
        [DataRow((int)SupportedSerializationFormats.None, (int)SupportedSerializationFeatures.NumberArrays, "None")]
        public void GetSupportedSerializationFormatsAndFeatures_HonorsFeatureSelection(
            int? formats,
            int? features,
            string expected)
        {
            Assert.AreEqual(expected, SerializationFormatUtil.GetSupportedSerializationFormatsAndFeatures(
                (SupportedSerializationFormats?)formats,
                (SupportedSerializationFeatures?)features));
        }
    }
}
