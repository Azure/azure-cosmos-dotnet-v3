//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;

    internal static class PathsHelper
    {
        private static readonly StringSegment[] EmptyArray = new StringSegment[0];

        private static readonly char[] PathSeparatorArray = new char[1] { '/' };

        /// <summary>
        ///     The output resourceId can be
        ///     a: (Rid based) DgJ5AJeIfQABAAAAAAAAAPy3CWY=
        ///     b: (name based) dbs/dbName/colls/collectionName/docs/documentName/attachments/attachmentName",
        ///     For name based, it always trimmed, RemoveTrailingSlashes, RemoveLeadingSlashes,  urldecoded
        /// </summary>
        /// <param name="resourceUrl"></param>
        /// <param name="isFeed"></param>
        /// <param name="resourcePath"> like dbs, colls</param>
        /// <param name="resourceIdOrFullName"></param>
        /// <param name="isNameBased"></param>
        /// <param name="clientVersion"></param>
        /// <returns></returns>
        public static bool TryParsePathSegments(
            string resourceUrl,
            out bool isFeed,
            out string resourcePath,
            out string resourceIdOrFullName,
            out bool isNameBased,
            string clientVersion = "")
        {
            string databaseName = string.Empty;
            string collectionName = string.Empty;

            return PathsHelper.TryParsePathSegmentsWithDatabaseAndCollectionNames(resourceUrl, out isFeed, out resourcePath, out resourceIdOrFullName, out isNameBased, out databaseName, out collectionName, clientVersion, false);
        }

        public static bool TryParsePathSegmentsWithDatabaseAndCollectionNames(
            string resourceUrl,
            out bool isFeed,
            out string resourcePath,
            out string resourceIdOrFullName,
            out bool isNameBased,
            out string databaseName,
            out string collectionName,
            string clientVersion = "",
            bool parseDatabaseAndCollectionNames = false)
        {
            return PathsHelper.TryParsePathSegmentsWithDatabaseAndCollectionAndDocumentNames(
                resourceUrl,
                out isFeed,
                out resourcePath,
                out resourceIdOrFullName,
                out isNameBased,
                out databaseName,
                out collectionName,
                out _,
                clientVersion,
                parseDatabaseAndCollectionNames);
        }

        /// <summary>
        ///     The output resourceId can be
        ///     a: (Rid based) DgJ5AJeIfQABAAAAAAAAAPy3CWY=
        ///     b: (name based) dbs/dbName/colls/collectionName/docs/documentName/attachments/attachmentName",
        ///     For name based, it always trimmed, RemoveTrailingSlashes, RemoveLeadingSlashes,  urldecoded
        /// </summary>
        /// <param name="resourceUrl"></param>
        /// <param name="isFeed"></param>
        /// <param name="resourcePath"> like dbs, colls</param>
        /// <param name="resourceIdOrFullName"></param>
        /// <param name="isNameBased"></param>
        /// <param name="databaseName"></param>
        /// <param name="collectionName"></param>
        /// <param name="documentName"></param>
        /// <param name="clientVersion"></param>
        /// <param name="parseDatabaseAndCollectionNames"></param>
        /// <returns></returns>
        public static bool TryParsePathSegmentsWithDatabaseAndCollectionAndDocumentNames(
            string resourceUrl,
            out bool isFeed,
            out string resourcePath,
            out string resourceIdOrFullName,
            out bool isNameBased,
            out string databaseName,
            out string collectionName,
            out string documentName,
            string clientVersion = "",
            bool parseDatabaseAndCollectionNames = false)
        {
            resourcePath = string.Empty;
            resourceIdOrFullName = string.Empty;
            isFeed = false;
            isNameBased = false;
            databaseName = string.Empty;
            collectionName = string.Empty;
            documentName = string.Empty;

            if (string.IsNullOrEmpty(resourceUrl))
            {
                return false;
            }

            string[] segments = resourceUrl.Split(PathsHelper.PathSeparatorArray, StringSplitOptions.RemoveEmptyEntries);

            if (segments == null || segments.Length < 1)
            {
                return false;
            }

            int uriSegmentsCount = segments.Length;
            StringSegment segmentOne = new StringSegment(segments[uriSegmentsCount - 1]).Trim(PathsHelper.PathSeparatorArray);
            StringSegment segmentTwo = new StringSegment(string.Empty);
            if (uriSegmentsCount >= 2)
            {
                segmentTwo = new StringSegment(segments[uriSegmentsCount - 2]).Trim(PathsHelper.PathSeparatorArray);
            } 

            // handle control operations
            if (PathsHelper.IsRootOperation(segmentTwo, segmentOne)
                || PathsHelper.IsTopLevelOperationOperation(segmentTwo, segmentOne))
            {
                isFeed = false;
                resourceIdOrFullName = string.Empty;
                resourcePath = Paths.Root;

                return true;
            }

            // handle name based operation
            if (uriSegmentsCount >= 2)
            {
                // parse the databaseId or snapshotId as RID. If failed, it is name based routing
                // mediaId is special, we will treat it always as RID based.
                ResourceId rid;
                if (segments[0].Equals(Paths.DatabasesPathSegment, StringComparison.OrdinalIgnoreCase) &&
                    (!ResourceId.TryParse(segments[1], out rid) || !rid.IsDatabaseId))
                {
                    isNameBased = true;
                }
                else if (segments[0].Equals(Paths.SnapshotsPathSegment, StringComparison.OrdinalIgnoreCase) &&
                    (!ResourceId.TryParse(segments[1], out rid) || !rid.IsSnapshotId))
                {
                    isNameBased = true;
                }
                else if (segments[0].Equals(Paths.RoleDefinitionsPathSegment, StringComparison.OrdinalIgnoreCase) &&
                         (!ResourceId.TryParse(segments[1], out rid) || !rid.IsRoleDefinitionId))
                {
                    isNameBased = true;
                }
                else if (segments[0].Equals(Paths.RoleAssignmentsPathSegment, StringComparison.OrdinalIgnoreCase) &&
                         (!ResourceId.TryParse(segments[1], out rid) || !rid.IsRoleAssignmentId))
                {
                    isNameBased = true;
                }

                if (isNameBased)
                {
                    return TryParseNameSegments(resourceUrl, segments, out isFeed, out resourcePath, out resourceIdOrFullName, out databaseName, out collectionName, out documentName, parseDatabaseAndCollectionNames);
                }
            }

            // Feed paths have odd number of segments
            if ((uriSegmentsCount % 2 != 0) && PathsHelper.IsResourceType(segmentOne))
            {
                isFeed = true;
                resourcePath = segmentOne.GetString();

                // The URL for dbs may contain the management endpoint as the segmentTwo which should not be used as resourceId
                if (!segmentOne.Equals(Paths.DatabasesPathSegment, StringComparison.OrdinalIgnoreCase))
                {
                    resourceIdOrFullName = segmentTwo.GetString();
                }
            }
            else if (PathsHelper.IsResourceType(segmentTwo))
            {
                isFeed = false;
                resourcePath = segmentTwo.GetString();
                resourceIdOrFullName = segmentOne.GetString();

                // Media ID is not supposed to be used for any ID verification. However, if the old client makes a call for media ID
                // we still need to support it.
                // For new clients, parse to return the attachment id. For old clients do not modify.
                if (!string.IsNullOrEmpty(clientVersion) &&
                    resourcePath.Equals(Paths.MediaPathSegment, StringComparison.OrdinalIgnoreCase))
                {
                    string attachmentId = null;
                    byte storeIndex = 0;
                    if (!MediaIdHelper.TryParseMediaId(resourceIdOrFullName, out attachmentId, out storeIndex))
                    {
                        return false;
                    }

                    resourceIdOrFullName = attachmentId;
                }
            }
            else
            {
                return false;
            }

            return true;
        }

        public static void ParseDatabaseNameAndCollectionNameFromUrlSegments(
            string[] segments,
            out string databaseName,
            out string collectionName)
        {
            PathsHelper.ParseDatabaseNameAndCollectionAndDocumentNameFromUrlSegments(
                segments,
                out databaseName,
                out collectionName,
                out _);
        }

        public static void ParseDatabaseNameAndCollectionAndDocumentNameFromUrlSegments(
            string[] segments,
            out string databaseName,
            out string collectionName,
            out string documentName)
        {
            databaseName = string.Empty;
            collectionName = string.Empty;
            documentName = string.Empty;

            if (segments == null || segments.Length < 2)
            {
                return;
            }

            if (string.Equals(segments[0], Paths.DatabasesPathSegment, StringComparison.OrdinalIgnoreCase))
            {
                databaseName = Uri.UnescapeDataString(UrlUtility.RemoveTrailingSlashes(UrlUtility.RemoveLeadingSlashes(new StringSegment(segments[1]))).GetString());
                if (segments.Length >= 4 && string.Equals(segments[2], Paths.CollectionsPathSegment, StringComparison.OrdinalIgnoreCase))
                {
                    collectionName = Uri.UnescapeDataString(UrlUtility.RemoveTrailingSlashes(UrlUtility.RemoveLeadingSlashes(new StringSegment(segments[3]))).GetString());
                }

                if (segments.Length >= 6 && string.Equals(segments[4], Paths.DocumentsPathSegment, StringComparison.OrdinalIgnoreCase))
                {
                    documentName = Uri.UnescapeDataString(UrlUtility.RemoveTrailingSlashes(UrlUtility.RemoveLeadingSlashes(new StringSegment(segments[5]))).GetString());
                }
            }
        }

        private static bool TryParseNameSegments(
            string resourceUrl,
            string[] segments,
            out bool isFeed,
            out string resourcePath,
            out string resourceFullName,
            out string databaseName,
            out string collectionName,
            out string documentName,
            bool parseDatabaseAndCollectionNames)
        {
            isFeed = false;
            resourcePath = string.Empty;
            resourceFullName = string.Empty;
            databaseName = string.Empty;
            collectionName = string.Empty;
            documentName = string.Empty;

            if (segments == null || segments.Length < 1)
            {
                return false;
            }

            if (segments.Length % 2 == 0)
            {
                // even number, assume it is individual resource.
                if (PathsHelper.IsResourceType(segments[segments.Length - 2]))
                {
                    resourcePath = segments[segments.Length - 2];
                    resourceFullName = Uri.UnescapeDataString(UrlUtility.RemoveTrailingSlashes(UrlUtility.RemoveLeadingSlashes(new StringSegment(resourceUrl))).GetString());
                    if (parseDatabaseAndCollectionNames)
                    {
                        PathsHelper.ParseDatabaseNameAndCollectionAndDocumentNameFromUrlSegments(segments, out databaseName, out collectionName, out documentName);
                    }

                    return true;
                }
            }
            else
            {
                // odd number, assume it is feed request
                if (PathsHelper.IsResourceType(segments[segments.Length - 1]))
                {
                    isFeed = true;
                    resourcePath = segments[segments.Length - 1];
                    // remove the trailing resource type
                    StringSegment trimmedSegment = resourceUrl;
                    trimmedSegment = trimmedSegment.Substring(0, UrlUtility.RemoveTrailingSlashes(trimmedSegment).LastIndexOf(Paths.Root[0]));
                    resourceFullName = Uri.UnescapeDataString(UrlUtility.RemoveTrailingSlashes(UrlUtility.RemoveLeadingSlashes(trimmedSegment)).GetString());
                    if (parseDatabaseAndCollectionNames)
                    {
                        // feed requests would not point to a document so ignore document Name.
                        PathsHelper.ParseDatabaseNameAndCollectionAndDocumentNameFromUrlSegments(segments, out databaseName, out collectionName, out _);
                    }

                    return true;
                }
            }

            return false;
        }

        public static ResourceType GetResourcePathSegment(string resourcePathSegment)
        {
            if (string.IsNullOrEmpty(resourcePathSegment))
            {
                string message = string.Format(CultureInfo.CurrentUICulture, RMResources.StringArgumentNullOrEmpty, "resourcePathSegment");
                Debug.Assert(false, message);
                throw new BadRequestException(message);
            }

            switch (resourcePathSegment.ToLowerInvariant())
            {
                case Paths.AttachmentsPathSegment:
                    return ResourceType.Attachment;

                case Paths.CollectionsPathSegment:
                    return ResourceType.Collection;

                case Paths.DatabasesPathSegment:
                    return ResourceType.Database;

                case Paths.PermissionsPathSegment:
                    return ResourceType.Permission;

                case Paths.UsersPathSegment:
                    return ResourceType.User;

                case Paths.ClientEncryptionKeysPathSegment:
                    return ResourceType.ClientEncryptionKey;

                case Paths.UserDefinedTypesPathSegment:
                    return ResourceType.UserDefinedType;

                case Paths.DocumentsPathSegment:
                    return ResourceType.Document;

                case Paths.StoredProceduresPathSegment:
                    return ResourceType.StoredProcedure;

                case Paths.UserDefinedFunctionsPathSegment:
                    return ResourceType.UserDefinedFunction;

                case Paths.TriggersPathSegment:
                    return ResourceType.Trigger;

                case Paths.ConflictsPathSegment:
                    return ResourceType.Conflict;

                case Paths.OffersPathSegment:
                    return ResourceType.Offer;

                case Paths.SchemasPathSegment:
                    return ResourceType.Schema;

                case Paths.PartitionKeyRangesPathSegment:
                    return ResourceType.PartitionKeyRange;

                case Paths.MediaPathSegment:
                    return ResourceType.Media;

                case Paths.AddressPathSegment:
                    return ResourceType.Address;

                case Paths.SnapshotsPathSegment:
                    return ResourceType.Snapshot;

                case Paths.RoleDefinitionsPathSegment:
                    return ResourceType.RoleDefinition;

                case Paths.RoleAssignmentsPathSegment:
                    return ResourceType.RoleAssignment;

                case Paths.SystemDocumentsPathSegment:
                    return ResourceType.SystemDocument;

                case Paths.PartitionedSystemDocumentsPathSegment:
                    return ResourceType.PartitionedSystemDocument;
            }

            string errorMessage = string.Format(CultureInfo.CurrentUICulture, RMResources.UnknownResourceType, resourcePathSegment);
            Debug.Assert(false, errorMessage);
            throw new BadRequestException(errorMessage);
        }

        public static string GetResourcePath(ResourceType resourceType)
        {
            switch (resourceType)
            {
                case ResourceType.Database:
                    return Paths.DatabasesPathSegment;

                case ResourceType.Collection:
                case ResourceType.PartitionKey:
                    return Paths.CollectionsPathSegment;

                case ResourceType.Document:
                    return Paths.DocumentsPathSegment;

                case ResourceType.StoredProcedure:
                    return Paths.StoredProceduresPathSegment;

                case ResourceType.UserDefinedFunction:
                    return Paths.UserDefinedFunctionsPathSegment;

                case ResourceType.Trigger:
                    return Paths.TriggersPathSegment;

                case ResourceType.Conflict:
                    return Paths.ConflictsPathSegment;

                case ResourceType.Attachment:
                    return Paths.AttachmentsPathSegment;

                case ResourceType.User:
                    return Paths.UsersPathSegment;

                case ResourceType.ClientEncryptionKey:
                    return Paths.ClientEncryptionKeysPathSegment;

                case ResourceType.UserDefinedType:
                    return Paths.UserDefinedTypesPathSegment;

                case ResourceType.Permission:
                    return Paths.PermissionsPathSegment;

                case ResourceType.Offer:
                    return Paths.OffersPathSegment;

                case ResourceType.PartitionKeyRange:
                    return Paths.PartitionKeyRangesPathSegment;

                case ResourceType.Media:
                    return Paths.Medias_Root;

                case ResourceType.Schema:
                    return Paths.SchemasPathSegment;

                case ResourceType.Snapshot:
                    return Paths.SnapshotsPathSegment;

                case ResourceType.PartitionedSystemDocument:
                    return Paths.PartitionedSystemDocumentsPathSegment;

                case ResourceType.RoleDefinition:
                    return Paths.RoleDefinitionsPathSegment;

                case ResourceType.RoleAssignment:
                    return Paths.RoleAssignmentsPathSegment;
                
                case ResourceType.Transaction:
                    return Paths.TransactionsPathSegment;

                case ResourceType.SystemDocument:
                    return Paths.SystemDocumentsPathSegment;

#if !COSMOSCLIENT
                case ResourceType.MasterPartition:
                case ResourceType.ServerPartition:
                    return Paths.PartitionsPathSegment;

                case ResourceType.RidRange:
                    return Paths.RidRangePathSegment;

                case ResourceType.VectorClock:
                    return Paths.VectorClockPathSegment;

                case ResourceType.PartitionSetInformation:
                case ResourceType.XPReplicatorAddress:
                case ResourceType.Topology:
                case ResourceType.Replica:
                case ResourceType.ServiceFabricService:
                case ResourceType.RestoreMetadata:
                case ResourceType.Module:
                case ResourceType.ModuleCommand:
#endif
                case ResourceType.DatabaseAccount:
                case ResourceType.Address:
                case ResourceType.Record:
                case ResourceType.BatchApply:
                case ResourceType.ControllerService:
                    return Paths.Root;

                default:
                    string errorMessage = string.Format(CultureInfo.CurrentUICulture, RMResources.UnknownResourceType, resourceType.ToString());
                    Debug.Assert(false, errorMessage);
                    throw new BadRequestException(errorMessage);
            }
        }

        public static string GeneratePath(ResourceType resourceType, DocumentServiceRequest request, bool isFeed, bool notRequireValidation = false)
        {
            if (request.IsNameBased)
            {
                return PathsHelper.GeneratePathForNameBased(resourceType, request.ResourceAddress, isFeed, request.OperationType, notRequireValidation);
            }
            else
            {
                return PathsHelper.GeneratePath(resourceType, request.ResourceId, isFeed, request.OperationType);
            }
        }

        public static string GenerateUserDefinedTypePath(string databaseName, string typeName)
        {
            return Paths.DatabasesPathSegment + "/" + databaseName + "/" + Paths.UserDefinedTypesPathSegment + "/" + typeName;
        }

        public static string GetCollectionPath(string resourceFullName)
        {
            if (resourceFullName != null)
            {
                int index = resourceFullName.Length > 0 && resourceFullName[0] == '/' ? resourceFullName.IndexOfNth('/', 5) : resourceFullName.IndexOfNth('/', 4);
                if (index > 0)
                    return resourceFullName.Substring(0, index);
            }
            return resourceFullName;
        }

        public static string GetDatabasePath(string resourceFullName)
        {
            if (resourceFullName != null)
            {
                int index = resourceFullName.Length > 0 && resourceFullName[0] == '/' ? resourceFullName.IndexOfNth('/', 3) : resourceFullName.IndexOfNth('/', 2);
                if (index > 0)
                    return resourceFullName.Substring(0, index);
            }
            return resourceFullName;
        }

        public static string GetParentByIndex(string resourceFullName, int segmentIndex)
        {
            int index = resourceFullName.IndexOfNth('/', segmentIndex);
            if (index > 0)
                return resourceFullName.Substring(0, index);
            else
            {
                index = resourceFullName.IndexOfNth('/', segmentIndex - 1);
                if (index > 0)
                    return resourceFullName;
                else
                    return null;
            }
        }

        public static string GeneratePathForNameBased(Type resourceType, string resourceOwnerFullName, string resourceName)
        {
            if (resourceName == null)
                return null;

            if (resourceType == typeof(Database))
            {
                return Paths.DatabasesPathSegment + "/" + resourceName;
            }
            else if (resourceType == typeof(Snapshot))
            {
                return Paths.SnapshotsPathSegment + "/" + resourceName;
            }
            else if (resourceOwnerFullName == null)
            {
                return null;
            }
            else if (resourceType == typeof(DocumentCollection))
            {
                return resourceOwnerFullName + "/" + Paths.CollectionsPathSegment + "/" + resourceName;
            }
            else if (resourceType == typeof(ClientEncryptionKey))
            {
                return resourceOwnerFullName + "/" + Paths.ClientEncryptionKeysPathSegment + "/" + resourceName;
            }
            else if (resourceType == typeof(StoredProcedure))
            {
                return resourceOwnerFullName + "/" + Paths.StoredProceduresPathSegment + "/" + resourceName;
            }
            else if (resourceType == typeof(UserDefinedFunction))
            {
                return resourceOwnerFullName + "/" + Paths.UserDefinedFunctionsPathSegment + "/" + resourceName;
            }
            else if (resourceType == typeof(Trigger))
            {
                return resourceOwnerFullName + "/" + Paths.TriggersPathSegment + "/" + resourceName;
            }
            else if (resourceType == typeof(Conflict))
            {
                return resourceOwnerFullName + "/" + Paths.ConflictsPathSegment + "/" + resourceName;
            }
            else if (typeof(Attachment).IsAssignableFrom(resourceType))
            {
                return resourceOwnerFullName + "/" + Paths.AttachmentsPathSegment + "/" + resourceName;
            }
            else if (resourceType == typeof(User))
            {
                return resourceOwnerFullName + "/" + Paths.UsersPathSegment + "/" + resourceName;
            }
            else if (resourceType == typeof(UserDefinedType))
            {
                return resourceOwnerFullName + "/" + Paths.UserDefinedTypesPathSegment + "/" + resourceName;
            }
            else if (typeof(Permission).IsAssignableFrom(resourceType))
            {
                return resourceOwnerFullName + "/" + Paths.PermissionsPathSegment + "/" + resourceName;
            }
            else if (typeof(Document).IsAssignableFrom(resourceType))
            {
                return resourceOwnerFullName + "/" + Paths.DocumentsPathSegment + "/" + resourceName;
            }
            else if (resourceType == typeof(Offer))
            {
                return Paths.OffersPathSegment + "/" + resourceName;
            }
            else if (resourceType == typeof(Schema))
            {
                return resourceOwnerFullName + "/" + Paths.SchemasPathSegment + "/" + resourceName;
            }
            else if (resourceType == typeof(SystemDocument))
            {
                return resourceOwnerFullName + "/" + Paths.SystemDocumentsPathSegment + "/" + resourceName;
            }
            else if (resourceType == typeof(PartitionedSystemDocument))
            {
                return resourceOwnerFullName + "/" + Paths.PartitionedSystemDocumentsPathSegment + "/" + resourceName;
            }
            else if (typeof(Resource).IsAssignableFrom(resourceType))
            {
                // just generic Resource type.
                return null;
            }

            string errorMessage = string.Format(CultureInfo.CurrentUICulture, RMResources.UnknownResourceType, resourceType.ToString());
            Debug.Assert(false, errorMessage);
            throw new BadRequestException(errorMessage);
        }

        public static string GeneratePathForNamedBasedInternalResources(ResourceType resourceType, string resourceName)
        {
            if (resourceName == null)
                return null;

            return resourceType switch
            {
                ResourceType.RoleAssignment => Paths.RoleAssignmentsPathSegment + "/" + resourceName,
                ResourceType.RoleDefinition => Paths.RoleDefinitionsPathSegment + "/" + resourceName,
                _ => null
            };
        }

        // for testing to set to verify server side validation
        private static bool isClientSideValidationEnabled = true;

        internal static void SetClientSidevalidation(bool validation)
        {
            isClientSideValidationEnabled = validation;
        }

        private static string GeneratePathForNameBased(ResourceType resourceType, string resourceFullName, bool isFeed, OperationType operationType, bool notRequireValidation = false)
        {
            if (isFeed &&
                string.IsNullOrEmpty(resourceFullName) &&
                resourceType != ResourceType.Database &&
                resourceType != ResourceType.Snapshot &&
                resourceType != ResourceType.RoleDefinition &&
                resourceType != ResourceType.RoleAssignment)
            {
                string errorMessage = string.Format(CultureInfo.InvariantCulture, RMResources.UnexpectedResourceType, resourceType);
                throw new BadRequestException(errorMessage);
            }

            string resourcePath = null;
            ResourceType resourceTypeToValidate;
            // Validate resourceFullName comply with the intended resource type.

            if (resourceType == ResourceType.PartitionKey && operationType == OperationType.Delete)
            {
                resourceTypeToValidate = resourceType;
                resourceFullName = resourceFullName + "/" + Paths.OperationsPathSegment + "/" + Paths.PartitionKeyDeletePathSegment;
                resourcePath = resourceFullName;
            }
            else if ((resourceType == ResourceType.Collection) && (operationType == OperationType.CollectionTruncate))
            {
                resourceTypeToValidate = ResourceType.Collection;
                resourcePath = resourceFullName + "/" + Paths.OperationsPathSegment + "/" + Paths.CollectionTruncatePathsegment;
            }
            else if (!isFeed)
            {
                resourceTypeToValidate = resourceType;
                resourcePath = resourceFullName;
            }
            else if (resourceType == ResourceType.Database)
            {
                return Paths.DatabasesPathSegment;
            }
            else if (resourceType == ResourceType.Collection)
            {
                resourceTypeToValidate = ResourceType.Database;
                resourcePath = resourceFullName + "/" + Paths.CollectionsPathSegment;
            }
            else if (resourceType == ResourceType.ClientEncryptionKey)
            {
                resourceTypeToValidate = ResourceType.Database;
                resourcePath = resourceFullName + "/" + Paths.ClientEncryptionKeysPathSegment;
            }
            else if (resourceType == ResourceType.StoredProcedure)
            {
                resourceTypeToValidate = ResourceType.Collection;
                resourcePath = resourceFullName + "/" + Paths.StoredProceduresPathSegment;
            }
            else if (resourceType == ResourceType.UserDefinedFunction)
            {
                resourceTypeToValidate = ResourceType.Collection;
                resourcePath = resourceFullName + "/" + Paths.UserDefinedFunctionsPathSegment;
            }
            else if (resourceType == ResourceType.Trigger)
            {
                resourceTypeToValidate = ResourceType.Collection;
                resourcePath = resourceFullName + "/" + Paths.TriggersPathSegment;
            }
            else if (resourceType == ResourceType.Conflict)
            {
                resourceTypeToValidate = ResourceType.Collection;
                resourcePath = resourceFullName + "/" + Paths.ConflictsPathSegment;
            }
            else if (resourceType == ResourceType.Attachment)
            {
                resourceTypeToValidate = ResourceType.Document;
                resourcePath = resourceFullName + "/" + Paths.AttachmentsPathSegment;
            }
            else if (resourceType == ResourceType.User)
            {
                resourceTypeToValidate = ResourceType.Database;
                resourcePath = resourceFullName + "/" + Paths.UsersPathSegment;
            }
            else if (resourceType == ResourceType.UserDefinedType)
            {
                resourceTypeToValidate = ResourceType.Database;
                resourcePath = resourceFullName + "/" + Paths.UserDefinedTypesPathSegment;
            }
            else if (resourceType == ResourceType.Permission)
            {
                resourceTypeToValidate = ResourceType.User;
                resourcePath = resourceFullName + "/" + Paths.PermissionsPathSegment;
            }
            else if (resourceType == ResourceType.Document)
            {
                resourceTypeToValidate = ResourceType.Collection;
                resourcePath = resourceFullName + "/" + Paths.DocumentsPathSegment;
            }
            else if (resourceType == ResourceType.Offer)
            {
                return resourceFullName + "/" + Paths.OffersPathSegment;
            }
            else if (resourceType == ResourceType.PartitionKeyRange)
            {
                return resourceFullName + "/" + Paths.PartitionKeyRangesPathSegment;
            }
            else if (resourceType == ResourceType.Schema)
            {
                resourceTypeToValidate = ResourceType.Collection;
                resourcePath = resourceFullName + "/" + Paths.SchemasPathSegment;
            }
            else if (resourceType == ResourceType.PartitionedSystemDocument)
            {
                resourceTypeToValidate = ResourceType.Collection;
                resourcePath = resourceFullName + "/" + Paths.PartitionedSystemDocumentsPathSegment;
            }
            else if (resourceType == ResourceType.Snapshot)
            {
                return Paths.SnapshotsPathSegment;
            }
            else if (resourceType == ResourceType.RoleDefinition)
            {
                return Paths.RoleDefinitionsPathSegment;
            }
            else if (resourceType == ResourceType.RoleAssignment)
            {
                return Paths.RoleAssignmentsPathSegment;
            }
            else if (resourceType == ResourceType.SystemDocument)
            {
                resourceTypeToValidate = ResourceType.Collection;
                resourcePath = resourceFullName + "/" + Paths.SystemDocumentsPathSegment;
            }
            else
            {
                string errorMessage = string.Format(CultureInfo.CurrentUICulture, RMResources.UnknownResourceType, resourceType.ToString());
                Debug.Assert(false, errorMessage);
                throw new BadRequestException(errorMessage);
            }

            if (!notRequireValidation && isClientSideValidationEnabled)
            {
                if (!ValidateResourceFullName(resourceTypeToValidate, resourceFullName))
                {
                    string errorMessage = string.Format(CultureInfo.InvariantCulture, RMResources.UnexpectedResourceType, resourceType);
                    throw new BadRequestException(errorMessage);
                }
            }
            return resourcePath;
        }


        public static string GeneratePath(ResourceType resourceType, string ownerOrResourceId, bool isFeed, OperationType operationType = default)
        {
            if (isFeed && string.IsNullOrEmpty(ownerOrResourceId) &&
                resourceType != ResourceType.Database &&
                resourceType != ResourceType.Offer &&
                resourceType != ResourceType.DatabaseAccount &&
                resourceType != ResourceType.Snapshot &&
                resourceType != ResourceType.RoleAssignment &&
                resourceType != ResourceType.RoleDefinition
#if !COSMOSCLIENT
                && resourceType != ResourceType.MasterPartition &&
                resourceType != ResourceType.ServerPartition &&
                resourceType != ResourceType.Topology &&
                resourceType != ResourceType.RidRange &&
                resourceType != ResourceType.VectorClock
#endif
                )
            {
                throw new BadRequestException(string.Format(CultureInfo.InvariantCulture, RMResources.UnexpectedResourceType, resourceType));
            }

            if (isFeed && resourceType == ResourceType.Database)
            {
                return Paths.DatabasesPathSegment;
            }
            else if (resourceType == ResourceType.Database)
            {
                return Paths.DatabasesPathSegment + "/" + ownerOrResourceId.ToString();
            }
            else if (isFeed && resourceType == ResourceType.Collection)
            {
                ResourceId documentCollectionId = ResourceId.Parse(ownerOrResourceId);

                return Paths.DatabasesPathSegment + "/" + documentCollectionId.DatabaseId.ToString() + "/" +
                    Paths.CollectionsPathSegment;
            }
            else if (resourceType == ResourceType.Collection)
            {
                ResourceId documentCollectionId = ResourceId.Parse(ownerOrResourceId);

                return Paths.DatabasesPathSegment + "/" + documentCollectionId.DatabaseId.ToString() + "/" +
                    Paths.CollectionsPathSegment + "/" + documentCollectionId.DocumentCollectionId.ToString();
            }
            else if (isFeed && resourceType == ResourceType.Offer)
            {
                return Paths.OffersPathSegment;
            }
            else if (resourceType == ResourceType.Offer)
            {
                return Paths.OffersPathSegment + "/" + ownerOrResourceId.ToString();
            }
            else if (isFeed && resourceType == ResourceType.StoredProcedure)
            {
                ResourceId documentCollectionId = ResourceId.Parse(ownerOrResourceId);

                return
                    Paths.DatabasesPathSegment + "/" + documentCollectionId.DatabaseId.ToString() + "/" +
                    Paths.CollectionsPathSegment + "/" + documentCollectionId.DocumentCollectionId.ToString() + "/" +
                    Paths.StoredProceduresPathSegment;
            }
            else if (resourceType == ResourceType.StoredProcedure)
            {
                ResourceId storedProcedureId = ResourceId.Parse(ownerOrResourceId);

                return Paths.DatabasesPathSegment + "/" + storedProcedureId.DatabaseId.ToString() + "/" +
                    Paths.CollectionsPathSegment + "/" + storedProcedureId.DocumentCollectionId.ToString() + "/" +
                    Paths.StoredProceduresPathSegment + "/" + storedProcedureId.StoredProcedureId.ToString();
            }
            else if (isFeed && resourceType == ResourceType.UserDefinedFunction)
            {
                ResourceId documentCollectionId = ResourceId.Parse(ownerOrResourceId);

                return
                    Paths.DatabasesPathSegment + "/" + documentCollectionId.DatabaseId.ToString() + "/" +
                    Paths.CollectionsPathSegment + "/" + documentCollectionId.DocumentCollectionId.ToString() + "/" +
                    Paths.UserDefinedFunctionsPathSegment;
            }
            else if (resourceType == ResourceType.UserDefinedFunction)
            {
                ResourceId functionId = ResourceId.Parse(ownerOrResourceId);

                return Paths.DatabasesPathSegment + "/" + functionId.DatabaseId.ToString() + "/" +
                    Paths.CollectionsPathSegment + "/" + functionId.DocumentCollectionId.ToString() + "/" +
                    Paths.UserDefinedFunctionsPathSegment + "/" + functionId.UserDefinedFunctionId.ToString();
            }
            else if (isFeed && resourceType == ResourceType.Trigger)
            {
                ResourceId documentCollectionId = ResourceId.Parse(ownerOrResourceId);

                return
                    Paths.DatabasesPathSegment + "/" + documentCollectionId.DatabaseId.ToString() + "/" +
                    Paths.CollectionsPathSegment + "/" + documentCollectionId.DocumentCollectionId.ToString() + "/" +
                    Paths.TriggersPathSegment;
            }
            else if (resourceType == ResourceType.Trigger)
            {
                ResourceId triggerId = ResourceId.Parse(ownerOrResourceId);

                return Paths.DatabasesPathSegment + "/" + triggerId.DatabaseId.ToString() + "/" +
                    Paths.CollectionsPathSegment + "/" + triggerId.DocumentCollectionId.ToString() + "/" +
                    Paths.TriggersPathSegment + "/" + triggerId.TriggerId.ToString();
            }
            else if (isFeed && resourceType == ResourceType.Conflict)
            {
                ResourceId documentCollectionId = ResourceId.Parse(ownerOrResourceId);

                return
                    Paths.DatabasesPathSegment + "/" + documentCollectionId.DatabaseId.ToString() + "/" +
                    Paths.CollectionsPathSegment + "/" + documentCollectionId.DocumentCollectionId.ToString() + "/" +
                    Paths.ConflictsPathSegment;
            }
            else if (resourceType == ResourceType.Conflict)
            {
                ResourceId conflictId = ResourceId.Parse(ownerOrResourceId);

                return Paths.DatabasesPathSegment + "/" + conflictId.DatabaseId.ToString() + "/" +
                    Paths.CollectionsPathSegment + "/" + conflictId.DocumentCollectionId.ToString() + "/" +
                    Paths.ConflictsPathSegment + "/" + conflictId.ConflictId.ToString();
            }
            else if (isFeed && resourceType == ResourceType.PartitionKeyRange)
            {
                ResourceId documentCollectionId = ResourceId.Parse(ownerOrResourceId);

                return
                    Paths.DatabasesPathSegment + "/" + documentCollectionId.DatabaseId.ToString() + "/" +
                    Paths.CollectionsPathSegment + "/" + documentCollectionId.DocumentCollectionId.ToString() + "/" +
                    Paths.PartitionKeyRangesPathSegment;
            }
            else if (resourceType == ResourceType.PartitionKeyRange)
            {
                ResourceId partitionKeyRangeId = ResourceId.Parse(ownerOrResourceId);

                return Paths.DatabasesPathSegment + "/" + partitionKeyRangeId.DatabaseId.ToString() + "/" +
                    Paths.CollectionsPathSegment + "/" + partitionKeyRangeId.DocumentCollectionId.ToString() + "/" +
                    Paths.PartitionKeyRangesPathSegment + "/" + partitionKeyRangeId.PartitionKeyRangeId.ToString();
            }
            else if (isFeed && resourceType == ResourceType.Attachment)
            {
                ResourceId documentCollectionId = ResourceId.Parse(ownerOrResourceId);

                return
                    Paths.DatabasesPathSegment + "/" + documentCollectionId.DatabaseId.ToString() + "/" +
                    Paths.CollectionsPathSegment + "/" + documentCollectionId.DocumentCollectionId.ToString() + "/" +
                    Paths.DocumentsPathSegment + "/" + documentCollectionId.DocumentId.ToString() + "/" +
                    Paths.AttachmentsPathSegment;
            }
            else if (resourceType == ResourceType.Attachment)
            {
                ResourceId attachmentId = ResourceId.Parse(ownerOrResourceId);

                return Paths.DatabasesPathSegment + "/" + attachmentId.DatabaseId.ToString() + "/" +
                    Paths.CollectionsPathSegment + "/" + attachmentId.DocumentCollectionId.ToString() + "/" +
                    Paths.DocumentsPathSegment + "/" + attachmentId.DocumentId.ToString() + "/" +
                    Paths.AttachmentsPathSegment + "/" + attachmentId.AttachmentId.ToString();
            }
            else if (isFeed && resourceType == ResourceType.User)
            {
                return
                    Paths.DatabasesPathSegment + "/" + ownerOrResourceId + "/" +
                    Paths.UsersPathSegment;
            }
            else if (resourceType == ResourceType.User)
            {
                ResourceId userId = ResourceId.Parse(ownerOrResourceId);

                return Paths.DatabasesPathSegment + "/" + userId.DatabaseId.ToString() + "/" +
                    Paths.UsersPathSegment + "/" + userId.UserId.ToString();
            }
            else if (isFeed && resourceType == ResourceType.ClientEncryptionKey)
            {
                return Paths.DatabasesPathSegment + "/" + ownerOrResourceId + "/" +
                    Paths.ClientEncryptionKeysPathSegment;
            }
            else if (resourceType == ResourceType.ClientEncryptionKey)
            {
                ResourceId clientEncryptionKeyId = ResourceId.Parse(ownerOrResourceId);

                return Paths.DatabasesPathSegment + "/" + clientEncryptionKeyId.DatabaseId.ToString() + "/" +
                    Paths.ClientEncryptionKeysPathSegment + "/" + clientEncryptionKeyId.ClientEncryptionKeyId.ToString();
            }
            else if (isFeed && resourceType == ResourceType.UserDefinedType)
            {
                return
                    Paths.DatabasesPathSegment + "/" + ownerOrResourceId + "/" +
                    Paths.UserDefinedTypesPathSegment;
            }
            else if (resourceType == ResourceType.UserDefinedType)
            {
                ResourceId userDefinedTypeId = ResourceId.Parse(ownerOrResourceId);

                return Paths.DatabasesPathSegment + "/" + userDefinedTypeId.DatabaseId.ToString() + "/" +
                    Paths.UserDefinedTypesPathSegment + "/" + userDefinedTypeId.UserDefinedTypeId.ToString();
            }
            else if (isFeed && resourceType == ResourceType.Permission)
            {
                ResourceId userId = ResourceId.Parse(ownerOrResourceId);

                return
                    Paths.DatabasesPathSegment + "/" + userId.DatabaseId.ToString() + "/" +
                    Paths.UsersPathSegment + "/" + userId.UserId.ToString() + "/" +
                    Paths.PermissionsPathSegment;
            }
            else if (resourceType == ResourceType.Permission)
            {
                ResourceId permissionId = ResourceId.Parse(ownerOrResourceId);

                return Paths.DatabasesPathSegment + "/" + permissionId.DatabaseId.ToString() + "/" +
                    Paths.UsersPathSegment + "/" + permissionId.UserId.ToString() + "/" +
                    Paths.PermissionsPathSegment + "/" + permissionId.PermissionId.ToString();
            }
            else if (isFeed && resourceType == ResourceType.Document)
            {
                ResourceId documentCollectionId = ResourceId.Parse(ownerOrResourceId);

                return
                    Paths.DatabasesPathSegment + "/" + documentCollectionId.DatabaseId.ToString() + "/" +
                    Paths.CollectionsPathSegment + "/" + documentCollectionId.DocumentCollectionId.ToString() + "/" +
                    Paths.DocumentsPathSegment;
            }
            else if (resourceType == ResourceType.Document)
            {
                ResourceId documentId = ResourceId.Parse(ownerOrResourceId);

                return Paths.DatabasesPathSegment + "/" + documentId.DatabaseId.ToString() + "/" +
                    Paths.CollectionsPathSegment + "/" + documentId.DocumentCollectionId.ToString() + "/" +
                    Paths.DocumentsPathSegment + "/" + documentId.DocumentId.ToString();
            }
            else if (isFeed && resourceType == ResourceType.Schema)
            {
                ResourceId schemaCollectionId = ResourceId.Parse(ownerOrResourceId);

                return
                    Paths.DatabasesPathSegment + "/" + schemaCollectionId.DatabaseId.ToString() + "/" +
                    Paths.CollectionsPathSegment + "/" + schemaCollectionId.DocumentCollectionId.ToString() + "/" +
                    Paths.SchemasPathSegment;
            }
            else if (resourceType == ResourceType.Schema)
            {
                ResourceId schemaId = ResourceId.Parse(ownerOrResourceId);

                return Paths.DatabasesPathSegment + "/" + schemaId.DatabaseId.ToString() + "/" +
                    Paths.CollectionsPathSegment + "/" + schemaId.DocumentCollectionId.ToString() + "/" +
                    Paths.SchemasPathSegment + "/" + schemaId.SchemaId.ToString();
            }
            else if (isFeed && resourceType == ResourceType.DatabaseAccount)
            {
                return Paths.DatabaseAccountSegment;
            }
            else if (resourceType == ResourceType.DatabaseAccount)
            {
                return Paths.DatabaseAccountSegment + "/" + ownerOrResourceId;
            }
            else if (isFeed && resourceType == ResourceType.Snapshot)
            {
                return Paths.SnapshotsPathSegment;
            }
            else if (resourceType == ResourceType.Snapshot)
            {
                return Paths.SnapshotsPathSegment + "/" + ownerOrResourceId.ToString();
            }
            else if(resourceType == ResourceType.PartitionKey && operationType == OperationType.Delete)
            {
                ResourceId documentCollectionId = ResourceId.Parse(ownerOrResourceId);

                return Paths.DatabasesPathSegment + "/" + documentCollectionId.DatabaseId.ToString() + "/" +
                    Paths.CollectionsPathSegment + "/" + documentCollectionId.DocumentCollectionId.ToString() + "/" + Paths.OperationsPathSegment + "/" + Paths.PartitionKeyDeletePathSegment;
            }
            else if (isFeed && resourceType == ResourceType.RoleAssignment)
            {
                return Paths.RoleAssignmentsPathSegment;
            }
            else if (isFeed && resourceType == ResourceType.RoleDefinition)
            {
                return Paths.RoleDefinitionsPathSegment;
            }
            else if (resourceType == ResourceType.RoleAssignment)
            {
                return Paths.RoleAssignmentsPathSegment + "/" + ownerOrResourceId.ToString();
            }
            else if (resourceType == ResourceType.RoleDefinition)
            {
                return Paths.RoleDefinitionsPathSegment + "/" + ownerOrResourceId.ToString();
            }
            else if (isFeed && resourceType == ResourceType.SystemDocument)
            {
                ResourceId documentCollectionId = ResourceId.Parse(ownerOrResourceId);

                return
                    Paths.DatabasesPathSegment + "/" + documentCollectionId.DatabaseId.ToString() + "/" +
                    Paths.CollectionsPathSegment + "/" + documentCollectionId.DocumentCollectionId.ToString() + "/" +
                    Paths.SystemDocumentsPathSegment;
            }
            else if (resourceType == ResourceType.SystemDocument)
            {
                ResourceId sysdocId = ResourceId.Parse(ownerOrResourceId);

                return Paths.DatabasesPathSegment + "/" + sysdocId.DatabaseId.ToString() + "/" +
                    Paths.CollectionsPathSegment + "/" + sysdocId.DocumentCollectionId.ToString() + "/" +
                    Paths.SystemDocumentsPathSegment + "/" + sysdocId.SystemDocumentId.ToString();
            }
            else if (isFeed && resourceType == ResourceType.PartitionedSystemDocument)
            {
                ResourceId documentCollectionId = ResourceId.Parse(ownerOrResourceId);

                return
                    Paths.DatabasesPathSegment + "/" + documentCollectionId.DatabaseId.ToString() + "/" +
                    Paths.CollectionsPathSegment + "/" + documentCollectionId.DocumentCollectionId.ToString() + "/" +
                    Paths.PartitionedSystemDocumentsPathSegment;
            }
            else if (resourceType == ResourceType.PartitionedSystemDocument)
            {
                ResourceId sysdocId = ResourceId.Parse(ownerOrResourceId);

                return Paths.DatabasesPathSegment + "/" + sysdocId.DatabaseId.ToString() + "/" +
                    Paths.CollectionsPathSegment + "/" + sysdocId.DocumentCollectionId.ToString() + "/" +
                    Paths.PartitionedSystemDocumentsPathSegment + "/" + sysdocId.PartitionedSystemDocumentId.ToString();
            }
#if !COSMOSCLIENT
            else if (isFeed && resourceType == ResourceType.MasterPartition)
            {
                return Paths.PartitionsPathSegment;
            }
            else if (resourceType == ResourceType.MasterPartition)
            {
                return Paths.PartitionsPathSegment + "/" + ownerOrResourceId;
            }
            else if (isFeed && resourceType == ResourceType.ServerPartition)
            {
                return Paths.PartitionsPathSegment;
            }
            else if (resourceType == ResourceType.ServerPartition)
            {
                return Paths.PartitionsPathSegment + "/" + ownerOrResourceId;
            }
            else if (isFeed && resourceType == ResourceType.Topology)
            {
                return Paths.TopologyPathSegment;
            }
            else if (resourceType == ResourceType.Topology)
            {
                return Paths.TopologyPathSegment + "/" + ownerOrResourceId;
            }
            else if (resourceType == ResourceType.RidRange)
            {
                return Paths.RidRangePathSegment + "/" + ownerOrResourceId;
            }
            else if (resourceType == ResourceType.VectorClock)
            {
                return Paths.VectorClockPathSegment + "/" + ownerOrResourceId;
            }
#endif

            string errorMessage = string.Format(CultureInfo.CurrentUICulture, RMResources.UnknownResourceType, resourceType.ToString());
            Debug.Assert(false, errorMessage);
            throw new BadRequestException(errorMessage);
        }

        public static string GenerateRootOperationPath(OperationType operationType)
        {
            switch (operationType)
            {
#if !COSMOSCLIENT
                case OperationType.Pause:
                    return Paths.OperationsPathSegment + "/" + Paths.ReplicaOperations_Pause;
                case OperationType.Recycle:
                    return Paths.OperationsPathSegment + "/" + Paths.ReplicaOperations_Recycle;
                case OperationType.Resume:
                    return Paths.OperationsPathSegment + "/" + Paths.ReplicaOperations_Resume;
                case OperationType.Stop:
                    return Paths.OperationsPathSegment + "/" + Paths.ReplicaOperations_Stop;
                case OperationType.Crash:
                    return Paths.OperationsPathSegment + "/" + Paths.ReplicaOperations_Crash;
                case OperationType.ForceConfigRefresh:
                    return Paths.OperationsPathSegment + "/" + Paths.ReplicaOperations_ForceConfigRefresh;
                case OperationType.ReportThroughputUtilization:
                    return Paths.OperationsPathSegment + "/" + Paths.ReplicaOperations_ReportThroughputUtilization;
                case OperationType.BatchReportThroughputUtilization:
                    return Paths.OperationsPathSegment + "/" + Paths.ReplicaOperations_BatchReportThroughputUtilization;
                case OperationType.ControllerBatchGetOutput:
                    return Paths.OperationsPathSegment + "/" + Paths.ControllerOperations_BatchGetOutput;
                case OperationType.ControllerBatchReportCharges:
                    return Paths.OperationsPathSegment + "/" + Paths.ControllerOperations_BatchReportCharges;
                case OperationType.GetConfiguration:
                    return Paths.OperationsPathSegment + "/" + Paths.Operations_GetConfiguration;
                case OperationType.GetFederationConfigurations:
                    return Paths.OperationsPathSegment + "/" + Paths.Operations_GetFederationConfigurations;
                case OperationType.GetDatabaseAccountConfigurations:
                    return Paths.OperationsPathSegment + "/" + Paths.Operations_GetDatabaseAccountConfigurations;
                case OperationType.GetStorageConfigurations:
                    return Paths.OperationsPathSegment + "/" + Paths.Operations_GetStorageServiceConfigurations;
                case OperationType.GetStorageAccountKey:
                    return Paths.OperationsPathSegment + "/" + Paths.Operations_GetStorageAccountKey;
                case OperationType.GetStorageAccountSas:
                    return Paths.OperationsPathSegment + "/" + Paths.Operations_GetStorageAccountSas;
                case OperationType.GetUnwrappedDek:
                    return Paths.OperationsPathSegment + "/" + Paths.Operations_GetUnwrappedDek;
                case OperationType.ReadReplicaFromMasterPartition:
                    return Paths.OperationsPathSegment + "/" + Paths.Operations_ReadReplicaFromMasterPartition;
                case OperationType.ReadReplicaFromServerPartition:
                    return Paths.OperationsPathSegment + "/" + Paths.Operations_ReadReplicaFromServerPartition;
                case OperationType.MasterInitiatedProgressCoordination:
                    return Paths.OperationsPathSegment + "/" + Paths.Operations_MasterInitiatedProgressCoordination;
                case OperationType.GetAadGroups:
                    return Paths.OperationsPathSegment + "/" + Paths.Operations_GetAadGroups;
#endif

                default:
                    Debug.Assert(false, "Unsupported operation type for replica");
                    throw new NotFoundException();
            }
        }

        private static bool IsResourceType(in StringSegment resourcePathSegment)
        {
            if (resourcePathSegment.IsNullOrEmpty())
            {
                return false;
            }

            return resourcePathSegment.Equals(Paths.AttachmentsPathSegment, StringComparison.OrdinalIgnoreCase) ||
                   resourcePathSegment.Equals(Paths.CollectionsPathSegment, StringComparison.OrdinalIgnoreCase) ||
                   resourcePathSegment.Equals(Paths.DatabasesPathSegment, StringComparison.OrdinalIgnoreCase) ||
                   resourcePathSegment.Equals(Paths.PermissionsPathSegment, StringComparison.OrdinalIgnoreCase) ||
                   resourcePathSegment.Equals(Paths.UsersPathSegment, StringComparison.OrdinalIgnoreCase) ||
                   resourcePathSegment.Equals(Paths.ClientEncryptionKeysPathSegment, StringComparison.OrdinalIgnoreCase) ||
                   resourcePathSegment.Equals(Paths.UserDefinedTypesPathSegment, StringComparison.OrdinalIgnoreCase) ||
                   resourcePathSegment.Equals(Paths.DocumentsPathSegment, StringComparison.OrdinalIgnoreCase) ||
                   resourcePathSegment.Equals(Paths.StoredProceduresPathSegment, StringComparison.OrdinalIgnoreCase) ||
                   resourcePathSegment.Equals(Paths.TriggersPathSegment, StringComparison.OrdinalIgnoreCase) ||
                   resourcePathSegment.Equals(Paths.UserDefinedFunctionsPathSegment, StringComparison.OrdinalIgnoreCase) ||
                   resourcePathSegment.Equals(Paths.ConflictsPathSegment, StringComparison.OrdinalIgnoreCase) ||
                   resourcePathSegment.Equals(Paths.MediaPathSegment, StringComparison.OrdinalIgnoreCase) ||
                   resourcePathSegment.Equals(Paths.OffersPathSegment, StringComparison.OrdinalIgnoreCase) ||
                   resourcePathSegment.Equals(Paths.PartitionsPathSegment, StringComparison.OrdinalIgnoreCase) ||
                   resourcePathSegment.Equals(Paths.DatabaseAccountSegment, StringComparison.OrdinalIgnoreCase) ||
                   resourcePathSegment.Equals(Paths.TopologyPathSegment, StringComparison.OrdinalIgnoreCase) ||
                   resourcePathSegment.Equals(Paths.PartitionKeyRangesPathSegment, StringComparison.OrdinalIgnoreCase) ||
                   resourcePathSegment.Equals(Paths.PartitionKeyRangePreSplitSegment, StringComparison.OrdinalIgnoreCase) ||
                   resourcePathSegment.Equals(Paths.PartitionKeyRangePostSplitSegment, StringComparison.OrdinalIgnoreCase) ||
                   resourcePathSegment.Equals(Paths.SchemasPathSegment, StringComparison.OrdinalIgnoreCase) ||
                   resourcePathSegment.Equals(Paths.RidRangePathSegment, StringComparison.OrdinalIgnoreCase) ||
                   resourcePathSegment.Equals(Paths.VectorClockPathSegment, StringComparison.OrdinalIgnoreCase) ||
                   resourcePathSegment.Equals(Paths.AddressPathSegment, StringComparison.OrdinalIgnoreCase) ||
                   resourcePathSegment.Equals(Paths.SnapshotsPathSegment, StringComparison.OrdinalIgnoreCase) ||
                   resourcePathSegment.Equals(Paths.PartitionedSystemDocumentsPathSegment, StringComparison.OrdinalIgnoreCase) ||
                   resourcePathSegment.Equals(Paths.RoleDefinitionsPathSegment, StringComparison.OrdinalIgnoreCase) ||
                   resourcePathSegment.Equals(Paths.RoleAssignmentsPathSegment, StringComparison.OrdinalIgnoreCase) ||
                   resourcePathSegment.Equals(Paths.TransactionsPathSegment, StringComparison.OrdinalIgnoreCase) ||
                   resourcePathSegment.Equals(Paths.SystemDocumentsPathSegment, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsRootOperation(in StringSegment operationSegment, in StringSegment operationTypeSegment)
        {
            if (operationSegment.IsNullOrEmpty())
            {
                return false;
            }

            if (operationTypeSegment.IsNullOrEmpty())
            {
                return false;
            }

            if (operationSegment.Compare(Paths.OperationsPathSegment, StringComparison.OrdinalIgnoreCase) != 0)
            {
                return false;
            }

            return operationTypeSegment.Equals(Paths.ReplicaOperations_Pause, StringComparison.OrdinalIgnoreCase) ||
                   operationTypeSegment.Equals(Paths.ReplicaOperations_Resume, StringComparison.OrdinalIgnoreCase) ||
                   operationTypeSegment.Equals(Paths.ReplicaOperations_Stop, StringComparison.OrdinalIgnoreCase) ||
                   operationTypeSegment.Equals(Paths.ReplicaOperations_Recycle, StringComparison.OrdinalIgnoreCase) ||
                   operationTypeSegment.Equals(Paths.ReplicaOperations_Crash, StringComparison.OrdinalIgnoreCase) ||
                   operationTypeSegment.Equals(Paths.ReplicaOperations_ReportThroughputUtilization, StringComparison.OrdinalIgnoreCase) ||
                   operationTypeSegment.Equals(Paths.ReplicaOperations_BatchReportThroughputUtilization, StringComparison.OrdinalIgnoreCase) ||
                   operationTypeSegment.Equals(Paths.ControllerOperations_BatchGetOutput, StringComparison.OrdinalIgnoreCase) ||
                   operationTypeSegment.Equals(Paths.ControllerOperations_BatchReportCharges, StringComparison.OrdinalIgnoreCase) ||
                   operationTypeSegment.Equals(Paths.Operations_GetFederationConfigurations, StringComparison.OrdinalIgnoreCase) ||
                   operationTypeSegment.Equals(Paths.Operations_GetConfiguration, StringComparison.OrdinalIgnoreCase) ||
                   operationTypeSegment.Equals(Paths.Operations_GetStorageAccountKey, StringComparison.OrdinalIgnoreCase) ||
                   operationTypeSegment.Equals(Paths.Operations_GetStorageAccountSas, StringComparison.OrdinalIgnoreCase) ||
                   operationTypeSegment.Equals(Paths.Operations_GetDatabaseAccountConfigurations, StringComparison.OrdinalIgnoreCase) ||
                   operationTypeSegment.Equals(Paths.Operations_GetUnwrappedDek, StringComparison.OrdinalIgnoreCase) ||
                   operationTypeSegment.Equals(Paths.Operations_ReadReplicaFromMasterPartition, StringComparison.OrdinalIgnoreCase) ||
                   operationTypeSegment.Equals(Paths.Operations_ReadReplicaFromServerPartition, StringComparison.OrdinalIgnoreCase) ||
                   operationTypeSegment.Equals(Paths.Operations_MasterInitiatedProgressCoordination, StringComparison.OrdinalIgnoreCase) ||
                   operationTypeSegment.Equals(Paths.Operations_GetAadGroups, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTopLevelOperationOperation(in StringSegment replicaSegment, in StringSegment addressSegment)
        {
            if (replicaSegment.IsNullOrEmpty() && // replica part should be empty
                (addressSegment.Compare(Paths.XPReplicatorAddressPathSegment, StringComparison.OrdinalIgnoreCase) == 0 ||
                 addressSegment.Compare(Paths.ComputeGatewayChargePathSegment, StringComparison.OrdinalIgnoreCase) == 0 ||
                 addressSegment.Compare(Paths.ServiceReservationPathSegment, StringComparison.OrdinalIgnoreCase) == 0))
            {
                return true;
            }

            return false;
        }

        internal static bool IsNameBased(string resourceIdOrFullName)
        {
            // quick way to tell whether it is resourceId nor not, non conclusively.
            if (!string.IsNullOrEmpty(resourceIdOrFullName) && resourceIdOrFullName.Length > 4 && resourceIdOrFullName[3] == '/')
            {
                return true;
            }
            return false;
        }

        internal static int IndexOfNth(this string str, char value, int n)
        {
            if (string.IsNullOrEmpty(str) || n <= 0 || n > str.Length)
            {
                return -1;
            }

            int remaining = n;
            for (int i = 0; i < str.Length; i++)
            {
                if (str[i] == value)
                {
                    if (--remaining == 0)
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        internal static bool ValidateResourceFullName(ResourceType resourceType, string resourceFullName)
        {
            string[] segments = resourceFullName.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            string[] resourcePathArray = GetResourcePathArray(resourceType);

            if (resourcePathArray == null)
                return false;

            if (segments.Length != resourcePathArray.Length * 2)
                return false;

            for (int i = 0; i < resourcePathArray.Length; i++)
            {
                if (string.Compare(resourcePathArray[i], segments[2 * i], StringComparison.Ordinal) != 0)
                {
                    return false;
                }
            }
            return true;
        }

        internal static string[] GetResourcePathArray(ResourceType resourceType)
        {
            List<string> segments = new List<string>();

            if (resourceType == ResourceType.Snapshot)
            {
                segments.Add(Paths.SnapshotsPathSegment);
                return segments.ToArray();
            }
            if (resourceType == ResourceType.RoleDefinition)
            {
                segments.Add(Paths.RoleDefinitionsPathSegment);
                return segments.ToArray();
            }

            if (resourceType == ResourceType.RoleAssignment)
            {
                segments.Add(Paths.RoleAssignmentsPathSegment);
                return segments.ToArray();
            }

            if (resourceType == ResourceType.Offer)
            {
                segments.Add(Paths.OffersPathSegment);
                return segments.ToArray();
            }

            if (resourceType == ResourceType.Address)
            {
                segments.Add(Paths.AddressPathSegment);
                return segments.ToArray();
            }

            segments.Add(Paths.DatabasesPathSegment);

            if (resourceType == ResourceType.Permission ||
                resourceType == ResourceType.User)
            {
                segments.Add(Paths.UsersPathSegment);
                if (resourceType == ResourceType.Permission)
                    segments.Add(Paths.PermissionsPathSegment);
            }
            else if (resourceType == ResourceType.UserDefinedType)
            {
                segments.Add(Paths.UserDefinedTypesPathSegment);
            }
            else if (resourceType == ResourceType.ClientEncryptionKey)
            {
                segments.Add(Paths.ClientEncryptionKeysPathSegment);
            }
            else if (
                resourceType == ResourceType.Collection ||
                resourceType == ResourceType.StoredProcedure ||
                resourceType == ResourceType.UserDefinedFunction ||
                resourceType == ResourceType.Trigger ||
                resourceType == ResourceType.Conflict ||
                resourceType == ResourceType.Attachment ||
                resourceType == ResourceType.Document ||
                resourceType == ResourceType.PartitionKeyRange ||
                resourceType == ResourceType.Schema ||
                resourceType == ResourceType.PartitionedSystemDocument ||
                resourceType == ResourceType.SystemDocument)
            {
                segments.Add(Paths.CollectionsPathSegment);
                if (resourceType == ResourceType.StoredProcedure)
                    segments.Add(Paths.StoredProceduresPathSegment);
                else if (resourceType == ResourceType.UserDefinedFunction)
                    segments.Add(Paths.UserDefinedFunctionsPathSegment);
                else if (resourceType == ResourceType.Trigger)
                    segments.Add(Paths.TriggersPathSegment);
                else if (resourceType == ResourceType.Conflict)
                    segments.Add(Paths.ConflictsPathSegment);
                else if (resourceType == ResourceType.Schema)
                    segments.Add(Paths.SchemasPathSegment);
                else if (resourceType == ResourceType.Document || resourceType == ResourceType.Attachment)
                {
                    segments.Add(Paths.DocumentsPathSegment);
                    if (resourceType == ResourceType.Attachment)
                        segments.Add(Paths.AttachmentsPathSegment);
                }
                else if (resourceType == ResourceType.PartitionKeyRange)
                {
                    segments.Add(Paths.PartitionKeyRangesPathSegment);
                }
                else if(resourceType == ResourceType.PartitionedSystemDocument)
                {
                    segments.Add(Paths.PartitionedSystemDocumentsPathSegment);
                }
                else if (resourceType == ResourceType.SystemDocument)
                {
                    segments.Add(Paths.SystemDocumentsPathSegment);
                }
            }
            else if (resourceType == ResourceType.PartitionKey)
            {
                segments.Add(Paths.CollectionsPathSegment);
                segments.Add(Paths.OperationsPathSegment);
            }
            else if (resourceType != ResourceType.Database)
            {
                return null;
            }

            return segments.ToArray();
        }


        internal static bool ValidateResourceId(ResourceType resourceType, string resourceId)
        {
            if (resourceType == ResourceType.Conflict)
            {
                return PathsHelper.ValidateConflictId(resourceId);
            }
            else if (resourceType == ResourceType.Database)
            {
                return PathsHelper.ValidateDatabaseId(resourceId);
            }
            else if (resourceType == ResourceType.Collection)
            {
                return PathsHelper.ValidateDocumentCollectionId(resourceId);
            }
            else if (resourceType == ResourceType.Document)
            {
                return PathsHelper.ValidateDocumentId(resourceId);
            }
            else if (resourceType == ResourceType.Permission)
            {
                return PathsHelper.ValidatePermissionId(resourceId);
            }
            else if (resourceType == ResourceType.StoredProcedure)
            {
                return PathsHelper.ValidateStoredProcedureId(resourceId);
            }
            else if (resourceType == ResourceType.Trigger)
            {
                return PathsHelper.ValidateTriggerId(resourceId);
            }
            else if (resourceType == ResourceType.UserDefinedFunction)
            {
                return PathsHelper.ValidateUserDefinedFunctionId(resourceId);
            }
            else if (resourceType == ResourceType.User)
            {
                return PathsHelper.ValidateUserId(resourceId);
            }
            else if (resourceType == ResourceType.ClientEncryptionKey)
            {
                return PathsHelper.ValidateClientEncryptionKeyId(resourceId);
            }
            else if (resourceType == ResourceType.UserDefinedType)
            {
                return PathsHelper.ValidateUserDefinedTypeId(resourceId);
            }
            else if (resourceType == ResourceType.Attachment)
            {
                return PathsHelper.ValidateAttachmentId(resourceId);
            }
            if (resourceType == ResourceType.Schema)
            {
                return PathsHelper.ValidateSchemaId(resourceId);
            }
            if (resourceType == ResourceType.Snapshot)
            {
                return PathsHelper.ValidateSnapshotId(resourceId);
            }
            if (resourceType == ResourceType.RoleDefinition)
            {
                return PathsHelper.ValidateRoleDefinitionId(resourceId);
            }
            if (resourceType == ResourceType.RoleAssignment)
            {
                return PathsHelper.ValidateRoleAssignmentId(resourceId);
            }
            if (resourceType == ResourceType.SystemDocument)
            {
                return PathsHelper.ValidateSystemDocumentId(resourceId);
            }
            if (resourceType == ResourceType.PartitionedSystemDocument)
            {
                return PathsHelper.ValidatePartitionedSystemDocumentId(resourceId);
            }
            else
            {
                Debug.Assert(false,
                    string.Format(CultureInfo.InvariantCulture, "ValidateResourceId not implemented for Type {0} in ResourceRequestHandler", resourceType.ToString()));

                return false;
            }
        }

        internal static bool ValidateDatabaseId(string resourceIdString)
        {
            ResourceId resourceId = null;
            return ResourceId.TryParse(resourceIdString, out resourceId) && resourceId.Database > 0;
        }

        internal static bool ValidateDocumentCollectionId(string resourceIdString)
        {
            ResourceId resourceId = null;
            return ResourceId.TryParse(resourceIdString, out resourceId) && resourceId.DocumentCollection > 0;
        }

        internal static bool ValidateDocumentId(string resourceIdString)
        {
            ResourceId resourceId = null;
            return ResourceId.TryParse(resourceIdString, out resourceId) && resourceId.Document > 0;
        }

        internal static bool ValidateConflictId(string resourceIdString)
        {
            ResourceId resourceId = null;
            return ResourceId.TryParse(resourceIdString, out resourceId) && resourceId.Conflict > 0;
        }

        internal static bool ValidateAttachmentId(string resourceIdString)
        {
            ResourceId resourceId = null;
            return ResourceId.TryParse(resourceIdString, out resourceId) && resourceId.Attachment > 0;
        }

        internal static bool ValidatePermissionId(string resourceIdString)
        {
            ResourceId resourceId = null;
            return ResourceId.TryParse(resourceIdString, out resourceId) && resourceId.Permission > 0;
        }

        internal static bool ValidateStoredProcedureId(string resourceIdString)
        {
            ResourceId resourceId = null;
            return ResourceId.TryParse(resourceIdString, out resourceId) && resourceId.StoredProcedure > 0;
        }

        internal static bool ValidateTriggerId(string resourceIdString)
        {
            ResourceId resourceId = null;
            return ResourceId.TryParse(resourceIdString, out resourceId) && resourceId.Trigger > 0;
        }

        internal static bool ValidateUserDefinedFunctionId(string resourceIdString)
        {
            ResourceId resourceId = null;
            return ResourceId.TryParse(resourceIdString, out resourceId) && resourceId.UserDefinedFunction > 0;
        }

        internal static bool ValidateUserId(string resourceIdString)
        {
            ResourceId resourceId = null;
            return ResourceId.TryParse(resourceIdString, out resourceId) && resourceId.User > 0;
        }

        internal static bool ValidateClientEncryptionKeyId(string resourceIdString)
        {
            ResourceId resourceId = null;
            return ResourceId.TryParse(resourceIdString, out resourceId) && resourceId.ClientEncryptionKey > 0;
        }

        internal static bool ValidateUserDefinedTypeId(string resourceIdString)
        {
            ResourceId resourceId = null;
            return ResourceId.TryParse(resourceIdString, out resourceId) && resourceId.UserDefinedType > 0;
        }

        internal static bool ValidateSchemaId(string resourceIdString)
        {
            ResourceId resourceId = null;
            return ResourceId.TryParse(resourceIdString, out resourceId) && resourceId.Schema > 0;
        }

        internal static bool ValidateSnapshotId(string resourceIdString)
        {
            ResourceId resourceId = null;
            return ResourceId.TryParse(resourceIdString, out resourceId) && resourceId.Snapshot > 0;
        }

        internal static bool ValidateRoleAssignmentId(string resourceIdString)
        {
            ResourceId resourceId = null;
            return ResourceId.TryParse(resourceIdString, out resourceId) && resourceId.RoleAssignment > 0;
        }

        internal static bool ValidateRoleDefinitionId(string resourceIdString)
        {
            ResourceId resourceId = null;
            return ResourceId.TryParse(resourceIdString, out resourceId) && resourceId.RoleDefinition > 0;
        }

        internal static bool ValidateSystemDocumentId(string resourceIdString)
        {
            ResourceId resourceId = null;
            return ResourceId.TryParse(resourceIdString, out resourceId) && resourceId.SystemDocument > 0;
        }

        internal static bool ValidatePartitionedSystemDocumentId(string resourceIdString)
        {
            ResourceId resourceId = null;
            return ResourceId.TryParse(resourceIdString, out resourceId) && resourceId.PartitionedSystemDocument > 0;
        }

        internal static bool IsPublicResource(Type resourceType)
        {
            if (resourceType == typeof(Database) ||
                resourceType == typeof(ClientEncryptionKey) ||
                resourceType == typeof(DocumentCollection) ||
                resourceType == typeof(StoredProcedure) ||
                resourceType == typeof(UserDefinedFunction) ||
                resourceType == typeof(Trigger) ||
                resourceType == typeof(Conflict) ||
                typeof(Attachment).IsAssignableFrom(resourceType) ||
                resourceType == typeof(User) ||
                typeof(Permission).IsAssignableFrom(resourceType) ||
                typeof(Document).IsAssignableFrom(resourceType) ||
                resourceType == typeof(Offer) ||
                resourceType == typeof(Schema) ||
                resourceType == typeof(Snapshot))
            {
                return true;
            }
            else
            {
                return false;
            }
        }


        internal static void ParseCollectionSelfLink(string collectionSelfLink, out string databaseId, out string collectionId)
        {
            string[] segments = collectionSelfLink.Split(RuntimeConstants.Separators.Url, StringSplitOptions.RemoveEmptyEntries);

            if (segments.Length != 4
                || !string.Equals(segments[0], Paths.DatabasesPathSegment, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(segments[2], Paths.CollectionsPathSegment, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(RMResources.BadUrl, nameof(collectionSelfLink));
            }

            databaseId = segments[1];
            collectionId = segments[3];
        }
    }
}
