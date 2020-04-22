//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Specialized;
    using System.Globalization;
    using System.IO;
    using System.Net;
    using System.Runtime.Serialization;
    using Microsoft.Azure.Cosmos.Scripts;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Newtonsoft.Json;

    /// <summary>
    /// Represents the response returned from a database stored procedure in the Azure Cosmos DB service. Wraps the response body and headers.
    /// </summary> 
    /// <typeparam name="TValue">The returned value type of the stored procedure.</typeparam>
    /// <remarks>
    /// Stored procedures can return any string output via the getContext().getResponse().setBody() method.
    /// This response body could be a serialized JSON object, or any other type.
    /// Within the .NET SDK, you can deserialize the response into a corresponding TValue type.
    /// </remarks>
    internal class StoredProcedureResponse<TValue> : IStoredProcedureResponse<TValue>
    {
        private DocumentServiceResponse response;
        private JsonSerializerSettings serializerSettings;

        /// <summary>
        /// Constructor exposed for mocking purposes in Azure Cosmos DB service.
        /// </summary>
        public StoredProcedureResponse()
        {
        }

        internal StoredProcedureResponse(DocumentServiceResponse response, JsonSerializerSettings serializerSettings = null)
        {
            this.response = response;
            this.serializerSettings = serializerSettings;

            if (typeof(TValue).IsSubclassOf(typeof(JsonSerializable)))
            {
                // load resource
                if (typeof(TValue) == typeof(Document) || typeof(Document).IsAssignableFrom(typeof(TValue)))
                {
                    this.Response = Documents.Resource.LoadFromWithConstructor<TValue>(response.ResponseBody, () => (TValue)(object)new Document(), this.serializerSettings);
                }
                else if (typeof(TValue) == typeof(Attachment) || typeof(Attachment).IsAssignableFrom(typeof(TValue)))
                {
                    this.Response = Documents.Resource.LoadFromWithConstructor<TValue>(response.ResponseBody, () => (TValue)(object)new Attachment(), this.serializerSettings);
                }
                else
                {
                    // sprocs should only have access to documents and attachments
                    throw new ArgumentException("Cannot serialize object if it is not document or attachment");
                }
            }
            else
            {
                // For empty response body use dummy stream and let the serialization take care of the rest,
                // so that e.g. for TValue = string that would be fine - null string, and for TValue = int -- an error.
                using (MemoryStream responseStream = new MemoryStream())
                using (StreamReader responseReader = new StreamReader(response.ResponseBody ?? responseStream))
                {
                    string responseString = responseReader.ReadToEnd();
                    try
                    {
                        this.Response = (TValue)JsonConvert.DeserializeObject(responseString, typeof(TValue), this.serializerSettings);
                    }
                    catch (JsonException ex)
                    {
                        // Don't expose JsonNewton exceptions to the user, convert to appropriate .net exception.
                        throw new SerializationException(
                            string.Format(CultureInfo.InvariantCulture, "Failed to deserialize stored procedure response or convert it to type '{0}': {1}", typeof(TValue).FullName, ex.Message),
                            ex);
                    }
                }
            }
        }

        /// <summary>
        /// Gets the Activity ID of the request from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The Activity ID of the request.
        /// </value>
        /// <remarks>Every request is traced with a globally unique ID. Include activity ID in tracing application failures and when contacting Azure Cosmos DB support</remarks>
        public string ActivityId
        {
            get
            {
                return this.response.Headers[HttpConstants.HttpHeaders.ActivityId];
            }
        }

        /// <summary>
        /// Gets the token for use with session consistency requests from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The token for use with session consistency requests.
        /// </value>
        public string SessionToken
        {
            get
            {
                return this.response.Headers[HttpConstants.HttpHeaders.SessionToken];
            }
        }

        /// <summary>
        /// Gets the output from stored procedure console.log() statements.
        /// </summary>
        /// <value>
        /// Output from console.log() statements in a stored procedure.
        /// </value>
        /// <seealso cref="StoredProcedureRequestOptions.EnableScriptLogging"/>
        public string ScriptLog
        {
            get
            {
                return Helpers.GetScriptLogHeader(this.response.Headers);
            }
        }

        /// <summary>
        /// Gets the request completion status code from the Azure Cosmos DB service.
        /// </summary>
        /// <value>The request completion status code</value>
        public HttpStatusCode StatusCode
        {
            get
            {
                return this.response.StatusCode;
            }
        }

        /// <summary>
        /// Gets the delimited string containing the quota of each resource type within the collection from the Azure Cosmos DB service.
        /// </summary>
        /// <value>The delimited string containing the number of used units per resource type within the collection.</value>
        public string MaxResourceQuota
        {
            get
            {
                return this.response.Headers[HttpConstants.HttpHeaders.MaxResourceQuota];
            }
        }

        /// <summary>
        /// Gets the delimited string containing the usage of each resource type within the collection from the Azure Cosmos DB service.
        /// </summary>
        /// <value>The delimited string containing the number of used units per resource type within the collection.</value>
        public string CurrentResourceQuotaUsage
        {
            get
            {
                return this.response.Headers[HttpConstants.HttpHeaders.CurrentResourceQuotaUsage];
            }
        }

        /// <summary>
        /// Gets the number of normalized Azure Cosmos DB request units (RUs) charged from Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The number of normalized Azure Cosmos DB request units (RUs) charged.
        /// </value>
        public double RequestCharge
        {
            get
            {
                return Helpers.GetHeaderValueDouble(
                    this.response.Headers,
                    HttpConstants.HttpHeaders.RequestCharge,
                    0);
            }
        }

        /// <summary>
        /// Gets the headers associated with the response from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// Headers associated with the response.
        /// </value>
        /// <remarks>
        /// Provides access to all HTTP response headers returned from the 
        /// Azure Cosmos DB API.
        /// </remarks>
        public NameValueCollection ResponseHeaders
        {
            get
            {
                return this.response.ResponseHeaders;
            }
        }

        internal INameValueCollection Headers
        {
            get { return this.response.Headers; }
        }

        /// <summary>
        /// Gets the response of a stored procedure, serialized into the given type from the Azure Cosmos DB service.
        /// </summary>
        /// <value>The response of a stored procedure, serialized into the given type.</value>
        public TValue Response { get; }

        /// <summary>
        /// Gets the clientside request statics for execution of stored procedure.
        /// </summary>
        /// <value>The clientside request statics for execution of stored procedure.</value>
        internal IClientSideRequestStatistics RequestStatistics
        {
            get
            {
                return this.response.RequestStats;
            }
        }

        /// <summary>
        /// Gets the resource implicitly from Azure Cosmos DB service.
        /// </summary>
        /// <param name="source">Stored procedure response.</param>
        /// <returns>The returned resource.</returns>
        public static implicit operator TValue(StoredProcedureResponse<TValue> source)
        {
            return source.Response;
        }
    }
}
