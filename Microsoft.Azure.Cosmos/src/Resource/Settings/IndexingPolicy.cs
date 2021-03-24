//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    /// <summary>
    /// Represents the indexing policy configuration for a collection in the Azure Cosmos DB service.
    /// </summary> 
    /// <remarks>
    /// Indexing policies can used to configure which properties (JSON paths) are included/excluded, whether the index is updated consistently
    /// or offline (lazy), automatic vs. opt-in per-document, as well as the precision and type of index per path.
    /// <para>
    /// Refer to https://docs.microsoft.com/azure/cosmos-db/index-policy for additional information on how to specify
    /// indexing policies.
    /// </para>
    /// </remarks>
    /// <seealso cref="ContainerProperties"/>
    public sealed class IndexingPolicy
    {
        internal const string DefaultPath = "/*";

        /// <summary>
        /// Initializes a new instance of the <see cref="IndexingPolicy"/> class for the Azure Cosmos DB service.
        /// </summary>
        /// <remarks>
        /// Indexing mode is set to consistent.
        /// </remarks>
        public IndexingPolicy()
        {
            this.Automatic = true;
            this.IndexingMode = IndexingMode.Consistent;
        }

        /// <summary>
        /// Gets or sets a value that indicates whether automatic indexing is enabled for a collection in the Azure Cosmos DB service.
        /// </summary>
        /// <remarks>
        /// In automatic indexing, documents can be explicitly excluded from indexing using <see cref="T:Microsoft.Azure.Documents.Client.RequestOptions"/>.  
        /// In manual indexing, documents can be explicitly included.
        /// </remarks>
        /// <value>
        /// True, if automatic indexing is enabled; otherwise, false.
        /// </value>
        [JsonProperty(PropertyName = Constants.Properties.Automatic)]
        public bool Automatic { get; set; }

        /// <summary>
        /// Gets or sets the indexing mode (consistent or lazy) in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// One of the values of the <see cref="T:Microsoft.Azure.Documents.IndexingMode"/> enumeration.
        /// </value>
        [JsonProperty(PropertyName = Constants.Properties.IndexingMode)]
        [JsonConverter(typeof(StringEnumConverter))]
        public IndexingMode IndexingMode { get; set; }

        /// <summary>
        /// Gets the collection containing <see cref="IncludedPath"/> objects in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The collection containing <see cref="IncludedPath"/> objects.
        /// </value>
        [JsonProperty(PropertyName = Constants.Properties.IncludedPaths)]
        public Collection<IncludedPath> IncludedPaths { get; internal set; } = new Collection<IncludedPath>();

        /// <summary>
        /// Gets the collection containing <see cref="ExcludedPath"/> objects in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The collection containing <see cref="ExcludedPath"/> objects.
        /// </value>
        [JsonProperty(PropertyName = Constants.Properties.ExcludedPaths)]
        public Collection<ExcludedPath> ExcludedPaths { get; internal set; } = new Collection<ExcludedPath>();

        /// <summary>
        /// Gets the composite indexes for additional indexes
        /// </summary>
        /// <example>
        /// <![CDATA[
        ///   "composite": [
        ///      [
        ///         {
        ///            "path": "/joining_year",
        ///            "order": "ascending"
        ///         },
        ///         {
        ///            "path": "/level",
        ///            "order": "descending"
        ///         }
        ///      ],
        ///      [
        ///         {
        ///            "path": "/country"
        ///         },
        ///         {
        ///            "path": "/city"
        ///         }
        ///      ]
        ///   ]
        /// ]]>
        /// </example>
        [JsonProperty(PropertyName = Constants.Properties.CompositeIndexes)]
        public Collection<Collection<CompositePath>> CompositeIndexes { get; internal set; } = new Collection<Collection<CompositePath>>();

        /// <summary>
        /// Collection of spatial index definitions to be used
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.SpatialIndexes)]
        public Collection<SpatialPath> SpatialIndexes { get; internal set; } = new Collection<SpatialPath>();

#if INTERNAL
        /// <summary>
        /// Indexing policy annotation.
        /// </summary>
        [JsonProperty(PropertyName = "annotation", NullValueHandling = NullValueHandling.Ignore)]
        public string Annotation { get; set; }
