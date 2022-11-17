//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System.Collections.ObjectModel;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Represents a unique key on that enforces uniqueness constraint on documents in the collection in the Azure Cosmos DB service.
    /// </summary>
    /// <remarks>
    /// 1) For partitioned collections, the value of partition key is implicitly a part of each unique key.
    /// 2) Uniqueness constraint is also enforced for missing values.
    /// For instance, if unique key policy defines a unique key with single property path, there could be only one document that has missing value for this property.
    /// </remarks>
    /// <seealso cref="UniqueKeyPolicy"/>
#if COSMOSCLIENT
    internal
#else
    public
#endif
    sealed class UniqueKey : JsonSerializable
    {
        private Collection<string> paths;
        private JObject filter;
        /// <summary>
        /// Gets or sets the paths, a set of which must be unique for each document in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// <![CDATA[The paths to enforce uniqueness on. Each path is a rooted path of the unique property in the document, such as "/name/first".]]>
        /// </value>
        /// <example>
        /// <![CDATA[
        /// uniqueKey.Paths = new Collection<string> { "/name/first", "/name/last" };
        /// ]]>
        /// </example>
        [JsonProperty(PropertyName = Constants.Properties.Paths)]
        public Collection<string> Paths
        {
            get
            {
                if (this.paths == null)
                {
                    this.paths = base.GetValue<Collection<string>>(Constants.Properties.Paths);
                    if (this.paths == null)
                    {
                        this.paths = new Collection<string>();
                    }
                }

                return this.paths;
            }
            set
            {
                this.paths = value;
                base.SetValue(Constants.Properties.Paths, value);
            }
        }

        [JsonProperty(PropertyName = Constants.Properties.Filter, NullValueHandling = NullValueHandling.Ignore)]
        internal JObject Filter
        {
            get
            {
                this.filter = this.GetValue<JObject>(Constants.Properties.Filter);
                return this.filter;
            }
            set
            {
                this.filter = value;
                this.SetValue(Constants.Properties.Filter, value);
            }
        }

        internal override void Validate()
        {
            base.Validate();
            base.GetValue<Collection<string>>(Constants.Properties.Paths);
            base.GetValue<JObject>(Constants.Properties.Filter);
        }

        public override bool Equals(object obj)
        {
            if (!(obj is UniqueKey uniqueKey))
            {
                return false;
            }

            if (this.Paths.Count != uniqueKey.Paths.Count) return false;

            foreach (string path in uniqueKey.paths)
            {
                if (!this.Paths.Contains(path))
                {
                    return false;
                }
            }

            if (this.Filter == null && uniqueKey.Filter == null) return true;

            if (this.Filter != null && uniqueKey.Filter != null)
            {
                JTokenEqualityComparer comparer = new JTokenEqualityComparer();
                return comparer.Equals(this.Filter, uniqueKey.Filter);
            }

            return false;
        }

        public override int GetHashCode()
        {
            int hashCode = 0;
            foreach (string token in this.Paths)
            {
                hashCode = hashCode ^ token.GetHashCode();
            }

            if (this.Filter != null)
            {
                JTokenEqualityComparer comparer = new JTokenEqualityComparer();
                hashCode = hashCode ^ comparer.GetHashCode(this.Filter.GetHashCode());
            }

            return hashCode;
        }

        internal override void OnSave()
        {
            if (this.paths != null)
            {
                base.SetValue(Constants.Properties.Paths, this.paths);
            }
            if (this.filter != null)
            {
                base.SetValue(Constants.Properties.Filter, this.filter);
            }
        }
    }
}
