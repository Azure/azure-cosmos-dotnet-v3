//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents.Common.AttributeBasedAccessControl
{
    using System;
    using System.IO;
    using Microsoft.Azure.Documents.Routing;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// An ABAC Policy grants a principal (identified by AAD Object ID)
    /// access to a specific partition key value within a container.
    /// </summary>
    internal sealed class AbacPolicy : Resource
    {
        public AbacPolicy()
        {
        }

        public AbacPolicy(
            string id,
            string oid,
            string collectionRid,
            string partitionKeyJson,
            PermissionMode permissionMode = PermissionMode.All,
            string databaseName = null,
            string containerName = null)
        {
            base.SetValue(Constants.Properties.Id, id);
            base.SetValue(AbacPolicy.SerializationConstants.Oid, oid);
            base.SetValue(AbacPolicy.SerializationConstants.CollectionRid, collectionRid);
            base.SetValue(AbacPolicy.SerializationConstants.PartitionKeyJson, partitionKeyJson);
            base.SetValue(AbacPolicy.SerializationConstants.PermissionMode, permissionMode.ToString());
            if (databaseName != null)
            {
                base.SetValue(AbacPolicy.SerializationConstants.DatabaseName, databaseName);
            }

            if (containerName != null)
            {
                base.SetValue(AbacPolicy.SerializationConstants.ContainerName, containerName);
            }

            this.Validate();
        }

        public override void LoadFrom(JsonReader reader)
        {
            reader.DateParseHandling = DateParseHandling.None;
            base.LoadFrom(reader);
        }

        public override void LoadFrom(
            JsonReader reader,
            JsonSerializerSettings serializerSettings)
        {
            reader.DateParseHandling = DateParseHandling.None;
            base.LoadFrom(reader, serializerSettings);
        }

        /// <summary>
        /// AAD Object ID of the principal this policy applies to.
        /// </summary>
        public string Oid
        {
            get
            {
                string oidValue = base.GetValue<string>(AbacPolicy.SerializationConstants.Oid);
                if (string.IsNullOrWhiteSpace(oidValue))
                {
                    throw new FormatException(
                        string.Format(
                            AbacPolicy.SerializationErrors.MissingRequiredProperty,
                            AbacPolicy.SerializationConstants.Oid));
                }

                return oidValue;
            }
        }

        /// <summary>
        /// Resource ID of the container this policy applies to.
        /// </summary>
        public string CollectionRid
        {
            get
            {
                string collectionRidValue = base.GetValue<string>(AbacPolicy.SerializationConstants.CollectionRid);
                if (string.IsNullOrWhiteSpace(collectionRidValue))
                {
                    throw new FormatException(
                        string.Format(
                            AbacPolicy.SerializationErrors.MissingRequiredProperty,
                            AbacPolicy.SerializationConstants.CollectionRid));
                }

                return collectionRidValue;
            }
        }

        public string DatabaseName => base.GetValue<string>(AbacPolicy.SerializationConstants.DatabaseName);

        public string ContainerName => base.GetValue<string>(AbacPolicy.SerializationConstants.ContainerName);

        /// <summary>
        /// Partition key value the principal is allowed to access.
        /// </summary>
        public string PartitionKeyJson
        {
            get
            {
                return base.GetValue<string>(AbacPolicy.SerializationConstants.PartitionKeyJson);
            }
        }

        internal JToken GetPartitionKeyValue()
        {
            string partitionKeyJson = this.PartitionKeyJson;
            if (partitionKeyJson == null)
            {
                return null;
            }

            JArray components;
            using (StringReader stringReader = new StringReader(partitionKeyJson))
            using (JsonTextReader jsonReader = new JsonTextReader(stringReader))
            {
                jsonReader.DateParseHandling = DateParseHandling.None;
                components = JArray.Load(jsonReader);
            }

            return components.Count == 1
                ? components[0].DeepClone()
                : components.DeepClone();
        }

        /// <summary>
        /// Operations the policy grants within its partition key scope.
        /// </summary>
        public PermissionMode PermissionMode
        {
            get
            {
                string permissionModeValue =
                    base.GetValue<string>(AbacPolicy.SerializationConstants.PermissionMode);

                if (string.IsNullOrWhiteSpace(permissionModeValue))
                {
                    return PermissionMode.All;
                }

                if (!Enum.TryParse(permissionModeValue, ignoreCase: true, out PermissionMode permissionMode) ||
                    (permissionMode != PermissionMode.Read && permissionMode != PermissionMode.All))
                {
                    throw new FormatException(
                        string.Format(
                            AbacPolicy.SerializationErrors.InvalidPermissionMode,
                            permissionModeValue));
                }

                return permissionMode;
            }
        }

        internal void SetContainerNames(string databaseName, string containerName)
        {
            base.SetValue(AbacPolicy.SerializationConstants.DatabaseName, databaseName);
            base.SetValue(AbacPolicy.SerializationConstants.ContainerName, containerName);
        }

        internal void ValidateReplaceableProperties(
            string oid,
            string collectionRid,
            string partitionKeyJson)
        {
            if (!Guid.Parse(this.Oid).Equals(Guid.Parse(oid)))
            {
                throw new BadRequestException(AbacPolicy.UpdateErrors.OidUpdateNotPermitted);
            }

            if (!string.Equals(this.CollectionRid, collectionRid, StringComparison.Ordinal))
            {
                throw new BadRequestException(AbacPolicy.UpdateErrors.CollectionRidUpdateNotPermitted);
            }

            if (!PartitionKeyInternal.FromJsonString(this.PartitionKeyJson).Equals(
                PartitionKeyInternal.FromJsonString(partitionKeyJson)))
            {
                throw new BadRequestException(AbacPolicy.UpdateErrors.PartitionKeyUpdateNotPermitted);
            }
        }

        internal override void Validate()
        {
            base.Validate();

            if (string.IsNullOrWhiteSpace(this.Id))
            {
                throw new FormatException(
                    string.Format(
                        AbacPolicy.SerializationErrors.MissingRequiredProperty,
                        Constants.Properties.Id));
            }

            _ = this.Oid;
            _ = this.CollectionRid;
            _ = this.PermissionMode;

            if (string.IsNullOrWhiteSpace(this.PartitionKeyJson))
            {
                throw new FormatException(
                    string.Format(
                        AbacPolicy.SerializationErrors.MissingRequiredProperty,
                        AbacPolicy.SerializationConstants.PartitionKeyJson));
            }
        }

        /// <summary>
        /// Validates RID inputs before a write without changing legacy read validation.
        /// </summary>
        internal void ValidateForWrite()
        {
            this.Validate();

            string collectionRidValue = this.CollectionRid;
            if (collectionRidValue.Length != Documents.ResourceId.EncodedCollectionIdLength ||
                !Documents.ResourceId.TryParse(collectionRidValue, out ResourceId collectionRid) ||
                !collectionRid.IsDocumentCollectionId)
            {
                throw new FormatException(AbacPolicy.SerializationErrors.InvalidCollectionRid);
            }

            if (!string.IsNullOrEmpty(this.ResourceId) &&
                (this.ResourceId.Length != Documents.ResourceId.EncodedRbacResourceIdLength ||
                 !Documents.ResourceId.TryParse(this.ResourceId, out ResourceId policyRid) ||
                 !policyRid.IsAbacPolicyId))
            {
                throw new FormatException(AbacPolicy.SerializationErrors.InvalidPolicyRid);
            }
        }

        private static class SerializationConstants
        {
            public const string Oid = "oid";
            public const string CollectionRid = "collectionRid";
            public const string DatabaseName = "databaseName";
            public const string ContainerName = "containerName";
            public const string PartitionKeyJson = "pkValueJson";
            public const string PermissionMode = "permissionMode";
        }

        internal static class SerializationErrors
        {
            public const string MissingRequiredProperty = "Required property [{0}] is not present or is empty.";
            public const string InvalidPermissionMode = "Property [permissionMode] has invalid value [{0}]. Expected [Read] or [All].";
            public const string InvalidCollectionRid = "Property [collectionRid] must be a valid collection RID.";
            public const string InvalidPolicyRid = "Property [_rid] must be a valid ABAC Policy RID.";
        }

        internal static class UpdateErrors
        {
            public const string OidUpdateNotPermitted =
                "Updating ABAC Policy oid is not permitted. You may only update permissionMode.";
            public const string CollectionRidUpdateNotPermitted =
                "Updating ABAC Policy target container is not permitted. You may only update permissionMode.";
            public const string PartitionKeyUpdateNotPermitted =
                "Updating ABAC Policy pkValue is not permitted. You may only update permissionMode.";
        }
    }
}