#endif

        #region EqualityComparers
        internal sealed class CompositePathEqualityComparer : IEqualityComparer<CompositePath>
        {
            public static readonly CompositePathEqualityComparer Singleton = new CompositePathEqualityComparer();

            public bool Equals(CompositePath compositePath1, CompositePath compositePath2)
            {
                if (Object.ReferenceEquals(compositePath1, compositePath2))
                {
                    return true;
                }

                if (compositePath1 == null || compositePath2 == null)
                {
                    return false;
                }

                if (compositePath1.Path == compositePath2.Path && compositePath2.Order == compositePath2.Order)
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

                return compositePath.Path.GetHashCode() ^ compositePath.Order.GetHashCode();
            }
        }

        internal sealed class CompositePathsEqualityComparer : IEqualityComparer<HashSet<CompositePath>>
        {
            public static readonly CompositePathsEqualityComparer Singleton = new CompositePathsEqualityComparer();
            private static readonly CompositePathEqualityComparer compositePathEqualityComparer = new CompositePathEqualityComparer();
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

        private sealed class CompositeIndexesEqualityComparer : IEqualityComparer<Collection<Collection<CompositePath>>>
        {
            private static readonly CompositePathEqualityComparer compositePathEqualityComparer = new CompositePathEqualityComparer();
            private static readonly CompositePathsEqualityComparer compositePathsEqualityComparer = new CompositePathsEqualityComparer();

            public bool Equals(Collection<Collection<CompositePath>> compositeIndexes1, Collection<Collection<CompositePath>> compositeIndexes2)
            {
                if (Object.ReferenceEquals(compositeIndexes1, compositeIndexes2))
                {
                    return true;
                }

                if (compositeIndexes1 == null || compositeIndexes2 == null)
                {
                    return false;
                }

                HashSet<HashSet<CompositePath>> hashedCompositeIndexes1 = new HashSet<HashSet<CompositePath>>(compositePathsEqualityComparer);
                HashSet<HashSet<CompositePath>> hashedCompositeIndexes2 = new HashSet<HashSet<CompositePath>>(compositePathsEqualityComparer);

                foreach (Collection<CompositePath> compositePaths in compositeIndexes1)
                {
                    HashSet<CompositePath> hashedCompositePaths = new HashSet<CompositePath>(compositePaths, compositePathEqualityComparer);
                    hashedCompositeIndexes1.Add(hashedCompositePaths);
                }

                foreach (Collection<CompositePath> compositePaths in compositeIndexes2)
                {
                    HashSet<CompositePath> hashedCompositePaths = new HashSet<CompositePath>(compositePaths, compositePathEqualityComparer);
                    hashedCompositeIndexes2.Add(hashedCompositePaths);
                }

                return hashedCompositeIndexes1.SetEquals(hashedCompositeIndexes2);
            }

            public int GetHashCode(Collection<Collection<CompositePath>> compositeIndexes)
            {
                int hashCode = 0;
                foreach (Collection<CompositePath> compositePaths in compositeIndexes)
                {
                    HashSet<CompositePath> hashedCompositePaths = new HashSet<CompositePath>(compositePaths, compositePathEqualityComparer);
                    hashCode = hashCode ^ compositePathsEqualityComparer.GetHashCode(hashedCompositePaths);
                }

                return hashCode;
            }
        }

        internal sealed class SpatialSpecEqualityComparer : IEqualityComparer<SpatialPath>
        {
            public static readonly SpatialSpecEqualityComparer Singleton = new SpatialSpecEqualityComparer();

            public bool Equals(SpatialPath spatialSpec1, SpatialPath spatialSpec2)
            {
                if (object.ReferenceEquals(spatialSpec1, spatialSpec2))
                {
                    return true;
                }

                if (spatialSpec1 == null || spatialSpec2 == null)
                {
                    return false;
                }

                if (spatialSpec1.Path != spatialSpec2.Path)
                {
                    return false;
                }

                HashSet<SpatialType> hashedSpatialTypes1 = new HashSet<SpatialType>(spatialSpec1.SpatialTypes);
                HashSet<SpatialType> hashedSpatialTypes2 = new HashSet<SpatialType>(spatialSpec2.SpatialTypes);

                if (!hashedSpatialTypes1.SetEquals(hashedSpatialTypes2))
                {
                    return false;
                }

                return true;
            }

            public int GetHashCode(SpatialPath spatialSpec)
            {
                int hashCode = 0;
                hashCode ^= spatialSpec.Path.GetHashCode();
                foreach (SpatialType spatialType in spatialSpec.SpatialTypes)
                {
                    hashCode ^= spatialType.GetHashCode();
                }

                return hashCode;
            }
        }

        internal sealed class AdditionalSpatialIndexesEqualityComparer : IEqualityComparer<Collection<SpatialPath>>
        {
            private static readonly SpatialSpecEqualityComparer spatialSpecEqualityComparer = new SpatialSpecEqualityComparer();

            public bool Equals(Collection<SpatialPath> additionalSpatialIndexes1, Collection<SpatialPath> additionalSpatialIndexes2)
            {
                if (object.ReferenceEquals(additionalSpatialIndexes1, additionalSpatialIndexes2))
                {
                    return true;
                }

                if (additionalSpatialIndexes1 == null || additionalSpatialIndexes2 == null)
                {
                    return false;
                }

                HashSet<SpatialPath> hashedAdditionalSpatialIndexes1 = new HashSet<SpatialPath>(additionalSpatialIndexes1, spatialSpecEqualityComparer);
                HashSet<SpatialPath> hashedAdditionalSpatialIndexes2 = new HashSet<SpatialPath>(additionalSpatialIndexes2, spatialSpecEqualityComparer);

                return hashedAdditionalSpatialIndexes1.SetEquals(additionalSpatialIndexes2);
            }

            public int GetHashCode(Collection<SpatialPath> additionalSpatialIndexes)
            {
                int hashCode = 0;
                foreach (SpatialPath spatialSpec in additionalSpatialIndexes)
                {
                    hashCode = hashCode ^ spatialSpecEqualityComparer.GetHashCode(spatialSpec);
                }

                return hashCode;
            }
        }

        internal sealed class IndexEqualityComparer : IEqualityComparer<Index>
        {
            public static readonly IndexEqualityComparer Comparer = new IndexEqualityComparer();

            public bool Equals(Index index1, Index index2)
            {
                if (object.ReferenceEquals(index1, index2))
                {
                    return true;
                }

                if (index1 == null || index2 == null)
                {
                    return false;
                }

                if (index1.Kind != index2.Kind)
                {
                    return false;
                }

                switch (index1.Kind)
                {
                    case IndexKind.Hash:
                        if (((HashIndex)index1).Precision != ((HashIndex)index2).Precision)
                        {
                            return false;
                        }

                        if (((HashIndex)index1).DataType != ((HashIndex)index2).DataType)
                        {
                            return false;
                        }
                        break;

                    case IndexKind.Range:
                        if (((RangeIndex)index1).Precision != ((RangeIndex)index2).Precision)
                        {
                            return false;
                        }

                        if (((RangeIndex)index1).DataType != ((RangeIndex)index2).DataType)
                        {
                            return false;
                        }
                        break;

                    case IndexKind.Spatial:
                        if (((SpatialIndex)index1).DataType != ((SpatialIndex)index2).DataType)
                        {
                            return false;
                        }
                        break;

                    default:
                        throw new ArgumentException($"Unexpected Kind: {index1.Kind}");
                }

                return true;
            }

            public int GetHashCode(Index index)
            {
                int hashCode = 0;
                hashCode = hashCode ^ (int)index.Kind;

                switch (index.Kind)
                {
                    case IndexKind.Hash:
                        hashCode = hashCode ^ ((HashIndex)index).Precision ?? 0;
                        hashCode = hashCode ^ ((HashIndex)index).DataType.GetHashCode();
                        break;

                    case IndexKind.Range:
                        hashCode = hashCode ^ ((RangeIndex)index).Precision ?? 0;
                        hashCode = hashCode ^ ((RangeIndex)index).DataType.GetHashCode();
                        break;

                    case IndexKind.Spatial:
                        hashCode = hashCode ^ ((SpatialIndex)index).DataType.GetHashCode();
                        break;

                    default:
                        throw new ArgumentException($"Unexpected Kind: {index.Kind}");
                }

                return hashCode;
            }
        }

        internal sealed class IncludedPathEqualityComparer : IEqualityComparer<IncludedPath>
        {
            public static readonly IncludedPathEqualityComparer Singleton = new IncludedPathEqualityComparer();
            private static readonly IndexEqualityComparer indexEqualityComparer = new IndexEqualityComparer();
            public bool Equals(IncludedPath includedPath1, IncludedPath includedPath2)
            {
                if (Object.ReferenceEquals(includedPath1, includedPath2))
                {
                    return true;
                }

                if (includedPath1 == null || includedPath2 == null)
                {
                    return false;
                }

                if (includedPath1.Path != includedPath2.Path)
                {
                    return false;
                }

                HashSet<Index> indexes1 = new HashSet<Index>(includedPath1.Indexes, indexEqualityComparer);
                HashSet<Index> indexes2 = new HashSet<Index>(includedPath2.Indexes, indexEqualityComparer);

                return indexes1.SetEquals(indexes2);
            }

            public int GetHashCode(IncludedPath includedPath)
            {
                int hashCode = 0;
                hashCode = hashCode ^ includedPath.Path.GetHashCode();
                foreach (Index index in includedPath.Indexes)
                {
                    hashCode = hashCode ^ indexEqualityComparer.GetHashCode(index);
                }

                return hashCode;
            }
        }

        internal sealed class ExcludedPathEqualityComparer : IEqualityComparer<ExcludedPath>
        {
            public static readonly ExcludedPathEqualityComparer Singleton = new ExcludedPathEqualityComparer();

            public bool Equals(ExcludedPath excludedPath1, ExcludedPath excludedPath2)
            {
                if (Object.ReferenceEquals(excludedPath1, excludedPath2))
                {
                    return true;
                }

                if (excludedPath1 == null || excludedPath2 == null)
                {
                    return false;
                }

                return (excludedPath1.Path == excludedPath2.Path);
            }

            public int GetHashCode(ExcludedPath excludedPath1)
            {
                return excludedPath1.Path.GetHashCode();
            }
        }

        internal sealed class IndexingPolicyEqualityComparer : IEqualityComparer<IndexingPolicy>
        {
            public static readonly IndexingPolicyEqualityComparer Singleton = new IndexingPolicyEqualityComparer();
            private static readonly IncludedPathEqualityComparer includedPathEqualityComparer = new IncludedPathEqualityComparer();
            private static readonly ExcludedPathEqualityComparer excludedPathEqualityComparer = new ExcludedPathEqualityComparer();
            private static readonly CompositeIndexesEqualityComparer compositeIndexesEqualityComparer = new CompositeIndexesEqualityComparer();
            private static readonly AdditionalSpatialIndexesEqualityComparer additionalSpatialIndexesEqualityComparer = new AdditionalSpatialIndexesEqualityComparer();

            public bool Equals(IndexingPolicy indexingPolicy1, IndexingPolicy indexingPolicy2)
            {
                if (Object.ReferenceEquals(indexingPolicy1, indexingPolicy2))
                {
                    return true;
                }

                bool isEqual = true;
                isEqual &= (indexingPolicy1 != null && indexingPolicy2 != null);
                isEqual &= (indexingPolicy1.Automatic == indexingPolicy2.Automatic);
                isEqual &= (indexingPolicy1.IndexingMode == indexingPolicy2.IndexingMode);
                isEqual &= compositeIndexesEqualityComparer.Equals(indexingPolicy1.CompositeIndexes, indexingPolicy2.CompositeIndexes);
                isEqual &= additionalSpatialIndexesEqualityComparer.Equals(indexingPolicy1.SpatialIndexes, indexingPolicy2.SpatialIndexes);

                HashSet<IncludedPath> includedPaths1 = new HashSet<IncludedPath>(indexingPolicy1.IncludedPaths, includedPathEqualityComparer);
                HashSet<IncludedPath> includedPaths2 = new HashSet<IncludedPath>(indexingPolicy2.IncludedPaths, includedPathEqualityComparer);
                isEqual &= includedPaths1.SetEquals(includedPaths2);

                HashSet<ExcludedPath> excludedPaths1 = new HashSet<ExcludedPath>(indexingPolicy1.ExcludedPaths, excludedPathEqualityComparer);
                HashSet<ExcludedPath> excludedPaths2 = new HashSet<ExcludedPath>(indexingPolicy2.ExcludedPaths, excludedPathEqualityComparer);
                isEqual &= excludedPaths1.SetEquals(excludedPaths2);

                return isEqual;
            }

            public int GetHashCode(IndexingPolicy indexingPolicy)
            {
                int hashCode = 0;
                hashCode = hashCode ^ indexingPolicy.Automatic.GetHashCode();
                hashCode = hashCode ^ indexingPolicy.IndexingMode.GetHashCode();
                hashCode = hashCode ^ compositeIndexesEqualityComparer.GetHashCode(indexingPolicy.CompositeIndexes);
                hashCode = hashCode ^ additionalSpatialIndexesEqualityComparer.GetHashCode(indexingPolicy.SpatialIndexes);

                foreach (IncludedPath includedPath in indexingPolicy.IncludedPaths)
                {
                    hashCode = hashCode ^ includedPathEqualityComparer.GetHashCode(includedPath);
                }

                foreach (ExcludedPath excludedPath in indexingPolicy.ExcludedPaths)
                {
                    hashCode = hashCode ^ excludedPathEqualityComparer.GetHashCode(excludedPath);
                }

                return hashCode;
            }
        }
        #endregion
    }
}
