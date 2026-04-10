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
    using Microsoft.Azure.Cosmos.Tests.Query.OfflineEngine;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// CosmosElementEqualityComparerUnitTests Class.
    /// </summary>
    [TestClass]
    public class DataSourceEvaluatorTests
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
          ""_rid"": ""0fomAIxnukU1AQAAAAAAAA==""
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
          ""_rid"": ""0fomAIxnukU1AQAAAAAAAB==""
        }");

        private static readonly CosmosElement[] DataSource = new CosmosElement[]
        {
            AndersenFamily,
            WakefieldFamily,
        };

        [TestMethod]
        [Owner("brchon")]
        public void AliasedCollectionExpressionTest()
        {
            // FROM c
            SqlAliasedCollectionExpression fromC = SqlAliasedCollectionExpression.Create(
                SqlInputPathCollection.Create(
                    SqlIdentifier.Create("c"),
                    relativePath: null),
                alias: null);

            CosmosObject andersenWrapped = CosmosObject.Create(new Dictionary<string, CosmosElement>
            {
                ["c"] = AndersenFamily,
                ["_rid"] = AndersenFamily["_rid"]
            });

            CosmosObject wakeFieldWrapped = CosmosObject.Create(new Dictionary<string, CosmosElement>
            {
                ["c"] = WakefieldFamily,
                ["_rid"] = WakefieldFamily["_rid"]
            });

            AssertEvaluation(new CosmosElement[] { andersenWrapped, wakeFieldWrapped }, fromC, DataSource);

            // FROM c.id
            SqlAliasedCollectionExpression fromCDotId = SqlAliasedCollectionExpression.Create(
                SqlInputPathCollection.Create(
                    SqlIdentifier.Create("c"),
                    SqlIdentifierPathExpression.Create(
                        null,
                        SqlIdentifier.Create("id"))),
                alias: null);

            CosmosObject andersenId = CosmosObject.Create(new Dictionary<string, CosmosElement>
            {
                ["id"] = CosmosString.Create("AndersenFamily"),
                ["_rid"] = AndersenFamily["_rid"]
            });

            CosmosObject wakefieldId = CosmosObject.Create(new Dictionary<string, CosmosElement>
            {
                ["id"] = CosmosString.Create("WakefieldFamily"),
                ["_rid"] = WakefieldFamily["_rid"]
            });

            AssertEvaluation(new CosmosElement[] { andersenId, wakefieldId }, fromCDotId, DataSource);

            // FROM c.id AS familyId
            SqlAliasedCollectionExpression fromCDotIdAsFamilyId = SqlAliasedCollectionExpression.Create(
                SqlInputPathCollection.Create(
                    SqlIdentifier.Create("c"),
                    SqlIdentifierPathExpression.Create(
                        null,
                        SqlIdentifier.Create("id"))),
                SqlIdentifier.Create("familyId"));

            CosmosObject andersenIdAsFamilyId = CosmosObject.Create(new Dictionary<string, CosmosElement>
            {
                ["familyId"] = CosmosString.Create("AndersenFamily"),
                ["_rid"] = AndersenFamily["_rid"]
            });

            CosmosObject wakefieldIdAsFamilyId = CosmosObject.Create(new Dictionary<string, CosmosElement>
            {
                ["familyId"] = CosmosString.Create("WakefieldFamily"),
                ["_rid"] = WakefieldFamily["_rid"]
            });

            AssertEvaluation(new CosmosElement[] { andersenIdAsFamilyId, wakefieldIdAsFamilyId }, fromCDotIdAsFamilyId, DataSource);

            // FROM (SELECT VALUE child["grade"] FROM child IN c.children) grade
            SqlAliasedCollectionExpression fromSubqueryGrades = SqlAliasedCollectionExpression.Create(
                SqlSubqueryCollection.Create(
                    SqlQuery.Create(
                        SqlSelectClause.Create(
                            SqlSelectValueSpec.Create(
                                TestUtils.CreatePathExpression("child", "grade"))),
                        SqlFromClause.Create(
                            SqlArrayIteratorCollectionExpression.Create(
                                SqlIdentifier.Create("child"),
                                SqlInputPathCollection.Create(
                                    SqlIdentifier.Create("c"),
                                    SqlStringPathExpression.Create(
                                        null,
                                        SqlStringLiteral.Create("children"))))),
                        whereClause: null,
                        groupByClause: null,
                        orderByClause: null,
                        offsetLimitClause: null)),
                SqlIdentifier.Create("grade"));

            CosmosObject henrietteWrapped = CosmosObject.Create(new Dictionary<string, CosmosElement>
            {
                ["grade"] = CosmosNumber64.Create(5),
                ["_rid"] = AndersenFamily["_rid"]
            });

            CosmosObject jesseWrapped = CosmosObject.Create(new Dictionary<string, CosmosElement>
            {
                ["grade"] = CosmosNumber64.Create(1),
                ["_rid"] = WakefieldFamily["_rid"]
            });

            CosmosObject lisaWrapped = CosmosObject.Create(new Dictionary<string, CosmosElement>
            {
                ["grade"] = CosmosNumber64.Create(8),
                ["_rid"] = WakefieldFamily["_rid"]
            });

            AssertEvaluation(
                new CosmosElement[]
                {
                    henrietteWrapped,
                    jesseWrapped,
                    lisaWrapped
                },
                fromSubqueryGrades,
                DataSource);
        }

        [TestMethod]
        [Owner("brchon")]
        public void ArrayIteratorCollectionExpressionTest()
        {
            // FROM p in f.parents
            SqlArrayIteratorCollectionExpression fromPInFDotParents = SqlArrayIteratorCollectionExpression.Create(
                SqlIdentifier.Create("p"),
                SqlInputPathCollection.Create(
                    SqlIdentifier.Create("f"),
                    SqlStringPathExpression.Create(
                        null,
                        SqlStringLiteral.Create("parents"))));

            CosmosElement andersenParent1 = CosmosElement.Parse(@"{ ""p"" : { ""firstName"": ""Thomas"" }, ""_rid"": ""0fomAIxnukU1AQAAAAAAAA==""}");
            CosmosElement andersenParent2 = CosmosElement.Parse(@"{ ""p"" : { ""firstName"": ""Mary Kay""}, ""_rid"": ""0fomAIxnukU1AQAAAAAAAA=="" }");
            CosmosElement wakeFieldParent1 = CosmosElement.Parse(@"{ ""p"" : { ""familyName"": ""Wakefield"", ""givenName"": ""Robin"" }, ""_rid"": ""0fomAIxnukU1AQAAAAAAAB=="" }");
            CosmosElement wakeFieldParent2 = CosmosElement.Parse(@"{ ""p"" : { ""familyName"": ""Miller"", ""givenName"": ""Ben"" }, ""_rid"": ""0fomAIxnukU1AQAAAAAAAB=="" }");

            AssertEvaluation(new CosmosElement[]
            {
                andersenParent1,
                andersenParent2,
                wakeFieldParent1,
                wakeFieldParent2,
            }, fromPInFDotParents, DataSource);
        }

        [TestMethod]
        [Owner("brchon")]
        public void JoinCollectionExpressionTest()
        {
            // FROM f
            // JOIN c IN f.children
            // JOIN p in c.pets
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

            SqlJoinCollectionExpression joinFC = SqlJoinCollectionExpression.Create(
               fromf,
               cInFDotChildren);

            CosmosElement andersenChild1 = CosmosElement.Parse(@"
            {
              ""f"": {
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
                  ""_rid"": ""0fomAIxnukU1AQAAAAAAAA==""
                },
              ""c"": {
                         ""firstName"": ""Henriette Thaulow"",
                         ""gender"": ""female"",
                         ""grade"": 5,
                         ""pets"": [{ ""givenName"": ""Fluffy"" }]
                     }, ""_rid"": ""0fomAIxnukU1AQAAAAAAAA==""
            }");

            CosmosElement wakeFieldChild1 = CosmosElement.Parse(@"
            {
              ""f"": {
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
                  ""_rid"": ""0fomAIxnukU1AQAAAAAAAB==""
                },
              ""c"": {
                        ""familyName"": ""Merriam"",
                        ""givenName"": ""Jesse"",
                        ""gender"": ""female"", ""grade"": 1,
                        ""pets"": [
                            { ""givenName"": ""Goofy"" },
                            { ""givenName"": ""Shadow"" }
                        ]
                      }, ""_rid"": ""0fomAIxnukU1AQAAAAAAAB==""
            }");

            CosmosElement wakeFieldChild2 = CosmosElement.Parse(@"
            {
              ""f"": {
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
                  ""_rid"": ""0fomAIxnukU1AQAAAAAAAB==""
                },
              ""c"": { 
                        ""familyName"": ""Miller"", 
                         ""givenName"": ""Lisa"", 
                         ""gender"": ""female"", 
                         ""grade"": 8 }, ""_rid"": ""0fomAIxnukU1AQAAAAAAAB==""
            }");

            AssertEvaluation(new CosmosElement[] { andersenChild1, wakeFieldChild1, wakeFieldChild2 }, joinFC, DataSource);

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

            CosmosElement andersenChild1Pet1 = CosmosElement.Parse(@"
            {
              ""f"": {
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
                  ""_rid"": ""0fomAIxnukU1AQAAAAAAAA==""
                },
              ""c"": {
                         ""firstName"": ""Henriette Thaulow"",
                         ""gender"": ""female"",
                         ""grade"": 5,
                         ""pets"": [{ ""givenName"": ""Fluffy"" }]
                     },
              ""p"": { ""givenName"": ""Fluffy"" }, 
              ""_rid"": ""0fomAIxnukU1AQAAAAAAAA==""
            }");

            CosmosElement wakeFieldChild1Pet1 = CosmosElement.Parse(@"
            {
              ""f"": {
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
                  ""_rid"": ""0fomAIxnukU1AQAAAAAAAB==""
                },
              ""c"": {
                        ""familyName"": ""Merriam"",
                        ""givenName"": ""Jesse"",
                        ""gender"": ""female"", ""grade"": 1,
                        ""pets"": [
                            { ""givenName"": ""Goofy"" },
                            { ""givenName"": ""Shadow"" }
                        ]
                      },
              ""p"": { ""givenName"": ""Goofy"" }, 
              ""_rid"": ""0fomAIxnukU1AQAAAAAAAB==""
            }");

            CosmosElement wakeFieldChild1Pet2 = CosmosElement.Parse(@"
            {
              ""f"": {
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
                  ""_rid"": ""0fomAIxnukU1AQAAAAAAAB==""
                },
              ""c"": {
                        ""familyName"": ""Merriam"",
                        ""givenName"": ""Jesse"",
                        ""gender"": ""female"", ""grade"": 1,
                        ""pets"": [
                            { ""givenName"": ""Goofy"" },
                            { ""givenName"": ""Shadow"" }
                        ]
                      },
              ""p"": { ""givenName"": ""Shadow"" }, 
              ""_rid"": ""0fomAIxnukU1AQAAAAAAAB==""
            }");

            AssertEvaluation(new CosmosElement[] { andersenChild1Pet1, wakeFieldChild1Pet1, wakeFieldChild1Pet2 }, joinFCP, DataSource);

            // SELECT c.id
            // FROM c
            // JOIN c.nonExistent

            SqlAliasedCollectionExpression c = SqlAliasedCollectionExpression.Create(
                SqlInputPathCollection.Create(
                    SqlIdentifier.Create("c"),
                    relativePath: null),
                alias: null);

            SqlAliasedCollectionExpression cDotNonExistent = SqlAliasedCollectionExpression.Create(
                SqlInputPathCollection.Create(
                    SqlIdentifier.Create("c"),
                    SqlStringPathExpression.Create(
                        null,
                        SqlStringLiteral.Create("nonExistent"))),
                alias: null);

            SqlJoinCollectionExpression joinCAndCDotNonExistent = SqlJoinCollectionExpression.Create(
               c,
               cDotNonExistent);

            AssertEvaluation(new CosmosElement[] { }, joinCAndCDotNonExistent, DataSource);
        }

        private static void AssertEvaluation(
            IEnumerable<CosmosElement> expected,
            SqlCollectionExpression collectionExpression,
            IEnumerable<CosmosElement> dataSource)
        {
            IEnumerable<CosmosElement> actual = collectionExpression.Accept(DataSourceEvaluator.Singleton, dataSource);
            if (expected.Count() != actual.Count())
            {
                Assert.Fail($"Expected had {expected.Count()} results while Actual has {actual.Count()} results.");
            }

            IEnumerable<Tuple<CosmosElement, CosmosElement>> expectedActuals = expected.Zip(actual, (first, second) => new Tuple<CosmosElement, CosmosElement>(first, second));
            foreach (Tuple<CosmosElement, CosmosElement> expectedActual in expectedActuals)
            {
                Assert.AreEqual(expectedActual.Item1, expectedActual.Item2);
            }
        }
    }
}