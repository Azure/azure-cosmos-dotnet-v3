// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System;
    using System.Linq;

    //Resource ID is 20 byte number. It is not a guid.
    //First 4 bytes for DB id -> 2^32 DBs per tenant --> uint
    //  Next 4 bytes for coll id OR user id -> 2^31 coll/users per DB. the first bit indicates if this is a collection or a user --> uint
    //     Next 8 bytes for doc id OR permission or sproc/trigger/function or conflicts -> 2^64 permission per user. Note that the disambiguation between collection
    //     based resources (document/sproc/trigger/function/conflict) and user based resource i.e. permission is based on ownerid. --> ulong
    //     Disambiguation between document/sproc/conflict is based upon first 4 bits of the highest byte. 0x00 is document, 0x08 is sproc, 0x07 is
    //     trigger, 0x06 is function and 0x04 is conflict. 2^60 document/sproc/trigger/function/conflict per collection.
    //       Last 4 bytes for attachment id -> 2^32 attachments per "Document". These bits are used only for document's children. Permission/Sproc/
    //       Conflict RID hierarchy is only 16 bytes.

    internal sealed class ResourceIdentifier
    {
        private const int OfferIdLength = 3;
        private const int RbacResourceIdLength = 6;
        private const int SnapshotIdLength = 7;
        public static readonly ushort Length = 20;
        public static readonly ushort MaxPathFragment = 8;  // for public resource
        public static readonly ResourceIdentifier Empty = new ResourceIdentifier();

        public ResourceIdentifier(
            uint offer = 0,
            uint database = 0,
            uint documentCollection = 0,
            ulong storedProcedure = 0,
            ulong trigger = 0,
            ulong userDefinedFunction = 0,
            ulong conflict = 0,
            ulong document = 0,
            ulong partitionKeyRange = 0,
            uint user = 0,
            uint clientEncryptionKey = 0,
            uint userDefinedType = 0,
            ulong permission = 0,
            uint attachment = 0,
            ulong schema = 0,
            ulong snapshot = 0,
            ulong roleAssignment = 0,
            ulong roleDefinition = 0)
        {
            this.Offer = offer;
            this.Database = database;
            this.DocumentCollection = documentCollection;
            this.StoredProcedure = storedProcedure;
            this.Trigger = trigger;
            this.UserDefinedFunction = userDefinedFunction;
            this.Conflict = conflict;
            this.Document = document;
            this.PartitionKeyRange = partitionKeyRange;
            this.User = user;
            this.ClientEncryptionKey = clientEncryptionKey;
            this.Permission = permission;
            this.Attachment = attachment;
            this.Schema = schema;
            this.UserDefinedType = userDefinedType;
            this.Snapshot = snapshot;
            this.RoleAssignment = roleAssignment;
            this.RoleDefinition = roleDefinition;
        }

        public uint Offer { get; }
        public uint Database { get; }
        public uint DocumentCollection { get; }
        public ulong StoredProcedure { get; }
        public ulong Trigger { get; }
        public ulong UserDefinedFunction { get; }
        public ulong Conflict { get; }
        public ulong Document { get; }
        public ulong PartitionKeyRange { get; }
        public uint User { get; }
        public uint ClientEncryptionKey { get; }
        public uint UserDefinedType { get; }
        public ulong Permission { get; }
        public uint Attachment { get; }
        public ulong Schema { get; }
        public ulong Snapshot { get; }
        public ulong RoleAssignment { get; }
        public ulong RoleDefinition { get; }

        public byte[] ToByteArray()
        {
            int len = 0;
            if (this.Offer > 0)
            {
                len += ResourceIdentifier.OfferIdLength;
            }
            else if (this.Snapshot > 0)
            {
                len += ResourceIdentifier.SnapshotIdLength;
            }
            else if (this.RoleAssignment > 0)
            {
                len += ResourceIdentifier.RbacResourceIdLength;
            }
            else if (this.RoleDefinition > 0)
            {
                len += ResourceIdentifier.RbacResourceIdLength;
            }
            else if (this.Database > 0)
            {
                len += 4;
            }

            if (this.DocumentCollection > 0 || this.User > 0 || this.UserDefinedType > 0 || this.ClientEncryptionKey > 0)
            {
                len += 4;
            }

            if (this.Document > 0 || this.Permission > 0 || this.StoredProcedure > 0 || this.Trigger > 0
                || this.UserDefinedFunction > 0 || this.Conflict > 0 || this.PartitionKeyRange > 0 || this.Schema > 0
                || this.UserDefinedType > 0 || this.ClientEncryptionKey > 0)
            {
                len += 8;
            }

            if (this.Attachment > 0)
            {
                len += 4;
            }

            byte[] val = new byte[len];

            if (this.Offer > 0)
            {
                ResourceIdentifier.BlockCopy(BitConverter.GetBytes(this.Offer), 0, val, 0, ResourceIdentifier.OfferIdLength);
            }
            else if (this.Database > 0)
            {
                ResourceIdentifier.BlockCopy(BitConverter.GetBytes(this.Database), 0, val, 0, 4);
            }
            else if (this.Snapshot > 0)
            {
                ResourceIdentifier.BlockCopy(BitConverter.GetBytes(this.Snapshot), 0, val, 0, ResourceIdentifier.SnapshotIdLength);
            }
            else if (this.RoleAssignment > 0)
            {
                ResourceIdentifier.BlockCopy(BitConverter.GetBytes(this.RoleAssignment), 0, val, 0, 4);
                ResourceIdentifier.BlockCopy(BitConverter.GetBytes(0x1000), 0, val, 4, 2);
            }
            else if (this.RoleDefinition > 0)
            {
                ResourceIdentifier.BlockCopy(BitConverter.GetBytes(this.RoleDefinition), 0, val, 0, ResourceIdentifier.RbacResourceIdLength);
            }

            if (this.DocumentCollection > 0)
            {
                ResourceIdentifier.BlockCopy(BitConverter.GetBytes(this.DocumentCollection), 0, val, 4, 4);
            }
            else if (this.User > 0)
            {
                ResourceIdentifier.BlockCopy(BitConverter.GetBytes(this.User), 0, val, 4, 4);
            }

            if (this.StoredProcedure > 0)
            {
                ResourceIdentifier.BlockCopy(BitConverter.GetBytes(this.StoredProcedure), 0, val, 8, 8);
            }
            else if (this.Trigger > 0)
            {
                ResourceIdentifier.BlockCopy(BitConverter.GetBytes(this.Trigger), 0, val, 8, 8);
            }
            else if (this.UserDefinedFunction > 0)
            {
                ResourceIdentifier.BlockCopy(BitConverter.GetBytes(this.UserDefinedFunction), 0, val, 8, 8);
            }
            else if (this.Conflict > 0)
            {
                ResourceIdentifier.BlockCopy(BitConverter.GetBytes(this.Conflict), 0, val, 8, 8);
            }
            else if (this.Document > 0)
            {
                ResourceIdentifier.BlockCopy(BitConverter.GetBytes(this.Document), 0, val, 8, 8);
            }
            else if (this.PartitionKeyRange > 0)
            {
                ResourceIdentifier.BlockCopy(BitConverter.GetBytes(this.PartitionKeyRange), 0, val, 8, 8);
            }
            else if (this.Permission > 0)
            {
                ResourceIdentifier.BlockCopy(BitConverter.GetBytes(this.Permission), 0, val, 8, 8);
            }
            else if (this.Schema > 0)
            {
                ResourceIdentifier.BlockCopy(BitConverter.GetBytes(this.Schema), 0, val, 8, 8);
            }
            else if (this.UserDefinedType > 0)
            {
                ResourceIdentifier.BlockCopy(BitConverter.GetBytes(this.UserDefinedType), 0, val, 8, 4);
                ResourceIdentifier.BlockCopy(BitConverter.GetBytes((uint)ExtendedDatabaseChildResourceType.UserDefinedType), 0, val, 12, 4);
            }
            else if (this.ClientEncryptionKey > 0)
            {
                ResourceIdentifier.BlockCopy(BitConverter.GetBytes(this.ClientEncryptionKey), 0, val, 8, 4);
                ResourceIdentifier.BlockCopy(BitConverter.GetBytes((uint)ExtendedDatabaseChildResourceType.ClientEncryptionKey), 0, val, 12, 4);
            }

            if (this.Attachment > 0)
            {
                ResourceIdentifier.BlockCopy(BitConverter.GetBytes(this.Attachment), 0, val, 16, 4);
            }

            return val;
        }

        public static ResourceIdentifier Parse(string id)
        {
            if (!ResourceIdentifier.TryParse(id, out ResourceIdentifier rid))
            {
                throw new FormatException("Failed to parse ResourceId");
            }

            return rid;
        }

        public static bool TryParse(string id, out ResourceIdentifier rid)
        {
            try
            {
                uint offer = 0;
                uint database = 0;
                uint documentCollection = 0;
                ulong storedProcedure = 0;
                ulong trigger = 0;
                ulong userDefinedFunction = 0;
                ulong conflict = 0;
                ulong document = 0;
                ulong partitionKeyRange = 0;
                uint user = 0;
                uint clientEncryptionKey = 0;
                ulong permission = 0;
                uint attachment = 0;
                ulong schema = 0;
                uint userDefinedType = 0;
                ulong snapshot = 0;
                ulong roleAssignment = 0;
                ulong roleDefinition = 0;

                if (string.IsNullOrEmpty(id))
                {
                    rid = default;
                    return false;
                }

                if (id.Length % 4 != 0)
                {
                    // our resourceId string is always padded
                    rid = default;
                    return false;
                }

                if (ResourceIdentifier.Verify(id, out byte[] buffer) == false)
                {
                    rid = default;
                    return false;
                }

                if (buffer.Length % 4 != 0 &&
                    buffer.Length != ResourceIdentifier.OfferIdLength &&
                    buffer.Length != ResourceIdentifier.SnapshotIdLength &&
                    buffer.Length != ResourceIdentifier.RbacResourceIdLength)
                {
                    rid = default;
                    return false;
                }

                if (buffer.Length == ResourceIdentifier.OfferIdLength)
                {
                    offer = (uint)ResourceIdentifier.ToUnsignedLong(buffer);
                    rid = new ResourceIdentifier(offer: offer);
                    return true;
                }

                if (buffer.Length == ResourceIdentifier.SnapshotIdLength)
                {
                    snapshot = ResourceIdentifier.ToUnsignedLong(buffer);
                    rid = new ResourceIdentifier(snapshot: snapshot);
                    return true;
                }

                if (buffer.Length == ResourceIdentifier.RbacResourceIdLength)
                {
                    byte rbacResourceType = buffer[ResourceIdentifier.RbacResourceIdLength - 1];
                    ulong rbacResourceId = ResourceIdentifier.ToUnsignedLong(buffer, 4);

                    switch ((RbacResourceType)rbacResourceType)
                    {
                        case RbacResourceType.RbacResourceType_RoleDefinition:
                            roleDefinition = rbacResourceId;
                            rid = new ResourceIdentifier(
                                offer, database, documentCollection, storedProcedure,
                                trigger, userDefinedFunction, conflict, document,
                                partitionKeyRange, user, clientEncryptionKey,
                                userDefinedType, permission, attachment, schema,
                                snapshot, roleAssignment, roleDefinition);
                            return true;

                        case RbacResourceType.RbacResourceType_RoleAssignment:
                            roleAssignment = rbacResourceId;
                            rid = new ResourceIdentifier(
                                offer, database, documentCollection, storedProcedure,
                                trigger, userDefinedFunction, conflict, document,
                                partitionKeyRange, user, clientEncryptionKey,
                                userDefinedType, permission, attachment, schema,
                                snapshot, roleAssignment, roleDefinition);
                            return true;

                        default:
                            rid = default;
                            return false;
                    }
                }

                if (buffer.Length >= 4)
                    database = BitConverter.ToUInt32(buffer, 0);

                if (buffer.Length >= 8)
                {
                    byte[] temp = new byte[4];
                    ResourceIdentifier.BlockCopy(buffer, 4, temp, 0, 4);

                    bool isCollection = (temp[0] & (128)) > 0;

                    if (isCollection)
                    {
                        documentCollection = BitConverter.ToUInt32(temp, 0);

                        if (buffer.Length >= 16)
                        {
                            byte[] subCollRes = new byte[8];
                            ResourceIdentifier.BlockCopy(buffer, 8, subCollRes, 0, 8);

                            UInt64 subCollectionResource = BitConverter.ToUInt64(buffer, 8);
                            if ((subCollRes[7] >> 4) == (byte)CollectionChildResourceType.Document)
                            {
                                document = subCollectionResource;

                                if (buffer.Length == 20)
                                {
                                    attachment = BitConverter.ToUInt32(buffer, 16);
                                }
                            }
                            else if ((subCollRes[7] >> 4) == (byte)CollectionChildResourceType.StoredProcedure)
                            {
                                storedProcedure = subCollectionResource;
                            }
                            else if ((subCollRes[7] >> 4) == (byte)CollectionChildResourceType.Trigger)
                            {
                                trigger = subCollectionResource;
                            }
                            else if ((subCollRes[7] >> 4) == (byte)CollectionChildResourceType.UserDefinedFunction)
                            {
                                userDefinedFunction = subCollectionResource;
                            }
                            else if ((subCollRes[7] >> 4) == (byte)CollectionChildResourceType.Conflict)
                            {
                                conflict = subCollectionResource;
                            }
                            else if ((subCollRes[7] >> 4) == (byte)CollectionChildResourceType.PartitionKeyRange)
                            {
                                partitionKeyRange = subCollectionResource;
                            }
                            else if ((subCollRes[7] >> 4) == (byte)CollectionChildResourceType.Schema)
                            {
                                schema = subCollectionResource;
                            }
                            else
                            {
                                rid = default;
                                return false;
                            }
                        }
                        else if (buffer.Length != 8)
                        {
                            rid = default;
                            return false;
                        }
                    }
                    else
                    {
                        user = BitConverter.ToUInt32(temp, 0);

                        if (buffer.Length == 16)
                        {
                            if (user > 0)
                            {
                                permission = BitConverter.ToUInt64(buffer, 8);
                            }
                            else
                            {
                                uint exDatabaseChildResourceId = BitConverter.ToUInt32(buffer, 8);
                                ExtendedDatabaseChildResourceType exDatabaseChildResType = (ExtendedDatabaseChildResourceType)BitConverter.ToUInt32(buffer, 12);

                                if (exDatabaseChildResType == ExtendedDatabaseChildResourceType.UserDefinedType)
                                {
                                    userDefinedType = exDatabaseChildResourceId;
                                }
                                else if (exDatabaseChildResType == ExtendedDatabaseChildResourceType.ClientEncryptionKey)
                                {
                                    clientEncryptionKey = exDatabaseChildResourceId;
                                }
                                else
                                {
                                    rid = default;
                                    return false;
                                }
                            }
                        }
                        else if (buffer.Length != 8)
                        {
                            rid = default;
                            return false;
                        }
                    }
                }

                rid = new ResourceIdentifier(
                    offer, database, documentCollection, storedProcedure,
                    trigger, userDefinedFunction, conflict, document,
                    partitionKeyRange, user, clientEncryptionKey,
                    userDefinedType, permission, attachment, schema,
                    snapshot, roleAssignment, roleDefinition);
                return true;
            }
            catch (Exception)
            {
                rid = default;
                return false;
            }
        }

        public static bool Verify(string id, out byte[] buffer)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentNullException(nameof(id));

            buffer = null;

            try
            {
                buffer = ResourceIdentifier.FromBase64String(id);
            }
            catch (FormatException)
            {
            }

            if (buffer == null || buffer.Length > ResourceIdentifier.Length)
            {
                buffer = null;
                return false;
            }

            return true;
        }

        public override string ToString()
        {
            return ResourceIdentifier.ToBase64String(this.ToByteArray());
        }

        public static byte[] FromBase64String(string s)
        {
            return Convert.FromBase64String(s.Replace('-', '/'));
        }

        public static ulong ToUnsignedLong(byte[] buffer)
        {
            return ResourceIdentifier.ToUnsignedLong(buffer, buffer.Length);
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
            return ResourceIdentifier.ToBase64String(buffer, 0, buffer.Length);
        }

        public static string ToBase64String(byte[] buffer, int offset, int length)
        {
            return Convert.ToBase64String(buffer, offset, length).Replace('/', '-');
        }

        // Copy the bytes provided with a for loop, faster when there are only a few bytes to copy
        public static void BlockCopy(byte[] src, int srcOffset, byte[] dst, int dstOffset, int count)
        {
            int stop = srcOffset + count;
            for (int i = srcOffset; i < stop; i++)
                dst[dstOffset++] = src[i];
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
