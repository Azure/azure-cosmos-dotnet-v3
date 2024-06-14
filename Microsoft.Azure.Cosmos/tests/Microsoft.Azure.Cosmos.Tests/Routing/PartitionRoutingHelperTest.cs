//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Routing
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Microsoft.Azure.Documents.Routing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using System.Collections.ObjectModel;
    using System.Net;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

    /// <summary>
    /// Tests for <see cref="PartitionRoutingHelper"/> class.
    /// </summary>
    [TestClass]
    public class PartitionRoutingHelperTest
    {
        private readonly PartitionRoutingHelper partitionRoutingHelper = new PartitionRoutingHelper();

        /// <summary>
        /// Tests for <see cref="PartitionRoutingHelper.ExtractPartitionKeyRangeFromContinuationToken"/> method.
        /// </summary>
        [TestMethod]
        [TestCategory(TestTypeCategory.Quarantine)]
        [Ignore] /* TODO: There is a TODO in PartitionRoutingHelper.ExtractPartitionKeyRangeFromContinuationToken that it's refering to some pending deployment */
        public void TestExtractPartitionKeyRangeFromHeaders()
        {
            Func<string, INameValueCollection> getHeadersWithContinuation = (string continuationToken) =>
            {
                INameValueCollection headers = new RequestNameValueCollection();
                headers[HttpConstants.HttpHeaders.Continuation] = continuationToken;
                return headers;
            };

            using (Stream stream = new MemoryStream(Properties.Resources.BaselineTest_PartitionRoutingHelper_ExtractPartitionKeyRangeFromHeaders))
            {
                using (StreamReader reader = new StreamReader(stream))
                {
                    TestSet<ExtractPartitionKeyRangeFromHeadersTestData> testSet = JsonConvert.DeserializeObject<TestSet<ExtractPartitionKeyRangeFromHeadersTestData>>(reader.ReadToEnd());

                    foreach (ExtractPartitionKeyRangeFromHeadersTestData testData in testSet.Postive)
                    {
                        INameValueCollection headers = getHeadersWithContinuation(testData.CompositeContinuationToken);
                        List<CompositeContinuationToken> suppliedTokens;
                        Range<string> range = this.partitionRoutingHelper.ExtractPartitionKeyRangeFromContinuationToken(headers, out suppliedTokens);

                        if (suppliedTokens != null)
                        {
                            Assert.AreEqual(testData.ContinuationToken, headers[HttpConstants.HttpHeaders.Continuation]);

                            Assert.AreEqual(JsonConvert.SerializeObject(testData.PartitionKeyRange), JsonConvert.SerializeObject(range));
                        }
                        else
                        {
                            Assert.IsTrue(testData.ContinuationToken == headers[HttpConstants.HttpHeaders.Continuation] || testData.ContinuationToken == null);
                        }
                    }

                    foreach (ExtractPartitionKeyRangeFromHeadersTestData testData in testSet.Negative)
                    {
                        INameValueCollection headers = getHeadersWithContinuation(testData.CompositeContinuationToken);
                        try
                        {
                            List<CompositeContinuationToken> suppliedTokens;
                            Range<string> rangeOrId = this.partitionRoutingHelper.ExtractPartitionKeyRangeFromContinuationToken(headers, out suppliedTokens);
                            Assert.Fail("Expect BadRequestException");
                        }
                        catch (BadRequestException)
                        {
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Tests for <see cref="PartitionRoutingHelper.ExtractPartitionKeyRangeFromContinuationToken"/> method.
        /// </summary>
        [TestMethod]
        [TestCategory(TestTypeCategory.Quarantine)]
        [Ignore] /* Buffer cannot be null */
        public async Task TestAddFormattedContinuationToHeader()
        {
            using (Stream stream = new MemoryStream(Properties.Resources.BaselineTest_PartitionRoutingHelper_AddFormattedContinuationToHeader))
            {
                using (StreamReader reader = new StreamReader(stream))
                {
                    AddFormattedContinuationToHeaderTestData testData = JsonConvert.DeserializeObject<AddFormattedContinuationToHeaderTestData>(reader.ReadToEnd());

                    CollectionRoutingMap routingMap =
                        CollectionRoutingMap.TryCreateCompleteRoutingMap(
                            testData.RoutingMap.Select(range => Tuple.Create(range, (ServiceIdentity)null)), string.Empty);
                    RoutingMapProvider routingMapProvider = new RoutingMapProvider(routingMap);              

                    foreach (AddFormattedContinuationToHeaderTestUnit positiveTestData in testData.TestSet.Postive)
                    {

                        INameValueCollection headers;
                        List<CompositeContinuationToken> resolvedContinuationTokens;
                        List<PartitionKeyRange> resolvedRanges;

                        this.AddFormattedContinuationHeaderHelper(positiveTestData, out headers, out resolvedRanges, out resolvedContinuationTokens);

                        bool answer = await this.partitionRoutingHelper.TryAddPartitionKeyRangeToContinuationTokenAsync(headers, positiveTestData.ProvidedRanges, routingMapProvider, null, new PartitionRoutingHelper.ResolvedRangeInfo(resolvedRanges[0], (resolvedContinuationTokens.Count > 0) ? resolvedContinuationTokens : null), NoOpTrace.Singleton);

                        Assert.AreEqual(positiveTestData.OutputCompositeContinuationToken, headers[HttpConstants.HttpHeaders.Continuation]);
                    }

                    foreach (AddFormattedContinuationToHeaderTestUnit negativeTestData in testData.TestSet.Negative)
                    {
                        try
                        {
                            INameValueCollection headers;
                            List<CompositeContinuationToken> resolvedContinuationTokens;
                            List<PartitionKeyRange> resolvedRanges;

                            this.AddFormattedContinuationHeaderHelper(negativeTestData, out headers, out resolvedRanges, out resolvedContinuationTokens);

                            bool answer = await this.partitionRoutingHelper.TryAddPartitionKeyRangeToContinuationTokenAsync(headers, negativeTestData.ProvidedRanges, routingMapProvider, null, new PartitionRoutingHelper.ResolvedRangeInfo(resolvedRanges[0], (resolvedContinuationTokens.Count > 0) ? resolvedContinuationTokens : null), NoOpTrace.Singleton);

                            Assert.Fail("Expect BadRequestException");
                        }
                        catch (BadRequestException)
                        {
                        }
                        catch (InternalServerErrorException)
                        {

                        }
                    }
                }
            }
        }

        private void AddFormattedContinuationHeaderHelper(AddFormattedContinuationToHeaderTestUnit positiveTestData, out INameValueCollection headers, out List<PartitionKeyRange> resolvedRanges, out List<CompositeContinuationToken> resolvedContinuationTokens)
        {
            Func<string, INameValueCollection> getHeadersWithContinuation = (string continuationToken) =>
            {
                INameValueCollection localHeaders = new RequestNameValueCollection();
                if (continuationToken != null)
                {
                    localHeaders[HttpConstants.HttpHeaders.Continuation] = continuationToken;
                }
                return localHeaders;
            };

            resolvedRanges = positiveTestData.ResolvedRanges.Select(x => new PartitionKeyRange() { MinInclusive = x.Min, MaxExclusive = x.Max }).ToList();
            resolvedContinuationTokens = new List<CompositeContinuationToken>();

            CompositeContinuationToken[] initialContinuationTokens = null;
            if (!string.IsNullOrEmpty(positiveTestData.InputCompositeContinuationToken))
            {
                if (positiveTestData.InputCompositeContinuationToken.Trim().StartsWith("[", StringComparison.Ordinal))
                {
                    initialContinuationTokens = JsonConvert.DeserializeObject<CompositeContinuationToken[]>(positiveTestData.InputCompositeContinuationToken);
                }
                else
                {
                    initialContinuationTokens = new CompositeContinuationToken[] { JsonConvert.DeserializeObject<CompositeContinuationToken>(positiveTestData.InputCompositeContinuationToken) };
                }
            }

            if (resolvedRanges.Count > 1)
            {
                CompositeContinuationToken continuationToBeCopied;
                if (initialContinuationTokens != null && initialContinuationTokens.Length > 0)
                {
                    continuationToBeCopied = (CompositeContinuationToken) initialContinuationTokens[0].ShallowCopy();
                }
                else
                {
                    continuationToBeCopied = new CompositeContinuationToken();
                    continuationToBeCopied.Token = string.Empty;
                }

                headers = getHeadersWithContinuation(continuationToBeCopied.Token);

                foreach (PartitionKeyRange pkrange in resolvedRanges)
                {
                    CompositeContinuationToken token = (CompositeContinuationToken) continuationToBeCopied.ShallowCopy();
                    token.Range = pkrange.ToRange();
                    resolvedContinuationTokens.Add(token);
                }

                if (initialContinuationTokens != null)
                {
                    resolvedContinuationTokens.AddRange(initialContinuationTokens.Skip(1));
                }
            }
            else
            {
                headers = getHeadersWithContinuation(null);

            }
        }

        /// <summary>
        /// Tests for <see cref="PartitionRoutingHelper.TryGetTargetRangeFromContinuationTokenRangeAsync"/> method.
        /// </summary>
        [TestMethod]
        public async Task TestGetPartitionRoutingInfo()
        {
            using (Stream stream = new MemoryStream(Properties.Resources.BaselineTest_PartitionRoutingHelper_GetPartitionRoutingInfo))
            {
                using (StreamReader reader = new StreamReader(stream))
                {
                    GetPartitionRoutingInfoTestData testData = JsonConvert.DeserializeObject<GetPartitionRoutingInfoTestData>(reader.ReadToEnd());

                    CollectionRoutingMap routingMap =
                        CollectionRoutingMap.TryCreateCompleteRoutingMap(
                            testData.RoutingMap.Select(range => Tuple.Create(range, (ServiceIdentity)null)), string.Empty);

                    foreach (GetPartitionRoutingInfoTestCase testCase in testData.TestCases)
                    {
                        List<string> actualPartitionKeyRangeIds = new List<string>();

                        Range<string> startRange = Range<string>.GetEmptyRange(PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey);

                        for (Range<string> currentRange = startRange; currentRange != null;)
                        {
                            RoutingMapProvider routingMapProvider = new RoutingMapProvider(routingMap);
                            PartitionRoutingHelper.ResolvedRangeInfo resolvedRangeInfo = await this.partitionRoutingHelper.TryGetTargetRangeFromContinuationTokenRangeAsync(testCase.ProvidedRanges, routingMapProvider, string.Empty, currentRange, null, NoOpTrace.Singleton);
                            actualPartitionKeyRangeIds.Add(resolvedRangeInfo.ResolvedRange.Id);
                            INameValueCollection headers = new RequestNameValueCollection();

                            await this.partitionRoutingHelper.TryAddPartitionKeyRangeToContinuationTokenAsync(headers, testCase.ProvidedRanges, routingMapProvider, string.Empty, resolvedRangeInfo, NoOpTrace.Singleton);

                            List<CompositeContinuationToken> suppliedTokens;
                            Range<string> nextRange = this.partitionRoutingHelper.ExtractPartitionKeyRangeFromContinuationToken(headers, out suppliedTokens);
                            currentRange = nextRange.IsEmpty ? null : nextRange;
                        }

                        Assert.AreEqual(string.Join(", ", testCase.RoutingRangeIds), string.Join(", ", actualPartitionKeyRangeIds));
                    }
                }
            }
        }
        [TestMethod]
        public async Task TestRoutingForPrefixedPartitionKeyQueriesAsync()
        {
            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition()
            {
                Kind = PartitionKind.MultiHash,
                Paths = new Collection<string>() { "/path1", "/path2", "/path3" },
                Version = PartitionKeyDefinitionVersion.V2
            };

            // Case 1: Query with 1 prefix path, split at 1st level. Should route to only one partition.
            {
                List<PartitionKeyRange> partitionKeyRanges = new List<PartitionKeyRange>()
                {
                     new PartitionKeyRange()
                        {
                            Id = "0",
                            MinInclusive = string.Empty,
                            MaxExclusive = "07E4D14180A45153F00B44907886F856" //Seattle
                        },
                     new PartitionKeyRange()
                        {
                            Id = "1",
                            MinInclusive = "07E4D14180A45153F00B44907886F856", //Seattle
                            MaxExclusive = "FF"
                        },
                };
                await PrefixPartitionKeyTestRunnerAsync(
                    partitionKeyDefinition,
                    $"SELECT VALUE r.id from r where r.path1 = \"Microsoft\"",
                    epkRanges =>
                    {
                        return epkRanges.Count == 1; //Routes to only one pkRange.
                    },
                    partitionKeyRanges);
            }

            //Case 2: Query with 1 prefix path value which is split at 2nd level. Should route to two partitions.
            //Case 3: Query with 2 prefix path values which is split at 2nd level. Should route to one partition.
            {
                List<PartitionKeyRange> partitionKeyRanges = new List<PartitionKeyRange>()
                {
                     new PartitionKeyRange()
                        {
                            Id = "0",
                            MinInclusive = string.Empty,
                            MaxExclusive = "07E4D14180A45153F00B44907886F85622E342F38A486A088463DFF7838A5963" //[Seattle, Redmond]
                        },
                     new PartitionKeyRange()
                        {
                            Id = "1",
                            MinInclusive = "07E4D14180A45153F00B44907886F85622E342F38A486A088463DFF7838A5963",//[Seattle, Redmond]
                            MaxExclusive = "FF"
                        },
                };
                await PrefixPartitionKeyTestRunnerAsync(
                    partitionKeyDefinition,
                    $"SELECT VALUE r.id from r where r.path1 = \"seattle\" or (r.path1 = \"seattle\" and r.path2 = \"bellevue\")",
                    epkRanges =>
                    {
                        return epkRanges.Count == 2; //Since data is split at pkey [seattle, redmond], it should route to two pkRange.
                    },
                    partitionKeyRanges);
                await PrefixPartitionKeyTestRunnerAsync(
                    partitionKeyDefinition,
                    $"SELECT VALUE r.id from r where r.path1 = \"seattle\" and r.path2 =\"redmond\"",
                    epkRanges =>
                    {
                        return epkRanges.Count == 1; //Since data is split at pkey [seattle, redmond], this query should route to one pkRange
                    },
                    partitionKeyRanges);
            }

            //Case 4: Query with 2 prefix path values split at the 3rd level. Should route to 2 paritions.
            //Case 5: Query with 1 prefix path value split at 3rd level. Should route to 2 partitions.
            {
                List<PartitionKeyRange> partitionKeyRanges = new List<PartitionKeyRange>()
                {
                     new PartitionKeyRange()
                        {
                            Id = "0",
                            MinInclusive = string.Empty,
                            MaxExclusive = "07E4D14180A45153F00B44907886F85622E342F38A486A088463DFF7838A59630EF2E2D82460884AF0F6440BE4F726A8"//[seattle, redmond, 5.12312419050912359123]
                        },
                     new PartitionKeyRange()
                        {
                            Id = "1",
                            MinInclusive = "07E4D14180A45153F00B44907886F85622E342F38A486A088463DFF7838A59630EF2E2D82460884AF0F6440BE4F726A8",//[seattle, redmond, 5.12312419050912359123]
                            MaxExclusive = "FF"
                        },
                };
                await PrefixPartitionKeyTestRunnerAsync(
                    partitionKeyDefinition,
                    $"SELECT VALUE r.id from r where r.path1 = \"seattle\" and r.path2 =\"redmond\"",
                    epkRanges =>
                    {
                        return epkRanges.Count == 2; //Since data is split at pkey [seattle, redmond, 5.12312419050912359123], it should route to two pkRange.
                    },
                    partitionKeyRanges);
                await PrefixPartitionKeyTestRunnerAsync(
                   partitionKeyDefinition,
                   $"SELECT VALUE r.id from r where r.path1 = \"seattle\" and r.path2 =\"redmond\" and r.path3=5.12312419050912359123",
                   epkRanges =>
                   {
                       return epkRanges.Count == 1;
                   },
                   partitionKeyRanges);
                await PrefixPartitionKeyTestRunnerAsync(
                    partitionKeyDefinition,
                    $"SELECT VALUE r.id from r where r.path1 = \"seattle\"",
                    epkRanges =>
                    {
                        return epkRanges.Count == 2;
                    },
                    partitionKeyRanges);
            }

            //Case 6: Query with 1 prefix path value split succesively at 2nd and then at the 3rd level. Should route to 3 partitions.
            {
                List<PartitionKeyRange> partitionKeyRanges = new List<PartitionKeyRange>()
                {
                     new PartitionKeyRange()
                        {
                            Id = "0",
                            MinInclusive = string.Empty,
                            MaxExclusive = "07E4D14180A45153F00B44907886F85622E342F38A486A088463DFF7838A5963"//[seattle, redmond]
                        },
                     new PartitionKeyRange()
                        {
                            Id = "1",
                            MinInclusive = "07E4D14180A45153F00B44907886F85622E342F38A486A088463DFF7838A5963",//[seattle, redmond]
                            MaxExclusive = "07E4D14180A45153F00B44907886F85622E342F38A486A088463DFF7838A59630EF2E2D82460884AF0F6440BE4F726A8"//[seattle, redmond, 5.12312419050912359123]
                        },
                     new PartitionKeyRange()
                        {
                            Id = "2",
                            MinInclusive = "07E4D14180A45153F00B44907886F85622E342F38A486A088463DFF7838A59630EF2E2D82460884AF0F6440BE4F726A8",//[seattle, redmond, 5.12312419050912359123]
                            MaxExclusive = "FF"
                        },
                };
                await PrefixPartitionKeyTestRunnerAsync(
                    partitionKeyDefinition,
                    $"SELECT VALUE r.id from r where r.path1 = \"seattle\"",
                    epkRanges =>
                    {
                        return epkRanges.Count == 3;
                    },
                    partitionKeyRanges);
            }

            //Case 7: Query with 1 prefix path value split succesively at 1st, 2nd and then at the 3rd level. Should route to 3 partitions.
            //Case 8: Query with 2 prefix path value split succesively at 1st, 2nd and then at the 3rd level. Should route to 2 partitions.
            //Case 9: Query with fully specfied pkey, split succesively at 1st, 2nd and then at the 3rd level. Should route to 1 partitions.
            {
                List<PartitionKeyRange> partitionKeyRanges = new List<PartitionKeyRange>()
                {
                     new PartitionKeyRange()
                        {
                            Id = "0",
                            MinInclusive = string.Empty,
                            MaxExclusive = "07E4D14180A45153F00B44907886F856" //[seattle]
                        },
                     new PartitionKeyRange()
                        {
                            Id = "1",
                            MinInclusive = "07E4D14180A45153F00B44907886F856", //[seattle]
                            MaxExclusive = "07E4D14180A45153F00B44907886F85622E342F38A486A088463DFF7838A5963" //[seattle, redmond]
                        },
                     new PartitionKeyRange()
                        {
                            Id = "2",
                            MinInclusive = "07E4D14180A45153F00B44907886F85622E342F38A486A088463DFF7838A5963", //[seattle, redmond]
                            MaxExclusive = "07E4D14180A45153F00B44907886F85622E342F38A486A088463DFF7838A59630EF2E2D82460884AF0F6440BE4F726A8"//[seattle, redmond, 5.12312419050912359123]
                        },
                     new PartitionKeyRange()
                        {
                            Id = "3",
                            MinInclusive = "07E4D14180A45153F00B44907886F85622E342F38A486A088463DFF7838A59630EF2E2D82460884AF0F6440BE4F726A8",//[seattle, redmond, 5.12312419050912359123]
                            MaxExclusive = "FF"
                        },
                };
                await PrefixPartitionKeyTestRunnerAsync(
                    partitionKeyDefinition,
                    $"SELECT VALUE r.id from r where r.path1 = \"seattle\"",
                    epkRanges =>
                    {
                        return epkRanges.Count == 3; //Routes tp three pkRanges
                    },
                    partitionKeyRanges);
                await PrefixPartitionKeyTestRunnerAsync(
                    partitionKeyDefinition,
                    $"SELECT VALUE r.id from r where r.path1 = \"seattle\" and r.path2 = \"redmond\"",
                    epkRanges =>
                    {
                        return epkRanges.Count == 2;  //Routes to two pkRanges.
                    },
                    partitionKeyRanges);
                await PrefixPartitionKeyTestRunnerAsync(
                   partitionKeyDefinition,
                   $"SELECT VALUE r.id from r where r.path1 = \"seattle\" and r.path2 = \"redmond\" and r.path3 = \"98052\"",
                   epkRanges =>
                   {
                       Assert.AreEqual(epkRanges.Count, 1);

                       return epkRanges.Count == 1; //Routes to only one pkRanges.
                   },
                   partitionKeyRanges);
            }
        }

        [TestMethod]
        public async Task TestRoutingForPrefixedPartitionKeyChangeFeedAsync()
        {
            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition()
            {
                Kind = PartitionKind.MultiHash,
                Paths = new Collection<string>() { "/path1", "/path2", "/path3" },
                Version = PartitionKeyDefinitionVersion.V2
            };

            // Case 1: ChangeFeed with 1 prefix path, split at 1st level. Should route to only one partition.
            {
                List<PartitionKeyRange> partitionKeyRanges = new List<PartitionKeyRange>()
                {
                     new PartitionKeyRange()
                        {
                            Id = "0",
                            MinInclusive = string.Empty,
                            MaxExclusive = "07E4D14180A45153F00B44907886F856" //Seattle
                        },
                     new PartitionKeyRange()
                        {
                            Id = "1",
                            MinInclusive = "07E4D14180A45153F00B44907886F856", //Seattle
                            MaxExclusive = "FF"
                        },
                };
                await PrefixPartitionKeyChangeFeedTestRunnerAsync(
                    partitionKeyDefinition,
                    new PartitionKeyBuilder().Add("Microsoft").Build(),
                    epkRanges =>
                    {
                        return epkRanges.Count == 1; //Routes to only one pkRange.
                    },
                    partitionKeyRanges);
            }

            //Case 2: ChangeFeed with 1 prefix path value which is split at 2nd level. Should route to two partitions.
            //Case 3: ChangeFeed with 2 prefix path values which is split at 2nd level. Should route to one partition.
            {
                List<PartitionKeyRange> partitionKeyRanges = new List<PartitionKeyRange>()
                {
                     new PartitionKeyRange()
                        {
                            Id = "0",
                            MinInclusive = string.Empty,
                            MaxExclusive = "07E4D14180A45153F00B44907886F85622E342F38A486A088463DFF7838A5963" //[Seattle, Redmond]
                        },
                     new PartitionKeyRange()
                        {
                            Id = "1",
                            MinInclusive = "07E4D14180A45153F00B44907886F85622E342F38A486A088463DFF7838A5963", //[Seattle, Redmond]
                            MaxExclusive = "FF"
                        },
                };
                await PrefixPartitionKeyChangeFeedTestRunnerAsync(
                    partitionKeyDefinition,
                    new PartitionKeyBuilder().Add("seattle").Build(),
                    epkRanges =>
                    {
                        return epkRanges.Count == 2; //Since data is split at pkey [seattle, redmond], it should route to two pkRange.
                    },
                    partitionKeyRanges);
                await PrefixPartitionKeyChangeFeedTestRunnerAsync(
                    partitionKeyDefinition,
                    new PartitionKeyBuilder().Add("seattle").Add("redmond").Build(),
                    epkRanges =>
                    {
                        return epkRanges.Count == 1; //Since data is split at pkey [seattle, redmond], this query should route to one pkRange
                    },
                    partitionKeyRanges);
            }

            //Case 4: ChangeFeed with 2 prefix path values split at the 3rd level. Should route to 2 paritions.
            //Case 5: ChangeFeed with 1 prefix path value split at 3rd level. Should route to 2 partitions.
            {
                List<PartitionKeyRange> partitionKeyRanges = new List<PartitionKeyRange>()
                {
                     new PartitionKeyRange()
                        {
                            Id = "0",
                            MinInclusive = string.Empty,
                            MaxExclusive = "07E4D14180A45153F00B44907886F85622E342F38A486A088463DFF7838A59630EF2E2D82460884AF0F6440BE4F726A8"//[seattle, redmond, 5.12312419050912359123]
                        },
                     new PartitionKeyRange()
                        {
                            Id = "1",
                            MinInclusive = "07E4D14180A45153F00B44907886F85622E342F38A486A088463DFF7838A59630EF2E2D82460884AF0F6440BE4F726A8", //[seattle, redmond, 5.12312419050912359123]
                            MaxExclusive = "FF"
                        },
                };
                await PrefixPartitionKeyChangeFeedTestRunnerAsync(
                    partitionKeyDefinition,
                    new PartitionKeyBuilder().Add("seattle").Add("redmond").Build(),
                    epkRanges =>
                    {
                        return epkRanges.Count == 2; //Since data is split at pkey [seattle, redmond, 5.12312419050912359123], it should route to two pkRange.
                    },
                    partitionKeyRanges);
                await PrefixPartitionKeyChangeFeedTestRunnerAsync(
                   partitionKeyDefinition,
                   new PartitionKeyBuilder().Add("seattle").Add("redmond").Add(5.12312419050912359123).Build(),
                   epkRanges =>
                   {
                       return epkRanges.Count == 1;
                   },
                   partitionKeyRanges);
                await PrefixPartitionKeyChangeFeedTestRunnerAsync(
                    partitionKeyDefinition,
                    new PartitionKeyBuilder().Add("seattle").Build(),
                    epkRanges =>
                    {
                        return epkRanges.Count == 2;
                    },
                    partitionKeyRanges);
            }

            //Case 6: ChangeFeed with 1 prefix path value split succesively at 2nd and then at the 3rd level. Should route to 3 partitions.
            {
                List<PartitionKeyRange> partitionKeyRanges = new List<PartitionKeyRange>()
                {
                     new PartitionKeyRange()
                        {
                            Id = "0",
                            MinInclusive = string.Empty,
                            MaxExclusive = "07E4D14180A45153F00B44907886F85622E342F38A486A088463DFF7838A5963" //[seattle, redmond]
                        },
                     new PartitionKeyRange()
                        {
                            Id = "1",
                            MinInclusive = "07E4D14180A45153F00B44907886F85622E342F38A486A088463DFF7838A5963", //[seattle, redmond]
                            MaxExclusive = "07E4D14180A45153F00B44907886F85622E342F38A486A088463DFF7838A59630EF2E2D82460884AF0F6440BE4F726A8" //[seattle, redmond, 5.12312419050912359123]
                        },
                     new PartitionKeyRange()
                        {
                            Id = "2",
                            MinInclusive = "07E4D14180A45153F00B44907886F85622E342F38A486A088463DFF7838A59630EF2E2D82460884AF0F6440BE4F726A8", //[seattle, redmond, 5.12312419050912359123]
                            MaxExclusive = "FF"
                        },
                };
                await PrefixPartitionKeyChangeFeedTestRunnerAsync(
                    partitionKeyDefinition,
                    new PartitionKeyBuilder().Add("seattle").Build(),
                    epkRanges =>
                    {
                        return epkRanges.Count == 3;
                    },
                    partitionKeyRanges);
            }

            //Case 7: ChangeFeed with 1 prefix path value split succesively at 1st, 2nd and then at the 3rd level. Should route to 3 partitions.
            //Case 8: ChangeFeed with 2 prefix path value split succesively at 1st, 2nd and then at the 3rd level. Should route to 2 partitions.
            //Case 9: ChangeFeed with fully specfied pkey, split succesively at 1st, 2nd and then at the 3rd level. Should route to 1 partitions.
            {
                List<PartitionKeyRange> partitionKeyRanges = new List<PartitionKeyRange>()
                {
                     new PartitionKeyRange()
                        {
                            Id = "0",
                            MinInclusive = string.Empty,
                            MaxExclusive = "07E4D14180A45153F00B44907886F856" //[seattle]
                        },
                     new PartitionKeyRange()
                        {
                            Id = "1",
                            MinInclusive = "07E4D14180A45153F00B44907886F856", //[seattle]
                            MaxExclusive = "07E4D14180A45153F00B44907886F85622E342F38A486A088463DFF7838A5963" //[seattle, redmond]
                        },
                     new PartitionKeyRange()
                        {
                            Id = "2",
                            MinInclusive = "07E4D14180A45153F00B44907886F85622E342F38A486A088463DFF7838A5963", //[seattle, redmond]
                            MaxExclusive = "07E4D14180A45153F00B44907886F85622E342F38A486A088463DFF7838A59630EF2E2D82460884AF0F6440BE4F726A8"//[seattle, redmond, 5.12312419050912359123]
                        },
                     new PartitionKeyRange()
                        {
                            Id = "3",
                            MinInclusive = "07E4D14180A45153F00B44907886F85622E342F38A486A088463DFF7838A59630EF2E2D82460884AF0F6440BE4F726A8", //[seattle, redmond, 5.12312419050912359123]
                            MaxExclusive = "FF"
                        },
                };
                await PrefixPartitionKeyChangeFeedTestRunnerAsync(
                    partitionKeyDefinition,
                    new PartitionKeyBuilder().Add("seattle").Build(),
                    epkRanges =>
                    {
                        return epkRanges.Count == 3; //Routes tp three pkRanges
                    },
                    partitionKeyRanges);
                await PrefixPartitionKeyChangeFeedTestRunnerAsync(
                    partitionKeyDefinition,
                    new PartitionKeyBuilder().Add("seattle").Add("redmond").Build(),
                    epkRanges =>
                    {
                        return epkRanges.Count == 2;  //Routes to two pkRanges.
                    },
                    partitionKeyRanges);
                await PrefixPartitionKeyChangeFeedTestRunnerAsync(
                   partitionKeyDefinition,
                   new PartitionKeyBuilder().Add("seattle").Add("redmond").Add("98052").Build(),
                   epkRanges =>
                   {
                       Assert.AreEqual(epkRanges.Count, 1);

                       return epkRanges.Count == 1; //Routes to only one pkRanges.
                   },
                   partitionKeyRanges);
            }
        }

        private static async Task PrefixPartitionKeyTestRunnerAsync(
                PartitionKeyDefinition partitionKeyDefinition,
                string queryText,
                Predicate<HashSet<PartitionKeyRange>> validator,
                List<PartitionKeyRange> partitionKeyRanges)
        {
            IDictionary<string, object> DefaultQueryengineConfiguration = new Dictionary<string, object>()
            {
                {"maxSqlQueryInputLength", 30720},
                {"maxJoinsPerSqlQuery", 5},
                {"maxLogicalAndPerSqlQuery", 200},
                {"maxLogicalOrPerSqlQuery", 200},
                {"maxUdfRefPerSqlQuery", 2},
                {"maxInExpressionItemsCount", 8000},
                {"queryMaxInMemorySortDocumentCount", 500},
                {"maxQueryRequestTimeoutFraction", 0.90},
                {"sqlAllowNonFiniteNumbers", false},
                {"sqlAllowAggregateFunctions", true},
                {"sqlAllowSubQuery", true},
                {"sqlAllowScalarSubQuery", false},
                {"allowNewKeywords", true},
                {"sqlAllowLike", true},
                {"sqlAllowGroupByClause", false},
                {"queryEnableMongoNativeRegex", true},
                {"maxSpatialQueryCells", 12},
                {"spatialMaxGeometryPointCount", 256},
                {"sqlDisableOptimizationFlags", 0},
                {"sqlEnableParameterExpansionCheck", true}
            };

            QueryPartitionProvider QueryPartitionProvider = new QueryPartitionProvider(DefaultQueryengineConfiguration);

            IEnumerable<Tuple<PartitionKeyRange, ServiceIdentity>> rangesAndServiceIdentity = partitionKeyRanges
                .Select(range => Tuple.Create(range, (ServiceIdentity)null));
            string collectionRid = string.Empty;
            CollectionRoutingMap routingMap =
                CollectionRoutingMap.TryCreateCompleteRoutingMap(
                    rangesAndServiceIdentity,
                    collectionRid);

            RoutingMapProvider routingMapProvider = new RoutingMapProvider(routingMap);
            TryCatch<PartitionedQueryExecutionInfo> tryGetQueryPlan =
                QueryPartitionProvider.TryGetPartitionedQueryExecutionInfo(
                        querySpecJsonString: JsonConvert.SerializeObject(new SqlQuerySpec(queryText)),
                        partitionKeyDefinition: partitionKeyDefinition,
                        vectorEmbeddingPolicy: null,
                        requireFormattableOrderByQuery: true,
                        isContinuationExpected: true,
                        allowNonValueAggregateQuery: false,
                        hasLogicalPartitionKey: false,
                        allowDCount: true,
                        useSystemPrefix: false,
                        geospatialType: Cosmos.GeospatialType.Geography);

            HashSet<PartitionKeyRange> resolvedPKRanges = new HashSet<PartitionKeyRange>();
            foreach (Range<string> range in tryGetQueryPlan.Result.QueryRanges)
            {
                resolvedPKRanges.UnionWith(await routingMapProvider.TryGetOverlappingRangesAsync(
                    collectionRid,
                    range,
                    NoOpTrace.Singleton));
            }

            Assert.IsTrue(validator(resolvedPKRanges));
        }

    private static async Task PrefixPartitionKeyChangeFeedTestRunnerAsync(
                PartitionKeyDefinition partitionKeyDefinition,
                Cosmos.PartitionKey partitionKey,
                Predicate<HashSet<PartitionKeyRange>> validator,
                List<PartitionKeyRange> partitionKeyRanges)
        {
            IEnumerable<Tuple<PartitionKeyRange, ServiceIdentity>> rangesAndServiceIdentity = partitionKeyRanges
                .Select(range => Tuple.Create(range, (ServiceIdentity)null));
            string collectionRid = string.Empty;
            CollectionRoutingMap routingMap =
                CollectionRoutingMap.TryCreateCompleteRoutingMap(
                    rangesAndServiceIdentity,
                    collectionRid);

            RoutingMapProvider routingMapProvider = new RoutingMapProvider(routingMap);
            
            HashSet<PartitionKeyRange> resolvedPKRanges = new HashSet<PartitionKeyRange>();
            FeedRangePartitionKey feedRangePartitionKey = new FeedRangePartitionKey(partitionKey);
            List<Range<string>> effectiveRanges = await feedRangePartitionKey.GetEffectiveRangesAsync(routingMapProvider, null, partitionKeyDefinition, null);
            foreach (Range<string> range in effectiveRanges)
            {
                resolvedPKRanges.UnionWith(await routingMapProvider.TryGetOverlappingRangesAsync(
                    collectionRid,
                    range,
                    NoOpTrace.Singleton));

            }

            Assert.IsTrue(validator(resolvedPKRanges));
        }

        [TestMethod]
        public void TestCrossPartitionAggregateQueries()
        {
            PartitionRoutingHelperTestCase[] testcases = new[]
            {
                new PartitionRoutingHelperTestCase
                {
                    Query = "SELECT AVG(r.key) FROM r WHERE IS_NUMBER(r.key)",
                    PartitionKey = "/key",
                    EnableCrossPartitionQuery = true,
                    ClientApiVersion = "2018-12-31",
                    StatusCode = HttpStatusCode.BadRequest,
                    SubStatusCode = SubStatusCodes.CrossPartitionQueryNotServable,
                    Message = RMResources.UnsupportedCrossPartitionQuery
                },
                new PartitionRoutingHelperTestCase
                {
                    Query = "SELECT COUNT(1), MAX(r.key) FROM r",
                    PartitionKey = "/key",
                    EnableCrossPartitionQuery = true,
                    ClientApiVersion = "2018-12-31",
                    StatusCode = HttpStatusCode.BadRequest,
                    SubStatusCode = SubStatusCodes.CrossPartitionQueryNotServable,
                    Message = RMResources.UnsupportedCrossPartitionQuery
                },
                new PartitionRoutingHelperTestCase
                {
                    Query = "SELECT c.age, COUNT(1) as count FROM c GROUP BY c.age",
                    PartitionKey = "/key",
                    EnableCrossPartitionQuery = true,
                    ClientApiVersion = "2018-12-31",
                    StatusCode = HttpStatusCode.BadRequest,
                    SubStatusCode = SubStatusCodes.CrossPartitionQueryNotServable,
                    Message = RMResources.UnsupportedCrossPartitionQuery
                },
                new PartitionRoutingHelperTestCase
                {
                    Query = @"SELECT
                                c.age,
                                AVG(c.doesNotExist) as undefined_avg,
                                MIN(c.doesNotExist) as undefined_min,
                                MAX(c.doesNotExist) as undefined_max,
                                COUNT(c.doesNotExist) as undefined_count,
                                SUM(c.doesNotExist) as undefined_sum
                              FROM c
                              GROUP BY c.age",
                    PartitionKey = "/key",
                    EnableCrossPartitionQuery = true,
                    ClientApiVersion = "2018-12-31",
                    StatusCode = HttpStatusCode.BadRequest,
                    SubStatusCode = SubStatusCodes.CrossPartitionQueryNotServable,
                    Message = RMResources.UnsupportedCrossPartitionQuery
                },
                new PartitionRoutingHelperTestCase
                {
                    Query = @"SELECT * FROM c ORDER BY c.age",
                    PartitionKey = "/key",
                    EnableCrossPartitionQuery = false,
                    ClientApiVersion = "2018-12-31",
                    StatusCode = HttpStatusCode.BadRequest,
                    SubStatusCode = SubStatusCodes.Unknown,
                    Message = RMResources.CrossPartitionQueryDisabled
                },
                new PartitionRoutingHelperTestCase
                {
                    Query = @"SELECT COUNT(1) FROM c WHERE c.age > 18",
                    PartitionKey = "/key",
                    EnableCrossPartitionQuery = true,
                    ClientApiVersion = "2016-07-11",
                    StatusCode = HttpStatusCode.BadRequest,
                    SubStatusCode = SubStatusCodes.Unknown,
                    Message = RMResources.UnsupportedCrossPartitionQueryWithAggregate
                },
                new PartitionRoutingHelperTestCase
                {
                    Query = @"SELECT COUNT(1) FROM c WHERE c.age > 18",
                    PartitionKey = "/key",
                    EnableCrossPartitionQuery = true,
                    ClientApiVersion = "2016-05-30",
                    StatusCode = HttpStatusCode.BadRequest,
                    SubStatusCode = SubStatusCodes.Unknown,
                    Message = RMResources.UnsupportedCrossPartitionQuery
                }
            };

            foreach(PartitionRoutingHelperTestCase testcase in testcases)
            {
                try
                {
                    IReadOnlyList<Range<string>> _ = PartitionRoutingHelper.GetProvidedPartitionKeyRanges(
                        querySpecJsonString: JsonConvert.SerializeObject(new Cosmos.Query.Core.SqlQuerySpec(testcase.Query)),
                        enableCrossPartitionQuery: testcase.EnableCrossPartitionQuery,
                        parallelizeCrossPartitionQuery: false,
                        isContinuationExpected: true,
                        hasLogicalPartitionKey: testcase.HasLogicalPartitionKey,
                        allowDCount: false,
                        allowNonValueAggregates: true,
                        useSystemPrefix: false,
                        partitionKeyDefinition: new PartitionKeyDefinition { Paths = new Collection<string> { testcase.PartitionKey }, Kind = PartitionKind.Hash },
                        vectorEmbeddingPolicy: null,
                        queryPartitionProvider: QueryPartitionProviderTestInstance.Object,
                        clientApiVersion: testcase.ClientApiVersion,
                        geospatialType: Cosmos.GeospatialType.Geography,
                        out QueryInfo info);

                    Assert.Fail();
                }
                catch(DocumentClientException dce)
                {
                    Assert.AreEqual(testcase.StatusCode, dce.StatusCode);
                    Assert.AreEqual(testcase.SubStatusCode, dce.GetSubStatus());
                    Assert.IsTrue(dce.Message.Contains(testcase.Message));
                    PartitionedQueryExecutionInfo info = JsonConvert.DeserializeObject<PartitionedQueryExecutionInfo>(dce.Error.AdditionalErrorInfo);
                    Assert.IsNotNull(info);
                }
            }
        }

        internal struct PartitionRoutingHelperTestCase
        {
            internal string Query { get; set; }

            internal string PartitionKey { get; set; }

            internal bool EnableCrossPartitionQuery { get; set; }

            internal string ClientApiVersion { get; set; }

            internal bool HasLogicalPartitionKey { get; set; }

            internal HttpStatusCode StatusCode { get; set;}

            internal SubStatusCodes SubStatusCode { get; set; }

            internal string Message { get; set; }
        }


        private class RoutingMapProvider : IRoutingMapProvider
        {
            private readonly CollectionRoutingMap collectionRoutingMap;

            public RoutingMapProvider(CollectionRoutingMap collectionRoutingMap)
            {
                this.collectionRoutingMap = collectionRoutingMap;
            }

            public Task<IReadOnlyList<PartitionKeyRange>> TryGetOverlappingRangesAsync(
                string collectionResourceId, 
                Range<string> range, 
                ITrace trace,
                bool forceRefresh = false)
            {
                return Task.FromResult(this.collectionRoutingMap.GetOverlappingRanges(range));
            }

            public Task<PartitionKeyRange> TryGetPartitionKeyRangeByIdAsync(
                string collectionResourceId, 
                string partitionKeyRangeId, 
                ITrace trace,
                bool forceRefresh = false)
            {
                return Task.FromResult(this.collectionRoutingMap.TryGetRangeByPartitionKeyRangeId(partitionKeyRangeId));
            }

            public Task<PartitionKeyRange> TryGetRangeByEffectivePartitionKey(
                string collectionResourceId, 
                string effectivePartitionKey)
            {
                return Task.FromResult(this.collectionRoutingMap.GetOverlappingRanges(Range<string>.GetPointRange(effectivePartitionKey)).Single());
            }
        }

        private class TestSet<T>
        {
            [JsonProperty(PropertyName = "positive")]
            public T[] Postive { get; set; }

            [JsonProperty(PropertyName = "negative")]
            public T[] Negative { get; set; }
        }

        private class ExtractPartitionKeyRangeFromHeadersTestData
        {
            [JsonProperty(PropertyName = "compositeContinuationToken")]
            public string CompositeContinuationToken { get; set; }

            [JsonProperty(PropertyName = "continuationToken")]
            public string ContinuationToken { get; set; }

            [JsonProperty(PropertyName = "partitionKeyRange")]
            public Range<string> PartitionKeyRange { get; set; }
        }

        private class AddFormattedContinuationToHeaderTestUnit
        {
            [JsonProperty(PropertyName = "inputCompositeContinuationToken")]
            public string InputCompositeContinuationToken { get; set; }

            [JsonProperty(PropertyName = "outputCompositeContinuationToken")]
            public string OutputCompositeContinuationToken { get; set; }

            [JsonProperty(PropertyName = "resolvedRanges")]
            public List<Range<string>> ResolvedRanges { get; set; }

            [JsonProperty(PropertyName = "providedRanges")]
            public List<Range<string>> ProvidedRanges { get; set; }
        }

        private class AddFormattedContinuationToHeaderTestData
        {
            [JsonProperty(PropertyName = "testSet")]
            public TestSet<AddFormattedContinuationToHeaderTestUnit> TestSet { get; set; }

            [JsonProperty(PropertyName = "routingMap")]
            public PartitionKeyRange[] RoutingMap { get; set; }
        }

        private class GetPartitionRoutingInfoTestCase
        {
            [JsonProperty(PropertyName = "providedRanges")]
            public List<Range<string>> ProvidedRanges { get; set; }

            [JsonProperty(PropertyName = "routingRangeIds")]
            public List<string> RoutingRangeIds { get; set; }
        }

        private class GetPartitionRoutingInfoTestData
        {
            [JsonProperty(PropertyName = "testCases")]
            public GetPartitionRoutingInfoTestCase[] TestCases { get; set; }

            [JsonProperty(PropertyName = "routingMap")]
            public PartitionKeyRange[] RoutingMap { get; set; }
        }
    }
}