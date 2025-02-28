//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    /// <summary>
    /// DOM for a full text index path. A full text index path is used in a full text index.
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
    ///             "path": "/path4"
    ///         }
    ///     ],
    ///     "fullTextIndexes": [
    ///         {
    ///             "path": "/path1"
    ///         },
    ///         {
    ///             "path": "/path2"
    ///         },
    ///         {
    ///             "path": "/path3/text1"
    ///         },
    ///         {
    ///             "path": "/path3/text2"
    ///         }
    ///     ]
    /// }
    /// ]]>
    /// </example>
    internal sealed class FullTextIndexPath : JsonSerializable, ICloneable
    {

        /// <summary>
        /// Gets or sets the full path in a document used for full text indexing.
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

        public object Clone()
        {
            FullTextIndexPath clonedFullTextIndexPath = new FullTextIndexPath();
            clonedFullTextIndexPath.Path = this.Path;
            return clonedFullTextIndexPath;
        }

        internal override void OnSave()
        {
            base.SetValue(Constants.Properties.Path, this.Path);
        }
    }
}