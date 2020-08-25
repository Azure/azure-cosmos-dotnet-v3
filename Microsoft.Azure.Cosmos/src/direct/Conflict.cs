﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Text;

    /// <summary>
    /// This is the conflicting resource resulting from a concurrent async operation in the Azure Cosmos DB service.
    /// </summary> 
    /// <remarks>
    /// On rare occasions, during an async operation (insert, replace and delete), a version conflict may occur on a resource.
    /// The conflicting resource is persisted as a Conflict resource.  
    /// Inspecting Conflict resources will allow you to determine which operations and resources resulted in conflicts.
    /// </remarks>
#if COSMOSCLIENT
    internal
#else
    public
#endif
    class Conflict : Resource
    {
        /// <summary>
        /// Initialize a new instance of a Conflict class in the Azure Cosmos DB service.
        /// </summary>
        public Conflict()
        {
        }

        /// <summary>
        /// Gets the resource ID for the conflict in the Azure Cosmos DB service.
        /// </summary>
        public string SourceResourceId
        {
            get
            {
                return base.GetValue<string>(Constants.Properties.SourceResourceId);
            }

            internal set
            {
                base.SetValue(Constants.Properties.SourceResourceId, value);
            }
        }

        internal long ConflictLSN
        {
            get
            {
                return base.GetValue<long>(Constants.Properties.ConflictLSN);
            }

            set
            {
                base.SetValue(Constants.Properties.ConflictLSN, value);
            }
        }

        /// <summary>
        /// Gets the conflicting resource in the Azure Cosmos DB service.
        /// </summary>
        /// <typeparam name="T">The returned type of conflicting resource.</typeparam>
        /// <returns>The conflicting resource.</returns>
        public T GetResource<T>() where T : Resource, new()
        {
            if (typeof(T) != this.ResourceType)
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.CurrentUICulture, RMResources.InvalidResourceType, typeof(T).Name, this.ResourceType.Name));
            }

            string content = base.GetValue<string>(Constants.Properties.Content);
            if (!string.IsNullOrEmpty(content))
            {
                using (MemoryStream stream = new MemoryStream())
                {
                    using (StreamWriter writer = new StreamWriter(stream))
                    {
                        writer.Write(content);
                        writer.Flush();
                        stream.Position = 0;
                        return JsonSerializable.LoadFrom<T>(stream);
                    }
                }
            }

            return null;
        }

        internal void SetResource<T>(T baseResource) where T : Resource, new()
        {
            if (typeof(T) != this.ResourceType)
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.CurrentUICulture, RMResources.InvalidResourceType, typeof(T).Name, this.ResourceType.Name));
            }

            StringBuilder sb = new StringBuilder();
            baseResource.SaveTo(sb);
            string content = sb.ToString();
            if (!string.IsNullOrEmpty(content))
            {
                this.SetValue(Constants.Properties.Content, content);
            }

            this.Id = baseResource.Id;
            this.ResourceId = baseResource.ResourceId;
        }

        /// <summary>
        /// Gets the operation that resulted in the conflict in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// One of the values of the <see cref="OperationKind"/> enumeration.
        /// </value>
        public OperationKind OperationKind
        {
            get
            {
                string operationKind = base.GetValue<string>(Constants.Properties.OperationType);

                if (string.Equals(Constants.Properties.OperationKindCreate, operationKind, StringComparison.OrdinalIgnoreCase))
                {
                    return OperationKind.Create;
                }
                else if (string.Equals(Constants.Properties.OperationKindReplace, operationKind, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(Constants.Properties.OperationKindPatch, operationKind, StringComparison.OrdinalIgnoreCase))
                {
                    return OperationKind.Replace;
                }
                else if (string.Equals(Constants.Properties.OperationKindDelete, operationKind, StringComparison.OrdinalIgnoreCase))
                {
                    return OperationKind.Delete;
                }
                else
                {
                    return OperationKind.Invalid;
                }
            }

            internal set
            {
                string operationKind = "";
                if (value == OperationKind.Create)
                {
                    operationKind = Constants.Properties.OperationKindCreate;
                }
                else if (value == OperationKind.Replace)
                {
                    operationKind = Constants.Properties.OperationKindReplace;
                }
                else if (value == OperationKind.Delete)
                {
                    operationKind = Constants.Properties.OperationKindDelete;
                }
                else
                {
                    throw new ArgumentException(String.Format(CultureInfo.CurrentUICulture, "Unsupported operation kind {0}", value.ToString()));
                }

                base.SetValue(Constants.Properties.OperationType, operationKind);
            }
        }

        /// <summary>
        /// Gets the type of the conflicting resource in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The type of the resource.
        /// </value>
        public Type ResourceType
        {
            get
            {
                string resourceType = base.GetValue<string>(Constants.Properties.ResourceType);

                if (string.Equals(Constants.Properties.ResourceTypeDocument, resourceType, StringComparison.OrdinalIgnoreCase))
                {
                    return typeof(Document);
                }
                else if (string.Equals(Constants.Properties.ResourceTypeStoredProcedure, resourceType, StringComparison.OrdinalIgnoreCase))
                {
                    return typeof(StoredProcedure);
                }
                else if (string.Equals(Constants.Properties.ResourceTypeTrigger, resourceType, StringComparison.OrdinalIgnoreCase))
                {
                    return typeof(Trigger);
                }
                else if (string.Equals(Constants.Properties.ResourceTypeUserDefinedFunction, resourceType, StringComparison.OrdinalIgnoreCase))
                {
                    return typeof(UserDefinedFunction);
                }
                else if (string.Equals(Constants.Properties.ResourceTypeAttachment, resourceType, StringComparison.OrdinalIgnoreCase))
                {
                    return typeof(Attachment);
                }
                else
                {
                    return null;
                }
            }

            internal set
            {
                string resourceType = null;
                if (value == typeof(Document))
                {
                    resourceType = Constants.Properties.ResourceTypeDocument;
                }
                else if (value == typeof(StoredProcedure))
                {
                    resourceType = Constants.Properties.ResourceTypeStoredProcedure;
                }
                else if (value == typeof(Trigger))
                {
                    resourceType = Constants.Properties.ResourceTypeTrigger;
                }
                else if (value == typeof(UserDefinedFunction))
                {
                    resourceType = Constants.Properties.ResourceTypeUserDefinedFunction;
                }
                else if (value == typeof(Attachment))
                {
                    resourceType = Constants.Properties.ResourceTypeAttachment;
                }
                else
                {
                    throw new ArgumentException(String.Format(CultureInfo.CurrentUICulture, "Unsupported resource type {0}", value.ToString()));
                }

                base.SetValue(Constants.Properties.ResourceType, resourceType);
            }
        }
    }
}
