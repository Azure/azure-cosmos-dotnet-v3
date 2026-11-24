//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using Microsoft.Azure.Documents;

    internal static class SerializationFormatUtil
    {
        private const SupportedSerializationFormats DefaultFormats =
            SupportedSerializationFormats.JsonText | SupportedSerializationFormats.CosmosBinary;

        private const SupportedSerializationFeatures DefaultFeatures =
            SupportedSerializationFeatures.Base64Strings
            | SupportedSerializationFeatures.NumberArrays
            | SupportedSerializationFeatures.UInt64;

        internal const string BinaryFormatWithSupportedFeatures = nameof(SupportedSerializationFormats.CosmosBinary)
            + "+" + nameof(SupportedSerializationFeatures.Base64Strings)
            + "+" + nameof(SupportedSerializationFeatures.NumberArrays)
            + "+" + nameof(SupportedSerializationFeatures.UInt64);

        // Example: JsonText,CosmosBinary+Base64Strings+NumberArrays+UInt64
        internal static readonly string DefaultSupportedSerializationFormats =
            string.Join(",", SupportedSerializationFormats.JsonText, BinaryFormatWithSupportedFeatures);

        internal static string GetSupportedSerializationFormatsAndFeatures(
            SupportedSerializationFormats? formats = null,
            SupportedSerializationFeatures? features = null)
        {
            if (!formats.HasValue && !features.HasValue)
            {
                return DefaultSupportedSerializationFormats;
            }

            return FormatSupportedSerializationFormatsAndFeatures(
                formats ?? DefaultFormats,
                features ?? DefaultFeatures);
        }

        private static string FormatSupportedSerializationFormatsAndFeatures(
            SupportedSerializationFormats formats,
            SupportedSerializationFeatures features)
        {
            string formatsText = formats.ToString().Replace(", ", ",");

            if ((formats & SupportedSerializationFormats.CosmosBinary) == 0
                || features == SupportedSerializationFeatures.None)
            {
                return formatsText;
            }

            string binaryFormatWithFeatures =
                nameof(SupportedSerializationFormats.CosmosBinary)
                + "+" + features.ToString().Replace(", ", "+");

            return formatsText.Replace(nameof(SupportedSerializationFormats.CosmosBinary), binaryFormatWithFeatures);
        }
    }
}
