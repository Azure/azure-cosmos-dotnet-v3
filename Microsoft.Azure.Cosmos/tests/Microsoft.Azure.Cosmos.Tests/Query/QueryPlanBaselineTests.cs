namespace Microsoft.Azure.Cosmos.Tests.Query
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Xml;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Routing;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Test.BaselineTest;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Aggregate;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.OrderBy;

    /// <summary>
    /// Tests for <see cref="QueryPartitionProvider"/>.
    /// </summary>
    [TestClass]
    public class QueryPlanBaselineTests : BaselineTests<QueryPlanBaselineTestInput, QueryPlanBaselineTestOutput>
    {
        private static readonly IDictionary<string, object> DefaultQueryEngineConfiguration = new Dictionary<string, object>()
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
            {"maxSpatialQueryCells", 12},
            {"spatialMaxGeometryPointCount", 256},
            {"sqlDisableQueryILOptimization", false},
            {"sqlDisableFilterPlanOptimization", false}
        };

        private static readonly QueryPartitionProvider queryPartitionProvider = new QueryPartitionProvider(DefaultQueryEngineConfiguration);

        [TestMethod]
        [Owner("brchon")]
        public void Aggregates()
        {
            List<QueryPlanBaselineTestInput> testVariations = new List<QueryPlanBaselineTestInput>
            {
                Hash(
                @"Aggregate No Partition Key",
                @"SELECT VALUE AVG(c.blah) FROM c"),

                Hash(
                @"Aggregate No Partition Key And No Value",
                @"SELECT AVG(c.blah) FROM c"),

                Hash(
                @"AVG",
                @"SELECT VALUE AVG(c.blah) FROM c",
                @"/key"),

                Hash(
                @"MIN",
                @"SELECT VALUE MIN(c.blah) FROM c",
                @"/key"),

                Hash(
                @"MAX",
                @"SELECT VALUE MAX(c.blah) FROM c",
                @"/key"),

                Hash(
                @"SUM",
                @"SELECT VALUE SUM(c.blah) FROM c",
                @"/key"),

                Hash(
                @"COUNT Field",
                @"SELECT VALUE COUNT(c.blah) FROM c",
                @"/key"),

                Hash(
                @"COUNT 1",
                @"SELECT VALUE COUNT(1) FROM c",
                @"/key"),

                Hash(
                @"Aggregate with Complex Field",
                @"SELECT VALUE MIN(c.blah + 1) FROM c",
                @"/key"),

                Hash(
                @"Aggregate Partition Key Field",
                @"SELECT VALUE MIN(c.key) FROM c",
                @"/key"),

                Hash(
                @"Aggregate Partition Key Filter Hash Filter",
                @"SELECT VALUE MIN(c.blah) FROM c WHERE c.key = 1",
                @"/key"),

                Hash(
                @"Aggregate Partition Key Filter Range Filter",
                @"SELECT VALUE MIN(c.blah) FROM c WHERE c.key > 1",
                @"/key"),

                Hash(
                @"Aggregate With JOIN",
                @"SELECT VALUE AVG(c.age + 1) FROM Root r JOIN c in r.children WHERE c.age > 1 ORDER BY c.name",
                @"/a/c"),

                Hash(
                @"Aggregate With TOP",
                @"SELECT TOP 10 VALUE AVG(r.age + 1) FROM Root r",
                @"/a/c")
            };

            this.ExecuteTestSuite(testVariations);
        }

        [TestMethod]
        [Owner("brchon")]
        public void NonValueAggregates()
        {
            //-------------------------
            //      Positive
            //-------------------------
            List<string> aggregateFunctionNames = new List<string>()
            {
                "SUM",
                "COUNT",
                "MIN",
                "MAX",
                "AVG"
            };

            List<QueryPlanBaselineTestInput> testVariations = new List<QueryPlanBaselineTestInput>();

            foreach (string aggregateFunctionName in aggregateFunctionNames)
            {
                testVariations.Add(Hash(
                    $"Single Aggregate ({aggregateFunctionName}) Without 'VALUE' and Without alias.",
                    $"SELECT {aggregateFunctionName}(c.blah) FROM c"));
            }

            foreach (string aggregateFunctionName in aggregateFunctionNames)
            {
                testVariations.Add(Hash(
                    $"Single Aggregate ({aggregateFunctionName}) Without 'VALUE' and With alias.",
                    $"SELECT {aggregateFunctionName}(c.blah) as {aggregateFunctionName.ToLower()}_blah FROM c"));
            }

            foreach (string aggregateFunctionName in aggregateFunctionNames)
            {
                testVariations.Add(Hash(
                    $"Multiple Aggregates ({aggregateFunctionName}) With alias.",
                    $@"
                    SELECT 
                        {aggregateFunctionName}(c.blah) as {aggregateFunctionName.ToLower()}_blah, 
                        {aggregateFunctionName}(c.blah) as {aggregateFunctionName.ToLower()}_blah2 
                    FROM c"));
            }

            foreach (string aggregateFunctionName in aggregateFunctionNames)
            {
                testVariations.Add(Hash(
                    $"Multiple Aggregates ({aggregateFunctionName}) Without alias.",
                    $@"
                    SELECT 
                        {aggregateFunctionName}(c.blah), 
                        {aggregateFunctionName}(c.blah)
                    FROM c"));
            }

            foreach (string aggregateFunctionName in aggregateFunctionNames)
            {
                testVariations.Add(Hash(
                    $"Multiple Aggregates ({aggregateFunctionName}) mixed alias.",
                    $@"
                    SELECT 
                        {aggregateFunctionName}(c.blah) as {aggregateFunctionName.ToLower()}_blah, 
                        {aggregateFunctionName}(c.blah)
                    FROM c"));

                testVariations.Add(Hash(
                    $"Multiple Aggregates ({aggregateFunctionName}) mixed alias.",
                    $@"
                    SELECT 
                        {aggregateFunctionName}(c.blah),
                        {aggregateFunctionName}(c.blah) as {aggregateFunctionName.ToLower()}_blah
                    FROM c"));
            }

            foreach (string aggregateFunctionName in aggregateFunctionNames)
            {
                testVariations.Add(Hash(
                    $"Multiple Aggregates ({aggregateFunctionName}) interleaved aliases.",
                    $@"
                    SELECT 
                        {aggregateFunctionName}(c.blah) as {aggregateFunctionName.ToLower()}_blah, 
                        {aggregateFunctionName}(c.blah),
                        {aggregateFunctionName}(c.blah) as {aggregateFunctionName.ToLower()}_blah2
                    FROM c"));
            }

            testVariations.Add(Hash(
                $"Single Aggregate with partition key",
                $@"SELECT MIN(c.blah) FROM c WHERE c.pk = 1",
                "/pk"));

            testVariations.Add(Hash(
                $"Multiple Aggregate with partition key",
                $@"SELECT MIN(c.blah), MAX(c.blah) FROM c WHERE c.pk = 1",
                "/pk"));

            //-------------------------
            //      Negative
            //-------------------------
            testVariations.Add(Hash(
                $"Composite Single Aggregate Outer",
                $@"SELECT MIN(c.blah) / 2 FROM c"));

            testVariations.Add(Hash(
                $"Composite Single Aggregate Inner",
                $@"SELECT MIN(c.blah / 2) FROM c"));

            testVariations.Add(Hash(
                $"Composite Multiple Aggregates",
                $@"SELECT MIN(c.blah) + MAX(c.blah) FROM c"));

            testVariations.Add(Hash(
                $"Multiple Composite Aggregate Outer",
                $@"SELECT MIN(c.blah) / 2, MAX(c.blah) / 2 FROM c"));

            testVariations.Add(Hash(
                $"Multiple Composite Aggregate Inner",
                $@"SELECT MIN(c.blah / 2), MAX(c.blah / 2) FROM c"));

            testVariations.Add(Hash(
                $"Mixed Composite Aggregate",
                $@"SELECT MIN(c.blah), MAX(c.blah) / 2 FROM c"));

            this.ExecuteTestSuite(testVariations);
        }

        [TestMethod]
        [Owner("brchon")]
        public void Basic()
        {
            List<QueryPlanBaselineTestInput> testVariations = new List<QueryPlanBaselineTestInput>
            {
                Hash(
                @"Projection Only",
                @"SELECT 5",
                @"/a"),

                Hash(
                @"Projection And Top",
                @"SELECT TOP 2 5",
                @"/a"),

                Hash(
                @"All",
                @"SELECT * FROM c WHERE true",
                @"/a"),

                Hash(
                @"None",
                @"SELECT * FROM c WHERE false",
                @"/a"),

                Hash(
                @"Key Is True",
                @"SELECT * FROM Root r WHERE r.a.b.c",
                @"/a/b/c"),

                Hash(
                @"Key Is False",
                @"SELECT * FROM Root r WHERE not r.a.b.c",
                @"/a/b/c"),

                Hash(
                @"SELECT * NonPartitioned",
                @"SELECT * FROM c"),

                Hash(
                @"SELECT *",
                @"SELECT * FROM c",
                @"/a"),

                Hash(
                @"UDF",
                @"SELECT udf.func(1) as x FROM Root r",
                @"/a")
            };

            this.ExecuteTestSuite(testVariations);
        }

        [TestMethod]
        [Owner("brchon")]
        public void Bugs()
        {
            List<QueryPlanBaselineTestInput> testVariations = new List<QueryPlanBaselineTestInput>
            {
                Hash(
                @"ARRAY_CONTAINS With Filter (This fans out to every partition when it really should just go to 1, 2, 3, 4).",
                @"SELECT * FROM c WHERE ARRAY_CONTAINS([1, 2, 3], c.a) and c.a = 4",
                @"/a")
            };

            this.ExecuteTestSuite(testVariations);
        }

        [TestMethod]
        [Owner("brchon")]
        public void Distinct()
        {
            List<QueryPlanBaselineTestInput> testVariations = new List<QueryPlanBaselineTestInput>
            {
                Hash(
                @"No Distinct",
                @"SELECT * FROM c",
                @"/key"),

                Hash(
                @"Distinct No Partition Key",
                @"SELECT * FROM c"),

                Hash(
                @"Unordered Distinct",
                @"SELECT DISTINCT c.blah FROM c"),

                Hash(
                @"Ordered Distinct",
                @"SELECT DISTINCT VALUE c.blah FROM c ORDER BY c.blah")
            };

            this.ExecuteTestSuite(testVariations);
        }

        [TestMethod]
        [Owner("brchon")]
        public void GroupBy()
        {
            List<QueryPlanBaselineTestInput> testVariations = new List<QueryPlanBaselineTestInput>
            {

                //-------------------------
                //      Negative
                //-------------------------
                Hash(
                @"non simple aggregate",
                @"
                SELECT c.name, ABS(AVG(c.age)) as abs_avg_age
                FROM c
                GROUP BY c.name"),

                Hash(
                @"SELECT * single",
                @"
                SELECT *
                FROM c
                GROUP BY c.age"),

                Hash(
                @"SELECT * multi",
                @"
                SELECT *
                FROM c
                GROUP BY c.age, c.name"),

                //-------------------------
                //      Positive
                //-------------------------
                Hash(
                @"SELECT VALUE",
                @"
                SELECT VALUE c.age
                FROM c
                GROUP BY c.age"),

                Hash(
                @"SELECT VALUE object create",
                @"
                SELECT VALUE {""age"": c.age}
                FROM c
                GROUP BY {""age"": c.age}"),

                Hash(
                @"Simple GROUP BY with no aggregates",
                @"
                SELECT c.age, c.name
                FROM c
                GROUP BY c.age, c.name"),

                Hash(
                @"GROUP BY with aggregates",
                @"
                SELECT c.team, c.name, COUNT(1) AS count, AVG(c.age) AS avg_age, MIN(c.age) AS min_age, MAX(c.age) AS max_age 
                FROM c
                GROUP BY c.team, c.name"),

                Hash(
                @"GROUP BY arbitrary scalar expressions",
                @"
                SELECT UPPER(c.name) AS name, SUBSTRING(c.address.city, 0, 3) AS city, Avg(c.income) AS income
                FROM c
                GROUP BY UPPER(c.name), SUBSTRING(c.address.city, 0, 3)"),

                Hash(
                @"GROUP BY in subquery",
                @"
                SELECT c.name, s 
                FROM c
                JOIN (
                    SELECT VALUE score
                    FROM score IN c.scores
                    GROUP BY score
                ) s"),

                Hash(
                @"GROUP BY in both subquery and outer query",
                @"
                SELECT c.name, s.score, s.repeat, Count(1) AS count
                FROM c
                JOIN (
                    SELECT score, Count(1) AS repeat
                    FROM score IN c.scores
                    GROUP BY score
                ) s
                GROUP BY c.name"),

                Hash(
                @"GROUP BY in multiple subqueries and outer query",
                @"
                SELECT info, Count(1) AS count, g.name AS group_name
                FROM c
                JOIN (SELECT VALUE s FROM s IN c.scores GROUP BY s) s
                JOIN (
                    SELECT s1 AS score, Avg(s1) AS avg_score, g1.name AS group_name
                    FROM s1 IN c.scores
                    JOIN g1 IN c.groups
                    GROUP BY s1, g1
                ) info
                JOIN (SELECT VALUE g FROM g IN c.groups GROUP BY g) g
                WHERE info.group_name = g.name
                GROUP BY info, g.name"),

                Hash(
                @"Non property reference without an alias",
                @"
                SELECT c.name, UPPER(c.name)
                FROM c
                GROUP BY c.name"),

                Hash(
                @"Aggregate without an alias",
                @"
                SELECT c.name, AVG(c.age)
                FROM c
                GROUP BY c.name"),

                Hash(
                @"Aggregate not in select list spec",
                @"
                SELECT VALUE AVG(c.age)
                FROM c
                GROUP BY c.name"),

                Hash(
               @"Interleaved projection types",
               @"
                SELECT c.team, COUNT(1) AS count, c.name, AVG(c.age) AS avg_age, MIN(c.age), MAX(c.age) AS max_age, MAX(c.age)
                FROM c
                GROUP BY c.team, c.name")
            };

            this.ExecuteTestSuite(testVariations);
        }

        [TestMethod]
        [Owner("girobins")]
        public void Like()
        {
            List<QueryPlanBaselineTestInput> testVariations = new List<QueryPlanBaselineTestInput>
            {
                Hash(
                @"Projection LIKE",
                @"SELECT VALUE 'a' LIKE '$a'",
                @"/a"),

                Hash(
                @"Projection LIKE",
                @"SELECT VALUE 'a' LIKE '!%a' ESCAPE '!'",
                @"/a"),

                Hash(
                @"LIKE SELECT * NonPartitioned",
                @"SELECT * FROM c WHERE c.a LIKE '%a%'"),

                Hash(
                @"LIKE SELECT *",
                @"SELECT * FROM c WHERE c.a LIKE '%a%'",
                @"/a"),

                Hash(
                @"Parameterized LIKE",
                new SqlQuerySpec(
                    @"SELECT * FROM c WHERE c.a LIKE @LIKEPATTERN",
                    new SqlParameterCollection(
                        new SqlParameter[]
                        {
                            new SqlParameter("@LIKEPATTERN", "%a"),
                        })),
                @"/a"),

                Hash(
                @"LIKE and non partition filter",
                @"SELECT * FROM c WHERE c.a LIKE 'a%'",
                @"/key"),

                Hash(
                @"LIKE and partition filter",
                @"SELECT * FROM c WHERE c.a LIKE 'a%'",
                @"/a"),

                Hash(
                @"LIKE and partition filter",
                @"SELECT * FROM c WHERE c.a LIKE 'a!%' ESCAPE '!'",
                @"/a")
            };

            this.ExecuteTestSuite(testVariations);
        }

        [TestMethod]
        [Owner("brchon")]
        public void ManyRanges()
        {
            List<QueryPlanBaselineTestInput> testVariations = new List<QueryPlanBaselineTestInput>
            {
                Hash(
                @"IN Many Number",
                @"SELECT * FROM c WHERE c.key IN (1, 2, 3)",
                @"/key"),

                Hash(
                @"IN Many String",
                @"SELECT * FROM c WHERE c.key IN (""a"", ""b"", ""c"")",
                @"/key"),

                Hash(
                @"IN 1",
                @"SELECT * FROM c WHERE c.key IN (1)",
                @"/key"),

                Hash(
                @"IN Many Arrays",
                @"SELECT * FROM c WHERE c.key IN ([], [1], [3,2])",
                @"/key"),

                Hash(
                @"IN Many Objects",
                @"SELECT * FROM c WHERE c.key IN ({}, {""x"":1,""y"":2})",
                @"/key"),

                Hash(
                @"IN Many Mixed Types",
                @"SELECT * FROM c WHERE c.key IN (1, ""a"", true, false, null, 2.0, {""x"":8}, [7])",
                @"/key"),

                Hash(
                @"ORs",
                @"SELECT * FROM c WHERE c.key = 1 OR c.key = 2",
                @"/key"),

                Hash(
                @"ANDs",
                @"SELECT * FROM c WHERE c.key = 1 AND c.key = 2",
                @"/key"),

                Hash(
                @"ABS",
                @"SELECT * FROM c WHERE ABS(c.key) = 1",
                @"/key"),

                Hash(
                @"Non Indexed System Function Filter",
                @"SELECT * FROM c WHERE LENGTH(c.key) = 1",
                @"/key"),

                Hash(
                @"IS_ARRAY",
                @"SELECT * FROM c WHERE IS_ARRAY(c.key)",
                @"/key"),

                Hash(
                @"IS_BOOL",
                @"SELECT * FROM c WHERE IS_BOOL(c.key)",
                @"/key"),

                Hash(
                @"IS_NULL",
                @"SELECT * FROM c WHERE IS_NULL(c.key)",
                @"/key"),

                Hash(
                @"IS_NUMBER",
                @"SELECT * FROM c WHERE IS_NUMBER(c.key)",
                @"/key"),

                Hash(
                @"IS_OBJECT",
                @"SELECT * FROM c WHERE IS_OBJECT(c.key)",
                @"/key"),

                Hash(
                @"IS_STRING",
                @"SELECT * FROM c WHERE IS_STRING(c.key)",
                @"/key"),

                Hash(
                @"IS_DEFINED",
                @"SELECT * FROM c WHERE IS_DEFINED(c.key)",
                @"/key"),

                Hash(
                @"IS_PRIMITIVE",
                @"SELECT * FROM c WHERE IS_PRIMITIVE(c.key)",
                @"/key"),

                Hash(
                @"_TRY_ARRAY_CONTAINS",
                @"SELECT * FROM c WHERE _TRY_ARRAY_CONTAINS([1, 2, 3], c.a)",
                @"/a"),

                Hash(
                @"ARRAY_CONTAINS",
                @"SELECT * FROM c WHERE ARRAY_CONTAINS([1, 2, 3], c.a)",
                @"/a")
            };

            this.ExecuteTestSuite(testVariations);
        }

        [TestMethod]
        [Owner("brchon")]
        public void MultipleKeys()
        {
            List<QueryPlanBaselineTestInput> testVariations = new List<QueryPlanBaselineTestInput>
            {
                Hash(
                @"Multi Key Is Defined",
                @"SELECT * FROM Root r WHERE r.a.b.c",
                @"/a/b/c", "/a/c"),

                Hash(
                @"Multi Key Point Lookup",
                @"SELECT * FROM Root r WHERE r.a.b.c = null AND r.a.c = false",
                @"/a/b/c", "/a/c")
            };

            this.ExecuteTestSuite(testVariations);
        }

        [TestMethod]
        [Owner("brchon")]
        public void Negative()
        {
            List<QueryPlanBaselineTestInput> testVariations = new List<QueryPlanBaselineTestInput>
            {
                Hash(
                @"Not A Recognized Built-in Function Name",
                @"SELECT BADFUNC(r.age) FROM Root r",
                @"/key"),

                Hash(
                @"Max UDF Calls",
                @"SELECT udf.func1(r.a), udf.func2(r.b), udf.func3(r.c), udf.func1(r.d), udf.func2(r.e), udf.func3(r.f) FROM Root r",
                @"/key"),

                Hash(
                @"Invalid number of args",
                @"SELECT * FROM Root r WHERE STARTSWITH(r.key, 'a', 'b', 'c')",
                @"/key"),

                Hash(
                @"Unsupported Order By",
                @"SELECT * FROM root WHERE root.key = ""key"" ORDER BY LOWER(root.field) ASC",
                @"/key"),

                Hash(
                @"Cannot perform an aggregate function on an expression containing an aggregate or a subquery",
                @"SELECT VALUE MAX(MIN(r)) From r",
                @"/key"),

                Hash(
                @"Aggregate Without VALUE",
                @"SELECT COUNT(r) From r",
                @"/key"),

                Hash(
                @"Agggregates in Object",
                @"SELECT VALUE {'sum': SUM(r)} From r",
                @"/key"),

                Hash(
                @"Agggregates in Array",
                @"SELECT VALUE [SUM(r)] From r",
                @"/key"),

                Hash(
                @"Agggregates in System Function",
                @"SELECT VALUE FLOOR(AVG(r)) From r",
                @"/key")
            };

            this.ExecuteTestSuite(testVariations);
        }

        [TestMethod]
        [Owner("brchon")]
        public void OrderBy()
        {
            List<QueryPlanBaselineTestInput> testVariations = new List<QueryPlanBaselineTestInput>
            {
                Hash(
                @"SELECT * ORDER BY Non Partition Key",
                @"SELECT * FROM Root r ORDER BY r.blah",
                @"/key"),

                Hash(
                @"SELECT * ORDER BY Partition Key",
                @"SELECT * FROM Root r ORDER BY r.key",
                @"/key"),

                Hash(
                @"ASC",
                @"SELECT * FROM Root r ORDER BY r.key ASC",
                @"/key"),

                Hash(
                @"DESC",
                @"SELECT * FROM Root r ORDER BY r.key DESC",
                @"/key"),

                Hash(
                @"SELECT * ORDER BY With Filter",
                @"SELECT * FROM Root r WHERE r.key = 'key' ORDER BY r.key",
                @"/key"),

                Hash(
                @"SELECT * ORDER BY With Multiple Ranges",
                @"SELECT * FROM Root r WHERE r.key IN (1, 2, 3, 4, 5, 6, 7, 8, 9) ORDER BY r.key",
                @"/key"),

                Hash(
                @"SELECT * ORDER BY Partition key And TOP",
                @"SELECT TOP 10 * FROM Root r ORDER BY r.key",
                @"/key"),

                Hash(
                @"SELECT * ORDER BY Partition key And Project Columns",
                @"SELECT c.blah, c.blah2 FROM Root c ORDER BY c.key",
                @"/key"),

                Hash(
                @"SELECT * ORDER BY Partition key And VALUE",
                @"SELECT VALUE c.blah FROM Root c ORDER BY c.key",
                @"/key"),

                Hash(
                @"SELECT * ORDER BY And Project Array",
                @"SELECT TOP 50 r[0] FROM r ORDER BY r.key ASC",
                @"/key"),

                Hash(
                @"SELECT * ORDER BY And Project Object",
                @"SELECT TOP 70 r[""a""] FROM Root r ORDER BY r.key DESC",
                @"/key"),

                Hash(
                @"SELECT LIST ORDER BY",
                @"SELECT r.field1, r.field2, r. field3 FROM r ORDER BY r.field4",
                @"/key"),

                Hash(
                @"SELECT ORDER BY Single Column",
                @"SELECT r.field1 FROM r ORDER BY r.field4",
                @"/key"),

                Hash(
                @"SELECT VALUE ORDER BY",
                @"SELECT VALUE r.a FROM r ORDER BY r.b",
                @"/key"),

                Hash(
                @"SELECT VALUE ORDER BY with filter ",
                @"SELECT VALUE r.a FROM r WHERE r.d = true ORDER BY r.b",
                @"/key"),

                Hash(
                @"SELECT VALUE 1 ORDER BY",
                @"SELECT VALUE 1 FROM r ORDER BY r._ts",
                @"/key"),

                Hash(
                @"SELECT 1 ORDER BY",
                @"SELECT 1 FROM r ORDER BY r._ts",
                @"/key")
            };

            this.ExecuteTestSuite(testVariations);
        }

        [TestMethod]
        [Owner("brchon")]
        public void MultiOrderBy()
        {
            List<QueryPlanBaselineTestInput> testVariations = new List<QueryPlanBaselineTestInput>();

            foreach (bool hasFilter in new bool[] { false, true })
            {
                foreach (bool hasTop in new bool[] { false, true })
                {
                    foreach (bool hasOrderByDescriptors in new bool[] { false, true })
                    {
                        foreach (bool hasProjections in new bool[] { false, true })
                        {
                            string description = $"Multi Order By hasTop: {hasTop}, hasProjections: {hasProjections}, hasOrderByDescriptors: {hasOrderByDescriptors}, hasFilter: {hasFilter}";
                            string query = $@"
                                SELECT {(hasTop ? "TOP 10" : string.Empty)} {(hasProjections ? "c.property1, c.property2, c.property3" : "*")}
                                FROM c
                                {(hasFilter ? "WHERE c.property4 = 'asdf'" : string.Empty)}
                                ORDER BY c.orderByField1 {(hasOrderByDescriptors ? "ASC" : string.Empty)}, c.orderByField2 {(hasOrderByDescriptors ? "ASC" : string.Empty)}";

                            testVariations.Add(Hash(description, query, "/key"));
                        }
                    }
                }
            }

            this.ExecuteTestSuite(testVariations);
        }

        [TestMethod]
        [Owner("brchon")]
        public void PointRange()
        {
            List<QueryPlanBaselineTestInput> testVariations = new List<QueryPlanBaselineTestInput>
            {
                Hash(
                @"Partition Key Equals Number",
                @"SELECT * FROM Root r WHERE r.a = 5",
                @"/a"),

                Hash(
                @"Partition Key Equals String",
                @"SELECT * FROM Root r WHERE r.a = ""Hello World""",
                @"/a"),

                Hash(
                @"Partition Key Equals Empty String",
                @"SELECT * FROM Root r WHERE r.a = """"",
                @"/a"),

                Hash(
                @"Partition Key Equals Date",
                @"SELECT * FROM Root r WHERE r.a = '/Date(1198908717056)/'",
                @"/a"),

                Hash(
                @"Partition Key Equals null",
                @"SELECT * FROM Root r WHERE r.a.b.c = null",
                @"/a/b/c"),

                Hash(
                @"Partition Key Equals True",
                @"SELECT * FROM Root r WHERE r.a.b.c = true",
                @"/a/b/c"),

                Hash(
                @"Partition Key Equals False",
                @"SELECT * FROM Root r WHERE r.a.b.c = false",
                @"/a/b/c"),

                Hash(
                @"Partition Key Equals Array",
                @"SELECT * FROM Root r WHERE r.a.b.c = []",
                @"/a/b/c"),

                Hash(
                @"Partition Key Equals Object",
                @"SELECT * FROM Root r WHERE r.a.b.c = {}",
                @"/a/b/c"),

                Hash(
                @"BETWEEN",
                @"SELECT * FROM Root r WHERE (r.a.c BETWEEN 2 AND 3)",
                @"/a/c"),

                Hash(
                @"Partition Key In Geospatial Query",
                @"SELECT * FROM Root r WHERE ST_DISTANCE(r.a.b.c, {""type"":""Point"", ""coordinates"":[71.0589,42.3601]}) < 20000",
                @"/a/b/c"),

                Hash(
                @"Partition Key Equals Unicode String",
                @"SELECT * FROM Root r WHERE r.key = '你好'",
                @"/key"),

                Hash(
                @"Partition Key Equals Unicode String 2",
                @"SELECT * FROM Root r WHERE r.key = 'こにちわ'",
                @"/key"),

                Hash(
                @"Partition Key Is Array Element",
                @"SELECT * FROM Root r WHERE r.key[0] = 'blah'",
                @"/key[0]")
            };

            this.ExecuteTestSuite(testVariations);
        }

        [TestMethod]
        [Owner("brchon")]
        public void RangePartitionKey()
        {
            List<QueryPlanBaselineTestInput> testVariations = new List<QueryPlanBaselineTestInput>
            {
                Range(
                @"SELECT *",
                @"SELECT * FROM Root r",
                @"/a"),

                Range(
                @"Multiple Range Partition Keys",
                @"SELECT * FROM Root r",
                @"/a/b/c", "/a", "/a/c"),

                Range(
                @"Equality On Partition Key",
                @"SELECT * FROM Root r WHERE r.a = 5",
                @"/a"),

                Hash(
                @"BETWEEN",
                @"SELECT * FROM Root r WHERE (r.a.c BETWEEN 2 AND 3)",
                @"/a/c"),

                Range(
                @"BETWEEN and point",
                @"SELECT * FROM Root r WHERE (r.a.b.c BETWEEN 2 AND 3) AND r.a.c = 4",
                @"/a/b/c", "/a/c"),

                Range(
                @"Complex filter",
                @"SELECT * FROM Root r WHERE ((r.a.b.c BETWEEN 2 AND 3) AND r.b = 4 AND r.a.c = 5) OR ((r.a.b.c BETWEEN 9 AND 10) AND r.b = 2 AND r.a.c != 7)",
                @"/a/b/c", "/b", "/a/c"),

                Range(
                @"Number Range On Partition Key (MaxNumber)",
                @"SELECT * FROM Root r WHERE r.a.c > 10",
                @"/a/c"),

                Range(
                @"Number Range On Partition Key (MinNumber)",
                @"SELECT * FROM Root r WHERE r.a.c <= -3",
                @"/a/c"),

                Range(
                @"String Range On Partition Key (MaxString)",
                @"SELECT * FROM Root r WHERE r.a.c > ""10""",
                @"/a/c"),

                Range(
                @"String Range On Partition Key (MinString)",
                @"SELECT * FROM Root r WHERE r.a.c <= ""-3""",
                @"/a/c"),

                Hash(
                @"Equality with null",
                @"SELECT * FROM Root r WHERE r.a.b.c = null",
                @"/a/b/c"),

                Hash(
                @"Multipe Keys and one Equality with null",
                @"SELECT * FROM Root r WHERE r.a.b.c = null",
                @"/a/b/c", "/a/c"),

                Range(
                @"Mixed Range On Partition Key",
                @"SELECT * FROM Root r WHERE (r.a.b.c = 2 AND r.a.c = 4) OR (r.a.b.c = ""2"" AND r.a.c = ""4"")",
                @"/a/b/c", "/a/c")
            };

            this.ExecuteTestSuite(testVariations);
        }

        [TestMethod]
        [Owner("brchon")]
        public void Subqueries()
        {
            List<QueryPlanBaselineTestInput> testVariations = new List<QueryPlanBaselineTestInput>
            {
                Hash(
                @"Basic Subquery",
                @"
                    SELECT (
                        SELECT * FROM c) 
                    FROM c
                ",
                @"/key"),

                Hash(
                @"Subquery With Filter in Outer Query",
                @"
                    SELECT (
                        SELECT * FROM c) 
                    FROM c 
                    WHERE c.key = 42
                ",
                @"/key"),

                Hash(
                @"Subquery With Filter in Inner Query",
                @"
                    SELECT (
                        SELECT * 
                        FROM c 
                        WHERE c.key = 42) 
                    FROM c
                ",
                @"/key"),

                Hash(
                @"Subquery With Filter in Inner And Outer Query",
                @"
                    SELECT (
                        SELECT * 
                        FROM c 
                        WHERE c.key = 42) 
                    FROM c 
                    WHERE c.key = 'a'
                ",
                @"/key"),

                Hash(
                @"Subquery as Filter",
                @"
                    SELECT * 
                    FROM c 
                    WHERE (c.blah = (
                        SELECT * 
                        FROM c 
                        WHERE c.key = 42 and c.id = 5)) and c.key = 32
                ",
                @"/key")
            };

            this.ExecuteTestSuite(testVariations);
        }

        [TestMethod]
        [Owner("brchon")]
        public void Top()
        {
            List<QueryPlanBaselineTestInput> testVariations = new List<QueryPlanBaselineTestInput>
            {
                Hash(
                @"Just TOP",
                @"SELECT TOP 5 * FROM c",
                @"/key"),

                Hash(
                @"Parameterized TOP",
                new SqlQuerySpec(
                    @"SELECT TOP @TOPCOUNT * FROM c",
                    new SqlParameterCollection(
                        new SqlParameter[]
                        {
                            new SqlParameter("@TOPCOUNT", 42),
                        })),
                @"/key"),

                Hash(
                @"TOP and non partition filter",
                @"SELECT TOP 5 * FROM c WHERE c.blah = 5",
                @"/key"),

                Hash(
                @"TOP and partition filter",
                @"SELECT TOP 5 * FROM c WHERE c.key = 5",
                @"/key"),

                Hash(
                @"TOP and partition filter",
                @"SELECT TOP 5 * FROM c WHERE c.key = 5",
                @"/key"),

                Hash(
                @"TOP 0",
                @"SELECT TOP 0 * FROM c",
                @"/key"),

                Hash(
                @"TOP 0 with partition key filter",
                @"SELECT TOP 0 * FROM c WHERE c.key = 5",
                @"/key"),

                Hash(
                @"TOP with Aggregate",
                @"SELECT TOP 5 VALUE AVG(c.name) FROM c",
                @"/key"),

                Hash(
                @"TOP with DISTINCT",
                @"SELECT DISTINCT TOP 5 VALUE c.name FROM c",
                @"/key"),

                Hash(
                @"TOP with GROUP BY",
                @"SELECT TOP 5 VALUE c.name FROM c GROUP BY c.name",
                @"/key")
            };

            this.ExecuteTestSuite(testVariations);
        }

        [TestMethod]
        [Owner("brchon")]
        public void OffsetLimit()
        {
            List<QueryPlanBaselineTestInput> testVariations = new List<QueryPlanBaselineTestInput>
            {
                Hash(
                    @"Just OFFSET LIMIT",
                    @"SELECT * FROM c OFFSET 1 LIMIT 2",
                    @"/key"),

                Hash(
                    @"Parameterized OFFSET and LIMIT",
                    new SqlQuerySpec(
                        @"SELECT * FROM c OFFSET @OFFSETCOUNT LIMIT @LIMITCOUNT",
                        new SqlParameterCollection(
                            new SqlParameter[]
                            {
                                new SqlParameter("@OFFSETCOUNT", 42),
                                new SqlParameter("@LIMITCOUNT", 1337),
                            })),
                    @"/key"),

                Hash(
                    @"OFFSET LIMIT and non partition filter",
                    @"SELECT * FROM c WHERE c.blah = 5 OFFSET 1 LIMIT 2",
                    @"/key"),

                Hash(
                    @"OFFSET LIMIT and partition filter",
                    @"SELECT * FROM c WHERE c.key = 5 OFFSET 1 LIMIT 2",
                    @"/key"),

                Hash(
                    @"OFFSET LIMIT 0",
                    @"SELECT * FROM c OFFSET 0 LIMIT 0",
                    @"/key"),

                Hash(
                    @"OFFSET LIMIT 0 with partition key filter",
                    @"SELECT * FROM c WHERE c.key = 5 OFFSET 0 LIMIT 0",
                    @"/key"),

                Hash(
                    @"OFFSET LIMIT and partition filter but aggregates",
                    @"SELECT VALUE COUNT(1) FROM c WHERE c.key = 5 OFFSET 1 LIMIT 2",
                    @"/key"),

                Hash(
                    @"OFFSET LIMIT and partition filter but distinct",
                    @"SELECT DISTINCT *  FROM c WHERE c.key = 5 OFFSET 1 LIMIT 2",
                    @"/key"),

                Hash(
                    @"OFFSET LIMIT and partition filter but group by",
                    @"SELECT c.name FROM c WHERE c.key = 5 GROUP BY c.name OFFSET 1 LIMIT 2",
                    @"/key"),
            };

            this.ExecuteTestSuite(testVariations);
        }

        [TestMethod]
        [Owner("brchon")]
        public void SystemFunctions()
        {
            string[] systemFunctionExpressions = new string[]
            {
                // Array
                "ARRAY_CONCAT([1, 2, 3], [4, 5, 6])",
                "ARRAY_CONCAT([1, 2, 3], [4, 5, 6], [7, 8, 9])",
                "ARRAY_CONTAINS([1, 2, 3], [4, 5, 6])",
                "ARRAY_CONTAINS([1, 2, 3], [4, 5, 6], true)",
                "ARRAY_LENGTH([1, 2, 3])",
                "ARRAY_SLICE([1, 2, 3], 2)",
                "ARRAY_SLICE([1, 2, 3], 2, 3)",

                // Date and Time
                "GetCurrentDateTime()",
                "GetCurrentTimestamp()",
                "GetCurrentTicks()",
                "DateTimeAdd(\"mm\", 1, \"2020-07-09T23:20:13.4575530Z\")",
                "DateTimeDiff(\"yyyy\", \"2028-01-01T01:02:03.1234527Z\", \"2020 - 01 - 03T01: 02:03.1234567Z\")",
                "DateTimeFromParts(2020, 9, 4)",
                "DateTimePart(\"mcs\", \"2020-01-02T03:04:05.6789123Z\")",
                "DateTimeToTicks(\"2020-01-02T03:04:05Z\")",
                "DateTimeToTimestamp(\"2020-07-09\")",
                "TicksToDateTime(15943368134575530)",
                "TimestampToDateTime(1594227912345)",

                // Mathematical
                "ABS(42)",
                "ACOS(42)",
                "ASIN(42)",
                "ATAN(42)",
                "ATN2(42, 1337)",
                "CEILING(42)",
                "COS(42)",
                "COT(42)",
                "DEGREES(42)",
                "EXP(42)",
                "FLOOR(42)",
                "LOG(42)",
                "LOG(42, 1337)",
                "LOG10(42)",
                "PI()",
                "POWER(42, 1337)",
                "RADIANS(42)",
                "RAND()",
                "SIGN(42)",
                "SIN(42)",
                "SQRT(42)",
                "SQUARE(42)",
                "TAN(42)",
                "TRUNC(42)",

                // Spatial
                "ST_DISTANCE(42, 1337)",
                "ST_INTERSECTS(42, 1337)",
                "ST_ISVALID(42)",
                "ST_ISVALIDDETAILED(42)",
                "ST_WITHIN(42, 1337)",

                // String
                "CONCAT('hello', 'world')",
                "CONCAT('hello', 'world', 'bye')",
                "CONTAINS('hello', 'world')",
                "CONTAINS('hello', 'world', true)",
                "ENDSWITH('hello', 'world')",
                "ENDSWITH('hello', 'world', true)",
                "INDEX_OF('hello', 'world')",
                "INDEX_OF('hello', 'world', 42)",
                "LEFT('hello', 42)",
                "LENGTH('hello')",
                "LOWER('hello')",
                "LTRIM('hello')",
                "REPLACE('hello', 'world', 'bye')",
                "REPLICATE('hello', 5)",
                "REVERSE('hello')",
                "RIGHT('hello', 2)",
                "RTRIM('hello')",
                "STARTSWITH('hello', 'world')",
                "STARTSWITH('hello', 'world', true)",
                "STRINGEQUALS('hello', 'world')",
                "STRINGEQUALS('hello', 'world', true)",
                "StringToArray('[]')",
                "StringToBoolean('false')",
                "StringToNull('null')",
                "StringToNumber('42')",
                "StringToObject('{}')",
                "SUBSTRING('hello', 2, 3)",
                "ToString('hello')",
                "TRIM('hello')",
                "UPPER('hello')",

                // Type Checking
                "IS_ARRAY([])",
                "IS_BOOL(true)",
                "IS_DEFINED(true)",
                "IS_NULL(true)",
                "IS_NUMBER(42)",
                "IS_OBJECT({})",
                "IS_PRIMITIVE({})",
                "IS_STRING('asdf')",
            };

            List<QueryPlanBaselineTestInput> testVariations = systemFunctionExpressions
                .Select((systemFunctionExpression) => Hash(
                    description: systemFunctionExpression,
                    query: $"SELECT VALUE {systemFunctionExpression}",
                    partitionkeys: @"/key")).ToList();

            this.ExecuteTestSuite(testVariations);
        }

        private static PartitionKeyDefinition CreateHashPartitionKey(
            params string[] partitionKeys) => new PartitionKeyDefinition()
            {
                Paths = new Collection<string>(partitionKeys),
                Kind = Microsoft.Azure.Documents.PartitionKind.Hash
            };

        private static PartitionKeyDefinition CreateRangePartitionKey(
            params string[] partitionKeys) => new PartitionKeyDefinition()
            {
                Paths = new Collection<string>(partitionKeys),
                Kind = PartitionKind.Range
            };

        private static PartitionKeyDefinition CreateMultiHashPartitionKey(
            params string[] partitionkeys) => new PartitionKeyDefinition()
            {
                Paths = new Collection<string>(partitionkeys),
                Kind = PartitionKind.MultiHash,
                Version = PartitionKeyDefinitionVersion.V2
            };

        private static QueryPlanBaselineTestInput None(
            string description,
            string query) => new QueryPlanBaselineTestInput(
                description,
                partitionKeyDefinition: null,
                new SqlQuerySpec(query));

        private static QueryPlanBaselineTestInput None(
            string description,
            SqlQuerySpec query) => new QueryPlanBaselineTestInput(
                description,
                partitionKeyDefinition: null,
                query);

        private static QueryPlanBaselineTestInput Hash(
            string description,
            string query,
            params string[] partitionkeys) => new QueryPlanBaselineTestInput(
                description,
                CreateHashPartitionKey(partitionkeys),
                new SqlQuerySpec(query));

        private static QueryPlanBaselineTestInput Hash(
            string description,
            SqlQuerySpec query,
            params string[] partitionkeys) => new QueryPlanBaselineTestInput(
                description,
                CreateHashPartitionKey(partitionkeys),
                query);

        private static QueryPlanBaselineTestInput MultiHash(
            string description,
            string query,
            params string[] partitionKeys) => new QueryPlanBaselineTestInput(
                description,
                CreateMultiHashPartitionKey(partitionKeys),
                new SqlQuerySpec(query));

        private static QueryPlanBaselineTestInput MultiHash(
            string description,
            SqlQuerySpec query,
            params string[] partitionKeys) => new QueryPlanBaselineTestInput(
                description,
                CreateMultiHashPartitionKey(partitionKeys),
                query);

        private static QueryPlanBaselineTestInput Range(
            string description,
            string query,
            params string[] partitionkeys) => new QueryPlanBaselineTestInput(
                description,
                CreateRangePartitionKey(partitionkeys),
                new SqlQuerySpec(query));

        private static QueryPlanBaselineTestInput Range(
            string description,
            SqlQuerySpec query,
            params string[] partitionkeys) => new QueryPlanBaselineTestInput(
                description,
                CreateRangePartitionKey(partitionkeys),
                query);

        public override QueryPlanBaselineTestOutput ExecuteTest(QueryPlanBaselineTestInput input)
        {
            TryCatch<PartitionedQueryExecutionInfoInternal> info = queryPartitionProvider.TryGetPartitionedQueryExecutionInfoInternal(
                input.SqlQuerySpec,
                input.PartitionKeyDefinition,
                requireFormattableOrderByQuery: true,
                isContinuationExpected: false,
                allowNonValueAggregateQuery: true,
                hasLogicalPartitionKey: false,
                allowDCount: true);

            if (info.Failed)
            {
                return new QueryPlanBaselineTestNegativeOutput(info.Exception);
            }

            return new QueryPlanBaselineTestPositiveOutput(info.Result);
        }
    }

    public sealed class QueryPlanBaselineTestInput : BaselineTestInput
    {
        internal PartitionKeyDefinition PartitionKeyDefinition { get; set; }
        internal SqlQuerySpec SqlQuerySpec { get; set; }

        internal QueryPlanBaselineTestInput(
            string description,
            PartitionKeyDefinition partitionKeyDefinition,
            SqlQuerySpec sqlQuerySpec)
            : base(description)
        {
            this.PartitionKeyDefinition = partitionKeyDefinition;
            this.SqlQuerySpec = sqlQuerySpec;
        }

        public override void SerializeAsXml(XmlWriter xmlWriter)
        {
            xmlWriter.WriteElementString("Description", this.Description);
            xmlWriter.WriteElementString("Query", this.SqlQuerySpec.QueryText);
            xmlWriter.WriteStartElement("PartitionKeys");
            if (this.PartitionKeyDefinition != null)
            {
                foreach (string path in this.PartitionKeyDefinition.Paths)
                {
                    xmlWriter.WriteElementString("Key", path);
                }
            }
            xmlWriter.WriteEndElement();

            if (this.PartitionKeyDefinition != null)
            {
                xmlWriter.WriteElementString(
                    "PartitionKeyType",
                    this.PartitionKeyDefinition.Kind == PartitionKind.Hash ? "Hash" : (
                        this.PartitionKeyDefinition.Kind == PartitionKind.MultiHash ? "MultiHash" : "Range"));
            }

            if (this.SqlQuerySpec.ShouldSerializeParameters())
            {
                xmlWriter.WriteStartElement("QueryParameters");
                xmlWriter.WriteCData(JsonConvert.SerializeObject(
                    this.SqlQuerySpec.Parameters,
                    Newtonsoft.Json.Formatting.Indented));
                xmlWriter.WriteEndElement();
            }
        }
    }

    internal struct GetPartitionedQueryExecutionInfoOptions
    {
        public bool RequireFormattableOrderByQuery { get; }
        public bool IsContinuationExpected { get; }
        public bool AllowNonValueAggregateQuery { get; }
        public bool HasLogicalPartitionKey { get; }

        public GetPartitionedQueryExecutionInfoOptions(
            bool requireFormattableOrderByQuery,
            bool isContinuationExpected,
            bool allowNonValueAggregateQuery,
            bool hasLogicalPartitionKey)
        {
            this.RequireFormattableOrderByQuery = requireFormattableOrderByQuery;
            this.IsContinuationExpected = isContinuationExpected;
            this.AllowNonValueAggregateQuery = allowNonValueAggregateQuery;
            this.HasLogicalPartitionKey = hasLogicalPartitionKey;
        }

        public override bool Equals(object obj)
        {
            if (object.ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj is GetPartitionedQueryExecutionInfoOptions other)
            {
                return this.AllowNonValueAggregateQuery == other.AllowNonValueAggregateQuery
                    && this.IsContinuationExpected == other.IsContinuationExpected
                    && this.RequireFormattableOrderByQuery == other.RequireFormattableOrderByQuery
                    && this.HasLogicalPartitionKey == other.HasLogicalPartitionKey;
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            return ((this.AllowNonValueAggregateQuery ? 1 : 0) << 0)
                | ((this.IsContinuationExpected ? 1 : 0) << 1)
                | ((this.RequireFormattableOrderByQuery ? 1 : 0) << 2)
                | ((this.HasLogicalPartitionKey ? 1 : 0) << 3);
        }
    }

    public abstract class QueryPlanBaselineTestOutput : BaselineTestOutput
    {
    }

    internal sealed class QueryPlanBaselineTestNegativeOutput : QueryPlanBaselineTestOutput
    {
        public QueryPlanBaselineTestNegativeOutput(Exception exception)
        {
            this.Exception = exception ?? throw new ArgumentNullException(nameof(exception));
        }

        public override void SerializeAsXml(XmlWriter xmlWriter)
        {
            xmlWriter.WriteElementString(nameof(this.Exception), this.Exception.Message);
        }

        public Exception Exception { get; }
    }

    internal sealed class QueryPlanBaselineTestPositiveOutput : QueryPlanBaselineTestOutput
    {
        public QueryPlanBaselineTestPositiveOutput(PartitionedQueryExecutionInfoInternal info)
        {
            this.Info = info ?? throw new ArgumentNullException(nameof(info));
        }

        public PartitionedQueryExecutionInfoInternal Info { get; }

        public override void SerializeAsXml(XmlWriter xmlWriter)
        {
            xmlWriter.WriteStartElement(nameof(PartitionedQueryExecutionInfoInternal));
            WritePartitionQueryExecutionInfoAsXML(this.Info, xmlWriter);
            xmlWriter.WriteEndElement();
        }

        private static void WriteQueryInfoAsXML(QueryInfo queryInfo, XmlWriter writer)
        {
            writer.WriteStartElement(nameof(QueryInfo));
            writer.WriteElementString(nameof(queryInfo.DistinctType), queryInfo.DistinctType.ToString());
            writer.WriteElementString(nameof(queryInfo.Top), queryInfo.Top.ToString());
            writer.WriteElementString(nameof(queryInfo.Offset), queryInfo.Offset.ToString());
            writer.WriteElementString(nameof(queryInfo.Limit), queryInfo.Limit.ToString());
            writer.WriteStartElement(nameof(queryInfo.GroupByExpressions));
            foreach (string GroupByExpression in queryInfo.GroupByExpressions)
            {
                writer.WriteElementString(nameof(GroupByExpression), GroupByExpression);
            }
            writer.WriteEndElement();
            writer.WriteStartElement(nameof(queryInfo.OrderBy));
            foreach (SortOrder sortorder in queryInfo.OrderBy)
            {
                writer.WriteElementString(nameof(SortOrder), sortorder.ToString());
            }
            writer.WriteEndElement();
            writer.WriteStartElement(nameof(queryInfo.OrderByExpressions));
            foreach (string OrderByExpression in queryInfo.OrderByExpressions)
            {
                writer.WriteElementString(nameof(OrderByExpression), OrderByExpression);
            }
            writer.WriteEndElement();
            writer.WriteStartElement(nameof(queryInfo.Aggregates));
            foreach (AggregateOperator aggregate in queryInfo.Aggregates)
            {
                writer.WriteElementString(nameof(AggregateOperator), aggregate.ToString());
            }
            writer.WriteEndElement();
            writer.WriteStartElement(nameof(queryInfo.GroupByAliasToAggregateType));
            foreach (KeyValuePair<string, AggregateOperator?> kvp in queryInfo.GroupByAliasToAggregateType)
            {
                writer.WriteStartElement("AliasToAggregateType");
                writer.WriteElementString("Alias", kvp.Key);
                writer.WriteElementString(nameof(AggregateOperator), kvp.Value.HasValue ? kvp.Value.ToString() : "null");
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
            writer.WriteStartElement(nameof(queryInfo.GroupByAliases));
            foreach (string alias in queryInfo.GroupByAliases)
            {
                writer.WriteElementString("Alias", alias);
            }
            writer.WriteEndElement();
            writer.WriteElementString(nameof(queryInfo.HasSelectValue), queryInfo.HasSelectValue.ToString());
            writer.WriteEndElement();
        }

        private static void WriteQueryRangesAsXML(List<Range<PartitionKeyInternal>> queryRanges, XmlWriter writer)
        {
            writer.WriteStartElement("QueryRanges");
            writer.WriteStartElement("Range");
            foreach (Range<PartitionKeyInternal> range in queryRanges)
            {
                string minBound = range.IsMinInclusive ? "[" : "(";
                string maxBound = range.IsMaxInclusive ? "]" : ")";
                JsonSerializerSettings settings = new JsonSerializerSettings
                {
                    DateParseHandling = DateParseHandling.None,
                    Formatting = Newtonsoft.Json.Formatting.None
                };
                string minRangeString = JsonConvert.SerializeObject(JsonConvert.DeserializeObject(range.Min.ToJsonString(), settings), settings);
                string maxRangeString = JsonConvert.SerializeObject(JsonConvert.DeserializeObject(range.Max.ToJsonString(), settings), settings);
                writer.WriteElementString("Range", $"{minBound}{minRangeString},{maxRangeString}{maxBound}");
            }
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        private static void WriteRewrittenQueryAsXML(string query, XmlWriter writer)
        {
            writer.WriteStartElement("RewrittenQuery");
            writer.WriteCData(query);
            writer.WriteEndElement();
        }

        private static void WritePartitionQueryExecutionInfoAsXML(PartitionedQueryExecutionInfoInternal info, XmlWriter writer)
        {
            if (info.QueryInfo != null)
            {
                WriteQueryInfoAsXML(info.QueryInfo, writer);
            }

            if (info.QueryRanges != null)
            {
                WriteQueryRangesAsXML(info.QueryRanges, writer);
            }

            if (info.QueryInfo.RewrittenQuery != null)
            {
                WriteRewrittenQueryAsXML(info.QueryInfo.RewrittenQuery, writer);
            }
        }
    }
}