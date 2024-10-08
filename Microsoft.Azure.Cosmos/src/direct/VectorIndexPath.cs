//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    /// <summary>
    /// DOM for a vector index path. A vector index path is used in a vector index.
    /// </summary>
    /// <example>
    /// <![CDATA[
    /// "indexingPolicy": {
    ///     "includedPaths": [
    ///         {
    ///             "path": "/*"
    ///         }
    ///     ],
    ///     "excludedPaths": [
    ///         {
    ///             "path": "/embeddings/vector/*"
    ///         }
    ///     ],
    ///     "vectorIndexes": [
    ///         {
    ///             "path": "/vector1",
    ///             "type": "flat"
    ///         },
    ///         {
    ///             "path": "/vector2",
    ///             "type": "flat"
    ///         },
    ///         {
    ///             "path": "/embeddings/vector",
    ///             "type": "flat"
    ///         }
    ///     ]
    /// }
    /// ]]>
    /// </example>
    internal sealed class VectorIndexPath : JsonSerializable, ICloneable
    {

        /// <summary>
        /// Gets or sets the full path in a document used for vector indexing.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.Path)]
        public string Path
        {
            get
            {
                return base.GetValue<string>(Constants.Properties.Path);
            }
            set
            {
                base.SetValue(Constants.Properties.Path, value);
            }
        }

        /// <summary>
        /// Gets or sets the <see cref="VectorIndexType"/> for the vector index path.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.Type)]
        public VectorIndexType Type
        {
            get
            {
                VectorIndexType result = default(VectorIndexType);
                string strValue = base.GetValue<string>(Constants.Properties.Type);
                if (!string.IsNullOrEmpty(strValue))
                {
                    result = (VectorIndexType)Enum.Parse(typeof(VectorIndexType), strValue, true);
                }
                return result;
            }
            set
            {
                base.SetValue(Constants.Properties.Type, value);
            }
        }

        public object Clone()
        {
            VectorIndexPath clonedVectorIndexPath = new VectorIndexPath();
            clonedVectorIndexPath.Path = this.Path;
            clonedVectorIndexPath.Type = this.Type;
            return clonedVectorIndexPath;
        }

        internal override void OnSave()
        {
            base.SetValue(Constants.Properties.Path, this.Path);
            base.SetValue(Constants.Properties.Type, this.Type);
        }
    }
}