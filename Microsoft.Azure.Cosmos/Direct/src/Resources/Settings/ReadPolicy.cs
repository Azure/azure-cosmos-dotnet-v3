//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using Newtonsoft.Json;

    internal sealed class ReadPolicy : JsonSerializable
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
        [JsonProperty(PropertyName = Constants.Properties.PrimaryReadCoefficient)]
        public int PrimaryReadCoefficient
        {
            get
            {
                return base.GetValue<int>(Constants.Properties.PrimaryReadCoefficient, ReadPolicy.DefaultPrimaryReadCoefficient);
            }
            set
            {
                base.SetValue(Constants.Properties.PrimaryReadCoefficient, value);
            }
        }

        /// <summary>
        /// Relative weight of secondary to serve read requests. Higher the value, it is preferred to issue reads to secondary.
        /// Direct connectivity client can use this value to dynamically decide where to send reads to effectively use the service.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.SecondaryReadCoefficient)]
        public int SecondaryReadCoefficient
        {
            get
            {
                return base.GetValue<int>(Constants.Properties.SecondaryReadCoefficient, ReadPolicy.DefaultSecondaryReadCoefficient);
            }
            set
            {
                base.SetValue(Constants.Properties.SecondaryReadCoefficient, value);
            }
        }
    }
}
