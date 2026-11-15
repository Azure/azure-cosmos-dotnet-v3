//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using Newtonsoft.Json;

    /// <summary> 
    /// Encapsulates error related details in the Azure Cosmos DB service.
    /// </summary>
#if COSMOSCLIENT
    internal
#else
    public
#endif
    class Error : Resource
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="T:Microsoft.Azure.Documents.Error"/> class for the Azure Cosmos DB service.
        /// </summary>
        public Error()
        {

        }

        /// <summary>
        /// Gets or sets the textual description of error code in the Azure Cosmos DB service.
        /// </summary>
        /// <value>The textual description of error code.</value>
        [JsonProperty(PropertyName = Constants.Properties.Code)]
        public string Code
        {
            get
            {
                return base.GetValue<string>(Constants.Properties.Code);
            }
            set
            {
                base.SetValue(Constants.Properties.Code, value);
            }
        }

        /// <summary>
        /// Gets or sets the error message in the Azure Cosmos DB service.
        /// </summary>
        /// <value>The error message.</value>
        [JsonProperty(PropertyName = Constants.Properties.Message)]
        public string Message
        {
            get
            {
                return base.GetValue<string>(Constants.Properties.Message);
            }
            set
            {
                base.SetValue(Constants.Properties.Message, value);
            }
        }

        [JsonProperty(PropertyName = Constants.Properties.ErrorDetails)]
        internal string ErrorDetails //Debug Only.
        {
            get
            {
                return base.GetValue<string>(Constants.Properties.ErrorDetails);
            }
            set
            {
                base.SetValue(Constants.Properties.ErrorDetails, value);
            }
        }

        [JsonProperty(PropertyName = Constants.Properties.AdditionalErrorInfo)]
        internal string AdditionalErrorInfo
        {
            get
            {
                return base.GetValue<string>(Constants.Properties.AdditionalErrorInfo);
            }
            set
            {
                base.SetValue(Constants.Properties.AdditionalErrorInfo, value);
            }
        }
    }
}