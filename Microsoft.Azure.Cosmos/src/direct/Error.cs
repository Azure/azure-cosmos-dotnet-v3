//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System.Text.Json.Serialization;

    /// <summary> 
    /// Encapsulates error related details in the Azure Cosmos DB service.
    /// </summary>
#if COSMOSCLIENT
    internal
#else
    public
#endif
    class Error : PlainResource
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
        [JsonPropertyName(Constants.Properties.Code)]
        public string Code { get; set; }

        /// <summary>
        /// Gets or sets the error message in the Azure Cosmos DB service.
        /// </summary>
        /// <value>The error message.</value>
        [JsonPropertyName(Constants.Properties.Message)]
        public string Message { get; set; }

        [JsonPropertyName(Constants.Properties.ErrorDetails)]
        internal string ErrorDetails { get; set; }

        [JsonPropertyName(Constants.Properties.AdditionalErrorInfo)]
        internal string AdditionalErrorInfo { get; set; }
    }
}