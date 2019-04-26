//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Documents;

    internal sealed class IndexingPolicyBuilder
    {
        [JsonProperty(PropertyName = Constants.Properties.IncludedPaths)]
        private readonly HashSet<IncludedPath> includedPaths;

        [JsonProperty(PropertyName = Constants.Properties.ExcludedPaths)]
        private readonly HashSet<ExcludedPath> excludedPaths;

        [JsonProperty(PropertyName = Constants.Properties.CompositeIndexes)]
        private readonly HashSet<HashSet<CompositePath>> compositeIndexes;

        [JsonProperty(PropertyName = Constants.Properties.SpatialIndexes)]
        private readonly HashSet<SpatialIndex> spatialIndexes;

        [JsonProperty(PropertyName = Constants.Properties.Automatic)]
        public bool Automatic { get; set; }

        [JsonProperty(PropertyName = Constants.Properties.IndexingMode)]
        [JsonConverter(typeof(StringEnumConverter))]
        public IndexingMode IndexingMode { get; set; }

        private static readonly Collection<Index> DefaultIndexes = new Collection<Index>()
        {
            Index.Range(DataType.String, -1),
            Index.Range(DataType.Number, -1)
        };

        public IndexingPolicyBuilder()
        {
            this.includedPaths = new HashSet<IncludedPath>(IndexingPolicy.IncludedPathEqualityComparer.Singleton);
            this.excludedPaths = new HashSet<ExcludedPath>(IndexingPolicy.ExcludedPathEqualityComparer.Singleton);
            this.compositeIndexes = new HashSet<HashSet<CompositePath>>();
            this.spatialIndexes = new HashSet<SpatialIndex>(SpatialIndex.SpatialIndexEqualityComparer.Singleton);
            this.Automatic = true;
            this.IndexingMode = IndexingMode.Consistent;
        }

        public void AddIncludedPath(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException($"{nameof(path)} must not be null.");
            }

            IncludedPath includedPath = new IncludedPath()
            {
                Path = path,
                Indexes = DefaultIndexes
            };

            this.includedPaths.Add(includedPath);
        }

        public void AddExcludedPath(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException($"{nameof(path)} must not be null.");
            }

            ExcludedPath excludedPath = new ExcludedPath()
            {
                Path = path
            };

            this.excludedPaths.Add(excludedPath);
        }

        public void AddCompositeIndex(params CompositePath[] compositePaths)
        {
            if (compositePaths == null)
            {
                throw new ArgumentNullException($"{nameof(compositePaths)} must not be null.");
            }

            HashSet<CompositePath> compositeIndex = new HashSet<CompositePath>(CompositePath.CompositePathEqualityComparer.Singleton);
            foreach (CompositePath compositePath in compositePaths)
            {
                if (compositePath == null)
                {
                    throw new ArgumentException($"{nameof(compositePaths)} must not have null elements.");
                }

                compositeIndex.Add(compositePath);
            }

            this.compositeIndexes.Add(compositeIndex);
        }

        public void AddSpatialIndex(SpatialIndex spatialIndex)
        {
            if (spatialIndex == null)
            {
                throw new ArgumentNullException($"{nameof(spatialIndex)} must not be null.");
            }

            this.spatialIndexes.Add(spatialIndex);
        }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }

        public sealed class CompositePath
        {
            [JsonProperty(PropertyName = Constants.Properties.Path)]
            public string Path { get; }

            [JsonProperty(PropertyName = Constants.Properties.Order)]
            [JsonConverter(typeof(StringEnumConverter))]
            public CompositePathSortOrder CompositePathSortOrder { get; }

            public CompositePath(
                string path,
                CompositePathSortOrder compositePathSortOrder = CompositePathSortOrder.Ascending)
            {
                if (path == null)
                {
                    throw new ArgumentNullException($"{nameof(path)} must not be null.");
                }

                this.Path = path;
                this.CompositePathSortOrder = compositePathSortOrder;
            }

            public sealed class CompositePathEqualityComparer : IEqualityComparer<CompositePath>
            {
                public static readonly CompositePathEqualityComparer Singleton = new CompositePathEqualityComparer();

                public bool Equals(CompositePath compositePath1, CompositePath compositePath2)
                {
                    if (object.ReferenceEquals(compositePath1, compositePath2))
                    {
                        return true;
                    }

                    if (compositePath1 == null || compositePath2 == null)
                    {
                        return false;
                    }

                    if (compositePath1.Path == compositePath2.Path && compositePath2.CompositePathSortOrder == compositePath2.CompositePathSortOrder)
                    {
                        return true;
                    }

                    return false;
                }

                public int GetHashCode(CompositePath compositePath)
                {
                    if (compositePath == null)
                    {
                        return 0;
                    }

                    return compositePath.Path.GetHashCode() ^ compositePath.CompositePathSortOrder.GetHashCode();
                }
            }
        }

        private sealed class CompositePathsEqualityComparer : IEqualityComparer<HashSet<CompositePath>>
        {
            public static readonly CompositePathsEqualityComparer Singleton = new CompositePathsEqualityComparer();
            private static readonly CompositePath.CompositePathEqualityComparer compositePathEqualityComparer = new CompositePath.CompositePathEqualityComparer();
            public bool Equals(HashSet<CompositePath> compositePaths1, HashSet<CompositePath> compositePaths2)
            {
                if (Object.ReferenceEquals(compositePaths1, compositePaths2))
                {
                    return true;
                }

                if (compositePaths1 == null || compositePaths2 == null)
                {
                    return false;
                }

                return compositePaths1.SetEquals(compositePaths2);
            }

            public int GetHashCode(HashSet<CompositePath> obj)
            {
                if (obj == null)
                {
                    return 0;
                }

                int hashCode = 0;
                foreach (CompositePath compositePath in obj)
                {
                    hashCode ^= compositePathEqualityComparer.GetHashCode(compositePath);
                }

                return hashCode;
            }
        }

        internal sealed class SpatialIndex
        {
            [JsonProperty(PropertyName = Constants.Properties.Path)]
            public string Path { get; }

            [JsonProperty(PropertyName = Constants.Properties.Types, ItemConverterType = typeof(StringEnumConverter))]
            public HashSet<SpatialType> SpatialTypes { get; }

            public SpatialIndex(string path, params SpatialType[] spatialTypes)
            {
                if (path == null)
                {
                    throw new ArgumentNullException($"{nameof(path)} must not be null.");
                }

                this.Path = path;
                this.SpatialTypes = new HashSet<SpatialType>();

                foreach (SpatialType spatialType in spatialTypes)
                {
                    this.SpatialTypes.Add(spatialType);
                }
            }

            public sealed class SpatialIndexEqualityComparer : IEqualityComparer<SpatialIndex>
            {
                public static readonly SpatialIndexEqualityComparer Singleton = new SpatialIndexEqualityComparer();

                public bool Equals(SpatialIndex x, SpatialIndex y)
                {
                    if (object.ReferenceEquals(x, y))
                    {
                        return true;
                    }

                    if (x == null || y == null)
                    {
                        return false;
                    }

                    bool equals = true;
                    equals &= x.Path.Equals(y.Path);
                    equals &= x.SpatialTypes.SetEquals(y.SpatialTypes);

                    return equals;
                }

                public int GetHashCode(SpatialIndex obj)
                {
                    int hashCode = 0;
                    hashCode ^= obj.Path.GetHashCode();
                    foreach (SpatialType spatialType in obj.SpatialTypes)
                    {
                        hashCode ^= obj.Path.GetHashCode();
                    }

                    return hashCode;
                }
            }
        }
    }
}
