//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Specialized;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.Azure.Documents.Routing;

    internal abstract class TransportClient : IDisposable
    {
        protected TransportClient()
        {

        }

        public virtual void Dispose()
        {
        }

        // Uses requests's ResourceOperation to determine the operation
        public virtual Task<StoreResponse> InvokeResourceOperationAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(physicalAddress, new ResourceOperation(request.OperationType, request.ResourceType), request);
        }

        #region Offer Operations

        public Task<StoreResponse> CreateOfferAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.CreateOffer,
                request);
        }

        public Task<StoreResponse> GetOfferAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.ReadOffer,
                request);
        }

        public Task<StoreResponse> ListOffersAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.ReadOfferFeed,
                request);
        }

        public Task<StoreResponse> DeleteOfferAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.DeleteOffer,
                request);
        }

        public Task<StoreResponse> ReplaceOfferAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.ReplaceOffer,
                request);
        }

        public Task<StoreResponse> QueryOfferAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeQueryStoreAsync(
                physicalAddress,
                ResourceType.Offer,
                request);
        }

        #endregion

#if !COSMOSCLIENT
        #region CrossPartitionReplicaSetInformation Operations
        public Task<StoreResponse> GetPartitionSetAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.ReadPartitionSetInformation,
                request);
        }

        public Task<StoreResponse> GetRestoreMetadataFeedAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.ReadRestoreMetadataFeed,
                request);
        }
        #endregion

#region Replica Operations
        public Task<StoreResponse> GetReplicaAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.ReadReplica,
                request);
        }
#endregion
#endif

#region Database Operations
        public Task<StoreResponse> ListDatabasesAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(physicalAddress, ResourceOperation.ReadDatabaseFeed, request);
        }
        public Task<StoreResponse> HeadDatabasesAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(physicalAddress, ResourceOperation.HeadDatabaseFeed, request);
        }
        public Task<StoreResponse> GetDatabaseAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.ReadDatabase,
                request);
        }
        public Task<StoreResponse> CreateDatabaseAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.CreateDatabase,
                request);
        }
        public Task<StoreResponse> UpsertDatabaseAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.UpsertDatabase,
                request);
        }
        public Task<StoreResponse> PatchDatabaseAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.PatchDatabase,
                request);
        }
        public Task<StoreResponse> ReplaceDatabaseAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.ReplaceDatabase,
                request);
        }
        public Task<StoreResponse> DeleteDatabaseAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.DeleteDatabase,
                request);
        }
        public Task<StoreResponse> QueryDatabasesAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeQueryStoreAsync(
                physicalAddress,
                ResourceType.Database,
                request);
        }
#endregion

#region DocumentCollection Operations
        public Task<StoreResponse> ListDocumentCollectionsAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.ReadCollectionFeed,
                request);
        }

        public Task<StoreResponse> GetDocumentCollectionAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.ReadCollection,
                request);
        }
        public Task<StoreResponse> HeadDocumentCollectionAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.HeadCollection,
                request);
        }
        public Task<StoreResponse> CreateDocumentCollectionAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.CreateCollection,
                request);
        }
        public Task<StoreResponse> PatchDocumentCollectionAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.PatchCollection,
                request);
        }
        public Task<StoreResponse> ReplaceDocumentCollectionAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.ReplaceCollection,
                request);
        }
        public Task<StoreResponse> DeleteDocumentCollectionAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.DeleteCollection,
                request);
        }
        public Task<StoreResponse> QueryDocumentCollectionsAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeQueryStoreAsync(
                physicalAddress,
                ResourceType.Collection,
                request);
        }
        #endregion

        #region EncryptionKey Operations

        public Task<StoreResponse> CreateClientEncryptionKeyAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.CreateClientEncryptionKey,
                request);
        }

        public Task<StoreResponse> ReadClientEncryptionKeyAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.ReadClientEncryptionKey,
                request);
        }

        public Task<StoreResponse> DeleteClientEncryptionKeyAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.DeleteClientEncryptionKey,
                request);
        }

        public Task<StoreResponse> ReadClientEncryptionKeyFeedAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.ReadClientEncryptionKeyFeed,
                request);
        }

        public Task<StoreResponse> ReplaceClientEncryptionKeyFeedAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.ReplaceClientEncryptionKey,
                request);
        }
      
        #endregion

        #region Sproc Operations
        public Task<StoreResponse> ListStoredProceduresAsync(Uri physicalAddress,
            DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.ReadStoredProcedureFeed,
                request);
        }

        public Task<StoreResponse> GetStoredProcedureAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.ReadStoredProcedure,
                request);
        }

        public Task<StoreResponse> CreateStoredProcedureAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.CreateStoredProcedure,
                request);
        }

        public Task<StoreResponse> UpsertStoredProcedureAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.UpsertStoredProcedure,
                request);
        }

        public Task<StoreResponse> ReplaceStoredProcedureAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.ReplaceStoredProcedure,
                request);
        }
        public Task<StoreResponse> DeleteStoredProcedureAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.DeleteStoredProcedure,
                request);
        }
        public Task<StoreResponse> QueryStoredProceduresAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeQueryStoreAsync(
                physicalAddress,
                ResourceType.StoredProcedure,
                request);
        }
