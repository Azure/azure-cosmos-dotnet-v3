//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Property-mapping fidelity tests for <see cref="ReadManyRequestOptions"/>'s
    /// two internal conversion helpers:
    ///
    ///   * <c>ConvertToQueryRequestOptions</c> — the legacy multi-id query path
    ///     mapper. Copies 8 properties.
    ///   * <c>ConvertToItemRequestOptions</c> — the PR #5905 single-physical-partition
    ///     point-read fast path mapper. Copies 6 properties; deliberately omits
    ///     <c>IfMatchEtag</c> / <c>IfNoneMatchEtag</c> because per-item ETags have no
    ///     coherent meaning at the ReadMany level (one ETag value cannot apply across
    ///     N (id, partitionKey) tuples). The legacy query path silently ignores those
    ///     headers on the wire today, so mirroring that silent-ignore here keeps
    ///     caller-observable behavior identical between the two execution branches.
    ///
    /// These tests exist because the failure mode of an unmirrored property addition
    /// is silent degradation (e.g., a new consistency-related property dropped on
    /// the point-read path would weaken effective consistency) rather than a crash,
    /// which is the hardest kind of regression to catch in integration tests.
    /// </summary>
    [TestClass]
    public class ReadManyRequestOptionsTests
    {
        // ---------------------------------------------------------------------
        //  Shared fixture: a fully-populated ReadManyRequestOptions with every
        //  inherited and ReadMany-specific property set to a distinguishable,
        //  non-default value. Reused across the mapper tests so each test does
        //  not have to re-state the contract for every property.
        // ---------------------------------------------------------------------

        private static readonly IReadOnlyDictionary<string, object> ExpectedProperties
            = new Dictionary<string, object> { { "key", "value" } };

        private static readonly Action<Headers> ExpectedAddRequestHeaders
            = headers => headers.Add("custom-header", "custom-value");

        private static readonly List<string> ExpectedExcludeRegions
            = new List<string> { "East US", "West Europe" };

        private static ReadManyRequestOptions CreateFullyPopulatedOptions()
        {
            return new ReadManyRequestOptions
            {
                ConsistencyLevel = Cosmos.ConsistencyLevel.BoundedStaleness,
                ReadConsistencyStrategy = Cosmos.ReadConsistencyStrategy.GlobalStrong,
                SessionToken = "0:1#42",
                IfMatchEtag = "if-match-distinct-value",
                IfNoneMatchEtag = "if-none-match-distinct-value",
                Properties = ExpectedProperties,
                AddRequestHeaders = ExpectedAddRequestHeaders,
                ExcludeRegions = ExpectedExcludeRegions
            };
        }

        // ---------------------------------------------------------------------
        //  Explicit per-property assertions
        // ---------------------------------------------------------------------

        [TestMethod]
        public void ConvertToItemRequestOptions_CopiesAllMappedPropertiesWithDistinguishableValues()
        {
            ReadManyRequestOptions source = CreateFullyPopulatedOptions();

            ItemRequestOptions mapped = source.ConvertToItemRequestOptions();

            Assert.IsNotNull(mapped, "ConvertToItemRequestOptions must always return a non-null ItemRequestOptions.");

            Assert.AreEqual(Cosmos.ConsistencyLevel.BoundedStaleness, mapped.ConsistencyLevel,
                "ConsistencyLevel must transfer.");
            Assert.AreEqual(Cosmos.ReadConsistencyStrategy.GlobalStrong, mapped.ReadConsistencyStrategy,
                "ReadConsistencyStrategy must transfer (regression guard for PR #5685's addition).");
            Assert.AreEqual("0:1#42", mapped.SessionToken,
                "SessionToken must transfer.");
            Assert.AreSame(ExpectedProperties, mapped.Properties,
                "Properties dictionary must transfer by reference; the mapper has no obligation to deep-copy.");
            Assert.AreSame(ExpectedAddRequestHeaders, mapped.AddRequestHeaders,
                "AddRequestHeaders delegate must transfer by reference.");
            CollectionAssert.AreEqual(ExpectedExcludeRegions, mapped.ExcludeRegions,
                "ExcludeRegions must transfer with identical contents.");
        }

        [TestMethod]
        public void ConvertToItemRequestOptions_DoesNotCopyETagHeaders_PolicyAssertion()
        {
            // Encodes the PR #5905 design decision (commit 9a92800ac): the new mapper
            // deliberately diverges from ConvertToQueryRequestOptions by NOT copying
            // IfMatchEtag / IfNoneMatchEtag. See the rationale block on
            // ConvertToItemRequestOptions in ReadManyRequestOptions.cs. If a future change
            // re-introduces this mapping, this test will fail and force the author to
            // revisit the design discussion on PR #5905.
            ReadManyRequestOptions source = CreateFullyPopulatedOptions();

            ItemRequestOptions mapped = source.ConvertToItemRequestOptions();

            Assert.IsNull(mapped.IfMatchEtag,
                "ConvertToItemRequestOptions must NOT copy IfMatchEtag. Per-item ETags have no coherent meaning at the ReadMany level. See the design discussion on PR #5905.");
            Assert.IsNull(mapped.IfNoneMatchEtag,
                "ConvertToItemRequestOptions must NOT copy IfNoneMatchEtag. Per-item ETags have no coherent meaning at the ReadMany level. See the design discussion on PR #5905.");
        }

        [TestMethod]
        public void ConvertToQueryRequestOptions_CopiesAllMappedPropertiesWithDistinguishableValues()
        {
            // Symmetric coverage requested by sdkReviewAgent on PR #5905 (comment 3382401890).
            // Although ConvertToQueryRequestOptions is pre-existing code, exercising it here
            // makes accidental property drops on either mapper symmetrically detectable.
            ReadManyRequestOptions source = CreateFullyPopulatedOptions();

            QueryRequestOptions mapped = source.ConvertToQueryRequestOptions();

            Assert.IsNotNull(mapped, "ConvertToQueryRequestOptions must always return a non-null QueryRequestOptions.");

            Assert.AreEqual(Cosmos.ConsistencyLevel.BoundedStaleness, mapped.ConsistencyLevel,
                "ConsistencyLevel must transfer.");
            Assert.AreEqual(Cosmos.ReadConsistencyStrategy.GlobalStrong, mapped.ReadConsistencyStrategy,
                "ReadConsistencyStrategy must transfer.");
            Assert.AreEqual("0:1#42", mapped.SessionToken,
                "SessionToken must transfer.");
            Assert.AreEqual("if-match-distinct-value", mapped.IfMatchEtag,
                "IfMatchEtag must transfer on the query mapper (pre-existing behavior; the wire silently ignores it on multi-id queries).");
            Assert.AreEqual("if-none-match-distinct-value", mapped.IfNoneMatchEtag,
                "IfNoneMatchEtag must transfer on the query mapper (pre-existing behavior).");
            Assert.AreSame(ExpectedProperties, mapped.Properties,
                "Properties dictionary must transfer by reference.");
            Assert.AreSame(ExpectedAddRequestHeaders, mapped.AddRequestHeaders,
                "AddRequestHeaders delegate must transfer by reference.");
            CollectionAssert.AreEqual(ExpectedExcludeRegions, mapped.ExcludeRegions,
                "ExcludeRegions must transfer with identical contents.");
        }

        // ---------------------------------------------------------------------
        //  Reflection-based completeness guard
        //
        //  Any new public-settable property added to ReadManyRequestOptions (or
        //  inherited from RequestOptions) must be deliberately classified into
        //  exactly one of these three buckets. Anything unclassified will fail
        //  this test and force the author to revisit both mappers.
        // ---------------------------------------------------------------------

        // Properties copied by BOTH ConvertToItemRequestOptions and
        // ConvertToQueryRequestOptions. The expected baseline.
        private static readonly HashSet<string> MappedOnBothMappers = new HashSet<string>(StringComparer.Ordinal)
        {
            nameof(ReadManyRequestOptions.ConsistencyLevel),
            nameof(ReadManyRequestOptions.SessionToken),
            nameof(RequestOptions.Properties),
            nameof(RequestOptions.AddRequestHeaders),
            nameof(RequestOptions.ExcludeRegions),
        };

        // Properties copied by ConvertToQueryRequestOptions but DELIBERATELY NOT
        // by ConvertToItemRequestOptions. See ReadManyRequestOptions.cs for the
        // rationale comment. This bucket is the codified output of the
        // PR #5905 design discussion (comment 3382347442).
        private static readonly HashSet<string> MappedOnQueryOnly = new HashSet<string>(StringComparer.Ordinal)
        {
            nameof(RequestOptions.IfMatchEtag),
            nameof(RequestOptions.IfNoneMatchEtag),
        };

        // Pre-existing gap: inherited RequestOptions properties that NEITHER mapper
        // copies. Documented here as a deliberate "known-unmapped" allowlist so that
        // (a) adding a new property to RequestOptions trips this test, forcing a
        // classification decision, and (b) future engineers see the gap explicitly
        // rather than discovering it through silent degradation. Mapping each of
        // these is a separate scope from PR #5905.
        private static readonly HashSet<string> NotMappedByEither_PreExistingGap = new HashSet<string>(StringComparer.Ordinal)
        {
            nameof(RequestOptions.PriorityLevel),
            nameof(RequestOptions.CosmosThresholdOptions),
            nameof(RequestOptions.AvailabilityStrategy),
        };

        [TestMethod]
        public void EveryPublicSettableProperty_OnReadManyRequestOptions_IsClassified()
        {
            IEnumerable<string> settableProperties = typeof(ReadManyRequestOptions)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0)
                .Select(p => p.Name);

            List<string> unclassified = new List<string>();
            foreach (string propertyName in settableProperties)
            {
                bool classified = MappedOnBothMappers.Contains(propertyName)
                                 || MappedOnQueryOnly.Contains(propertyName)
                                 || NotMappedByEither_PreExistingGap.Contains(propertyName);

                if (!classified)
                {
                    unclassified.Add(propertyName);
                }
            }

            Assert.AreEqual(
                0,
                unclassified.Count,
                "Every public-settable property on ReadManyRequestOptions must be deliberately classified " +
                "into MappedOnBothMappers, MappedOnQueryOnly, or NotMappedByEither_PreExistingGap. " +
                "Unclassified properties (likely newly added): " + string.Join(", ", unclassified) + ". " +
                "Update ReadManyRequestOptionsTests + the relevant mapper(s).");
        }

        [TestMethod]
        public void ClassifiedProperties_MustExist_OnReadManyRequestOptions_OrInheritedFromRequestOptions()
        {
            // Guard the other direction: if a classified property is renamed or removed
            // from ReadManyRequestOptions / RequestOptions, the allowlists become stale
            // silently. Catch that here.
            IEnumerable<string> liveProperties = typeof(ReadManyRequestOptions)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0)
                .Select(p => p.Name)
                .ToHashSet(StringComparer.Ordinal);

            List<string> stale = new List<string>();
            foreach (string classified in MappedOnBothMappers.Concat(MappedOnQueryOnly).Concat(NotMappedByEither_PreExistingGap))
            {
                if (!liveProperties.Contains(classified))
                {
                    stale.Add(classified);
                }
            }

            Assert.AreEqual(
                0,
                stale.Count,
                "Some classified properties no longer exist on ReadManyRequestOptions (renamed or removed): " +
                string.Join(", ", stale) + ". Update the allowlists in ReadManyRequestOptionsTests.");
        }
    }
}
