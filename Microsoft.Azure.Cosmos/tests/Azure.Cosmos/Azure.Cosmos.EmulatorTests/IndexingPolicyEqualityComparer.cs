//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;

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

    internal sealed class CompositeIndexesEqualityComparer : IEqualityComparer<Collection<Collection<CompositePath>>>
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

    internal sealed class AdditionalSpatialSpecesEqualityComparer : IEqualityComparer<Collection<SpatialPath>>
    {
        private static readonly SpatialSpecEqualityComparer spatialSpecEqualityComparer = new SpatialSpecEqualityComparer();

        public bool Equals(Collection<SpatialPath> additionalSpatialSpeces1, Collection<SpatialPath> additionalSpatialSpeces2)
        {
            if (object.ReferenceEquals(additionalSpatialSpeces1, additionalSpatialSpeces2))
            {
                return true;
            }

            if (additionalSpatialSpeces1 == null || additionalSpatialSpeces2 == null)
            {
                return false;
            }

            HashSet<SpatialPath> hashedAdditionalSpatialSpeces1 = new HashSet<SpatialPath>(additionalSpatialSpeces1, spatialSpecEqualityComparer);
            HashSet<SpatialPath> hashedAdditionalSpatialSpeces2 = new HashSet<SpatialPath>(additionalSpatialSpeces2, spatialSpecEqualityComparer);

            return hashedAdditionalSpatialSpeces1.SetEquals(additionalSpatialSpeces2);
        }

        public int GetHashCode(Collection<SpatialPath> additionalSpatialSpeces)
        {
            int hashCode = 0;
            foreach (SpatialPath spatialSpec in additionalSpatialSpeces)
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
        private static readonly AdditionalSpatialSpecesEqualityComparer additionalSpatialSpecesEqualityComparer = new AdditionalSpatialSpecesEqualityComparer();

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
            isEqual &= additionalSpatialSpecesEqualityComparer.Equals(indexingPolicy1.SpatialIndexes, indexingPolicy2.SpatialIndexes);

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
            hashCode = hashCode ^ additionalSpatialSpecesEqualityComparer.GetHashCode(indexingPolicy.SpatialIndexes);

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