#endregion

#region Trigger Operations
        public Task<StoreResponse> ListTriggersAsync(Uri physicalAddress,
            DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.XXReadTriggerFeed,
                request);
        }

        public Task<StoreResponse> GetTriggerAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.XXReadTrigger,
                request);
        }

        public Task<StoreResponse> CreateTriggerAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.XXCreateTrigger,
                request);
        }

        public Task<StoreResponse> UpsertTriggerAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.XXUpsertTrigger,
                request);
        }

        public Task<StoreResponse> ReplaceTriggerAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.XXReplaceTrigger,
                request);
        }
        public Task<StoreResponse> DeleteTriggerAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.XXDeleteTrigger,
                request);
        }
        public Task<StoreResponse> QueryTriggersAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeQueryStoreAsync(
                physicalAddress,
                ResourceType.Trigger,
                request);
        }
#endregion

#region UDF Operations
        public Task<StoreResponse> ListUserDefinedFunctionsAsync(Uri physicalAddress,
            DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.XXReadUserDefinedFunctionFeed,
                request);
        }

        public Task<StoreResponse> GetUserDefinedFunctionAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.XXReadUserDefinedFunction,
                request);
        }

        public Task<StoreResponse> CreateUserDefinedFunctionAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.XXCreateUserDefinedFunction,
                request);
        }

        public Task<StoreResponse> UpsertUserDefinedFunctionAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.XXUpsertUserDefinedFunction,
                request);
        }

        public Task<StoreResponse> ReplaceUserDefinedFunctionAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.XXReplaceUserDefinedFunction,
                request);
        }
        public Task<StoreResponse> DeleteUserDefinedFunctionAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.XXDeleteUserDefinedFunction,
                request);
        }
        public Task<StoreResponse> QueryUserDefinedFunctionsAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeQueryStoreAsync(
                physicalAddress,
                ResourceType.UserDefinedFunction,
                request);
        }
#endregion

#region Conflict Operations
        public Task<StoreResponse> ListConflictsAsync(Uri physicalAddress,
            DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.XReadConflictFeed,
                request);
        }

        public Task<StoreResponse> GetConflictAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.XReadConflict,
                request);
        }

        public Task<StoreResponse> DeleteConflictAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.XDeleteConflict,
                request);
        }
        public Task<StoreResponse> QueryConflictsAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeQueryStoreAsync(
                physicalAddress,
                ResourceType.Conflict,
                request);
        }
#endregion

#region Document Operations
        public Task<StoreResponse> ListDocumentsAsync(Uri physicalAddress,
            DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.ReadDocumentFeed,
                request);
        }

        public Task<StoreResponse> GetDocumentAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.ReadDocument,
                request);
        }

        public Task<StoreResponse> CreateDocumentAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.CreateDocument,
                request);
        }

        public Task<StoreResponse> UpsertDocumentAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.UpsertDocument,
                request);
        }

        public Task<StoreResponse> PatchDocumentAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.PatchDocument,
                request);
        }
        public Task<StoreResponse> ReplaceDocumentAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.ReplaceDocument,
                request);
        }
        public Task<StoreResponse> DeleteDocumentAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.DeleteDocument,
                request);
        }
        public Task<StoreResponse> QueryDocumentsAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeQueryStoreAsync(
                physicalAddress,
                ResourceType.Document,
                request);
        }
