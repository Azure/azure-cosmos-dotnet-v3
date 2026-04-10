//-----------------------------------------------------------------------
// <copyright file="CosmosElementEqualityComparerUnitTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Tests.Query.OfflineEngineTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
    using Microsoft.Azure.Cosmos.SqlObjects;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using OfflineEngine;
    using VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// CosmosElementEqualityComparerUnitTests Class.
    /// </summary>
    [TestClass]
    public class SqlInterpreterTests
    {
        private static readonly CosmosObject AndersenFamily = CosmosObject.Parse(@"
        {
          ""id"": ""AndersenFamily"",
          ""lastName"": ""Andersen"",
          ""parents"": [
             { ""firstName"": ""Thomas"" },
             { ""firstName"": ""Mary Kay""}
          ],
          ""children"": [
             {
                 ""firstName"": ""Henriette Thaulow"",
                 ""gender"": ""female"",
                 ""grade"": 5,
                 ""pets"": [{ ""givenName"": ""Fluffy"" }]
             }
          ],
          ""address"": { ""state"": ""WA"", ""county"": ""King"", ""city"": ""seattle"" },
          ""creationDate"": 1431620472,
          ""isRegistered"": true,
          ""_rid"": ""iDdHAJ74DSoBAAAAAAAAAA==""
        }");

        private static readonly CosmosObject WakefieldFamily = CosmosObject.Parse(@"
        {
          ""id"": ""WakefieldFamily"",
          ""parents"": [
              { ""familyName"": ""Wakefield"", ""givenName"": ""Robin"" },
              { ""familyName"": ""Miller"", ""givenName"": ""Ben"" }
          ],
          ""children"": [
              {
                ""familyName"": ""Merriam"",
                ""givenName"": ""Jesse"",
                ""gender"": ""female"", ""grade"": 1,
                ""pets"": [
                    { ""givenName"": ""Goofy"" },
                    { ""givenName"": ""Shadow"" }
                ]
              },
              {
                ""familyName"": ""Miller"",
                 ""givenName"": ""Lisa"",
                 ""gender"": ""female"",
                 ""grade"": 8 }
          ],
          ""address"": { ""state"": ""NY"", ""county"": ""Manhattan"", ""city"": ""NY"" },
          ""creationDate"": 1431620462,
          ""isRegistered"": false,
          ""_rid"": ""iDdHAJ74DSoCAAAAAAAAAA==""
        }");

        private static readonly CosmosElement[] DataSource = new CosmosElement[]
        {
            AndersenFamily,
            WakefieldFamily,

            // Add some duplicates
            AndersenFamily,
            WakefieldFamily
        };

        private static readonly CosmosElement[] RidOrderedDataSource = new CosmosElement[]
        {
            AndersenFamily,
            AndersenFamily,
            WakefieldFamily,
            WakefieldFamily
        };

        private static readonly SqlFromClause FromClause = SqlFromClause.Create(SqlAliasedCollectionExpression.Create(
            SqlInputPathCollection.Create(
                SqlIdentifier.Create("c"), null),
            null));

        [TestMethod]
        [Owner("brchon")]
        public void SelectClauseTest()
        {
            // SELECT VALUE ROUND(4.84090499760142E+15)
            AssertEvaluation(
                new CosmosElement[]
                {
                    CosmosNumber64.Create(4.84090499760142E+15),
                    CosmosNumber64.Create(4.84090499760142E+15),
                    CosmosNumber64.Create(4.84090499760142E+15),
                    CosmosNumber64.Create(4.84090499760142E+15)
                },
                CreateQueryWithSelectClause(
                    SqlSelectClause.Create(
                        SqlSelectValueSpec.Create(
                            SqlFunctionCallScalarExpression.CreateBuiltin(
                                SqlFunctionCallScalarExpression.Identifiers.Round,
                                SqlLiteralScalarExpression.Create(
                                    SqlNumberLiteral.Create(4840904997601420)))))),
                DataSource);

            // SELECT *
            SqlQuery selectStar = CreateQueryWithSelectClause(SqlSelectClause.SelectStar);
            AssertEvaluation(RidOrderedDataSource, selectStar, DataSource);

            // SELECT c.id
            SqlScalarExpression cDotId = TestUtils.CreatePathExpression("c", "id");
            SqlQuery selectCDotId = CreateQueryWithSelectClause(
                SqlSelectClause.Create(
                    SqlSelectListSpec.Create(SqlSelectItem.Create(cDotId))));
            CosmosObject andersonFamilyId = CosmosObject.Create(new Dictionary<string, CosmosElement>
            {
                ["id"] = CosmosString.Create("AndersenFamily")
            });

            CosmosObject wakefieldFamilyId = CosmosObject.Create(new Dictionary<string, CosmosElement>
            {
                ["id"] = CosmosString.Create("WakefieldFamily")
            });

            AssertEvaluation(new CosmosElement[]
            {
                andersonFamilyId,
                andersonFamilyId,
                wakefieldFamilyId,
                wakefieldFamilyId,
            }, selectCDotId, DataSource);

            // SELECT TOP 1
            SqlQuery selectTop1 = CreateQueryWithSelectClause(
                SqlSelectClause.Create(
                    SqlSelectStarSpec.Singleton,
                    SqlTopSpec.Create(
                        SqlNumberLiteral.Create(1))));
            AssertEvaluation(DataSource.Take(1), selectTop1, DataSource);

            // SELECT DISTINCT *
            SqlQuery selectDistinct = CreateQueryWithSelectClause(
                SqlSelectClause.Create(SqlSelectStarSpec.Singleton, topSpec: null, hasDistinct: true));
            AssertEvaluation(new CosmosElement[] { AndersenFamily, WakefieldFamily }, selectDistinct, DataSource);

            // Ordering Across Partitions and rids
            CosmosObject doc1 = CosmosObject.Parse(@"{""id"":""1"",""_rid"":""iDdHALHU6EkBAAAAAAAAAA==""}");
            CosmosObject doc2 = CosmosObject.Parse(@"{""id"":""2"",""_rid"":""iDdHALHU6EkBAAAAAAAACA==""}");
            CosmosObject doc3 = CosmosObject.Parse(@"{""id"":""3"",""_rid"":""iDdHALHU6EkCAAAAAAAACA==""}");
            CosmosObject doc4 = CosmosObject.Parse(@"{""id"":""4"",""_rid"":""iDdHALHU6EkBAAAAAAAADA==""}");
            CosmosObject doc5 = CosmosObject.Parse(@"{""id"":""5"",""_rid"":""iDdHALHU6EkBAAAAAAAABA==""}");
            CosmosObject doc6 = CosmosObject.Parse(@"{""id"":""6"",""_rid"":""iDdHALHU6EkCAAAAAAAADA==""}");

            PartitionKeyRange pk1 = new PartitionKeyRange()
            {
                MinInclusive = "0",
                MaxExclusive = "1",
            };
            PartitionKeyRange pk2 = new PartitionKeyRange()
            {
                MinInclusive = "1",
                MaxExclusive = "2",
            };
            PartitionKeyRange pk3 = new PartitionKeyRange()
            {
                MinInclusive = "2",
                MaxExclusive = "3",
            };

            Dictionary<string, PartitionKeyRange> ridToPartitionKeyRange = new Dictionary<string, PartitionKeyRange>()
            {
                { ((CosmosString)doc1["_rid"]).Value, pk2 },
                { ((CosmosString)doc2["_rid"]).Value, pk3 },
                { ((CosmosString)doc3["_rid"]).Value, pk1 },
                { ((CosmosString)doc4["_rid"]).Value, pk2 },
                { ((CosmosString)doc5["_rid"]).Value, pk3 },
                { ((CosmosString)doc6["_rid"]).Value, pk1 },
            };

            AssertEvaluation(
                new CosmosElement[] { doc3, doc6, doc1, doc4, doc5, doc2 },
                selectStar,
                new CosmosElement[] { doc1, doc2, doc3, doc5, doc4, doc6 },
                ridToPartitionKeyRange);
        }

        [TestMethod]
        [Owner("brchon")]
        public void FromClauseTest()
        {
            //SELECT
            //    f.id AS familyName,
            //    c.givenName AS childGivenName,
            //    c.firstName AS childFirstName,
            //    p.givenName AS petName
            //FROM Families f
            //JOIN c IN f.children
            //JOIN p IN c.pets

            SqlSelectItem familyName = SqlSelectItem.Create(
                TestUtils.CreatePathExpression("f", "id"),
                SqlIdentifier.Create("familyName"));

            SqlSelectItem childGivenName = SqlSelectItem.Create(
                TestUtils.CreatePathExpression("c", "givenName"),
                SqlIdentifier.Create("childGivenName"));

            SqlSelectItem childFirstName = SqlSelectItem.Create(
                TestUtils.CreatePathExpression("c", "firstName"),
                SqlIdentifier.Create("childFirstName"));

            SqlSelectItem petName = SqlSelectItem.Create(
                TestUtils.CreatePathExpression("p", "givenName"),
                SqlIdentifier.Create("petName"));

            SqlSelectClause selectClause = SqlSelectClause.Create(SqlSelectListSpec.Create(familyName, childGivenName, childFirstName, petName));

            SqlAliasedCollectionExpression fromf = SqlAliasedCollectionExpression.Create(
                SqlInputPathCollection.Create(
                    SqlIdentifier.Create("f"),
                    relativePath: null),
                alias: null);

            SqlArrayIteratorCollectionExpression cInFDotChildren = SqlArrayIteratorCollectionExpression.Create(
                SqlIdentifier.Create("c"),
                SqlInputPathCollection.Create(
                    SqlIdentifier.Create("f"),
                    SqlStringPathExpression.Create(
                        null,
                        SqlStringLiteral.Create("children"))));

            SqlArrayIteratorCollectionExpression pInCDotPets = SqlArrayIteratorCollectionExpression.Create(
                SqlIdentifier.Create("p"),
                SqlInputPathCollection.Create(
                    SqlIdentifier.Create("c"),
                    SqlStringPathExpression.Create(
                        null,
                        SqlStringLiteral.Create("pets"))));

            SqlJoinCollectionExpression joinFCP = SqlJoinCollectionExpression.Create(
                SqlJoinCollectionExpression.Create(fromf, cInFDotChildren),
                pInCDotPets);

            SqlFromClause fromClause = SqlFromClause.Create(joinFCP);

            SqlQuery query = SqlQuery.Create(
                selectClause,
                fromClause,
                whereClause: null,
                groupByClause: null,
                orderByClause: null,
                offsetLimitClause: null);

            CosmosElement result1 = CosmosElement.Parse(@"{
                ""familyName"": ""AndersenFamily"",
                ""childFirstName"": ""Henriette Thaulow"",
                ""petName"": ""Fluffy""
            }");
            CosmosElement result2 = CosmosElement.Parse(@"{
                ""familyName"": ""WakefieldFamily"",
                ""childGivenName"": ""Jesse"",
                ""petName"": ""Goofy""
            }");
            CosmosElement result3 = CosmosElement.Parse(@"{
                ""familyName"": ""WakefieldFamily"",
                ""childGivenName"": ""Jesse"",
                ""petName"": ""Shadow""
            }");

            AssertEvaluation(
                new CosmosElement[] { result1, result2, result3 },
                query,
                new CosmosElement[] { AndersenFamily, WakefieldFamily });


            // SELECT VALUE givenName
            // FROM c
            // JOIN ( SELECT VALUE c.id FROM c ) AS givenName
            SqlQuery joinOnSubquery = SqlQuery.Create(
                SqlSelectClause.Create(
                    SqlSelectValueSpec.Create(
                        SqlPropertyRefScalarExpression.Create(
                            null,
                            SqlIdentifier.Create("givenName")))),
                SqlFromClause.Create(
                    SqlJoinCollectionExpression.Create(
                        SqlAliasedCollectionExpression.Create(
                            SqlInputPathCollection.Create(
                                SqlIdentifier.Create("c"), null), null),
                        SqlAliasedCollectionExpression.Create(
                            SqlSubqueryCollection.Create(
                                SqlQuery.Create(
                                    SqlSelectClause.Create(
                                        SqlSelectValueSpec.Create(
                                            TestUtils.CreatePathExpression("c", "id"))),
                                    FromClause,
                                    whereClause: null,
                                    groupByClause: null,
                                    orderByClause: null,
                                    offsetLimitClause: null)),
                            SqlIdentifier.Create("givenName")))),
                whereClause: null,
                groupByClause: null,
                orderByClause: null,
                offsetLimitClause: null);

            AssertEvaluation(
                new CosmosElement[] { AndersenFamily["id"], WakefieldFamily["id"] },
                joinOnSubquery,
                new CosmosElement[] { AndersenFamily, WakefieldFamily });
        }

        private static SqlQuery CreateQueryWithSelectClause(SqlSelectClause selectClause)
        {
            return SqlQuery.Create(
                selectClause,
                FromClause,
                whereClause: null,
                groupByClause: null,
                orderByClause: null,
                offsetLimitClause: null);
        }

        [TestMethod]
        [Owner("brchon")]
        public void WhereClauseTest()
        {
            SqlScalarExpression cDotId = TestUtils.CreatePathExpression("c", "id");

            SqlLiteralScalarExpression WakefieldFamilyString = SqlLiteralScalarExpression.Create(SqlStringLiteral.Create("WakefieldFamily"));

            SqlBinaryScalarExpression filter = SqlBinaryScalarExpression.Create(
                SqlBinaryScalarOperatorKind.Equal,
                cDotId,
                WakefieldFamilyString);

            SqlQuery selectStar = SqlQuery.Create(
                SqlSelectClause.SelectStar,
                FromClause,
                SqlWhereClause.Create(filter),
                groupByClause: null,
                orderByClause: null,
                offsetLimitClause: null);
            AssertEvaluation(new CosmosElement[] { WakefieldFamily, WakefieldFamily }, selectStar, DataSource);

            SqlLiteralScalarExpression whereUndefined = SqlLiteralScalarExpression.Create(SqlUndefinedLiteral.Create());

            SqlQuery selectStarWhereUndefined = SqlQuery.Create(
                SqlSelectClause.SelectStar,
                FromClause,
                SqlWhereClause.Create(whereUndefined),
                groupByClause: null,
                orderByClause: null,
                offsetLimitClause: null);
            AssertEvaluation(new CosmosElement[] { }, selectStarWhereUndefined, DataSource);
        }

        [TestMethod]
        [Owner("brchon")]
        public void GroupByClauseTest()
        {
            CosmosObject adam = CosmosObject.Parse(@"{
                ""id"":""01"",
                ""_rid"":""AYIMAMmFOw8YAAAAAAAAAA=="",
                ""Name"":""Adam"",
                ""Age"":23,
                ""Income"":25000,
                ""Children"":[
                    {
                        ""Name"":""Ian"",
                        ""Age"":12,
                        ""Scores"":{
                            ""Math"":[
                                89,
                                72,
                                92
                            ],
                            ""English"":[
                                91,
                                77
                            ],
                            ""History"":[
                                98,
                                84
                            ]
                        }
                    }
                ]
            }");

            CosmosObject stacy = CosmosObject.Parse(@"{
                ""id"":""02"",
                ""_rid"":""AYIMAMmFOw8YAAAAAAAAAA=="",
                ""Name"":""Stacy"",
                ""Age"":17,
                ""Income"":100000,
                ""Children"":null
            }");

            CosmosObject john = CosmosObject.Parse(@"{
                ""id"":""03"",
                ""_rid"":""AYIMAMmFOw8YAAAAAAAAAA=="",
                ""Name"":""John"",
                ""Age"":31,
                ""Children"":[
                    {
                        ""Name"":""Mike"",
                        ""Age"":7,
                        ""Scores"":{
                            ""Math"":[
                                80,
                                99,
                                97
                            ],
                            ""English"":[
                                75,
                                79
                            ]
                        }
                    },
                    {
                        ""Name"":""Candy"",
                        ""Age"":11,
                        ""Scores"":{
                            ""Math"":[
                                100,
                                97
                            ],
                            ""History"":[
                                88,
                                90
                            ]
                        }
                    },
                    {
                        ""Name"":""Jack"",
                        ""Age"":10,
                        ""Scores"":{
                            ""English"":[
                                68,
                                89
                            ]
                        }
                    }
                ]
            }");

            Dictionary<string, CosmosElement> adamTwoProperties = new Dictionary<string, CosmosElement>();
            foreach (KeyValuePair<string, CosmosElement> kvp in adam)
            {
                adamTwoProperties[kvp.Key] = kvp.Value;
            }
            adamTwoProperties["id"] = CosmosString.Create(4.ToString());
            adamTwoProperties["Age"] = stacy["Age"];
            adamTwoProperties["Income"] = stacy["Income"];
            CosmosObject adam2 = CosmosObject.Create(adamTwoProperties);

            Dictionary<string, CosmosElement> stacyTwoProperties = new Dictionary<string, CosmosElement>();
            foreach (KeyValuePair<string, CosmosElement> kvp in stacy)
            {
                adamTwoProperties[kvp.Key] = kvp.Value;
            }
            stacyTwoProperties["id"] = CosmosString.Create(5.ToString());
            stacyTwoProperties["Age"] = john["Age"];
            CosmosObject stacy2 = CosmosObject.Create(adamTwoProperties);

            CosmosElement[] dataset = new CosmosElement[] { adam, stacy, john, adam2, stacy2 };

            // Simple GROUP BY with no aggregates
            // SELECT c.Name
            // FROM c
            // GROUP BY c.Name

            SqlScalarExpression cDotName = TestUtils.CreatePathExpression("c", "Name");
            SqlSelectClause selectCDotName = SqlSelectClause.Create(
                SqlSelectListSpec.Create(SqlSelectItem.Create(cDotName)));
            SqlGroupByClause groupByCDotName = SqlGroupByClause.Create(
                cDotName);
            SqlQuery groupByWithNoAggregates = SqlQuery.Create(
                selectClause: selectCDotName,
                fromClause: FromClause,
                whereClause: null,
                groupByClause: groupByCDotName,
                orderByClause: null,
                offsetLimitClause: null);
            AssertEvaluationNoOrder(
                new CosmosElement[]
                {
                    CosmosObject.Create(new Dictionary<string, CosmosElement>()
                    {
                        {"Name", adam["Name"]}
                    }),
                    CosmosObject.Create(new Dictionary<string, CosmosElement>()
                    {
                        {"Name", stacy["Name"]}
                    }),
                    CosmosObject.Create(new Dictionary<string, CosmosElement>()
                    {
                        {"Name", john["Name"]}
                    }),
                },
                groupByWithNoAggregates,
                dataset);

            // GROUP BY with aggregate
            // SELECT c.Name, Count(1) AS Count, Avg(c.Age) AS AverageAge,
            // FROM c
            // GROUP BY c.Name

            SqlFunctionCallScalarExpression countOne = SqlFunctionCallScalarExpression.Create(
                SqlFunctionCallScalarExpression.Identifiers.Count,
                false,
                SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(1)));

            SqlFunctionCallScalarExpression averageAge = SqlFunctionCallScalarExpression.Create(
                SqlFunctionCallScalarExpression.Identifiers.Avg,
                false,
                TestUtils.CreatePathExpression("c", "Age"));

            SqlSelectClause selectNameCountAverage = SqlSelectClause.Create(
                SqlSelectListSpec.Create(
                    SqlSelectItem.Create(cDotName),
                    SqlSelectItem.Create(countOne, SqlIdentifier.Create("Count")),
                    SqlSelectItem.Create(averageAge, SqlIdentifier.Create("AverageAge"))));

            SqlQuery groupByWithAggregates = SqlQuery.Create(
                selectClause: selectNameCountAverage,
                fromClause: FromClause,
                whereClause: null,
                groupByClause: groupByCDotName,
                orderByClause: null,
                offsetLimitClause: null);
            AssertEvaluationNoOrder(
                new CosmosElement[]
                {
                    CosmosObject.Create(new Dictionary<string, CosmosElement>()
                    {
                        {"Name", adam["Name"]},
                        {"Count", CosmosNumber64.Create(2)},
                        {"AverageAge", CosmosNumber64.Create((Number64.ToDouble(((CosmosNumber)adam["Age"]).Value) + Number64.ToDouble(((CosmosNumber)adam2["Age"]).Value)) / 2)},
                    }),
                    CosmosObject.Create(new Dictionary<string, CosmosElement>()
                    {
                        {"Name", stacy["Name"]},
                        {"Count", CosmosNumber64.Create(2)},
                        {"AverageAge", CosmosNumber64.Create((Number64.ToDouble(((CosmosNumber)stacy["Age"]).Value) + Number64.ToDouble(((CosmosNumber)stacy2["Age"]).Value)) / 2)},
                    }),
                    CosmosObject.Create(new Dictionary<string, CosmosElement>()
                    {
                        {"Name", john["Name"]},
                        {"Count", CosmosNumber64.Create(1)},
                        {"AverageAge", john["Age"]},
                    }),
                },
                groupByWithAggregates,
                dataset);

            // GROUP BY arbitrary scalar expressions
            // SELECT UPPER(c.Name) AS Name
            // FROM c
            // GROUP BY UPPER(c.Name)

            SqlFunctionCallScalarExpression upperCDotName = SqlFunctionCallScalarExpression.Create(
                SqlFunctionCallScalarExpression.Identifiers.Upper,
                false,
                cDotName);
            SqlSelectClause selectUpperCDotName = SqlSelectClause.Create(
                SqlSelectListSpec.Create(
                    SqlSelectItem.Create(
                        upperCDotName,
                        SqlIdentifier.Create("Name"))));
            SqlGroupByClause groupByUpperCDotName = SqlGroupByClause.Create(
                upperCDotName);
            SqlQuery groupByWithArbitaryScalarExpression = SqlQuery.Create(
                selectClause: selectUpperCDotName,
                fromClause: FromClause,
                whereClause: null,
                groupByClause: groupByUpperCDotName,
                orderByClause: null,
                offsetLimitClause: null);
            AssertEvaluationNoOrder(
                new CosmosElement[]
                {
                    CosmosObject.Create(new Dictionary<string, CosmosElement>()
                    {
                        {"Name", CosmosString.Create(((CosmosString)adam["Name"]).Value.ToString().ToUpperInvariant())}
                    }),
                    CosmosObject.Create(new Dictionary<string, CosmosElement>()
                    {
                        {"Name", CosmosString.Create(((CosmosString)stacy["Name"]).Value.ToString().ToUpperInvariant())}
                    }),
                    CosmosObject.Create(new Dictionary<string, CosmosElement>()
                    {
                        {"Name", CosmosString.Create(((CosmosString)john["Name"]).Value.ToString().ToUpperInvariant())}
                    }),
                },
                groupByWithArbitaryScalarExpression,
                dataset);
        }

        [TestMethod]
        [Owner("brchon")]
        public void OrderByClauseTest()
        {
            // Single Order By
            SqlScalarExpression cDotId = TestUtils.CreatePathExpression("c", "id");

            SqlQuery idAsc = CreateQueryWithOrderBy(SqlOrderByClause.Create(
                SqlOrderByItem.Create(cDotId, isDescending: false)));
            AssertEvaluation(new CosmosElement[] { AndersenFamily, AndersenFamily, WakefieldFamily, WakefieldFamily }, idAsc, DataSource);

            SqlQuery idDesc = CreateQueryWithOrderBy(SqlOrderByClause.Create(
                SqlOrderByItem.Create(cDotId, isDescending: true)));
            AssertEvaluation(new CosmosElement[] { WakefieldFamily, WakefieldFamily, AndersenFamily, AndersenFamily }, idDesc, DataSource);

            // Single Order By Undefined, Array, Object
            CosmosElement undefined = CosmosElement.Parse(@"{""_rid"": ""iDdHAJ74DSoBAAAAAAAAAA=="" }");
            CosmosElement array = CosmosElement.Parse(@"{""id"": [], ""_rid"": ""iDdHAJ74DSoCAAAAAAAAAA==""}");
            CosmosElement obj = CosmosElement.Parse(@"{""id"": {}, ""_rid"": ""iDdHAJ74DSoDAAAAAAAAAA==""}");
            CosmosElement[] undefinedArrayObject = new CosmosElement[]
            {
                array,
                undefined,
                obj,
            };

            SqlQuery orderByUndefinedArrayObject = CreateQueryWithOrderBy(SqlOrderByClause.Create(
                SqlOrderByItem.Create(cDotId, isDescending: false)));
            AssertEvaluation(new CosmosElement[] { array, obj }, orderByUndefinedArrayObject, undefinedArrayObject);

            // Order by Object
            CosmosElement objA = CosmosElement.Parse(@"{""id"": {""a"": ""a""}, ""_rid"": ""iDdHAJ74DSoEAAAAAAAAAA=="" }");
            CosmosElement objB = CosmosElement.Parse(@"{""id"": {""a"": ""b""}, ""_rid"": ""iDdHAJ74DSoFAAAAAAAAAA==""}");
            CosmosElement objC = CosmosElement.Parse(@"{""id"": {""a"": ""c""}, ""_rid"": ""iDdHAJ74DSoGAAAAAAAAAA==""}");
            CosmosElement[] objectData = new CosmosElement[]
            {
                obj,
                objA,
                objB,
                objC,
            };

            SqlQuery orderByObject = CreateQueryWithOrderBy(SqlOrderByClause.Create(
                SqlOrderByItem.Create(cDotId, isDescending: false)));
            AssertEvaluation(new CosmosElement[] { objA, obj, objB, objC }, orderByObject, objectData);

            // Order by Array
            CosmosElement arrayA = CosmosElement.Parse(@"{""id"": [""a""], ""_rid"": ""iDdHAJ74DSoEAAAAAAAAAA=="" }");
            CosmosElement arrayAB = CosmosElement.Parse(@"{""id"": [""a"", ""b""], ""_rid"": ""iDdHAJ74DSoFAAAAAAAAAA==""}");
            CosmosElement arrayABC = CosmosElement.Parse(@"{""id"": [""a"", ""b"", ""c""], ""_rid"": ""iDdHAJ74DSoGAAAAAAAAAA==""}");
            CosmosElement[] arrayData = new CosmosElement[]
            {
                array,
                arrayA,
                arrayAB,
                arrayABC,
            };

            SqlQuery orderByArray = CreateQueryWithOrderBy(SqlOrderByClause.Create(
                SqlOrderByItem.Create(cDotId, isDescending: false)));
            AssertEvaluation(new CosmosElement[] { arrayA, array, arrayABC, arrayAB }, orderByArray, arrayData);

            // Rid Tie Break Within a Partition
            CosmosObject rid1 = CosmosObject.Parse(@"{""id"": ""A"", ""_rid"": ""iDdHAJ74DSoBAAAAAAAAAA=="" }");
            CosmosObject rid2 = CosmosObject.Parse(@"{""id"": ""A"", ""_rid"": ""iDdHAJ74DSoCAAAAAAAAAA==""}");
            CosmosObject rid3 = CosmosObject.Parse(@"{""id"": ""A"", ""_rid"": ""iDdHAJ74DSoDAAAAAAAAAA==""}");
            CosmosElement[] ridTieBreakData = new CosmosElement[]
            {
                rid1,
                rid3,
                rid2,
            };

            SqlQuery ridTieBreakAsc = CreateQueryWithOrderBy(SqlOrderByClause.Create(
                SqlOrderByItem.Create(cDotId, isDescending: false)));
            AssertEvaluation(new CosmosElement[] { rid1, rid2, rid3 }, ridTieBreakAsc, ridTieBreakData);

            SqlQuery ridTieBreakDesc = CreateQueryWithOrderBy(SqlOrderByClause.Create(
                SqlOrderByItem.Create(cDotId, isDescending: true)));
            AssertEvaluation(new CosmosElement[] { rid3, rid2, rid1 }, ridTieBreakDesc, ridTieBreakData);

            // Tie Break Across Partions and rids
            PartitionKeyRange pk1 = new PartitionKeyRange()
            {
                MinInclusive = "0",
                MaxExclusive = "1",
            };
            PartitionKeyRange pk2 = new PartitionKeyRange()
            {
                MinInclusive = "1",
                MaxExclusive = "2",
            };

            Dictionary<string, PartitionKeyRange> ridToPartitionKeyRange = new Dictionary<string, PartitionKeyRange>()
            {
                { ((CosmosString)rid3["_rid"]).Value, pk1 },
                { ((CosmosString)rid2["_rid"]).Value, pk2 },
                { ((CosmosString)rid1["_rid"]).Value, pk2 },
            };
            _ = CreateQueryWithOrderBy(SqlOrderByClause.Create(
                SqlOrderByItem.Create(cDotId, isDescending: false)));
            AssertEvaluation(new CosmosElement[] { rid3, rid1, rid2 }, ridTieBreakAsc, ridTieBreakData, ridToPartitionKeyRange);
            _ = CreateQueryWithOrderBy(SqlOrderByClause.Create(
                SqlOrderByItem.Create(cDotId, isDescending: true)));
            AssertEvaluation(new CosmosElement[] { rid3, rid2, rid1 }, ridTieBreakDesc, ridTieBreakData, ridToPartitionKeyRange);

            // Mixed Type Order By
            CosmosElement numberField = CosmosElement.Parse(@"{""id"": 0, ""_rid"": ""iDdHAJ74DSoBAAAAAAAAAA=="" }");
            CosmosElement stringField = CosmosElement.Parse(@"{""id"": ""0"", ""_rid"": ""iDdHAJ74DSoCAAAAAAAAAA==""}");
            CosmosElement trueField = CosmosElement.Parse(@"{""id"": true, ""_rid"": ""iDdHAJ74DSoDAAAAAAAAAA==""}");
            CosmosElement falseField = CosmosElement.Parse(@"{""id"": false, ""_rid"": ""iDdHAJ74DSoEAAAAAAAAAA==""}");
            CosmosElement nullField = CosmosElement.Parse(@"{""id"": null, ""_rid"": ""iDdHAJ74DSoFAAAAAAAAAA==""}");
            CosmosElement undefinedField = CosmosElement.Parse(@"{""_rid"": ""iDdHAJ74DSoGAAAAAAAAAA==""}");
            CosmosElement arrayField = CosmosElement.Parse(@"{""id"": [], ""_rid"": ""iDdHAJ74DSoHAAAAAAAAAA==""}");
            CosmosElement objectField = CosmosElement.Parse(@"{""id"": {}, ""_rid"": ""iDdHAJ74DSoIAAAAAAAAAA==""}");
            CosmosElement[] mixedTypeData = new CosmosElement[]
            {
                numberField,
                stringField,
                nullField,
                trueField,
                falseField,
                undefinedField,
                arrayField,
                objectField
            };

            SqlQuery mixedTypeAsc = CreateQueryWithOrderBy(SqlOrderByClause.Create(
                SqlOrderByItem.Create(cDotId, isDescending: false)));
            AssertEvaluation(new CosmosElement[]
            {
                nullField,
                falseField,
                trueField,
                numberField,
                stringField,
                arrayField,
                objectField,
            }, mixedTypeAsc, mixedTypeData);

            SqlQuery mixedTypeDesc = CreateQueryWithOrderBy(SqlOrderByClause.Create(
                SqlOrderByItem.Create(cDotId, isDescending: true)));
            AssertEvaluation(new CosmosElement[]
            {
                objectField,
                arrayField,
                stringField,
                numberField,
                trueField,
                falseField,
                nullField,
            }, mixedTypeDesc, mixedTypeData);

            // Multi Order By
            CosmosElement john29 = CosmosElement.Parse(@"{ ""name"" : ""John"", ""age"" : 29, ""_rid"" : ""iDdHAJ74DSoBAAAAAAAAAA=="" }");
            CosmosElement john32 = CosmosElement.Parse(@"{ ""name"" : ""John"", ""age"" : 32, ""_rid"" : ""iDdHAJ74DSoCAAAAAAAAAA=="" }");
            CosmosElement adam23 = CosmosElement.Parse(@"{ ""name"" : ""Adam"", ""age"" : 23, ""_rid"" : ""iDdHAJ74DSoDAAAAAAAAAA=="" }");
            CosmosElement adam40 = CosmosElement.Parse(@"{ ""name"" : ""Adam"", ""age"" : 40, ""_rid"" : ""iDdHAJ74DSoEAAAAAAAAAA=="" }");
            CosmosElement adamUndefinedAge = CosmosElement.Parse(@"{ ""name"" : ""Adam"", ""_rid"" : ""iDdHAJ74DSoFAAAAAAAAAA==""}");
            CosmosElement[] multiOrderByData = new CosmosElement[] { john29, john32, adam23, adam40, adamUndefinedAge };

            SqlScalarExpression cDotName = TestUtils.CreatePathExpression("c", "name");

            SqlScalarExpression cDotAge = TestUtils.CreatePathExpression("c", "age");

            SqlQuery multiOrderByAscAsc = CreateQueryWithOrderBy(SqlOrderByClause.Create(
                SqlOrderByItem.Create(cDotName, isDescending: false),
                SqlOrderByItem.Create(cDotAge, isDescending: false)));
            AssertEvaluation(
                new CosmosElement[] { adamUndefinedAge, adam23, adam40, john29, john32 },
                multiOrderByAscAsc,
                multiOrderByData);

            SqlQuery multiOrderByAscDesc = CreateQueryWithOrderBy(SqlOrderByClause.Create(
                SqlOrderByItem.Create(cDotName, isDescending: false),
                SqlOrderByItem.Create(cDotAge, isDescending: true)));
            AssertEvaluation(
                new CosmosElement[] { adam40, adam23, adamUndefinedAge, john32, john29 },
                multiOrderByAscDesc,
                multiOrderByData);

            SqlQuery multiOrderByDescAsc = CreateQueryWithOrderBy(SqlOrderByClause.Create(
                SqlOrderByItem.Create(cDotName, isDescending: true),
                SqlOrderByItem.Create(cDotAge, isDescending: false)));
            AssertEvaluation(
                new CosmosElement[] { john29, john32, adamUndefinedAge, adam23, adam40 },
                multiOrderByDescAsc,
                multiOrderByData);

            SqlQuery multiOrderByDescDesc = CreateQueryWithOrderBy(SqlOrderByClause.Create(
                SqlOrderByItem.Create(cDotName, isDescending: true),
                SqlOrderByItem.Create(cDotAge, isDescending: true)));
            AssertEvaluation(
                new CosmosElement[] { john32, john29, adam40, adam23, adamUndefinedAge },
                multiOrderByDescDesc,
                multiOrderByData);
        }

        [TestMethod]
        [Owner("brchon")]
        public void AggregateTest()
        {
            CosmosElement[] emptyDataset = new CosmosElement[] { };

            CosmosObject doc1 = CosmosObject.Parse(@"{""field"" : 1, ""_rid"" : ""iDdHAJ74DSoCAAAAAAAAAA=="" }");
            CosmosObject doc2 = CosmosObject.Parse(@"{""field"" : 2, ""_rid"" : ""iDdHAJ74DSoCAAAAAAAAAA=="" }");
            CosmosObject doc3 = CosmosObject.Parse(@"{""field"" : 3, ""_rid"" : ""iDdHAJ74DSoCAAAAAAAAAA=="" }");
            CosmosObject doc4 = CosmosObject.Parse(@"{""field"" : 4, ""_rid"" : ""iDdHAJ74DSoCAAAAAAAAAA=="" }");

            CosmosElement[] simpleDataset = new CosmosElement[] { doc1, doc2, doc3, doc4 };

            Dictionary<string, CosmosElement> arrayDocProperties = new Dictionary<string, CosmosElement>();
            CosmosArray arrayField = CosmosArray.Create(
                new List<CosmosElement>()
                {
                    CosmosNumber64.Create(1),
                    CosmosNumber64.Create(2),
                    CosmosNumber64.Create(3)
                });
            arrayDocProperties["field"] = arrayField;
            arrayDocProperties["_rid"] = CosmosString.Create("iDdHAJ74DSoCAAAAAAAAAA==");
            CosmosObject arrayDoc = CosmosObject.Create(arrayDocProperties);

            CosmosObject docWithUndefined = CosmosObject.Parse(@"{""_rid"" : ""iDdHAJ74DSoCAAAAAAAAAA=="" }");
            CosmosObject stringFieldDoc = CosmosObject.Parse(@"{""field"" : ""hello"", ""_rid"" : ""iDdHAJ74DSoCAAAAAAAAAA=="" }");

            CosmosElement[] arrayOnlyDataset = new CosmosElement[] { arrayDoc };
            CosmosElement[] primitiveAndNonPrimitiveDataset = new CosmosElement[] { doc1, doc2, arrayDoc, doc3, doc4 };
            CosmosElement[] dataSetWithUndefined = new CosmosElement[] { doc1, doc2, docWithUndefined, doc3, doc4 };
            CosmosElement[] mixedTypeDataset = new CosmosElement[] { doc1, doc2, stringFieldDoc, doc3, doc4 };

            SqlScalarExpression cDotField = TestUtils.CreatePathExpression("c", "field");

            // SELECT VALUE MIN(c.field)
            double min = 1;
            SqlFunctionCallScalarExpression minCDotField = SqlFunctionCallScalarExpression.CreateBuiltin(SqlFunctionCallScalarExpression.Identifiers.Min, cDotField);

            SqlQuery selectValueMinCDotField = CreateQueryWithSelectClause(
                SqlSelectClause.Create(
                    SqlSelectValueSpec.Create(minCDotField)));

            AssertEvaluation(new CosmosElement[] { CosmosNumber64.Create(min) }, selectValueMinCDotField, simpleDataset);
            AssertEvaluation(new CosmosElement[] { CosmosNumber64.Create(min) }, selectValueMinCDotField, mixedTypeDataset);
            AssertEvaluation(new CosmosElement[] { CosmosNumber64.Create(min) }, selectValueMinCDotField, dataSetWithUndefined);
            AssertEvaluation(new CosmosElement[] { }, selectValueMinCDotField, arrayOnlyDataset);
            AssertEvaluation(new CosmosElement[] { }, selectValueMinCDotField, primitiveAndNonPrimitiveDataset);
            AssertEvaluation(new CosmosElement[] { }, selectValueMinCDotField, emptyDataset);

            // SELECT VALUE MAX(c.field)
            double max = 4;
            SqlFunctionCallScalarExpression maxCDotField = SqlFunctionCallScalarExpression.CreateBuiltin(SqlFunctionCallScalarExpression.Identifiers.Max, cDotField);

            SqlQuery selectValueMaxCDotField = CreateQueryWithSelectClause(
                SqlSelectClause.Create(
                    SqlSelectValueSpec.Create(maxCDotField)));

            AssertEvaluation(new CosmosElement[] { CosmosNumber64.Create(max) }, selectValueMaxCDotField, simpleDataset);
            AssertEvaluation(new CosmosElement[] { CosmosString.Create("hello") }, selectValueMaxCDotField, mixedTypeDataset);
            AssertEvaluation(new CosmosElement[] { CosmosNumber64.Create(max) }, selectValueMaxCDotField, dataSetWithUndefined);
            AssertEvaluation(new CosmosElement[] { }, selectValueMaxCDotField, arrayOnlyDataset);
            AssertEvaluation(new CosmosElement[] { }, selectValueMaxCDotField, primitiveAndNonPrimitiveDataset);
            AssertEvaluation(new CosmosElement[] { }, selectValueMaxCDotField, emptyDataset);

            // SELECT VALUE SUM(c.field)
            double sum = 1 + 2 + 3 + 4;
            SqlFunctionCallScalarExpression sumCDotField = SqlFunctionCallScalarExpression.CreateBuiltin(SqlFunctionCallScalarExpression.Identifiers.Sum, cDotField);

            SqlQuery selectValueSumCDotField = CreateQueryWithSelectClause(
                SqlSelectClause.Create(
                    SqlSelectValueSpec.Create(sumCDotField)));

            AssertEvaluation(new CosmosElement[] { CosmosNumber64.Create(sum) }, selectValueSumCDotField, simpleDataset);
            AssertEvaluation(new CosmosElement[] { }, selectValueSumCDotField, mixedTypeDataset);
            AssertEvaluation(new CosmosElement[] { CosmosNumber64.Create(sum) }, selectValueSumCDotField, dataSetWithUndefined);
            AssertEvaluation(new CosmosElement[] { }, selectValueSumCDotField, arrayOnlyDataset);
            AssertEvaluation(new CosmosElement[] { }, selectValueSumCDotField, primitiveAndNonPrimitiveDataset);
            AssertEvaluation(new CosmosElement[] { CosmosNumber64.Create(0) }, selectValueSumCDotField, emptyDataset);

            // SELECT VALUE COUNT(c.field)
            double count = 4;
            SqlFunctionCallScalarExpression countCDotField = SqlFunctionCallScalarExpression.CreateBuiltin(SqlFunctionCallScalarExpression.Identifiers.Count, cDotField);

            SqlQuery selectValueCountCDotField = CreateQueryWithSelectClause(
                SqlSelectClause.Create(
                    SqlSelectValueSpec.Create(countCDotField)));

            AssertEvaluation(new CosmosElement[] { CosmosNumber64.Create(count) }, selectValueCountCDotField, simpleDataset);
            AssertEvaluation(new CosmosElement[] { CosmosNumber64.Create(mixedTypeDataset.Count()) }, selectValueCountCDotField, mixedTypeDataset);
            AssertEvaluation(new CosmosElement[] { CosmosNumber64.Create(count) }, selectValueCountCDotField, dataSetWithUndefined);
            AssertEvaluation(new CosmosElement[] { CosmosNumber64.Create(1) }, selectValueCountCDotField, arrayOnlyDataset);
            AssertEvaluation(new CosmosElement[] { CosmosNumber64.Create(5) }, selectValueCountCDotField, primitiveAndNonPrimitiveDataset);
            AssertEvaluation(new CosmosElement[] { CosmosNumber64.Create(0) }, selectValueCountCDotField, emptyDataset);

            // SELECT VALUE AVG(c.field)
            double avg = sum / count;
            SqlFunctionCallScalarExpression avgCDotField = SqlFunctionCallScalarExpression.CreateBuiltin(SqlFunctionCallScalarExpression.Identifiers.Avg, cDotField);

            SqlQuery selectValueAvgCDotField = CreateQueryWithSelectClause(
                SqlSelectClause.Create(
                    SqlSelectValueSpec.Create(avgCDotField)));

            AssertEvaluation(new CosmosElement[] { CosmosNumber64.Create(avg) }, selectValueAvgCDotField, simpleDataset);
            AssertEvaluation(new CosmosElement[] { }, selectValueAvgCDotField, mixedTypeDataset);
            AssertEvaluation(new CosmosElement[] { CosmosNumber64.Create(avg) }, selectValueAvgCDotField, dataSetWithUndefined);
            AssertEvaluation(new CosmosElement[] { }, selectValueAvgCDotField, arrayOnlyDataset);
            AssertEvaluation(new CosmosElement[] { }, selectValueAvgCDotField, primitiveAndNonPrimitiveDataset);
            AssertEvaluation(new CosmosElement[] { }, selectValueAvgCDotField, emptyDataset);

            // SELECT VALUE AGGREGATE(c.field) + 5
            SqlBinaryScalarExpression maxPlusFive = SqlBinaryScalarExpression.Create(
                SqlBinaryScalarOperatorKind.Add,
                maxCDotField,
                SqlLiteralScalarExpression.Create(
                    SqlNumberLiteral.Create(5)));

            SqlQuery selectValueMaxCDotFieldPlusFive = CreateQueryWithSelectClause(
                SqlSelectClause.Create(
                    SqlSelectValueSpec.Create(maxPlusFive)));

            AssertEvaluation(new CosmosElement[] { CosmosNumber64.Create(max + 5) }, selectValueMaxCDotFieldPlusFive, simpleDataset);

            // SELECT VALUE AGGREGATE(BUILTIN(c.field))
            SqlFunctionCallScalarExpression maxSquare = SqlFunctionCallScalarExpression.CreateBuiltin(
                SqlFunctionCallScalarExpression.Identifiers.Max,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Square,
                        cDotField));

            SqlQuery selectValueMaxCDotFieldSquared = CreateQueryWithSelectClause(
                SqlSelectClause.Create(
                    SqlSelectValueSpec.Create(maxSquare)));

            AssertEvaluation(new CosmosElement[] { CosmosNumber64.Create(max * max) }, selectValueMaxCDotFieldSquared, simpleDataset);

            // SELECT VALUE AGGREGATE1(c.field) + AGGREGATE2(c.field)
            SqlBinaryScalarExpression minPlusMax = SqlBinaryScalarExpression.Create(
                SqlBinaryScalarOperatorKind.Add,
                maxCDotField,
                minCDotField);

            SqlQuery selectValueMinPlusMax = CreateQueryWithSelectClause(
                SqlSelectClause.Create(
                    SqlSelectValueSpec.Create(minPlusMax)));

            AssertEvaluation(new CosmosElement[] { CosmosNumber64.Create(min + max) }, selectValueMinPlusMax, simpleDataset);

            // SELECT AGGREGATE1(c.field) , AGGREGATE2(c.field)
            SqlQuery selectMinAndMax = CreateQueryWithSelectClause(
                SqlSelectClause.Create(
                    SqlSelectListSpec.Create(
                        SqlSelectItem.Create(minCDotField, SqlIdentifier.Create("min")),
                        SqlSelectItem.Create(maxCDotField, SqlIdentifier.Create("max")))));

            CosmosElement minAndMax = CosmosObject.Create(new Dictionary<string, CosmosElement>
            {
                ["min"] = CosmosNumber64.Create(min),
                ["max"] = CosmosNumber64.Create(max)
            });

            AssertEvaluation(new CosmosElement[] { minAndMax }, selectMinAndMax, simpleDataset);
        }

        [TestMethod]
        [Owner("brchon")]
        public void OffsetLimitTest()
        {
            foreach (int offset in new int[] { 0, 1, 2 })
            {
                foreach (int limit in new int[] { 0, 1, 2 })
                {
                    SqlQuery query = CreateQueryWithOffsetLimit(offset, limit);
                    AssertEvaluation(RidOrderedDataSource.Skip(offset).Take(limit), query, DataSource);
                }
            }
        }

        private static SqlQuery CreateQueryWithOffsetLimit(int offset, int limit)
        {
            return SqlQuery.Create(
                SqlSelectClause.SelectStar,
                FromClause,
                whereClause: null,
                groupByClause: null,
                orderByClause: null,
                offsetLimitClause: SqlOffsetLimitClause.Create(
                    SqlOffsetSpec.Create(SqlNumberLiteral.Create(offset)),
                    SqlLimitSpec.Create(SqlNumberLiteral.Create(limit))));
        }

        internal sealed class Scores
        {
            public Scores(int[] math, int[] english, int[] history)
            {
                this.Math = math;
                this.English = english;
                this.History = history;
            }

            public int[] Math { get; }
            public int[] English { get; }
            public int[] History { get; }
        }

        internal sealed class Child
        {
            public Child(string name, int age, Scores scores)
            {
                this.Name = name;
                this.Age = age;
                this.Scores = scores;
            }

            public string Name { get; }
            public int Age { get; }
            public Scores Scores { get; }
        }

        internal sealed class Person
        {
            public Person(string id, string name, int age, int income, Child[] children)
            {
                this.id = id;
                this.Name = name;
                this.Age = age;
                this.Income = income;
                this.Children = children;
            }

            public string id { get; }
            public string Name { get; }
            public int Age { get; }
            public int Income { get; }
            public Child[] Children { get; }
        }

        [TestMethod]
        [Owner("brchon")]
        public void SubqueryTest()
        {
            CosmosElement adam = CosmosElement.Parse(@"{""id"":""01"",""_rid"":""AYIMAMmFOw8YAAAAAAAAAA=="", ""Name"":""Adam"",""Age"":23,""Income"":25000,""Children"":[{""Name"":""Ian"",""Age"":12,""Scores"":{""Math"":[89,72,92],""English"":[91,77],""History"":[98,84]}}]}");
            CosmosElement stacy = CosmosElement.Parse(@"{ ""id"":""02"",""_rid"": ""AYIMAMmFOw8YAAAAAAAAAA=="",""Name"":""Stacy"",""Age"":17,""Income"":100000,""Children"":null}");
            CosmosElement john = CosmosElement.Parse(@"{""id"":""03"",""_rid"": ""AYIMAMmFOw8YAAAAAAAAAA=="",""Name"":""John"",""Age"":31,""Children"":[{""Name"":""Mike"",""Age"":7,""Scores"":{""Math"":[80,99,97],""English"":[75,79]}},{""Name"":""Candy"",""Age"":11,""Scores"":{""Math"":[100,97],""History"":[88,90]}},{""Name"":""Jack"",""Age"":10,""Scores"":{""English"":[68,89]}}]}");
            CosmosElement lucy = CosmosElement.Parse(@"{""id"":""04"",""_rid"": ""AYIMAMmFOw8YAAAAAAAAAA=="",""Name"":""Lucy"",""Age"":21}");
            CosmosElement bart = CosmosElement.Parse(@"{""id"":""05"",""_rid"": ""AYIMAMmFOw8YAAAAAAAAAA=="",""Name"":""Bart"",""Age"":38,""Income"":150000,""Children"":[{""Name"":""Tom"",""Age"":16,""Scores"":{""Math"":[71,55,64],""English"":[100,98,98]}}]}");
            CosmosElement susan = CosmosElement.Parse(@"{""id"":""06"",""_rid"": ""AYIMAMmFOw8YAAAAAAAAAA=="",""Name"":""Susan"",""Age"":28,""Income"":90000,""Children"":[{""Name"":""Lara"",""Age"":6,""Scores"":{""Math"":[84,91]}},{""Name"":""Dan"",""Age"":4,""Scores"":null}]}");
            CosmosElement chris = CosmosElement.Parse(@"{""id"":""07"",""_rid"": ""AYIMAMmFOw8YAAAAAAAAAA=="",""Name"":""Chris"",""Age"":33,""Income"":75000,""Children"":[{""Name"":""Ben"",""Age"":8,""Scores"":{""Math"":[79,81,76],""History"":[88,65]}}]}");
            CosmosElement mark = CosmosElement.Parse(@"{""id"":""08"",""_rid"": ""AYIMAMmFOw8YAAAAAAAAAA=="",""Name"":""Mark"",""Income"":175000,""Children"":[{""Name"":""Dan"",""Age"":14},{""Name"":""Sarah"",""Age"":12},{""Name"":""Lucy"",""Age"":9}]}");
            CosmosElement clint = CosmosElement.Parse(@"{""id"":""09"",""_rid"": ""AYIMAMmFOw8YAAAAAAAAAA=="",""Name"":""Clint"",""Age"":21,""Income"":55000}");
            CosmosElement lisa = CosmosElement.Parse(@"{""id"":""10"",""_rid"": ""AYIMAMmFOw8YAAAAAAAAAA=="",""Name"":""Lisa"",""Age"":39,""Income"":165000,""Children"":[{""Name"":""Pete"",""Age"":12,""Scores"":{""Math"":[99,99,97],""English"":[87,88,90]}},{""id"":""Cindy"",""Name"":""Cindy"",""Age"":7,""Scores"":{""Math"":[77,81,84],""English"":[95,96,99]}}]}");

            CosmosElement[] dataset = new CosmosElement[] { adam, stacy, john, lucy, bart, susan, chris, mark, clint, lisa };

            // SELECT VALUE(SELECT * FROM c) FROM c
            // Sanity test to see if we get back all the values
            SqlQuery selectStar = CreateQueryWithSelectClause(SqlSelectClause.SelectStar);
            SqlQuery selectStarWrapped = CreateQueryWithSelectClause(
                SqlSelectClause.Create(
                    SqlSelectValueSpec.Create(SqlSubqueryScalarExpression.Create(selectStar))));

            AssertEvaluation(dataset, selectStarWrapped, dataset);
        }

        private static SqlQuery CreateQueryWithOrderBy(SqlOrderByClause orderBy)
        {
            return SqlQuery.Create(
                SqlSelectClause.SelectStar,
                FromClause,
                whereClause: null,
                groupByClause: null,
                orderByClause: orderBy,
                offsetLimitClause: null);
        }

        private static void AssertEvaluation(
            IEnumerable<CosmosElement> expectedElements,
            SqlQuery sqlQuery,
            IEnumerable<CosmosElement> dataSource,
            IReadOnlyDictionary<string, PartitionKeyRange> ridToPartitionKeyRange = null)
        {
            List<CosmosElement> actual = SqlInterpreter.ExecuteQuery(
                dataSource,
                sqlQuery,
                ridToPartitionKeyRange).ToList();

            List<CosmosElement> expected = expectedElements.ToList();

            CollectionAssert.AreEqual(expected, actual);
        }

        private static void AssertEvaluationNoOrder(
            IEnumerable<CosmosElement> expected,
            SqlQuery sqlQuery,
            IEnumerable<CosmosElement> dataSource,
            IReadOnlyDictionary<string, PartitionKeyRange> ridToPartitionKeyRange = null)
        {
            IEnumerable<CosmosElement> actual = SqlInterpreter.ExecuteQuery(
                dataSource,
                sqlQuery,
                ridToPartitionKeyRange);
            if (expected.Count() != actual.Count())
            {
                Assert.Fail($"Expected had {expected.Count()} results while Actual has {actual.Count()} results.");
            }

            HashSet<CosmosElement> expectedNoOrder = new HashSet<CosmosElement>(expected);
            HashSet<CosmosElement> actualNoOrder = new HashSet<CosmosElement>(actual);
            Assert.IsTrue(
                expectedNoOrder.SetEquals(actualNoOrder),
                $@"Expected: {JsonConvert.SerializeObject(expected, Formatting.Indented)}
                  Actual: {JsonConvert.SerializeObject(actual, Formatting.Indented)}");
        }
    }
}