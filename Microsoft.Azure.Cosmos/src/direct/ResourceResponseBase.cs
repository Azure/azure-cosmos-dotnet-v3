//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.Client
{
    using Microsoft.Azure.Documents.Collections;
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Net;

    /// <summary>
    /// Represents the non-resource specific service response headers returned by any request in the Azure Cosmos DB service.
    /// </summary>
#if COSMOSCLIENT
    internal
#else
    public
#endif
    abstract class ResourceResponseBase : IResourceResponseBase
    {
        // Ideally response was intended to be marked as protected(to be accessed by sub-classes) but since DocumentServiceResponse class is marked internal,
        // it gives Inconsistent accessibility error saying DocumentServiceResponse is less accessible than field ServiceResponse.response if I mark it as protected.
        internal DocumentServiceResponse response;
        private Dictionary<string, long> usageHeaders;
        private Dictionary<string, long> quotaHeaders;

        /// <summary>
        /// Constructor exposed for mocking purposes for the Azure Cosmos DB service.
        /// </summary>
        public ResourceResponseBase()
        {

        }

        internal ResourceResponseBase(DocumentServiceResponse response)
        {
            this.response = response;
            this.usageHeaders = new Dictionary<string, long>();
            this.quotaHeaders = new Dictionary<string, long>();
        }

        /// <summary>
        /// Gets the maximum quota for database resources within the account from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The maximum quota for the account.
        /// </value>
        public long DatabaseQuota
        {
            get
            {
                return this.GetMaxQuotaHeader(Constants.Quota.Database);
            }
        }

        /// <summary>
        /// Gets the current number of database resources within the account from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The number of databases.
        /// </value>
        public long DatabaseUsage
        {
            get
            {
                return this.GetCurrentQuotaHeader(Constants.Quota.Database);
            }
        }

        /// <summary>
        /// Gets the maximum quota for collection resources within an account from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The maximum quota for the account.
        /// </value>
        public long CollectionQuota
        {
            get
            {
                return this.GetMaxQuotaHeader(Constants.Quota.Collection);
            }
        }

        /// <summary>
        /// Gets the current number of collection resources within the account from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The number of collections.
        /// </value>
        public long CollectionUsage
        {
            get
            {
                return this.GetCurrentQuotaHeader(Constants.Quota.Collection);
            }
        }

        /// <summary>
        /// Gets the maximum quota for user resources within an account from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The maximum quota for the account.
        /// </value>
        public long UserQuota
        {
            get
            {
                return this.GetMaxQuotaHeader(Constants.Quota.User);
            }
        }

        /// <summary>
        /// Gets the current number of user resources within the account from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The number of users.
        /// </value>
        public long UserUsage
        {
            get
            {
                return this.GetCurrentQuotaHeader(Constants.Quota.User);
            }
        }

        /// <summary>
        /// Gets the maximum quota for permission resources within an account from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The maximum quota for the account.
        /// </value>
        public long PermissionQuota
        {
            get
            {
                return this.GetMaxQuotaHeader(Constants.Quota.Permission);
            }
        }

        /// <summary>
        /// Gets the current number of permission resources within the account from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The number of permissions.
        /// </value>
        public long PermissionUsage
        {
            get
            {
                return this.GetCurrentQuotaHeader(Constants.Quota.Permission);
            }
        }

        /// <summary>
        /// Gets the maximum size of a collection in kilobytes from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// Quota in kilobytes.
        /// </value>
        public long CollectionSizeQuota
        {
            get
            {
                return this.GetMaxQuotaHeader(Constants.Quota.CollectionSize);
            }
        }

        /// <summary>
        /// Gets the current size of a collection in kilobytes from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// Current collection size in kilobytes.
        /// </value>
        public long CollectionSizeUsage
        {
            get
            {
                return this.GetCurrentQuotaHeader(Constants.Quota.CollectionSize);
            }
        }

        /// <summary>
        /// Gets the maximum size of a documents within a collection in kilobytes from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// Quota in kilobytes.
        /// </value>
        public long DocumentQuota
        {
            get
            {
                return this.GetMaxQuotaHeader(Constants.Quota.DocumentsSize);
            }
        }

        /// <summary>
        /// Gets the current size of documents within a collection in kilobytes from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// Current documents size in kilobytes.
        /// </value>
        public long DocumentUsage
        {
            get
            {
                return this.GetCurrentQuotaHeader(Constants.Quota.DocumentsSize);
            }
        }

        /// <summary>
        /// Gets the maximum quota of stored procedures for a collection from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The maximum quota.
        /// </value>
        public long StoredProceduresQuota
        {
            get
            {
                return this.GetMaxQuotaHeader(Constants.Quota.StoredProcedure);
            }
        }

        /// <summary>
        /// Gets the current number of stored procedures for a collection from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// Current number of stored procedures.
        /// </value>
        public long StoredProceduresUsage
        {
            get
            {
                return this.GetCurrentQuotaHeader(Constants.Quota.StoredProcedure);
            }
        }

        /// <summary>
        /// Gets the maximum quota of triggers for a collection from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The maximum quota.
        /// </value>
        public long TriggersQuota
        {
            get
            {
                return this.GetMaxQuotaHeader(Constants.Quota.Trigger);
            }
        }

        /// <summary>
        /// Gets the current number of triggers for a collection from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// Current number of triggers.
        /// </value>
        public long TriggersUsage
        {
            get
            {
                return this.GetCurrentQuotaHeader(Constants.Quota.Trigger);
            }
        }

        /// <summary>
        /// Gets the maximum quota of user defined functions for a collection from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The maximum quota.
        /// </value>
        public long UserDefinedFunctionsQuota
        {
            get
            {
                return this.GetMaxQuotaHeader(Constants.Quota.UserDefinedFunction);
            }
        }

        /// <summary>
        /// Gets the current number of user defined functions for a collection from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// Current number of user defined functions.
        /// </value>
        public long UserDefinedFunctionsUsage
        {
            get
            {
                return this.GetCurrentQuotaHeader(Constants.Quota.UserDefinedFunction);
            }
        }

        /// <summary>
        /// Gets the current count of documents within a collection from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// Current count of documents.
        /// </value>
        internal long DocumentCount
        {
            get
            {
                return this.GetCurrentQuotaHeader(Constants.Quota.DocumentsCount);
            }
        }

        /// <summary>
        /// Gets the activity ID for the request from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The activity ID for the request.
        /// </value>
        public string ActivityId
        {
            get
            {
                return this.response.Headers[HttpConstants.HttpHeaders.ActivityId];
            }
        }

        /// <summary>
        /// Gets the session token for use in sesssion consistency reads from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The session token for use in session consistency.
        /// </value>
        public string SessionToken
        {
            get
            {
                return this.response.Headers[HttpConstants.HttpHeaders.SessionToken];
            }
        }

        /// <summary>
        /// Gets the HTTP status code associated with the response from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The HTTP status code associated with the response.
        /// </value>
        public HttpStatusCode StatusCode
        {
            get
            {
                return this.response.StatusCode;
            }
        }

        /// <summary>
        /// Gets the maximum size limit for this entity from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The maximum size limit for this entity. Measured in kilobytes for document resources 
        /// and in counts for other resources.
        /// </value>
        public string MaxResourceQuota
        {
            get
            {
                return this.response.Headers[HttpConstants.HttpHeaders.MaxResourceQuota];
            }
        }

        /// <summary>
        /// Gets the current size of this entity from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The current size for this entity. Measured in kilobytes for document resources 
        /// and in counts for other resources.
        /// </value>
        public string CurrentResourceQuotaUsage
        {
            get
            {
                return this.response.Headers[HttpConstants.HttpHeaders.CurrentResourceQuotaUsage];
            }
        }

        /// <summary>
        /// Gets the underlying stream of the response from the Azure Cosmos DB service.
        /// </summary>
        public Stream ResponseStream
        {
            get
            {
                return this.response.ResponseBody;
            }
        }

        /// <summary>
        /// Gets the request charge for this request from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The request charge measured in reqest units.
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
        /// Gets the flag associated with the response from the Azure Cosmos DB service whether this request is served from Request Units(RUs)/minute capacity or not.
        /// </summary>
        /// <value>
        /// True if this request is served from RUs/minute capacity. Otherwise, false.
        /// </value>
        public bool IsRUPerMinuteUsed
        {
            get
            {
                if (Helpers.GetHeaderValueByte(this.response.Headers, HttpConstants.HttpHeaders.IsRUPerMinuteUsed, 0) != 0)
                {
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Gets the response headers from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The response headers.
        /// </value>
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
        /// The content parent location, for example, dbs/foo/colls/bar in the Azure Cosmos DB service.
        /// </summary>
        public string ContentLocation
        {
            get
            {
                return this.response.Headers[HttpConstants.HttpHeaders.OwnerFullName];
            }
        }

        /// <summary>
        /// Gets the progress of an index transformation, if one is underway from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// An integer from 0 to 100 representing percentage completion of the index transformation process.
        /// Returns -1 if the index transformation progress header could not be found.
        /// </value>
        /// <remarks>
        /// An index will be rebuilt when the IndexPolicy of a collection is updated.
        /// </remarks>
        public long IndexTransformationProgress
        {
            get
            {
                return Helpers.GetHeaderValueLong(this.response.Headers, HttpConstants.HttpHeaders.CollectionIndexTransformationProgress);
            }
        }

        /// <summary>
        /// Gets the progress of lazy indexing from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// An integer from 0 to 100 representing percentage completion of the lazy indexing process.
        /// Returns -1 if the lazy indexing progress header could not be found.
        /// </value>
        /// <remarks>
        /// Lazy indexing progress only applies to the collection with indexing mode Lazy.
        /// </remarks>
        public long LazyIndexingProgress
        {
            get
            {
                return Helpers.GetHeaderValueLong(this.response.Headers, HttpConstants.HttpHeaders.CollectionLazyIndexingProgress);
            }
        }

        /// <summary>
        /// Gets the end-to-end request latency for the current request to Azure Cosmos DB service.
        /// </summary>
        /// <remarks>
        /// This field is only valid when the request uses direct connectivity.
        /// </remarks>
        public TimeSpan RequestLatency
        {
            get
            {
                if (this.response.RequestStats == null)
                {
                    return TimeSpan.Zero;
                }

                return this.response.RequestStats.RequestLatency;
            }
        }

        /// <summary>
        /// Gets the diagnostics information for the current request to Azure Cosmos DB service.
        /// </summary>
        /// <remarks>
        /// This field is only valid when the request uses direct connectivity.
        /// </remarks>
        public string RequestDiagnosticsString
        {
            get
            {
                if (this.response.RequestStats == null)
                {
                    return string.Empty;
                }

                return this.response.RequestStats.ToString();
            }
        }

        /// <summary>
        /// Gets the request statistics for the current request to Azure Cosmos DB service.
        /// </summary>
        /// <remarks>
        /// This field is only valid when the request uses direct connectivity.
        /// </remarks>
        internal IClientSideRequestStatistics RequestStatistics
        {
            get
            {
                return this.response.RequestStats;
            }
        }

        internal long GetCurrentQuotaHeader(string headerName)
        {
            long headerValue = 0;
            if (this.usageHeaders.Count == 0 && !string.IsNullOrEmpty(this.MaxResourceQuota) && !string.IsNullOrEmpty(this.CurrentResourceQuotaUsage))
            {
                this.PopulateQuotaHeader(this.MaxResourceQuota, this.CurrentResourceQuotaUsage);
            }

            if(this.usageHeaders.TryGetValue(headerName, out headerValue))
            {
                return headerValue;
            }

            return 0;
        }

        internal long GetMaxQuotaHeader(string headerName)
        {
            long headerValue = 0;
            if (this.quotaHeaders.Count == 0 && !string.IsNullOrEmpty(this.MaxResourceQuota) && !string.IsNullOrEmpty(this.CurrentResourceQuotaUsage))
            {
                this.PopulateQuotaHeader(this.MaxResourceQuota, this.CurrentResourceQuotaUsage);
            }

            if(this.quotaHeaders.TryGetValue(headerName, out headerValue))
            {
                return headerValue;
            }

            return 0;
        }

        private void PopulateQuotaHeader(string headerMaxQuota, string headerCurrentUsage)
        {
            string[] headerMaxQuotaWords = headerMaxQuota.Split(Constants.Quota.DelimiterChars, StringSplitOptions.RemoveEmptyEntries);
            string[] headerCurrentUsageWords = headerCurrentUsage.Split(Constants.Quota.DelimiterChars, StringSplitOptions.RemoveEmptyEntries);

            Debug.Assert(headerMaxQuotaWords.Length == headerCurrentUsageWords.Length, "Headers returned should be consistent for max and current usage");

            for(int i = 0; i < headerMaxQuotaWords.Length; ++i)
            {
                if(string.Equals(
                    headerMaxQuotaWords[i],
                    Constants.Quota.Database,
                    StringComparison.OrdinalIgnoreCase))
                {
                    this.quotaHeaders.Add(Constants.Quota.Database, long.Parse(headerMaxQuotaWords[i + 1], CultureInfo.InvariantCulture));
                    this.usageHeaders.Add(Constants.Quota.Database, long.Parse(headerCurrentUsageWords[i + 1], CultureInfo.InvariantCulture));
                }
                else if(string.Equals(
                    headerMaxQuotaWords[i],
                    Constants.Quota.Collection,
                    StringComparison.OrdinalIgnoreCase))
                {
                    this.quotaHeaders.Add(Constants.Quota.Collection, long.Parse(headerMaxQuotaWords[i + 1], CultureInfo.InvariantCulture));
                    this.usageHeaders.Add(Constants.Quota.Collection, long.Parse(headerCurrentUsageWords[i + 1], CultureInfo.InvariantCulture));

                }
                else if(string.Equals(
                    headerMaxQuotaWords[i],
                    Constants.Quota.User,
                    StringComparison.OrdinalIgnoreCase))
                {
                    this.quotaHeaders.Add(Constants.Quota.User, long.Parse(headerMaxQuotaWords[i + 1], CultureInfo.InvariantCulture));
                    this.usageHeaders.Add(Constants.Quota.User, long.Parse(headerCurrentUsageWords[i + 1], CultureInfo.InvariantCulture));
                }
                else if(string.Equals(
                    headerMaxQuotaWords[i],
                    Constants.Quota.Permission,
                    StringComparison.OrdinalIgnoreCase))
                {
                    this.quotaHeaders.Add(Constants.Quota.Permission, long.Parse(headerMaxQuotaWords[i + 1], CultureInfo.InvariantCulture));
                    this.usageHeaders.Add(Constants.Quota.Permission, long.Parse(headerCurrentUsageWords[i + 1], CultureInfo.InvariantCulture));
                }
                else if(string.Equals(
                    headerMaxQuotaWords[i],
                    Constants.Quota.CollectionSize,
                    StringComparison.OrdinalIgnoreCase))
                {
                    this.quotaHeaders.Add(Constants.Quota.CollectionSize, long.Parse(headerMaxQuotaWords[i + 1], CultureInfo.InvariantCulture));
                    this.usageHeaders.Add(Constants.Quota.CollectionSize, long.Parse(headerCurrentUsageWords[i + 1], CultureInfo.InvariantCulture));
                }
                else if(string.Equals(
                    headerMaxQuotaWords[i],
                    Constants.Quota.DocumentsSize,
                    StringComparison.OrdinalIgnoreCase))
                {
                    this.quotaHeaders.Add(Constants.Quota.DocumentsSize, long.Parse(headerMaxQuotaWords[i + 1], CultureInfo.InvariantCulture));
                    this.usageHeaders.Add(Constants.Quota.DocumentsSize, long.Parse(headerCurrentUsageWords[i + 1], CultureInfo.InvariantCulture));
                }
                else if (string.Equals(
                    headerMaxQuotaWords[i],
                    Constants.Quota.DocumentsCount,
                    StringComparison.OrdinalIgnoreCase))
                {
                    this.quotaHeaders.Add(Constants.Quota.DocumentsCount, long.Parse(headerMaxQuotaWords[i + 1], CultureInfo.InvariantCulture));
                    this.usageHeaders.Add(Constants.Quota.DocumentsCount, long.Parse(headerCurrentUsageWords[i + 1], CultureInfo.InvariantCulture));
                }
                else if (string.Equals(
                    headerMaxQuotaWords[i],
                    Constants.Quota.StoredProcedure,
                    StringComparison.OrdinalIgnoreCase))
                {
                    this.quotaHeaders.Add(Constants.Quota.StoredProcedure, long.Parse(headerMaxQuotaWords[i + 1], CultureInfo.InvariantCulture));
                    this.usageHeaders.Add(Constants.Quota.StoredProcedure, long.Parse(headerCurrentUsageWords[i + 1], CultureInfo.InvariantCulture));
                }
                else if(string.Equals(
                    headerMaxQuotaWords[i],
                    Constants.Quota.Trigger,
                    StringComparison.OrdinalIgnoreCase))
                {
                    this.quotaHeaders.Add(Constants.Quota.Trigger, long.Parse(headerMaxQuotaWords[i + 1], CultureInfo.InvariantCulture));
                    this.usageHeaders.Add(Constants.Quota.Trigger, long.Parse(headerCurrentUsageWords[i + 1], CultureInfo.InvariantCulture));
                }
                else if(string.Equals(
                    headerMaxQuotaWords[i],
                    Constants.Quota.UserDefinedFunction,
                    StringComparison.OrdinalIgnoreCase))
                {
                    this.quotaHeaders.Add(Constants.Quota.UserDefinedFunction, long.Parse(headerMaxQuotaWords[i + 1], CultureInfo.InvariantCulture));
                    this.usageHeaders.Add(Constants.Quota.UserDefinedFunction, long.Parse(headerCurrentUsageWords[i + 1], CultureInfo.InvariantCulture));
                }
            }
        }
    }
}