#endregion

#region Attachment Operations
        public Task<StoreResponse> ListAttachmentsAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.ReadAttachmentFeed,
                request);
        }

        public Task<StoreResponse> GetAttachmentAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.ReadAttachment,
                request);
        }

        public Task<StoreResponse> CreateAttachmentAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.CreateAttachment,
                request);
        }

        public Task<StoreResponse> UpsertAttachmentAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.UpsertAttachment,
                request);
        }

        public Task<StoreResponse> ReplaceAttachmentAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.ReplaceAttachment,
                request);
        }

        public Task<StoreResponse> DeleteAttachmentAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.DeleteAttachment,
                request);
        }

        public Task<StoreResponse> QueryAttachmentsAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeQueryStoreAsync(
                physicalAddress,
                ResourceType.Attachment,
                request);
        }
#endregion

#region User Operations

        public Task<StoreResponse> ListUsersAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(physicalAddress,
                ResourceOperation.ReadUserFeed,
                request);
        }
        public Task<StoreResponse> GetUserAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.ReadUser,
                request);
        }
        public Task<StoreResponse> CreateUserAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.CreateUser,
                request);
        }

        public Task<StoreResponse> UpsertUserAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.UpsertUser,
                request);
        }
        public Task<StoreResponse> PatchUserAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.PatchUser,
                request);
        }
        public Task<StoreResponse> ReplaceUserAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.ReplaceUser,
                request);
        }
        public Task<StoreResponse> DeleteUserAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.DeleteUser,
                request);
        }

        public Task<StoreResponse> QueryUsersAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeQueryStoreAsync(
                physicalAddress,
                ResourceType.User,
                request);
        }

#endregion

#region Permission Operations

        public Task<StoreResponse> ListPermissionsAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.ReadPermissionFeed,
                request);
        }
        public Task<StoreResponse> GetPermissionAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.ReadPermission,
                request);
        }
        public Task<StoreResponse> CreatePermissionAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.CreatePermission,
                request);
        }
        public Task<StoreResponse> UpsertPermissionAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.UpsertPermission,
                request);
        }
        public Task<StoreResponse> PatchPermissionAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.PatchPermission,
                request);
        }
        public Task<StoreResponse> ReplacePermissionAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.ReplacePermission,
                request);
        }
        public Task<StoreResponse> DeletePermissionAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.DeletePermission,
                request);
        }

        public Task<StoreResponse> QueryPermissionsAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeQueryStoreAsync(
                physicalAddress,
                ResourceType.Permission,
                request);
        }
#endregion

#region Row Operations
        public Task<StoreResponse> ListRecordsAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.XReadRecordFeed,
                request);
        }

        public Task<StoreResponse> CreateRecordAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.XCreateRecord,
                request);
        }

        public Task<StoreResponse> ReadRecordAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.XReadRecord,
                request);
        }

        public Task<StoreResponse> PatchRecordAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.XUpdateRecord,
                request);
        }

        public Task<StoreResponse> DeleteRecordAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.XDeleteRecord,
                request);
        }
#endregion

#region Execute Operations

        public Task<StoreResponse> ExecuteAsync(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.ExecuteDocumentFeed,
                request);
        }

#endregion

#region Transaction Operations

        public Task<StoreResponse> CompleteUserTransaction(Uri physicalAddress, DocumentServiceRequest request)
        {
            return this.InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.CompleteUserTransaction,
                request);
        }
