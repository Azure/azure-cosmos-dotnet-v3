//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.ObjectModel;
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
    /// Represents the previous image retention policy per feature for a collection in the Azure Cosmos DB service.
    /// </summary>
    internal sealed class PreviousImageRetentionPolicyPerFeature : JsonSerializable, ICloneable
    {
        private Collection<string> includedPaths;

        /// <summary>
        /// Initializes a new instance of the <see cref="PreviousImageRetentionPolicyPerFeature"/> class for the Azure Cosmos DB service.
        /// </summary>
        public PreviousImageRetentionPolicyPerFeature()
        {
        }

        /// <summary>
        /// Gets or sets the PreviousImageRetentionMode (Disabled, EnabledForReplaceOperations, EnabledForDeleteOperation, or EnabledForAllOperations) in the Azure Cosmos DB service.
        /// This is an optional property with a default value of null.
        /// </summary>
        /// <value>
        /// One of the values of the <see cref="T:Microsoft.Azure.Documents.PreviousImageRetentionMode"/> enumeration, or null if not set.
        /// </value>
        [JsonProperty(PropertyName = Constants.Properties.PreviousImageRetentionPolicyMode, NullValueHandling = NullValueHandling.Ignore)]
        public PreviousImageRetentionMode? Mode
        {
            get
            {
                return base.GetEnumValue<PreviousImageRetentionMode>(Constants.Properties.PreviousImageRetentionPolicyMode);
            }
            set
            {
                if (value.HasValue)
                {
                    base.SetValue(Constants.Properties.PreviousImageRetentionPolicyMode, (int)value.Value);
                }
                else
                {
                    base.SetValue(Constants.Properties.PreviousImageRetentionPolicyMode, null);
                }
            }
        }

        /// <summary>
        /// Gets or sets the included paths for the previous image retention policy.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.IncludedPaths, NullValueHandling = NullValueHandling.Ignore)]
        public Collection<string> IncludedPaths
        {
            get
            {
                if (this.includedPaths == null)
                {
                    this.includedPaths = base.GetValue<Collection<string>>(Constants.Properties.IncludedPaths);
                    if (this.includedPaths == null)
                    {
                        this.includedPaths = new Collection<string>();
                    }
                }

                return this.includedPaths;
            }
            set
            {
                this.includedPaths = value;
                base.SetValue(Constants.Properties.IncludedPaths, value);
            }
        }

        /// <summary>
        /// Determines whether previous image retention is enabled for replace operations.
        /// </summary>
        /// <returns>True if previous image retention is enabled for replace operations; otherwise, false.</returns>
        public bool IsEnabledForReplaceOperation()
        {
            return this.Mode.HasValue &&
                   (this.Mode.Value & PreviousImageRetentionMode.EnabledForReplaceOperations) != PreviousImageRetentionMode.Disabled;
        }

        /// <summary>
        /// Determines whether previous image retention is enabled for delete operations.
        /// </summary>
        /// <returns>True if previous image retention is enabled for delete operations; otherwise, false.</returns>
        public bool IsEnabledForDeleteOperation()
        {
            return this.Mode.HasValue &&
                   (this.Mode.Value & PreviousImageRetentionMode.EnabledForDeleteOperations) != PreviousImageRetentionMode.Disabled;
        }

        /// <summary>
        /// Performs a deep copy of the previous image retention policy per feature.
        /// </summary>
        /// <returns>
        /// A clone of the previous image retention policy per feature.
        /// </returns>
        public object Clone()
        {
            PreviousImageRetentionPolicyPerFeature cloned = new PreviousImageRetentionPolicyPerFeature()
            {
                Mode = this.Mode,
            };

            if (this.includedPaths != null)
            {
                cloned.includedPaths = new Collection<string>();
                foreach (string path in this.includedPaths)
                {
                    cloned.includedPaths.Add(path);
                }
            }

            return cloned;
        }
    }

    /// <summary>
    /// Represents the supported features for previous image retention policy for a collection in the Azure Cosmos DB service.
    /// </summary>
    internal sealed class PreviousImageRetentionPolicySupportedFeatures : JsonSerializable, ICloneable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PreviousImageRetentionPolicySupportedFeatures"/> class for the Azure Cosmos DB service.
        /// </summary>
        public PreviousImageRetentionPolicySupportedFeatures()
        {
        }

        /// <summary>
        /// Gets or sets the previous image retention policy for global secondary index feature.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.GlobalSecondaryIndex, NullValueHandling = NullValueHandling.Ignore)]
        public PreviousImageRetentionPolicyPerFeature GlobalSecondaryIndex
        {
            get
            {
                return base.GetObject<PreviousImageRetentionPolicyPerFeature>(Constants.Properties.GlobalSecondaryIndex);
            }
            set
            {
                base.SetObject(Constants.Properties.GlobalSecondaryIndex, value);
            }
        }

        /// <summary>
        /// Gets or sets the previous image retention policy for container copy feature.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.ContainerCopy, NullValueHandling = NullValueHandling.Ignore)]
        public PreviousImageRetentionPolicyPerFeature ContainerCopy
        {
            get
            {
                return base.GetObject<PreviousImageRetentionPolicyPerFeature>(Constants.Properties.ContainerCopy);
            }
            set
            {
                base.SetObject(Constants.Properties.ContainerCopy, value);
            }
        }

        /// <summary>
        /// Gets or sets the previous image retention policy for all versions and deletes feature.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.AllVersionsAndDeletes, NullValueHandling = NullValueHandling.Ignore)]
        public PreviousImageRetentionPolicyPerFeature AllVersionsAndDeletes
        {
            get
            {
                return base.GetObject<PreviousImageRetentionPolicyPerFeature>(Constants.Properties.AllVersionsAndDeletes);
            }
            set
            {
                base.SetObject(Constants.Properties.AllVersionsAndDeletes, value);
            }
        }

        /// <summary>
        /// Performs a deep copy of the supported features.
        /// </summary>
        /// <returns>
        /// A clone of the supported features.
        /// </returns>
        public object Clone()
        {
            PreviousImageRetentionPolicySupportedFeatures cloned = new PreviousImageRetentionPolicySupportedFeatures();

            if (this.GlobalSecondaryIndex != null)
            {
                cloned.GlobalSecondaryIndex = (PreviousImageRetentionPolicyPerFeature)this.GlobalSecondaryIndex.Clone();
            }

            if (this.ContainerCopy != null)
            {
                cloned.ContainerCopy = (PreviousImageRetentionPolicyPerFeature)this.ContainerCopy.Clone();
            }

            if (this.AllVersionsAndDeletes != null)
            {
                cloned.AllVersionsAndDeletes = (PreviousImageRetentionPolicyPerFeature)this.AllVersionsAndDeletes.Clone();
            }

            return cloned;
        }
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
        /// This field is deprecated and maintained for backward compatibility only. Use SupportedFeatures.AllVersionsAndDeletes.Mode instead.
        /// </summary>
        /// <value>
        /// One of the values of the <see cref="T:Microsoft.Azure.Documents.PreviousImageRetentionMode"/> enumeration, or null if not set.
        /// </value>
        [Obsolete("This field is deprecated. Use SupportedFeatures.AllVersionsAndDeletes.Mode instead.")]
        [JsonProperty(PropertyName = Constants.Properties.PreviousImageRetentionPolicyMode, NullValueHandling = NullValueHandling.Ignore)]
        public PreviousImageRetentionMode? Mode
        {
            get
            {
                return base.GetEnumValue<PreviousImageRetentionMode>(Constants.Properties.PreviousImageRetentionPolicyMode);
            }
            set
            {
                if (value.HasValue)
                {
                    base.SetValue(Constants.Properties.PreviousImageRetentionPolicyMode, (int)value.Value);
                }
                else
                {
                    base.SetValue(Constants.Properties.PreviousImageRetentionPolicyMode, null);
                }
            }
        }

        /// <summary>
        /// Gets or sets the supported features for previous image retention policy.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.SupportedFeatures, NullValueHandling = NullValueHandling.Ignore)]
        public PreviousImageRetentionPolicySupportedFeatures SupportedFeatures
        {
            get
            {
                return base.GetObject<PreviousImageRetentionPolicySupportedFeatures>(Constants.Properties.SupportedFeatures);
            }
            set
            {
                base.SetObject(Constants.Properties.SupportedFeatures, value);
            }
        }

        /// <summary>
        /// Performs a deep copy of the previous image retention policy.
        /// </summary>
        /// <returns>
        /// A clone of the previous image retention policy.
        /// </returns>
        public object Clone()
        {
#pragma warning disable CS0618 // Type or member is obsolete
            PreviousImageRetentionPolicy cloned = new PreviousImageRetentionPolicy()
            {
                Mode = this.Mode,
            };
#pragma warning restore CS0618 // Type or member is obsolete

            if (this.SupportedFeatures != null)
            {
                cloned.SupportedFeatures = (PreviousImageRetentionPolicySupportedFeatures)this.SupportedFeatures.Clone();
            }

            return cloned;
        }
    }
}
