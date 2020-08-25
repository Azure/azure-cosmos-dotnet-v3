//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Documents.Collections;
    using Newtonsoft.Json;

    //This is core Transport/Connection agnostic request to DocumentService.
    //It is marked internal today. If needs arises for client to do no-serialized processing
    //we can open this up to public.
    internal sealed class DocumentServiceRequest : IDisposable
    {
        private bool isDisposed = false;

        // The lock is used for locking request body operations like sending request or changing stream position.
        private const char PreferHeadersSeparator = ';';
        private const string PreferHeaderValueFormat = "{0}={1}";

        private ServiceIdentity serviceIdentity;

        private PartitionKeyRangeIdentity partitionKeyRangeIdentity;

        private DocumentServiceRequest()
        {
        }

        /// <summary>
        /// This is constructed from the existing request, either RId based or name based.
        /// resourceIdOrFullName can be either: (trimmed, RemoveTrailingSlashes, RemoveLeadingSlashes, urldecoded)
        /// 1. wo1ZAP7zFQA=
        /// 2. dbs/dbName/colls/collectionName/docs/documentName
        /// </summary>
        /// <param name="operationType"></param>
        /// <param name="resourceIdOrFullName"></param>
        /// <param name="resourceType"></param>
        /// <param name="body"></param>
        /// <param name="headers"></param>
        /// <param name="isNameBased">resourceIdOrFullName is resourceId or fullName</param>
        /// <param name="authorizationTokenType"></param>
        internal DocumentServiceRequest(
            OperationType operationType,
            string resourceIdOrFullName,
            ResourceType resourceType,
            Stream body,
            INameValueCollection headers,
            bool isNameBased,
            AuthorizationTokenType authorizationTokenType)
        {
            this.OperationType = operationType;
            this.ForceNameCacheRefresh = false;
            this.ResourceType = resourceType;
            this.Body = body;
            this.Headers = headers ?? new DictionaryNameValueCollection();
            this.IsFeed = false;
            this.IsNameBased = isNameBased;

            if (isNameBased)
            {
                this.ResourceAddress = resourceIdOrFullName;
            }
            else
            {
                this.ResourceId = resourceIdOrFullName;
                this.ResourceAddress = resourceIdOrFullName;
            }

            this.RequestAuthorizationTokenType = authorizationTokenType;
            this.RequestContext = new DocumentServiceRequestContext();

            if (!string.IsNullOrEmpty(this.Headers[WFConstants.BackendHeaders.PartitionKeyRangeId]))
            {
                this.PartitionKeyRangeIdentity = PartitionKeyRangeIdentity.FromHeader(this.Headers[WFConstants.BackendHeaders.PartitionKeyRangeId]);
            }
        }

        /// <summary>
        ///  The path is the incoming Uri.PathAndQuery, it can be:  (the name is url encoded).
        ///  1. 	dbs/dbName/colls/collectionName/docs/documentName/attachments/
        ///  2.     dbs/wo1ZAA==/colls/wo1ZAP7zFQA=/
        /// </summary>
        /// <param name="operationType"></param>
        /// <param name="resourceType"></param>
        /// <param name="path"></param>
        /// <param name="body"></param>
        /// <param name="headers"></param>
        /// <param name="authorizationTokenType"></param>
        internal DocumentServiceRequest(
            OperationType operationType,
            ResourceType resourceType,
            string path,
            Stream body,
            AuthorizationTokenType authorizationTokenType,
            INameValueCollection headers)
        {
            this.OperationType = operationType;
            this.ForceNameCacheRefresh = false;
            this.ResourceType = resourceType;
            this.Body = body;
            this.Headers = headers ?? new DictionaryNameValueCollection();
            this.RequestAuthorizationTokenType = authorizationTokenType;
            this.RequestContext = new DocumentServiceRequestContext();

            bool isNameBased = false;
            bool isFeed = false;
            string resourceTypeString;
            string resourceIdOrFullName;
            string databaseName = string.Empty;
            string collectionName = string.Empty;

            // for address, no parsing is needed.
            if (resourceType == ResourceType.Address
#if !COSMOSCLIENT
                || resourceType == ResourceType.XPReplicatorAddress
#endif
               )
                return;

            if (PathsHelper.TryParsePathSegmentsWithDatabaseAndCollectionNames(
                path,
                out isFeed,
                out resourceTypeString,
                out resourceIdOrFullName,
                out isNameBased,
                out databaseName,
                out collectionName,
                parseDatabaseAndCollectionNames: true)
                )
            {
                this.IsNameBased = isNameBased;
                this.IsFeed = isFeed;

                if (this.ResourceType == ResourceType.Unknown)
                {
                    this.ResourceType = PathsHelper.GetResourcePathSegment(resourceTypeString);
                }

                if (isNameBased)
                {
                    this.ResourceAddress = resourceIdOrFullName;
                    this.DatabaseName = databaseName;
                    this.CollectionName = collectionName;
                }
                else
                {
                    this.ResourceId = resourceIdOrFullName;
                    this.ResourceAddress = resourceIdOrFullName;
                    ResourceId rid = null;

                    // throw exception when the address parsing fail
                    // do not parse address for offer/snapshot resource
                    if (!string.IsNullOrEmpty(this.ResourceId) &&
                        !Documents.ResourceId.TryParse(this.ResourceId, out rid) &&
                        !(this.ResourceType == ResourceType.Offer) &&
                        !(this.ResourceType == ResourceType.Media) &&
                        !(this.ResourceType == ResourceType.DatabaseAccount) &&
                        !(this.ResourceType == ResourceType.Snapshot) &&
                        !(this.ResourceType == ResourceType.RoleDefinition) &&
                        !(this.ResourceType == ResourceType.RoleAssignment)
#if !COSMOSCLIENT
                        && !(this.ResourceType == ResourceType.MasterPartition) &&
                        !(this.ResourceType == ResourceType.ServerPartition) &&
                        !(this.ResourceType == ResourceType.RidRange) &&
                        !(this.ResourceType == ResourceType.VectorClock)
#endif
                        )
                    {
                        throw new NotFoundException(string.Format(CultureInfo.CurrentUICulture, RMResources.InvalidResourceUrlQuery, path, HttpConstants.QueryStrings.Url));
                    }
                }
            }
            else
            {
                throw new NotFoundException(string.Format(CultureInfo.CurrentUICulture, RMResources.InvalidResourceUrlQuery, path, HttpConstants.QueryStrings.Url));
            }

            if (!string.IsNullOrEmpty(this.Headers[WFConstants.BackendHeaders.PartitionKeyRangeId]))
            {
                this.PartitionKeyRangeIdentity = PartitionKeyRangeIdentity.FromHeader(this.Headers[WFConstants.BackendHeaders.PartitionKeyRangeId]);
            }
        }

        public bool IsNameBased { get; private set; }

        public string DatabaseName { get; private set; }

        public string CollectionName { get; private set; }

        /// <summary>
        /// This is currently used to force non-Windows .NET Core target platforms(Linux and OSX)
        /// and on 32-bit host process on Windows for NETFX, to always use Gateway mode for sending
        /// cross partition query requests to get partition execution info as that logic is there in
        /// ServiceInterop native dll which we haven't ported to Linux and OSX yet and it exists only
        /// in 64 bit version on Windows.
        /// </summary>
        public bool UseGatewayMode { get; set; }

        /// <summary>
        /// This is a flag that indicates whether the DocumentClient internally
        /// throws exceptions for status codes 404, 412, and 409 or whether it returns
        /// the status codes as part of the result for failures.
        /// </summary>
        public bool UseStatusCodeForFailures { get; set; }

        /// <summary>
        /// This is a flag that indicates whether the DocumentClient internally
        /// throws exceptions for 429 status codes
        /// the status codes as part of the result for failures.
        /// </summary>
        public bool UseStatusCodeFor429 { get; set; }

        /// <summary>
        /// ServiceIdentity of the target service where this request should reach
        /// Only valid for gateway
        /// </summary>
        public ServiceIdentity ServiceIdentity
        {
            get
            {
                return this.serviceIdentity;
            }
            private set
            {
                this.serviceIdentity = value;
            }
        }

        public SystemAuthorizationParameters SystemAuthorizationParams { get; set; }

        public sealed class SystemAuthorizationParameters
        {
            public string FederationId { get; set; }

            public string Verb { get; set; }

            public string ResourceId { get; set; }

            public SystemAuthorizationParameters Clone()
            {
                return new SystemAuthorizationParameters()
                {
                    FederationId = this.FederationId,
                    Verb = this.Verb,
                    ResourceId = this.ResourceId
                };
            }
        }

        public PartitionKeyRangeIdentity PartitionKeyRangeIdentity
        {
            get
            {
                return this.partitionKeyRangeIdentity;
            }
            private set
            {
                this.partitionKeyRangeIdentity = value;
                if (value != null)
                {
                    this.Headers[WFConstants.BackendHeaders.PartitionKeyRangeId] = value.ToHeader();
                }
                else
                {
                    this.Headers.Remove(WFConstants.BackendHeaders.PartitionKeyRangeId);
                }
            }
        }

        public void RouteTo(ServiceIdentity serviceIdentity)
        {
            if(this.PartitionKeyRangeIdentity != null)
            {
                DefaultTrace.TraceCritical("This request was going to be routed to partition key range");
                throw new InternalServerErrorException();
            }

            this.ServiceIdentity = serviceIdentity;
        }

        public void RouteTo(PartitionKeyRangeIdentity partitionKeyRangeIdentity)
        {
            if(this.ServiceIdentity != null)
            {
                DefaultTrace.TraceCritical("This request was going to be routed to service identity");
                throw new InternalServerErrorException();
            }

            this.PartitionKeyRangeIdentity = partitionKeyRangeIdentity;
        }

        public string ResourceId { get; set; }

        public DocumentServiceRequestContext RequestContext { get; set; }

        /// <summary>
        /// Normalized resourcePath, for both Name based and Rid based.
        /// This is the string passed for AuthZ.
        /// It is resourceId in Rid case passed for AuthZ
        /// </summary>
        public string ResourceAddress { get; private set; }

        public bool IsFeed { get; set; }

        public string EntityId { get; set; }

        public INameValueCollection Headers { get; private set; }

        /// <summary>
        /// Contains the context shared by handlers.
        /// </summary>
        public IDictionary<string, object> Properties { get; set; }

        public Stream Body { get; set; }

        public CloneableStream CloneableBody { get; private set; }

        /// <summary>
        /// Authorization token used for the request.
        /// This will be used to generate any child requests that are needed to process the request.
        /// </summary>
        public AuthorizationTokenType RequestAuthorizationTokenType { get; set; }

        public bool IsBodySeekableClonableAndCountable
        {
            get
            {
                return (this.Body == null) || (this.CloneableBody != null);
            }
        }

        public OperationType OperationType { get; private set; }

        public ResourceType ResourceType { get; private set; }

        public string QueryString { get; set; }

        //Backend Continuation.
        public string Continuation
        {
            get
            {
                return this.Headers[HttpConstants.HttpHeaders.Continuation];
            }
            set
            {
                this.Headers[HttpConstants.HttpHeaders.Continuation] = value;
            }
        }

        internal string ApiVersion
        {
            get
            {
                return this.Headers[HttpConstants.HttpHeaders.Version];
            }
        }

        public bool ForceNameCacheRefresh { get; set; }

        public bool ForcePartitionKeyRangeRefresh { get; set; }

        public bool ForceCollectionRoutingMapRefresh { get; set; }

        public bool ForceMasterRefresh { get; set; }

        public bool IsReadOnlyRequest
        {
            get
            {
                return this.OperationType == Documents.OperationType.Read
                    || this.OperationType == Documents.OperationType.ReadFeed
                    || this.OperationType == Documents.OperationType.Head
                    || this.OperationType == Documents.OperationType.HeadFeed
                    || this.OperationType == Documents.OperationType.Query
                    || this.OperationType == Documents.OperationType.SqlQuery
                    || this.OperationType == Documents.OperationType.QueryPlan;
            }
        }

        public bool IsReadOnlyScript
        {
            get
            {
                string isReadOnlyScript = this.Headers.Get(HttpConstants.HttpHeaders.IsReadOnlyScript);
                if (string.IsNullOrEmpty(isReadOnlyScript))
                {
                    return false;
                }
                else
                {
                    return this.OperationType == Documents.OperationType.ExecuteJavaScript
                        && isReadOnlyScript.Equals(bool.TrueString, StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        public JsonSerializerSettings SerializerSettings
        {
            get;
            set;
        }

#region Test hooks

        public uint? DefaultReplicaIndex
        {
            get;
            set;
        }

#endregion

        public void Dispose()
        {
            if (this.isDisposed)
            {
                return;
            }

            if (this.Body != null)
            {
                this.Body.Dispose();
                this.Body = null;
            }

            if (this.CloneableBody != null)
            {
                this.CloneableBody.Dispose();
                this.CloneableBody = null;
            }

            this.isDisposed = true;
        }

        /// <summary>
        /// Verify the address is same as claimed resourceType
        /// </summary>
        /// <returns></returns>
        public bool IsValidAddress(ResourceType resourceType = ResourceType.Unknown)
        {
            ResourceType resourceTypeToValidate = ResourceType.Unknown;

            if (resourceType != ResourceType.Unknown)
            {
                resourceTypeToValidate = resourceType;
            }
            else
            {
                if (!this.IsFeed)
                {
                    resourceTypeToValidate = this.ResourceType;
                }
                else
                {
                    if (this.ResourceType == ResourceType.Database)
                    {
                        return true;
                    }
                    else if (this.ResourceType == ResourceType.Collection ||
                        this.ResourceType == ResourceType.User ||
                        this.ResourceType == ResourceType.ClientEncryptionKey ||
                        this.ResourceType == ResourceType.UserDefinedType)
                    {
                        resourceTypeToValidate = ResourceType.Database;
                    }
                    else if (this.ResourceType == ResourceType.Permission)
                    {
                        resourceTypeToValidate = ResourceType.User;
                    }
                    else if (this.ResourceType == ResourceType.Document ||
                            this.ResourceType == ResourceType.StoredProcedure ||
                            this.ResourceType == ResourceType.UserDefinedFunction ||
                            this.ResourceType == ResourceType.Trigger ||
                            this.ResourceType == ResourceType.Conflict ||
                            this.ResourceType == ResourceType.StoredProcedure ||
                            this.ResourceType == ResourceType.PartitionKeyRange ||
                            this.ResourceType == ResourceType.Schema ||
                            this.ResourceType == ResourceType.PartitionedSystemDocument)
                    {
                        resourceTypeToValidate = ResourceType.Collection;
                    }
                    else if (this.ResourceType == ResourceType.Attachment)
                    {
                        resourceTypeToValidate = ResourceType.Document;
                    }
                    else if (this.ResourceType == ResourceType.Snapshot)
                    {
                        return true;
                    }
                    else if (this.ResourceType == ResourceType.RoleDefinition)
                    {
                        return true;
                    }
                    else if (this.ResourceType == ResourceType.RoleAssignment)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
            }

            if (IsNameBased)
            {
                return PathsHelper.ValidateResourceFullName(resourceType != ResourceType.Unknown ? resourceType : resourceTypeToValidate, this.ResourceAddress);
            }
            else
            {
                return PathsHelper.ValidateResourceId(resourceTypeToValidate, this.ResourceId);
            }
        }

        public void AddPreferHeader(string preferHeaderName, string preferHeaderValue)
        {
            string headerToAdd = string.Format(
                CultureInfo.InvariantCulture,
                DocumentServiceRequest.PreferHeaderValueFormat,
                preferHeaderName,
                preferHeaderValue);

            string preferHeader = this.Headers[HttpConstants.HttpHeaders.Prefer];

            if (!string.IsNullOrEmpty(preferHeader))
            {
                preferHeader += DocumentServiceRequest.PreferHeadersSeparator + headerToAdd;
            }
            else
            {
                preferHeader = headerToAdd;
            }

            this.Headers[HttpConstants.HttpHeaders.Prefer] = preferHeader;
        }

        // Clone from request
        public static DocumentServiceRequest CreateFromResource(
            DocumentServiceRequest request,
            Resource modifiedResource)
        {
            DocumentServiceRequest modifiedRequest;
            if (!request.IsNameBased)
            {
                modifiedRequest = DocumentServiceRequest.Create(request.OperationType, modifiedResource, request.ResourceType, request.RequestAuthorizationTokenType, request.Headers, request.ResourceId);
            }
            else
            {
                modifiedRequest = DocumentServiceRequest.CreateFromName(request.OperationType, modifiedResource, request.ResourceType, request.Headers, request.ResourceAddress, request.RequestAuthorizationTokenType);
            }

            return modifiedRequest;
        }


        //POST or PUT
        [SuppressMessage("Microsoft.Reliability", "CA2000:DisposeObjectsBeforeLosingScope", Justification = "Stream is disposed with request instance")]
        public static DocumentServiceRequest Create(
            OperationType operationType,
            Resource resource,
            ResourceType resourceType,
            AuthorizationTokenType authorizationTokenType,
            INameValueCollection headers = null,
            string ownerResourceId = null,
            SerializationFormattingPolicy formattingPolicy = SerializationFormattingPolicy.None)
        {
            MemoryStream stream = new MemoryStream();
            if (resource != null)
            {
                resource.SaveTo(stream, formattingPolicy);
            }
            stream.Position = 0;

            DocumentServiceRequest request = new DocumentServiceRequest(
                operationType,
                ownerResourceId != null ? ownerResourceId : resource.ResourceId,
                resourceType,
                stream,
                headers,
                false,
                authorizationTokenType);

            request.CloneableBody = new CloneableStream(stream);
            return request;
        }

        public static DocumentServiceRequest Create(
            OperationType operationType,
            ResourceType resourceType,
            MemoryStream stream,
            AuthorizationTokenType authorizationTokenType,
            INameValueCollection headers = null)
        {
            DocumentServiceRequest request = new DocumentServiceRequest(
                operationType,
                null, // resourceIdOrFullName
                resourceType,
                stream,
                headers,
                false, // isNameBased
                authorizationTokenType);

            request.CloneableBody = new CloneableStream(stream);
            return request;
        }

        [SuppressMessage("Microsoft.Reliability", "CA2000:DisposeObjectsBeforeLosingScope", Justification = "Stream is disposed with request instance")]
        public static DocumentServiceRequest Create(
            OperationType operationType,
            string ownerResourceId,
            byte[] seralizedResource,
            ResourceType resourceType,
            AuthorizationTokenType authorizationTokenType,
            INameValueCollection headers = null,
            SerializationFormattingPolicy formattingPolicy = SerializationFormattingPolicy.None)
        {
            MemoryStream stream = new MemoryStream(seralizedResource);

            return new DocumentServiceRequest(
                operationType,
                ownerResourceId,
                resourceType,
                stream,
                headers,
                false,
                authorizationTokenType);
        }

        [SuppressMessage("Microsoft.Reliability", "CA2000:DisposeObjectsBeforeLosingScope", Justification = "Stream is disposed with request instance")]
        public static DocumentServiceRequest Create(
            OperationType operationType,
            string ownerResourceId,
            ResourceType resourceType,
            bool isNameBased,
            AuthorizationTokenType authorizationTokenType,
            byte[] seralizedResource = null,
            INameValueCollection headers = null,
            SerializationFormattingPolicy formattingPolicy = SerializationFormattingPolicy.None)
        {
            MemoryStream stream = seralizedResource == null ? null : new MemoryStream(seralizedResource);

            return new DocumentServiceRequest(
                operationType,
                ownerResourceId,
                resourceType,
                stream,
                headers,
                isNameBased,
                authorizationTokenType);
        }

        public static DocumentServiceRequest Create(
            OperationType operationType,
            string resourceId,
            ResourceType resourceType,
            Stream body,
            AuthorizationTokenType authorizationTokenType,
            INameValueCollection headers = null)
        {
            return new DocumentServiceRequest(operationType, resourceId, resourceType, body, headers, false, authorizationTokenType);
        }

        //Get, Delete, FeedRead.
        public static DocumentServiceRequest Create(
            OperationType operationType,
            string resourceId,
            ResourceType resourceType,
            AuthorizationTokenType authorizationTokenType,
            INameValueCollection headers = null)
        {
            return new DocumentServiceRequest(operationType, resourceId, resourceType, null, headers, false, authorizationTokenType);
        }

        public static DocumentServiceRequest CreateFromName(
            OperationType operationType,
            string resourceFullName,
            ResourceType resourceType,
            AuthorizationTokenType authorizationTokenType,
            INameValueCollection headers = null)
        {
            return new DocumentServiceRequest(operationType, resourceFullName, resourceType, null, headers, true, authorizationTokenType);
        }

        public static DocumentServiceRequest CreateFromName(
            OperationType operationType,
            Resource resource,
            ResourceType resourceType,
            INameValueCollection headers,
            string resourceFullName,
            AuthorizationTokenType authorizationTokenType,
            SerializationFormattingPolicy formattingPolicy = SerializationFormattingPolicy.None)
        {
            MemoryStream stream = new MemoryStream();
            resource.SaveTo(stream, formattingPolicy);
            stream.Position = 0;

            return new DocumentServiceRequest(operationType, resourceFullName, resourceType, stream, headers, true, authorizationTokenType);
        }

        //Replica Request.
        public static DocumentServiceRequest Create(
            OperationType operationType,
            ResourceType resourceType,
            AuthorizationTokenType authorizationTokenType)
        {
            return new DocumentServiceRequest(operationType, null, resourceType, null, null, false, authorizationTokenType);
        }

        [SuppressMessage("Microsoft.Reliability", "CA2000:DisposeObjectsBeforeLosingScope", Justification = "Stream is disposed with request instance")]
        public static DocumentServiceRequest Create(
            OperationType operationType,
            string relativePath,
            Resource resource,
            ResourceType resourceType,
            AuthorizationTokenType authorizationTokenType,
            INameValueCollection headers = null,
            SerializationFormattingPolicy formattingPolicy = SerializationFormattingPolicy.None,
            JsonSerializerSettings settings = null)
        {
            MemoryStream stream = new MemoryStream();
            resource.SaveTo(stream, formattingPolicy, settings);
            stream.Position = 0;

            DocumentServiceRequest request = new DocumentServiceRequest(operationType, resourceType, relativePath, stream, authorizationTokenType, headers);
            request.SerializerSettings = settings;

            // since we constructed the body ourselves in a way that it can be safely given to a CloneableStream,
            // assign that here, so that EnsureBufferedBodyAsync will be a no-op (won't incur unnecessary memcpy)
            request.CloneableBody = new CloneableStream(stream);
            return request;
        }

        [SuppressMessage("Microsoft.Reliability", "CA2000:DisposeObjectsBeforeLosingScope", Justification = "Stream is disposed with request instance")]
        public static DocumentServiceRequest Create(
            OperationType operationType,
            Uri requestUri,
            Resource resource,
            ResourceType resourceType,
            AuthorizationTokenType authorizationTokenType,
            INameValueCollection headers = null,
            SerializationFormattingPolicy formattingPolicy = SerializationFormattingPolicy.None)
        {
            MemoryStream stream = new MemoryStream();
            resource.SaveTo(stream, formattingPolicy);
            stream.Position = 0;

            DocumentServiceRequest request = new DocumentServiceRequest(operationType, resourceType, requestUri.PathAndQuery, stream, authorizationTokenType, headers);

            // since we constructed the body ourselves in a way that it can be safely given to a CloneableStream,
            // assign that here, so that EnsureBufferedBodyAsync will be a no-op (won't incur unnecessary memcpy)
            request.CloneableBody = new CloneableStream(stream);
            return request;

        }

        public static DocumentServiceRequest Create(
            OperationType operationType,
            ResourceType resourceType,
            string relativePath,
            AuthorizationTokenType authorizationTokenType,
            INameValueCollection headers = null)
        {
            return new DocumentServiceRequest(operationType, resourceType, relativePath, null, authorizationTokenType, headers);
        }

        public static DocumentServiceRequest Create(
            OperationType operationType,
            ResourceType resourceType,
            Uri requestUri,
            AuthorizationTokenType authorizationTokenType,
            INameValueCollection headers = null)
        {
            return new DocumentServiceRequest(operationType, resourceType, requestUri.PathAndQuery, null, authorizationTokenType, headers);
        }

        public static DocumentServiceRequest Create(OperationType operationType,
            ResourceType resourceType,
            string relativePath,
            Stream resourceStream,
            AuthorizationTokenType authorizationTokenType,
            INameValueCollection headers = null)
        {
            return new DocumentServiceRequest(
                operationType,
                resourceType,
                relativePath,
                resourceStream,
                authorizationTokenType,
                headers);
        }

        public static DocumentServiceRequest Create(OperationType operationType,
            ResourceType resourceType,
            Uri requestUri,
            Stream resourceStream,
            AuthorizationTokenType authorizationTokenType,
            INameValueCollection headers)
        {
            return new DocumentServiceRequest(
                operationType,
                resourceType,
                requestUri.PathAndQuery,
                resourceStream,
                authorizationTokenType,
                headers);
        }

        public async Task EnsureBufferedBodyAsync()
        {
            if (this.Body == null)
                return;
            else if (this.CloneableBody != null)
                return;

            this.CloneableBody = await StreamExtension.AsClonableStreamAsync(this.Body);
        }

        public void ClearRoutingHints()
        {
            this.PartitionKeyRangeIdentity = null;
            this.ServiceIdentity = null;
            this.RequestContext.TargetIdentity = null;
            this.RequestContext.ResolvedPartitionKeyRange = null;
        }

        public DocumentServiceRequest Clone()
        {
            if (!this.IsBodySeekableClonableAndCountable)
            {
                throw new InvalidOperationException();
            }

            return new DocumentServiceRequest{
               OperationType = this.OperationType,
               ForceNameCacheRefresh = this.ForceNameCacheRefresh,
               ResourceType = this.ResourceType,
               ServiceIdentity = this.ServiceIdentity,
               SystemAuthorizationParams = this.SystemAuthorizationParams == null ? null : this.SystemAuthorizationParams.Clone(),
               // Body = this.Body, // intentionally don't clone body, as it is not cloneable.
               CloneableBody = this.CloneableBody != null ? this.CloneableBody.Clone() : null,
               Headers = (INameValueCollection)this.Headers.Clone(),
               IsFeed = this.IsFeed,
               IsNameBased = this.IsNameBased,
               ResourceAddress = this.ResourceAddress,
               ResourceId = this.ResourceId,
               RequestAuthorizationTokenType = this.RequestAuthorizationTokenType,
               RequestContext = this.RequestContext.Clone(),
               PartitionKeyRangeIdentity = this.PartitionKeyRangeIdentity,
               UseGatewayMode = this.UseGatewayMode,
               QueryString  = this.QueryString,
               Continuation = this.Continuation,
               ForcePartitionKeyRangeRefresh = this.ForcePartitionKeyRangeRefresh,
               ForceCollectionRoutingMapRefresh = this.ForceCollectionRoutingMapRefresh,
               ForceMasterRefresh = this.ForceMasterRefresh,
               DefaultReplicaIndex = this.DefaultReplicaIndex,
               Properties = this.Properties,
               UseStatusCodeForFailures = this.UseStatusCodeForFailures,
               UseStatusCodeFor429 = this.UseStatusCodeFor429,
               DatabaseName = this.DatabaseName,
               CollectionName = this.CollectionName
            };
        }
    }
}