#endregion

        public static void ThrowServerException(string resourceAddress, StoreResponse storeResponse, Uri physicalAddress, Guid activityId, DocumentServiceRequest request = null)
        {
            INameValueCollection responseHeaders;
            string errorMessage = null;

            // If the status code is < 300 or 304 NotModified (we treat not modified as success) then it means that it's a success code and shouldn't throw.
            // NotFound, PreconditionFailed and Conflict should not throw either if useStatusCodeForFailures is set
            if (storeResponse.Status < 300
               || (StatusCodes)storeResponse.Status == StatusCodes.NotModified
               || (request != null && request.IsValidStatusCodeForExceptionlessRetry(storeResponse.Status, storeResponse.SubStatusCode))
               )
            {
                return;
            }

            DocumentClientException exception;

            switch ((StatusCodes)storeResponse.Status)
            {
                case StatusCodes.Unauthorized:
                    errorMessage = TransportClient.GetErrorResponse(storeResponse, RMResources.Unauthorized, out responseHeaders);
                    exception = new UnauthorizedException(
                        string.Format(CultureInfo.CurrentUICulture,
                            RMResources.ExceptionMessage,
                            errorMessage),
                        responseHeaders,
                        physicalAddress);
                    break;

                case StatusCodes.Forbidden:
                    errorMessage = TransportClient.GetErrorResponse(storeResponse, RMResources.Forbidden, out responseHeaders);
                    exception = new ForbiddenException(
                        string.Format(CultureInfo.CurrentUICulture,
                            RMResources.ExceptionMessage,
                            errorMessage),
                        responseHeaders,
                        physicalAddress);
                    break;

                case StatusCodes.NotFound:
                    errorMessage = TransportClient.GetErrorResponse(storeResponse, RMResources.NotFound, out responseHeaders);
                    exception = new NotFoundException(
                        string.Format(CultureInfo.CurrentUICulture,
                            RMResources.ExceptionMessage,
                            errorMessage),
                        responseHeaders,
                        physicalAddress);
                    break;

                case StatusCodes.BadRequest:
                    errorMessage = TransportClient.GetErrorResponse(storeResponse, RMResources.BadRequest, out responseHeaders);
                    exception = new BadRequestException(
                        string.Format(CultureInfo.CurrentUICulture,
                            RMResources.ExceptionMessage,
                            errorMessage),
                        responseHeaders,
                        physicalAddress);
                    break;

                case StatusCodes.MethodNotAllowed:
                    errorMessage = TransportClient.GetErrorResponse(storeResponse, RMResources.MethodNotAllowed, out responseHeaders);
                    exception = new MethodNotAllowedException(
                        string.Format(CultureInfo.CurrentUICulture,
                            RMResources.ExceptionMessage,
                            errorMessage),
                        responseHeaders,
                        physicalAddress);
                    break;

                case StatusCodes.Gone:
                    {
#if NETFX
                        if (PerfCounters.Counters.RoutingFailures != null)
                        {
                            PerfCounters.Counters.RoutingFailures.Increment();
                        }
#endif

                        TransportClient.LogGoneException(physicalAddress, activityId.ToString());
                        errorMessage = TransportClient.GetErrorResponse(storeResponse,
                            RMResources.Gone,
                            out responseHeaders);

                        uint nSubStatus = 0;
                        string valueSubStatus = responseHeaders.Get(WFConstants.BackendHeaders.SubStatus);
                        if (!string.IsNullOrEmpty(valueSubStatus))
                        {
                            if (!uint.TryParse(valueSubStatus, NumberStyles.Integer, CultureInfo.InvariantCulture, out nSubStatus))
                            {
                                exception = new BadRequestException(
                                    string.Format(CultureInfo.CurrentUICulture,
                                        RMResources.ExceptionMessage,
                                        string.IsNullOrEmpty(errorMessage) ? RMResources.BadRequest : errorMessage),
                                    responseHeaders,
                                    physicalAddress);
                                break;
                            }
                        }

                        if ((SubStatusCodes)nSubStatus == SubStatusCodes.NameCacheIsStale)
                        {
                            exception = new InvalidPartitionException(
                                string.Format(CultureInfo.CurrentUICulture,
                                    RMResources.ExceptionMessage,
                                    errorMessage),
                                responseHeaders,
                                physicalAddress);
                            break;
                        }
                        else if ((SubStatusCodes)nSubStatus == SubStatusCodes.PartitionKeyRangeGone)
                        {
                            exception = new PartitionKeyRangeGoneException(
                                string.Format(CultureInfo.CurrentUICulture,
                                    RMResources.ExceptionMessage,
                                    errorMessage),
                                responseHeaders,
                                physicalAddress);
                            break;
                        }
                        else if ((SubStatusCodes)nSubStatus == SubStatusCodes.CompletingSplit)
                        {
                            exception = new PartitionKeyRangeIsSplittingException(
                                string.Format(CultureInfo.CurrentUICulture,
                                    RMResources.ExceptionMessage,
                                    errorMessage),
                                responseHeaders,
                                physicalAddress);
                            break;
                        }
                        else if ((SubStatusCodes)nSubStatus == SubStatusCodes.CompletingPartitionMigration)
                        {
                            exception = new PartitionIsMigratingException(
                                string.Format(CultureInfo.CurrentUICulture,
                                    RMResources.ExceptionMessage,
                                    errorMessage),
                                responseHeaders,
                                physicalAddress);
                            break;
                        }
                        else
                        {
                            // Have the request URL in the exception message for debugging purposes.
                            // Activity ID should already be there in the response headers.
                            exception = new GoneException(
                                string.Format(CultureInfo.CurrentUICulture,
                                        RMResources.ExceptionMessage,
                                        RMResources.Gone),
                                    responseHeaders,
                                    physicalAddress);
                            break;
                        }
                    }

                case StatusCodes.Conflict:
                    errorMessage = TransportClient.GetErrorResponse(storeResponse, RMResources.EntityAlreadyExists, out responseHeaders);
                    exception = new ConflictException(
                        string.Format(CultureInfo.CurrentUICulture,
                            RMResources.ExceptionMessage,
                            errorMessage),
                        responseHeaders,
                        physicalAddress);
                    break;

                case StatusCodes.PreconditionFailed:
                    errorMessage = TransportClient.GetErrorResponse(storeResponse, RMResources.PreconditionFailed, out responseHeaders);
                    exception = new PreconditionFailedException(
                        string.Format(CultureInfo.CurrentUICulture,
                            RMResources.ExceptionMessage,
                            errorMessage),
                        responseHeaders,
                        physicalAddress);
                    break;

                case StatusCodes.RequestEntityTooLarge:
                    errorMessage = TransportClient.GetErrorResponse(storeResponse, string.Format(CultureInfo.CurrentUICulture,
                        RMResources.RequestEntityTooLarge,
                        HttpConstants.HttpHeaders.PageSize),
                        out responseHeaders);
                    exception = new RequestEntityTooLargeException(
                        string.Format(CultureInfo.CurrentUICulture,
                            RMResources.ExceptionMessage,
                            errorMessage),
                        responseHeaders,
                        physicalAddress);
                    break;

                case StatusCodes.Locked:
                    errorMessage = TransportClient.GetErrorResponse(storeResponse, RMResources.Locked, out responseHeaders);
                    exception = new LockedException(
                        string.Format(CultureInfo.CurrentUICulture,
                            RMResources.ExceptionMessage,
                            errorMessage),
                        responseHeaders,
                        physicalAddress);
                    break;

                case StatusCodes.TooManyRequests:
                    errorMessage = TransportClient.GetErrorResponse(storeResponse, RMResources.TooManyRequests, out responseHeaders);
                    exception = new RequestRateTooLargeException(
                        string.Format(CultureInfo.CurrentUICulture,
                            RMResources.ExceptionMessage,
                            errorMessage),
                        responseHeaders,
                        physicalAddress);
                    break;

                case StatusCodes.ServiceUnavailable:
                    errorMessage = TransportClient.GetErrorResponse(storeResponse, RMResources.ServiceUnavailable, out responseHeaders);
                    exception = new ServiceUnavailableException(
                        string.Format(CultureInfo.CurrentUICulture,
                            RMResources.ExceptionMessage,
                            errorMessage),
                        responseHeaders,
                        physicalAddress);
                    break;

                case StatusCodes.RequestTimeout:
                    errorMessage = TransportClient.GetErrorResponse(storeResponse, RMResources.RequestTimeout, out responseHeaders);
                    exception = new RequestTimeoutException(
                        string.Format(CultureInfo.CurrentUICulture,
                            RMResources.ExceptionMessage,
                            errorMessage),
                        responseHeaders,
                        physicalAddress);
                    break;

                case StatusCodes.RetryWith:
                    errorMessage = TransportClient.GetErrorResponse(storeResponse, RMResources.RetryWith, out responseHeaders);
                    exception = new RetryWithException(
                        string.Format(CultureInfo.CurrentUICulture,
                            RMResources.ExceptionMessage,
                            errorMessage),
                        responseHeaders,
                        physicalAddress);
                    break;

                case StatusCodes.InternalServerError:
                    errorMessage = TransportClient.GetErrorResponse(storeResponse, RMResources.InternalServerError, out responseHeaders);
                    exception = new InternalServerErrorException(
                        string.Format(CultureInfo.CurrentUICulture,
                            RMResources.ExceptionMessage,
                            errorMessage),
                        responseHeaders,
                        physicalAddress);
                    break;

                default:
                    {
                        DefaultTrace.TraceCritical("Unrecognized status code {0} returned by backend. ActivityId {1}", storeResponse.Status, activityId);
                        TransportClient.LogException(null, physicalAddress, resourceAddress, activityId);
                        errorMessage = TransportClient.GetErrorResponse(storeResponse, RMResources.InvalidBackendResponse, out responseHeaders);
                        exception = new InternalServerErrorException(
                            string.Format(CultureInfo.CurrentUICulture,
                                RMResources.ExceptionMessage,
                                errorMessage),
                            responseHeaders,
                            physicalAddress);
                    }
                    break;
            }

            exception.LSN = storeResponse.LSN;
            exception.PartitionKeyRangeId = storeResponse.PartitionKeyRangeId;
            exception.ResourceAddress = resourceAddress;
            throw exception;
        }

        protected Task<StoreResponse> InvokeQueryStoreAsync(
            Uri physicalAddress,
            ResourceType resourceType,
            DocumentServiceRequest request)
        {
            string contentType = request.Headers[HttpConstants.HttpHeaders.ContentType];

            OperationType operationType;
            if (string.Equals(contentType, RuntimeConstants.MediaTypes.SQL, StringComparison.Ordinal))
            {
                operationType = OperationType.SqlQuery;
            }
            else
            {
                operationType = OperationType.Query;
            }

            return InvokeStoreAsync(
                physicalAddress,
                ResourceOperation.Query(operationType, resourceType),
                request);
        }
        
        internal abstract Task<StoreResponse> InvokeStoreAsync(
            Uri physicalAddress,
            ResourceOperation resourceOperation,
            DocumentServiceRequest request);

        protected async static Task<string> GetErrorResponseAsync(HttpResponseMessage responseMessage)
        {
            if (responseMessage.Content != null)
            {
                Stream responseStream = await responseMessage.Content.ReadAsStreamAsync();
                return TransportClient.GetErrorFromStream(responseStream);
            }
            else
            {
                return "";
            }
        }

        protected static string GetErrorResponse(StoreResponse storeResponse, string defaultMessage, out INameValueCollection responseHeaders)
        {
            string result = null;
            responseHeaders = storeResponse.Headers;

            if (storeResponse.ResponseBody != null)
            {
                result = TransportClient.GetErrorFromStream(storeResponse.ResponseBody);
            }

            return string.IsNullOrEmpty(result) ? defaultMessage : result;
        }

        protected static string GetErrorFromStream(Stream responseStream)
        {
            using (responseStream)
            {
                return new StreamReader(responseStream).ReadToEnd();
            }
        }

        protected static void LogException(Uri physicalAddress, string activityId)
        {
            DefaultTrace.TraceInformation(string.Format(CultureInfo.InvariantCulture, "Store Request Failed. Store Physical Address {0} ActivityId {1}",
                physicalAddress, activityId));
        }

        protected static void LogException(Exception exception, Uri physicalAddress, string rid, Guid activityId)
        {
            if (exception != null)
            {
                DefaultTrace.TraceInformation(string.Format(CultureInfo.InvariantCulture,
                    "Store Request Failed. Exception {0} Store Physical Address {1} RID {2} ActivityId {3}",
                    exception.Message, physicalAddress, rid, activityId.ToString()));
            }
            else
            {
                DefaultTrace.TraceInformation(string.Format(CultureInfo.InvariantCulture,
                    "Store Request Failed. Store Physical Address {0} RID {1} ActivityId {2}",
                    physicalAddress, rid, activityId.ToString()));
            }
        }

        protected static void LogGoneException(Uri physicalAddress, string activityId)
        {
            DefaultTrace.TraceInformation(string.Format(CultureInfo.InvariantCulture, "Listener not found. Store Physical Address {0} ActivityId {1}",
                physicalAddress, activityId));
        }
    }
}
