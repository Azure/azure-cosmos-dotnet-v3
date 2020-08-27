//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Net.Http.Headers;
    using System.Runtime.Serialization;
    using System.Text;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Documents.Collections;

    /// <summary>
    /// The base class for client exceptions in the Azure Cosmos DB service.
    /// </summary>
    [Serializable]
#if COSMOSCLIENT
    internal
#else
    public
#endif
    class DocumentClientException : Exception
    {
        private Error error;
        private SubStatusCodes? substatus = null;
        private INameValueCollection responseHeaders;

        internal DocumentClientException(Error errorResource,
            HttpResponseHeaders responseHeaders,
            HttpStatusCode? statusCode)
            : base(DocumentClientException.MessageWithActivityId(errorResource.Message, responseHeaders))
        {
            this.error = errorResource;
            this.responseHeaders = new DictionaryNameValueCollection();
            this.StatusCode = statusCode;

            if (responseHeaders != null)
            {
                foreach (KeyValuePair<string, IEnumerable<string>> header in responseHeaders)
                {
                    this.responseHeaders.Add(header.Key, string.Join(",", header.Value));
                }
            }

            // Stamp the activity ID if present. Exception throwers can override this if need be.
            if ((Trace.CorrelationManager.ActivityId != null) &&
                (this.responseHeaders.Get(HttpConstants.HttpHeaders.ActivityId) == null))
            {
                this.responseHeaders.Set(HttpConstants.HttpHeaders.ActivityId,
                                         Trace.CorrelationManager.ActivityId.ToString());
            }

            this.LSN = -1;
            this.PartitionKeyRangeId = null;
            if (this.StatusCode != HttpStatusCode.Gone)
            {
                DefaultTrace.TraceError(
                    "DocumentClientException with status code: {0}, message: {1}, and response headers: {2}",
                    this.StatusCode ?? 0,
                    errorResource.Message,
                    SerializeHTTPResponseHeaders(responseHeaders));
            }
        }

        internal DocumentClientException(string message,
            Exception innerException,
            HttpStatusCode? statusCode,
            Uri requestUri = null,
            string statusDescription = null)
            : this(DocumentClientException.MessageWithActivityId(message), innerException, (INameValueCollection)null, statusCode, requestUri)
        {
        }

        internal DocumentClientException(string message,
            Exception innerException,
            HttpResponseHeaders responseHeaders,
            HttpStatusCode? statusCode,
            Uri requestUri = null,
            SubStatusCodes? substatusCode = null)
            : base(DocumentClientException.MessageWithActivityId(message, responseHeaders), innerException)
        {
            this.responseHeaders = new DictionaryNameValueCollection();
            this.StatusCode = statusCode;
            this.substatus = substatusCode;
            if (this.substatus.HasValue)
            {
                this.responseHeaders[WFConstants.BackendHeaders.SubStatus] = ((int)this.substatus).ToString(CultureInfo.InvariantCulture);
            }

            if (responseHeaders != null)
            {
                foreach (KeyValuePair<string, IEnumerable<string>> header in responseHeaders)
                {
                    this.responseHeaders.Add(header.Key, string.Join(",", header.Value));
                }
            }

            // Stamp the ambient activity ID (if present) over the server's response ActivityId (if present).
            if (Trace.CorrelationManager.ActivityId != null)
            {
                this.responseHeaders.Set(HttpConstants.HttpHeaders.ActivityId,
                                         Trace.CorrelationManager.ActivityId.ToString());
            }

            this.RequestUri = requestUri;
            this.LSN = -1;
            this.PartitionKeyRangeId = null;

            if (this.StatusCode != HttpStatusCode.Gone)
            {
                DefaultTrace.TraceError(
                    "DocumentClientException with status code {0}, message: {1}, inner exception: {2}, and response headers: {3}",
                    this.StatusCode ?? 0,
                    message,
                    innerException != null ? innerException.ToString() : "null",
                    SerializeHTTPResponseHeaders(responseHeaders));
            }
        }

        internal DocumentClientException(string message,
            Exception innerException,
            INameValueCollection responseHeaders,
            HttpStatusCode? statusCode,
            SubStatusCodes? substatusCode,
            Uri requestUri = null)
            : this(message, innerException, responseHeaders, statusCode, requestUri)
        {
            this.substatus = substatusCode;
            this.responseHeaders[WFConstants.BackendHeaders.SubStatus] = ((int)this.substatus).ToString(CultureInfo.InvariantCulture);
        }

        internal DocumentClientException(string message,
            Exception innerException,
            INameValueCollection responseHeaders,
            HttpStatusCode? statusCode,
            Uri requestUri = null)
            : base(DocumentClientException.MessageWithActivityId(message, responseHeaders), innerException)
        {
            this.responseHeaders = new DictionaryNameValueCollection();
            this.StatusCode = statusCode;

            if (responseHeaders != null)
            {
                this.responseHeaders.Add(responseHeaders);
            }

            // Stamp the ambient activity ID (if present) over the server's response ActivityId (if present).
            if (Trace.CorrelationManager.ActivityId != null)
            {
                this.responseHeaders.Set(HttpConstants.HttpHeaders.ActivityId,
                                         Trace.CorrelationManager.ActivityId.ToString());
            }

            this.RequestUri = requestUri;
            this.LSN = -1;
            this.PartitionKeyRangeId = null;

            if (this.StatusCode != HttpStatusCode.Gone)
            {
                DefaultTrace.TraceError(
                    "DocumentClientException with status code {0}, message: {1}, inner exception: {2}, and response headers: {3}",
                    this.StatusCode ?? 0,
                    message,
                    innerException != null ? innerException.ToString() : "null",
                    SerializeHTTPResponseHeaders(responseHeaders));
            }
        }

        internal DocumentClientException(string message,
            HttpStatusCode statusCode,
            SubStatusCodes subStatusCode)
            : this(message, null, statusCode, null)
        {
            this.substatus = subStatusCode;
            this.responseHeaders[WFConstants.BackendHeaders.SubStatus] = ((int)this.substatus).ToString(CultureInfo.InvariantCulture);
        }

        // Deserialisation constructor.
#if !NETSTANDARD16
        internal DocumentClientException(SerializationInfo info, StreamingContext context, HttpStatusCode? statusCode)
            : base(info, context)
        {
            this.StatusCode = statusCode;
            this.LSN = -1;
            this.PartitionKeyRangeId = null;

            if (this.StatusCode != HttpStatusCode.Gone)
            {
                DefaultTrace.TraceError(
                    "DocumentClientException with status code {0}, and serialization info: {1}",
                    this.StatusCode ?? 0,
                    info.ToString());
            }
        }
#endif

        /// <summary>
        /// Gets the error code associated with the exception in the Azure Cosmos DB service.
        /// </summary>
        public Error Error
        {
            get
            {
                if (this.error == null)
                {
                    this.error = new Error
                    {
                        Code = this.StatusCode.ToString(),
                        Message = this.Message
                    };
                }

                return this.error;
            }

            internal set
            {
                this.error = value;
            }
        }

        /// <summary>
        /// Gets the activity ID associated with the request from the Azure Cosmos DB service.
        /// </summary>
        public string ActivityId
        {
            get
            {
                if (this.responseHeaders != null)
                {
                    return this.responseHeaders[HttpConstants.HttpHeaders.ActivityId];
                }

                return null;
            }
        }

        /// <summary>
        /// Gets the recommended time interval after which the client can retry failed requests from the Azure Cosmos DB service
        /// </summary>
        public TimeSpan RetryAfter
        {
            get
            {
                if (this.responseHeaders != null)
                {
                    string header = this.responseHeaders[HttpConstants.HttpHeaders.RetryAfterInMilliseconds];
                    if (!string.IsNullOrEmpty(header))
                    {
                        long retryIntervalInMilliseconds = 0;
                        if (long.TryParse(header, NumberStyles.Number, CultureInfo.InvariantCulture, out retryIntervalInMilliseconds))
                        {
                            return TimeSpan.FromMilliseconds(retryIntervalInMilliseconds);
                        }
                    }
                }

                //
                // In the absence of explicit guidance from the backend, don't introduce
                // any unilateral retry delays here.
                //
                return TimeSpan.Zero;
            }
        }

        //
        // Constructors will allocate the response headers so that
        // the activity ID can be set correctly in a single location.
        //

        /// <summary>
        /// Gets the headers associated with the response from the Azure Cosmos DB service.
        /// </summary>
        public NameValueCollection ResponseHeaders
        {
            get
            {
                return this.responseHeaders.ToNameValueCollection();
            }
        }

        internal INameValueCollection Headers
        {
            get { return this.responseHeaders; }
            set { this.responseHeaders = value; }
        }

        /// <summary>
        /// Gets or sets the request status code in the Azure Cosmos DB service.
        /// </summary>
        public HttpStatusCode? StatusCode { get; internal set; }

        /// <summary>
        /// Gets the textual description of request completion status.
        /// </summary>
        internal string StatusDescription { get; set; }

        /// <summary>
        /// Cost of the request in the Azure Cosmos DB service.
        /// </summary>
        public double RequestCharge
        {
            get
            {
                if (this.responseHeaders != null)
                {
                    return Helpers.GetHeaderValueDouble(
                        this.responseHeaders,
                        HttpConstants.HttpHeaders.RequestCharge,
                        0);
                }
                else
                {
                    return 0;
                }
            }
        }

        /// <summary>
        /// Gets the console.log output from server side scripts statements when script logging is enabled.
        /// </summary>
        public string ScriptLog
        {
            get
            {
                return Helpers.GetScriptLogHeader(this.Headers);
            }
        }

        /// <summary>
        /// Gets a message that describes the current exception from the Azure Cosmos DB service.
        /// </summary>
        public override string Message
        {
            get
            {
                string requestStatisticsMessage = this.RequestStatistics == null ? string.Empty : this.RequestStatistics.ToString();
                if (this.RequestUri != null)
                {
                    return string.Format(CultureInfo.CurrentUICulture,
                        RMResources.ExceptionMessageAddRequestUri,
                        base.Message,
                        this.RequestUri.PathAndQuery,
                        requestStatisticsMessage,
                        CustomTypeExtensions.GenerateBaseUserAgentString());
                }
                else
                {
                    if (string.IsNullOrEmpty(requestStatisticsMessage))
                    {
                        return string.Format(CultureInfo.CurrentCulture, "{0}, {1}", base.Message, CustomTypeExtensions.GenerateBaseUserAgentString());
                    }
                    else
                    {
                        return string.Format(CultureInfo.CurrentUICulture, "{0}, {1}, {2}", base.Message, requestStatisticsMessage, CustomTypeExtensions.GenerateBaseUserAgentString());
                    }
                }
            }
        }

        internal virtual string PublicMessage
        {
            get
            {
                string requestStatisticsMessage = this.RequestStatistics == null ? string.Empty : this.RequestStatistics.ToString();
                if (this.RequestUri != null)
                {
                    return string.Format(CultureInfo.CurrentUICulture,
                        RMResources.ExceptionMessageAddRequestUri,
                        base.Message,
                        this.RequestUri.PathAndQuery,
                        requestStatisticsMessage,
                        CustomTypeExtensions.GenerateBaseUserAgentString());
                }
                else
                {
                    if (string.IsNullOrEmpty(requestStatisticsMessage))
                    {
                        return string.Format(CultureInfo.CurrentCulture,
                            "{0}, {1}",
                            base.Message,
                            CustomTypeExtensions.GenerateBaseUserAgentString());
                    }
                    else
                    {
                        return string.Format(CultureInfo.CurrentUICulture,
                            "{0}, {1}, {2}",
                            base.Message,
                            requestStatisticsMessage,
                            CustomTypeExtensions.GenerateBaseUserAgentString());
                    }
                }
            }
        }

        internal string RawErrorMessage
        {
            get
            {
                return base.Message;
            }
        }

        internal IClientSideRequestStatistics RequestStatistics
        {
            get;
            set;
        }

        internal long LSN { get; set; }

        internal string PartitionKeyRangeId { get; set; }

        internal string ResourceAddress { get; set; }

        /// <summary>
        /// Gets the request uri from the current exception from the Azure Cosmos DB service.
        /// </summary>
        internal Uri RequestUri { get; private set; }

        private static string MessageWithActivityId(string message, INameValueCollection responseHeaders)
        {
            string[] activityIds = null;

            if (responseHeaders != null)
            {
                activityIds = responseHeaders.GetValues(HttpConstants.HttpHeaders.ActivityId);
            }

            if (activityIds != null)
            {
                return DocumentClientException.MessageWithActivityId(message, activityIds.FirstOrDefault());
            }
            else
            {
                return DocumentClientException.MessageWithActivityId(message);
            }
        }

        private static string MessageWithActivityId(string message, HttpResponseHeaders responseHeaders)
        {
            IEnumerable<string> activityIds = null;
            if (responseHeaders != null && responseHeaders.TryGetValues(HttpConstants.HttpHeaders.ActivityId, out activityIds))
            {
                if (activityIds != null)
                {
                    return DocumentClientException.MessageWithActivityId(message, activityIds.FirstOrDefault());
                }
            }

            return DocumentClientException.MessageWithActivityId(message);
        }

        private static string MessageWithActivityId(string message, string activityIdFromHeaders = null)
        {
            string activityId = null;
            if (!string.IsNullOrEmpty(activityIdFromHeaders))
            {
                activityId = activityIdFromHeaders;
            }
            else if (Trace.CorrelationManager.ActivityId != Guid.Empty)
            {
                activityId = Trace.CorrelationManager.ActivityId.ToString();
            }
            else
            {
                // If we couldn't find an ActivityId either in the headers or in the CorrelationManager,
                // just return the message as-is.
                return message;
            }

            // If we're making this exception on the client side using the message from the Gateway,
            // the message may already have activityId stamped in it. If so, just use the message as-is
            if (message.Contains(activityId))
            {
                return message;
            }
            else
            {
                return string.Format(CultureInfo.InvariantCulture, "{0}" + Environment.NewLine + "ActivityId: {1}",
                    message, activityId);
            }
        }

        private static string SerializeHTTPResponseHeaders(HttpResponseHeaders responseHeaders)
        {
            if (responseHeaders == null) return "null";

            StringBuilder result = new StringBuilder("{");
            result.Append(Environment.NewLine);

            foreach (KeyValuePair<string, IEnumerable<string>> pair in responseHeaders)
            {
                foreach (string value in pair.Value)
                {
                    result.Append(string.Format(CultureInfo.InvariantCulture,
                        "\"{0}\": \"{1}\",{2}",
                        pair.Key,
                        value,
                        Environment.NewLine));
                }
            }

            result.Append("}");
            return result.ToString();
        }

        internal SubStatusCodes GetSubStatus()
        {
            if (this.substatus == null)
            {
                this.substatus = SubStatusCodes.Unknown;

                string valueSubStatus = this.responseHeaders.Get(WFConstants.BackendHeaders.SubStatus);
                if (!string.IsNullOrEmpty(valueSubStatus))
                {
                    uint nSubStatus = 0;
                    if (uint.TryParse(valueSubStatus, NumberStyles.Integer, CultureInfo.InvariantCulture, out nSubStatus))
                    {
                        this.substatus = (SubStatusCodes)nSubStatus;
                    }
                }
            }

            return this.substatus != null ? this.substatus.Value : SubStatusCodes.Unknown;
        }

        private static string SerializeHTTPResponseHeaders(INameValueCollection responseHeaders)
        {
            if (responseHeaders == null) return "null";

            IEnumerable<Tuple<string, string>> items = responseHeaders.AllKeys().SelectMany(responseHeaders.GetValues, (k, v) => new Tuple<string, string>(k, v ));

            StringBuilder result = new StringBuilder("{");
            result.Append(Environment.NewLine);

            foreach (Tuple<string, string> item in items)
            {
                result.Append(string.Format(CultureInfo.InvariantCulture,
                    "\"{0}\": \"{1}\",{2}",
                    item.Item1,
                    item.Item2,
                    Environment.NewLine));
            }

            result.Append("}");
            return result.ToString();
        }
    }
}
