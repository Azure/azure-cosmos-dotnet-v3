//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// ReadPolicy for the account
    /// </summary>
    public sealed class ReadPolicy
    {
        private const int DefaultPrimaryReadCoefficient = 0;
        private const int DefaultSecondaryReadCoefficient = 1;
        /// <summary>
        /// Constructor.
        /// </summary>
        public ReadPolicy()
        {
        }

        /// <summary>
        /// Relative weight of primary to serve read requests. Higher the value, it is preferred to issue reads to primary.
        /// Direct connectivity client can use this value to dynamically decide where to send reads to effectively use the service.
        /// </summary>
        [JsonPropertyName(Constants.Properties.PrimaryReadCoefficient)]
        public int PrimaryReadCoefficient { get; set; } = ReadPolicy.DefaultPrimaryReadCoefficient;

        /// <summary>
        /// Relative weight of secondary to serve read requests. Higher the value, it is preferred to issue reads to secondary.
        /// Direct connectivity client can use this value to dynamically decide where to send reads to effectively use the service.
        /// </summary>
        [JsonPropertyName(Constants.Properties.SecondaryReadCoefficient)]
        public int SecondaryReadCoefficient { get; set; } = ReadPolicy.DefaultSecondaryReadCoefficient;
    }
}
