//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;

    internal static class Constants
    {
        public const ushort DefaultDekRefreshFrequencyAsPercentageOfTtl = 10;
        public const string DocumentsResourcePropertyName = "Documents";
        public const string EncryptedData = "_ed";
        public const string EncryptedInfo = "_ei";
        public const string EncryptionAlgorithm = "_ea";
        public const string EncryptionDekId = "_en";
        public const string EncryptionFormatVersion = "_ef";
        public static readonly TimeSpan DefaultDekPropertiesTimeToLive = TimeSpan.FromHours(2);
    }
}