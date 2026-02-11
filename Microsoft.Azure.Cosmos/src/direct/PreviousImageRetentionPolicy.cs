//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using Newtonsoft.Json;

    /// <summary>
    /// Bitwise Enum for Previous Image mode for Collection features like PITR, all versions and deletes change feed, global secondary indexes etc.
    /// </summary>
    [Flags]
    internal enum PreviousImageRetentionMode
    {
        /// <summary>
        /// Previous image retention is disabled.
        /// </summary>
        Disabled = 0x00,

        /// <summary>
        /// Previous image retention is enabled for replace operations.
        /// </summary>
        EnabledForReplaceOperations = 0x01,

        /// <summary>
        /// Previous image retention is enabled for delete operations.
        /// </summary>
        EnabledForDeleteOperations = 0x02,

        /// <summary>
        /// Previous image retention is enabled for all operations.
        /// </summary>
        EnabledForAllOperations = EnabledForReplaceOperations | EnabledForDeleteOperations
    }

    /// <summary>
    /// Represents the previous image retention policy for a collection in the Azure Cosmos DB service.
    /// </summary>
    /// <seealso cref="Microsoft.Azure.Documents.DocumentCollection"/>
    internal sealed class PreviousImageRetentionPolicy : JsonSerializable, ICloneable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PreviousImageRetentionPolicy"/> class for the Azure Cosmos DB service.
        /// </summary>
        public PreviousImageRetentionPolicy()
        {
        }

        /// <summary>
        /// Gets or sets the PreviousImageRetentionMode (Disabled, EnabledForReplaceOperations, EnabledForDeleteOperation, or EnabledForAllOperations) in the Azure Cosmos DB service.
        /// This is an optional property with a default value of null.
        /// </summary>
        /// <value>
        /// One of the values of the <see cref="T:Microsoft.Azure.Documents.PreviousImageRetentionMode"/> enumeration, or null if not set.
        /// </value>
        [JsonProperty(PropertyName = Constants.Properties.Mode, NullValueHandling = NullValueHandling.Ignore)]
        public PreviousImageRetentionMode? PreviousImageRetentionMode
        {
            get
            {
                return base.GetEnumValue<PreviousImageRetentionMode>(Constants.Properties.Mode);
            }
            set
            {
                if (value.HasValue)
                {
                    base.SetValue(Constants.Properties.Mode, (int)value.Value);
                }
                else
                {
                    base.SetValue(Constants.Properties.Mode, null);
                }
            }
        }

        /// <summary>
        /// Determines whether previous image retention is enabled for replace operations.
        /// </summary>
        /// <returns>True if previous image retention is enabled for replace operations; otherwise, false.</returns>
        public bool IsEnabledForReplaceOperation()
        {
            return this.PreviousImageRetentionMode.HasValue &&
                   (this.PreviousImageRetentionMode.Value & Documents.PreviousImageRetentionMode.EnabledForReplaceOperations) != Documents.PreviousImageRetentionMode.Disabled;
        }

        /// <summary>
        /// Determines whether previous image retention is enabled for delete operations.
        /// </summary>
        /// <returns>True if previous image retention is enabled for delete operations; otherwise, false.</returns>
        public bool IsEnabledForDeleteOperation()
        {
            return this.PreviousImageRetentionMode.HasValue &&
                   (this.PreviousImageRetentionMode.Value & Documents.PreviousImageRetentionMode.EnabledForDeleteOperations) != Documents.PreviousImageRetentionMode.Disabled;
        }

        /// <summary>
        /// Performs a deep copy of the previous image retention policy.
        /// </summary>
        /// <returns>
        /// A clone of the previous image retention policy.
        /// </returns>
        public object Clone()
        {
            PreviousImageRetentionPolicy cloned = new PreviousImageRetentionPolicy()
            {
                PreviousImageRetentionMode = this.PreviousImageRetentionMode,
            };

            return cloned;
        }
    }
}
