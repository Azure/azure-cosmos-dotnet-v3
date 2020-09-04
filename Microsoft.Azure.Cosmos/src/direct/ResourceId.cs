//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.Text;
    using System.Threading;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Core.Trace;

    //Resource ID is 20 byte number. It is not a guid.
    //First 4 bytes for DB id -> 2^32 DBs per tenant --> uint
    //  Next 4 bytes for coll id OR user id -> 2^31 coll/users per DB. the first bit indicates if this is a collection or a user --> uint
    //     Next 8 bytes for doc id OR permission or sproc/trigger/function or conflicts -> 2^64 permission per user. Note that the disambiguation between collection
    //     based resources (document/sproc/trigger/function/conflict) and user based resource i.e. permission is based on ownerid. --> ulong
    //     Disambiguation between document/sproc/conflict is based upon first 4 bits of the highest byte. 0x00 is document, 0x08 is sproc, 0x07 is
    //     trigger, 0x06 is function and 0x04 is conflict. 2^60 document/sproc/trigger/function/conflict per collection.
    //       Last 4 bytes for attachment id -> 2^32 attachments per "Document". These bits are used only for document's children. Permission/Sproc/
    //       Conflict RID hierarchy is only 16 bytes.

    internal sealed class ResourceId : IEquatable<ResourceId>
    {
        private const int OfferIdLength = 3;
        private const int RbacResourceIdLength = 6;
        private const int SnapshotIdLength = 7;
        public static readonly ushort Length = 20;
        public static readonly ushort MaxPathFragment = 8;  // for public resource
        public static readonly ResourceId Empty = new ResourceId();

        private ResourceId()
        {
            this.Offer = 0;
            this.Database = 0;
            this.DocumentCollection = 0;
            this.ClientEncryptionKey = 0;
            this.StoredProcedure = 0;
            this.Trigger = 0;
            this.UserDefinedFunction = 0;
            this.Document = 0;
            this.PartitionKeyRange = 0;
            this.User = 0;
            this.ClientEncryptionKey = 0;
            this.Permission = 0;
            this.Attachment = 0;
            this.Schema = 0;
            this.UserDefinedType = 0;
            this.Snapshot = 0;
            this.RoleAssignment = 0;
            this.RoleDefinition = 0;
        }

        public uint Offer
        {
            get;
            private set;
        }

        public ResourceId OfferId
        {
            get
            {
                ResourceId rid = new ResourceId();
                rid.Offer = this.Offer;
                return rid;
            }
        }

        public uint Database
        {
            get;
            private set;
        }

        public ResourceId DatabaseId
        {
            get
            {
                ResourceId rid = new ResourceId();
                rid.Database = this.Database;
                return rid;
            }
        }

        public bool IsDatabaseId
        {
            get
            {
                return this.Database != 0 && (this.DocumentCollection == 0 && this.User == 0 && this.UserDefinedType == 0 && this.ClientEncryptionKey == 0);
            }
        }

        public bool IsDocumentCollectionId
        {
            get
            {
                return this.Database != 0 && this.DocumentCollection != 0
                    && (this.Document == 0 && this.StoredProcedure == 0 && this.Trigger == 0 && this.UserDefinedFunction == 0);
            }
        }

        public uint DocumentCollection
        {
            get;
            private set;
        }

        public ResourceId DocumentCollectionId
        {
            get
            {
                ResourceId rid = new ResourceId();
                rid.Database = this.Database;
                rid.DocumentCollection = this.DocumentCollection;
                return rid;
            }
        }

        public bool IsClientEncryptionKeyId
        {
            get
            {
                return (this.Database != 0 && this.ClientEncryptionKey != 0);
            }
        }

        /// <summary>
        /// Unique (across all databases) Id for the DocumentCollection.
        /// First 4 bytes are DatabaseId and next 4 bytes are CollectionId.
        /// </summary>
        public UInt64 UniqueDocumentCollectionId
        {
            get
            {
                return (UInt64)this.Database << 32 | this.DocumentCollection;
            }
        }

        public UInt64 StoredProcedure
        {
            get;
            private set;
        }

        public ResourceId StoredProcedureId
        {
            get
            {
                ResourceId rid = new ResourceId();
                rid.Database = this.Database;
                rid.DocumentCollection = this.DocumentCollection;
                rid.StoredProcedure = this.StoredProcedure;
                return rid;
            }
        }

        public UInt64 Trigger
        {
            get;
            private set;
        }

        public ResourceId TriggerId
        {
            get
            {
                ResourceId rid = new ResourceId();
                rid.Database = this.Database;
                rid.DocumentCollection = this.DocumentCollection;
                rid.Trigger = this.Trigger;
                return rid;
            }
        }

        public UInt64 UserDefinedFunction
        {
            get;
            private set;
        }

        public ResourceId UserDefinedFunctionId
        {
            get
            {
                ResourceId rid = new ResourceId();
                rid.Database = this.Database;
                rid.DocumentCollection = this.DocumentCollection;
                rid.UserDefinedFunction = this.UserDefinedFunction;
                return rid;
            }
        }

        public UInt64 Conflict
        {
            get;
            private set;
        }

        public ResourceId ConflictId
        {
            get
            {
                ResourceId rid = new ResourceId();
                rid.Database = this.Database;
                rid.DocumentCollection = this.DocumentCollection;
                rid.Conflict = this.Conflict;
                return rid;
            }
        }

        public ulong Document
        {
            get;
            private set;
        }

        public ResourceId DocumentId
        {
            get
            {
                ResourceId rid = new ResourceId();
                rid.Database = this.Database;
                rid.DocumentCollection = this.DocumentCollection;
                rid.Document = this.Document;
                return rid;
            }
        }

        public ulong PartitionKeyRange
        {
            get;
            private set;
        }

        public ResourceId PartitionKeyRangeId
        {
            get
            {
                ResourceId rid = new ResourceId();
                rid.Database = this.Database;
                rid.DocumentCollection = this.DocumentCollection;
                rid.PartitionKeyRange = this.PartitionKeyRange;
                return rid;
            }
        }

        public uint User
        {
            get;
            private set;
        }

        public ResourceId UserId
        {
            get
            {
                ResourceId rid = new ResourceId();
                rid.Database = this.Database;
                rid.User = this.User;
                return rid;
            }
        }

        public uint ClientEncryptionKey
        {
            get;
            private set;
        }


        public ResourceId ClientEncryptionKeyId
        {
            get
            {
                ResourceId rid = new ResourceId();
                rid.Database = this.Database;
                rid.ClientEncryptionKey = this.ClientEncryptionKey;
                return rid;
            }
        }

        public uint UserDefinedType
        {
            get;
            private set;
        }


        public ResourceId UserDefinedTypeId
        {
            get
            {
                ResourceId rid = new ResourceId();
                rid.Database = this.Database;
                rid.UserDefinedType = this.UserDefinedType;
                return rid;
            }
        }

        public ulong Permission
        {
            get;
            private set;
        }


        public ResourceId PermissionId
        {
            get
            {
                ResourceId rid = new ResourceId();
                rid.Database = this.Database;
                rid.User = this.User;
                rid.Permission = this.Permission;
                return rid;
            }
        }

        public uint Attachment
        {
            get;
            private set;
        }

        public ResourceId AttachmentId
        {
            get
            {
                ResourceId rid = new ResourceId();
                rid.Database = this.Database;
                rid.DocumentCollection = this.DocumentCollection;
                rid.Document = this.Document;
                rid.Attachment = this.Attachment;
                return rid;
            }
        }

        public ulong Schema
        {
            get;
            private set;
        }


        public ResourceId SchemaId
        {
            get
            {
                ResourceId rid = new ResourceId();
                rid.Database = this.Database;
                rid.DocumentCollection = this.DocumentCollection;
                rid.Schema = this.Schema;
                return rid;
            }
        }

        public ulong Snapshot
        {
            get;
            private set;
        }

        public ResourceId SnapshotId
        {
            get
            {
                ResourceId rid = new ResourceId();
                rid.Snapshot = this.Snapshot;
                return rid;
            }
        }

        public bool IsSnapshotId
        {
            get
            {
                return this.Snapshot != 0;
            }
        }

        public ulong RoleAssignment
        {
            get;
            private set;
        }

        public ResourceId RoleAssignmentId
        {
            get
            {
                ResourceId rid = new ResourceId();
                rid.RoleAssignment = this.RoleAssignment;
                return rid;
            }
        }

        public bool IsRoleAssignmentId
        {
            get
            {
                return this.RoleAssignment != 0;
            }
        }

        public ulong RoleDefinition
        {
            get;
            private set;
        }

        public ResourceId RoleDefinitionId
        {
            get
            {
                ResourceId rid = new ResourceId();
                rid.RoleDefinition = this.RoleDefinition;
                return rid;
            }
        }

        public bool IsRoleDefinitionId
        {
            get
            {
                return this.RoleDefinition != 0;
            }
        }

        public byte[] Value
        {
            get
            {
                int len = 0;
                if (this.Offer > 0)
                    len += ResourceId.OfferIdLength;
                else if (this.Snapshot > 0)
                    len += ResourceId.SnapshotIdLength;
                else if (this.RoleAssignment > 0)
                    len += ResourceId.RbacResourceIdLength;
                else if (this.RoleDefinition > 0)
                    len += ResourceId.RbacResourceIdLength;
                else if (this.Database > 0)
                    len += 4;
                if (this.DocumentCollection > 0 || this.User > 0 || this.UserDefinedType > 0 || this.ClientEncryptionKey > 0)
                    len += 4;
                if (this.Document > 0 || this.Permission > 0 || this.StoredProcedure > 0 || this.Trigger > 0
                    || this.UserDefinedFunction > 0 || this.Conflict > 0 || this.PartitionKeyRange > 0 || this.Schema > 0
                    || this.UserDefinedType > 0 || this.ClientEncryptionKey > 0)
                    len += 8;
                if (this.Attachment > 0)
                    len += 4;

                byte[] val = new byte[len];

                if (this.Offer > 0)
                    ResourceId.BlockCopy(BitConverter.GetBytes(this.Offer), 0, val, 0, ResourceId.OfferIdLength);
                else if (this.Database > 0)
                    ResourceId.BlockCopy(BitConverter.GetBytes(this.Database), 0, val, 0, 4);
                else if (this.Snapshot > 0)
                    ResourceId.BlockCopy(BitConverter.GetBytes(this.Snapshot), 0, val, 0, ResourceId.SnapshotIdLength);
                else if (this.RoleAssignment > 0)
                {
                    ResourceId.BlockCopy(BitConverter.GetBytes(this.RoleAssignment), 0, val, 0, 4);
                    ResourceId.BlockCopy(BitConverter.GetBytes(0x1000), 0, val, 4, 2);
                }
                else if (this.RoleDefinition > 0)
                    ResourceId.BlockCopy(BitConverter.GetBytes(this.RoleDefinition), 0, val, 0, ResourceId.RbacResourceIdLength);

                if (this.DocumentCollection > 0)
                    ResourceId.BlockCopy(BitConverter.GetBytes(this.DocumentCollection), 0, val, 4, 4);
                else if (this.User > 0)
                    ResourceId.BlockCopy(BitConverter.GetBytes(this.User), 0, val, 4, 4);

                if (this.StoredProcedure > 0)
                    ResourceId.BlockCopy(BitConverter.GetBytes(this.StoredProcedure), 0, val, 8, 8);
                else if (this.Trigger > 0)
                    ResourceId.BlockCopy(BitConverter.GetBytes(this.Trigger), 0, val, 8, 8);
                else if (this.UserDefinedFunction > 0)
                    ResourceId.BlockCopy(BitConverter.GetBytes(this.UserDefinedFunction), 0, val, 8, 8);
                else if (this.Conflict > 0)
                    ResourceId.BlockCopy(BitConverter.GetBytes(this.Conflict), 0, val, 8, 8);
                else if (this.Document > 0)
                    ResourceId.BlockCopy(BitConverter.GetBytes(this.Document), 0, val, 8, 8);
                else if (this.PartitionKeyRange > 0)
                    ResourceId.BlockCopy(BitConverter.GetBytes(this.PartitionKeyRange), 0, val, 8, 8);
                else if (this.Permission > 0)
                    ResourceId.BlockCopy(BitConverter.GetBytes(this.Permission), 0, val, 8, 8);
                else if(this.Schema > 0)
                    ResourceId.BlockCopy(BitConverter.GetBytes(this.Schema), 0, val, 8, 8);
                else if (this.UserDefinedType > 0)
                {
                    ResourceId.BlockCopy(BitConverter.GetBytes(this.UserDefinedType), 0, val, 8, 4);
                    ResourceId.BlockCopy(BitConverter.GetBytes((uint)ExtendedDatabaseChildResourceType.UserDefinedType), 0, val, 12, 4);
                }
                else if(this.ClientEncryptionKey > 0)
                {
                    ResourceId.BlockCopy(BitConverter.GetBytes(this.ClientEncryptionKey), 0, val, 8, 4);
                    ResourceId.BlockCopy(BitConverter.GetBytes((uint)ExtendedDatabaseChildResourceType.ClientEncryptionKey), 0, val, 12, 4);
                }

                if (this.Attachment > 0)
                    ResourceId.BlockCopy(BitConverter.GetBytes(this.Attachment), 0, val, 16, 4);

                return val;
            }
        }

        public static ResourceId Parse(string id)
        {
            ResourceId rid = null;

            bool parsed = ResourceId.TryParse(id, out rid);

            if (!parsed)
                throw new BadRequestException(
                    string.Format(CultureInfo.CurrentUICulture, RMResources.InvalidResourceID, id));

            return rid;
        }

        public static byte[] Parse(ResourceType eResourceType, string id)
        {
            if (ResourceId.HasNonHierarchicalResourceId(eResourceType))
            {
                return Encoding.UTF8.GetBytes(id);
            }

            return ResourceId.Parse(id).Value;
        }

        public static ResourceId NewDatabaseId(uint dbid)
        {
            ResourceId resourceId = new ResourceId();
            resourceId.Database = dbid;
            return resourceId;
        }

        public static ResourceId NewRoleDefinitionId(ulong roleDefinitionId)
        {
            return new ResourceId()
            {
                RoleDefinition = roleDefinitionId
            };
        }

        public static ResourceId NewRoleAssignmentId(ulong roleAssignmentId)
        {
            return new ResourceId()
            {
                RoleAssignment = roleAssignmentId
            };
        }

        public static ResourceId NewDocumentCollectionId(string databaseId, uint collectionId)
        {
            ResourceId dbId = ResourceId.Parse(databaseId);

            return ResourceId.NewDocumentCollectionId(dbId.Database, collectionId);
        }

        public static ResourceId NewDocumentCollectionId(uint databaseId, uint collectionId)
        {
            ResourceId collectionResourceId = new ResourceId();
            collectionResourceId.Database = databaseId;
            collectionResourceId.DocumentCollection = collectionId;

            return collectionResourceId;
        }

        public static ResourceId NewClientEncryptionKeyId(string databaseId, uint clientEncryptionKeyId)
        {
            ResourceId dbId = ResourceId.Parse(databaseId);

            return ResourceId.NewClientEncryptionKeyId(dbId.Database, clientEncryptionKeyId);
        }

        public static ResourceId NewClientEncryptionKeyId(uint databaseId, uint clientEncryptionKeyId)
        {
            ResourceId clientEncryptionKeyResourceId = new ResourceId();
            clientEncryptionKeyResourceId.Database = databaseId;
            clientEncryptionKeyResourceId.ClientEncryptionKey = clientEncryptionKeyId;

            return clientEncryptionKeyResourceId;
        }

        public static ResourceId NewCollectionChildResourceId(string collectionId, ulong childId, ResourceType resourceType)
        {
            ResourceId collId = ResourceId.Parse(collectionId);

            if(!collId.IsDocumentCollectionId)
            {
                string errorMessage = string.Format(CultureInfo.InvariantCulture, "Invalid collection RID '{0}'.", collectionId);
                DefaultTrace.TraceError(errorMessage);
                throw new ArgumentException(errorMessage);
            }

            ResourceId childResourceId = new ResourceId();
            childResourceId.Database = collId.Database;
            childResourceId.DocumentCollection = collId.DocumentCollection;

            switch (resourceType)
            {
                case ResourceType.StoredProcedure:
                    childResourceId.StoredProcedure = childId;
                    return childResourceId;

                case ResourceType.Trigger:
                    childResourceId.Trigger = childId;
                    return childResourceId;

                case ResourceType.UserDefinedFunction:
                    childResourceId.UserDefinedFunction = childId;
                    return childResourceId;

                case ResourceType.PartitionKeyRange:
                    childResourceId.PartitionKeyRange = childId;
                    return childResourceId;

                case ResourceType.Document:
                    childResourceId.Document = childId;
                    return childResourceId;

                default:
                    string errorMessage = string.Format(CultureInfo.InvariantCulture, "ResourceType '{0}'  not a child of Collection.", resourceType);
                    DefaultTrace.TraceError(errorMessage);
                    throw new ArgumentException(errorMessage);
            }
        }

        public static ResourceId NewUserId(string databaseId, uint userId)
        {
            ResourceId dbId = ResourceId.Parse(databaseId);

            ResourceId userResourceId = new ResourceId();
            userResourceId.Database = dbId.Database;
            userResourceId.User = userId;

            return userResourceId;
        }

        public static ResourceId NewPermissionId(string userId, ulong permissionId)
        {
            ResourceId usrId = ResourceId.Parse(userId);

            ResourceId permissionResourceId = new ResourceId();
            permissionResourceId.Database = usrId.Database;
            permissionResourceId.User = usrId.User;
            permissionResourceId.Permission = permissionId;
            return permissionResourceId;
        }

        public static ResourceId NewAttachmentId(string documentId, uint attachmentId)
        {
            ResourceId docId = ResourceId.Parse(documentId);

            ResourceId attachmentResourceId = new ResourceId();
            attachmentResourceId.Database = docId.Database;
            attachmentResourceId.DocumentCollection = docId.DocumentCollection;
            attachmentResourceId.Document = docId.Document;
            attachmentResourceId.Attachment = attachmentId;

            return attachmentResourceId;
        }

        public static string CreateNewCollectionChildResourceId(int childResourceIdIndex, ResourceType resourceType, string ownerResourceId)
        {
            byte[] subCollRes = new byte[8];
            switch (resourceType)
            {
                case ResourceType.PartitionKeyRange:
                    subCollRes[7] = (byte)CollectionChildResourceType.PartitionKeyRange << 4;
                    break;

                case ResourceType.UserDefinedFunction:
                    subCollRes[7] = (byte)CollectionChildResourceType.UserDefinedFunction << 4;
                    break;

                case ResourceType.Trigger:
                    subCollRes[7] = (byte)CollectionChildResourceType.Trigger << 4;
                    break;

                case ResourceType.StoredProcedure:
                    subCollRes[7] = (byte)CollectionChildResourceType.StoredProcedure << 4;
                    break;

                case ResourceType.Document:
                    subCollRes[7] = (byte)CollectionChildResourceType.Document << 4;
                    break;

                default:
                    string errorMessage = string.Format(CultureInfo.InvariantCulture, "Invalid resource for CreateNewCollectionChildResourceId: '{0}'.", resourceType);
                    DefaultTrace.TraceError(errorMessage);
                    throw new ArgumentException(errorMessage);
            }

            byte[] childResourceIdIndexAsBytes = BitConverter.GetBytes(childResourceIdIndex);

            if(childResourceIdIndexAsBytes.Length > 6)
            {
                throw new BadRequestException("ChildResourceIdIndex size is too big to be used as resource id.");
            }

            for (int i = 0; i < childResourceIdIndexAsBytes.Length; i++)
            {
                subCollRes[i] = childResourceIdIndexAsBytes[i];
            }

            int startIndex = 0;
            ulong childResourceKey = BitConverter.ToUInt64(subCollRes, startIndex);

            string childResourceId = ResourceId.NewCollectionChildResourceId(ownerResourceId, childResourceKey, resourceType).ToString();

            return childResourceId;
        }

        public static bool TryParse(string id, out ResourceId rid)
        {
            rid = null;

            try
            {
                if (string.IsNullOrEmpty(id))
                    return false;

                if (id.Length % 4 != 0)
                {
                    // our resourceId string is always padded
                    return false;
                }

                byte[] buffer = null;

                if (ResourceId.Verify(id, out buffer) == false)
                    return false;

                if (buffer.Length % 4 != 0 &&
                    buffer.Length != ResourceId.OfferIdLength &&
                    buffer.Length != ResourceId.SnapshotIdLength &&
                    buffer.Length != ResourceId.RbacResourceIdLength)
                {
                    return false;
                }

                rid = new ResourceId();

                if (buffer.Length == ResourceId.OfferIdLength)
                {
                    rid.Offer = (uint)ResourceId.ToUnsignedLong(buffer);
                    return true;
                }

                if (buffer.Length == ResourceId.SnapshotIdLength)
                {
                    rid.Snapshot = ResourceId.ToUnsignedLong(buffer);
                    return true;
                }

                if (buffer.Length == ResourceId.RbacResourceIdLength)
                {
                    byte rbacResourceType = buffer[ResourceId.RbacResourceIdLength - 1];
                    ulong rbacResourceId = ResourceId.ToUnsignedLong(buffer, 4);

                    switch((RbacResourceType)rbacResourceType)
                    {
                        case RbacResourceType.RbacResourceType_RoleDefinition:
                            rid.RoleDefinition = rbacResourceId;
                            break;

                        case RbacResourceType.RbacResourceType_RoleAssignment:
                            rid.RoleAssignment = rbacResourceId;
                            break;

                        default:
                            return false;
                    }
                    
                    return true;
                }

                if (buffer.Length >= 4)
                    rid.Database = BitConverter.ToUInt32(buffer, 0);

                if (buffer.Length >= 8)
                {
                    byte[] temp = new byte[4];
                    ResourceId.BlockCopy(buffer, 4, temp, 0, 4);

                    bool isCollection = (temp[0] & (128)) > 0 ? true : false;

                    if (isCollection)
                    {
                        rid.DocumentCollection = BitConverter.ToUInt32(temp, 0);

                        if (buffer.Length >= 16)
                        {
                            byte[] subCollRes = new byte[8];
                            ResourceId.BlockCopy(buffer, 8, subCollRes, 0, 8);

                            UInt64 subCollectionResource = BitConverter.ToUInt64(buffer, 8);
                            if ((subCollRes[7] >> 4) == (byte)CollectionChildResourceType.Document)
                            {
                                rid.Document = subCollectionResource;

                                if (buffer.Length == 20)
                                {
                                    rid.Attachment = BitConverter.ToUInt32(buffer, 16);
                                }
                            }
                            else if ((subCollRes[7] >> 4) == (byte)CollectionChildResourceType.StoredProcedure)
                            {
                                rid.StoredProcedure = subCollectionResource;
                            }
                            else if ((subCollRes[7] >> 4) == (byte)CollectionChildResourceType.Trigger)
                            {
                                rid.Trigger = subCollectionResource;
                            }
                            else if ((subCollRes[7] >> 4) == (byte)CollectionChildResourceType.UserDefinedFunction)
                            {
                                rid.UserDefinedFunction = subCollectionResource;
                            }
                            else if ((subCollRes[7] >> 4) == (byte)CollectionChildResourceType.Conflict)
                            {
                                rid.Conflict = subCollectionResource;
                            }
                            else if ((subCollRes[7] >> 4) == (byte)CollectionChildResourceType.PartitionKeyRange)
                            {
                                rid.PartitionKeyRange = subCollectionResource;
                            }
                            else if((subCollRes[7] >> 4) == (byte)CollectionChildResourceType.Schema)
                            {
                                rid.Schema = subCollectionResource;
                            }
                            else
                            {
                                return false;
                            }
                        }
                        else if (buffer.Length != 8)
                        {
                            return false;
                        }
                    }
                    else
                    {
                        rid.User = BitConverter.ToUInt32(temp, 0);

                        if (buffer.Length == 16)
                        {
                            if (rid.User > 0)
                            {
                                rid.Permission = BitConverter.ToUInt64(buffer, 8);
                            }
                            else
                            {
                                uint exDatabaseChildResourceId = BitConverter.ToUInt32(buffer, 8);
                                ExtendedDatabaseChildResourceType exDatabaseChildResType = (ExtendedDatabaseChildResourceType)BitConverter.ToUInt32(buffer, 12);

                                if (exDatabaseChildResType == ExtendedDatabaseChildResourceType.UserDefinedType)
                                {
                                    rid.UserDefinedType = exDatabaseChildResourceId;
                                }
                                else if(exDatabaseChildResType == ExtendedDatabaseChildResourceType.ClientEncryptionKey)
                                {
                                    rid.ClientEncryptionKey = exDatabaseChildResourceId;
                                }
                                else
                                {
                                    return false;
                                }
                            }
                        }
                        else if (buffer.Length != 8)
                        {
                            return false;
                        }
                    }
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool Verify(string id, out byte[] buffer)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentNullException("id");

            buffer = null;

            try
            {
                buffer = ResourceId.FromBase64String(id);
            }
            catch (FormatException)
            { }

            if (buffer == null || buffer.Length > ResourceId.Length)
            {
                buffer = null;
                return false;
            }

            return true;
        }

        public static bool Verify(string id)
        {
            byte[] buffer = null;
            return Verify(id, out buffer);
        }

        public override string ToString()
        {
            return ResourceId.ToBase64String(this.Value);
        }

        public bool Equals(ResourceId other)
        {
            if (other == null)
            {
                return false;
            }

            return Enumerable.SequenceEqual(this.Value, other.Value);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }
            if (ReferenceEquals(this, obj))
            {
                return true;
            }
            return obj is ResourceId && this.Equals((ResourceId)obj);
        }

        public override int GetHashCode()
        {
            throw new NotImplementedException();
        }

        public static byte[] FromBase64String(string s)
        {
            return Convert.FromBase64String(s.Replace('-', '/'));
        }

        public static ulong ToUnsignedLong(byte[] buffer)
        {
            return ResourceId.ToUnsignedLong(buffer, buffer.Length);
        }

        public static ulong ToUnsignedLong(byte[] buffer, int length)
        {
            ulong value = 0;

            for (int index = 0; index < length; index++)
            {
                value |= (uint)(buffer[index] << (index * 8));
            }

            return value;
        }

        public static string ToBase64String(byte[] buffer)
        {
            return ResourceId.ToBase64String(buffer, 0, buffer.Length);
        }

        public static string ToBase64String(byte[] buffer, int offset, int length)
        {
            return Convert.ToBase64String(buffer, offset, length).Replace('/', '-');
        }

        #region Private unused, here for conceptual purpose
        private static ResourceId NewDocumentId(uint dbId, uint collId)
        {
            ResourceId rid = new ResourceId();

            rid.Database = dbId;
            rid.DocumentCollection = collId;

            byte[] guidBytes = Guid.NewGuid().ToByteArray();
            rid.Document = BitConverter.ToUInt64(guidBytes, 0);

            return rid;
        }

        private static ResourceId NewDocumentCollectionId(uint dbId)
        {
            ResourceId rid = new ResourceId();

            rid.Database = dbId;

            byte[] temp = new byte[4];
            byte[] guidBytes = Guid.NewGuid().ToByteArray();

            //collection has the first bit set
            guidBytes[0] |= 128;

            ResourceId.BlockCopy(guidBytes, 0, temp, 0, 4);

            rid.DocumentCollection = BitConverter.ToUInt32(temp, 0);

            rid.Document = 0;
            rid.User = 0;
            rid.Permission = 0;

            return rid;
        }

        private static ResourceId NewDatabaseId()
        {
            ResourceId rid = new ResourceId();

            byte[] guidBytes = Guid.NewGuid().ToByteArray();
            rid.Database = BitConverter.ToUInt32(guidBytes, 0);

            rid.DocumentCollection = 0;
            rid.Document = 0;
            rid.User = 0;
            rid.Permission = 0;

            return rid;
        }
        #endregion

        // Copy the bytes provided with a for loop, faster when there are only a few bytes to copy
        public static void BlockCopy(byte[] src, int srcOffset, byte[] dst, int dstOffset, int count)
        {
            int stop = srcOffset + count;
            for (int i = srcOffset; i < stop; i++)
                dst[dstOffset++] = src[i];
        }

        private static bool HasNonHierarchicalResourceId(ResourceType eResourceType)
        {
#if !COSMOSCLIENT
            return eResourceType == ResourceType.MasterPartition ||
                eResourceType == ResourceType.ServerPartition ||
                eResourceType == ResourceType.RidRange;
#else
            return false;
#endif
        }

        // Using a byte however, we only need nibble here.
        private enum CollectionChildResourceType : byte
        {
            Document = 0x00,
            StoredProcedure = 0x08,
            Trigger = 0x07,
            UserDefinedFunction = 0x06,
            Conflict = 0x04,
            PartitionKeyRange = 0x05,
            Schema = 0x09,
        }

        private enum ExtendedDatabaseChildResourceType
        {
            UserDefinedType = 0x01,
            ClientEncryptionKey = 0x02
        }

        internal enum RbacResourceType : byte
        {
            RbacResourceType_RoleDefinition = 0x00,
            RbacResourceType_RoleAssignment = 0x10,
        }
    }
}
