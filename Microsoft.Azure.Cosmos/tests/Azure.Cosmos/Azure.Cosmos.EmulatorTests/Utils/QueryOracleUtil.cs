//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public interface IField
    {
        string Name { get; }
        object GetValue(uint id);
        bool IsPresent(uint id);
        string ToString(uint id);
    }

    public sealed class Field<T> : IField
    {
        private readonly string name;
        private readonly Func<uint, T> lambda;
        private readonly double density;
        private readonly IDictionary<uint, object> valueDict;

        public Field(string name, Func<uint, T> func, double density = 1)
        {
            this.name = name;
            this.lambda = func;
            this.density = Math.Min(1, Math.Max(0, density));
            this.valueDict = new Dictionary<uint, object>();
        }

        public string Name
        {
            get
            {
                return name;
            }
        }

        public object GetValue(uint id)
        {
            object value;
            if (this.valueDict.TryGetValue(id, out value))
            {
                return value;
            }

            value = lambda(id);
            this.valueDict.Add(new KeyValuePair<uint, object>(id, value));

            return value;
        }

        public string ToString(uint id)
        {
            string strValue;
            object value = this.GetValue(id);

            if (value is bool)
            {
                strValue = (bool)value ? "true" : "false";
            }
            else if (value is string)
            {
                strValue = "\"" + (string)value + "\"";
            }
            else
            {
                strValue = value.ToString();
            }

            return String.Format(CultureInfo.InvariantCulture, "\"{0}\":{1}", name, strValue);
        }

        public bool IsPresent(uint id)
        {
            if (density == 1) return true;

            if (density == 0) return false;

            return id % (uint)(Math.Round((1 - density) / density + 1)) == 0;
        }
    }

    public class FieldComparer : IComparer<KeyValuePair<uint, string>>
    {
        public IField field;
        public int order;

        public FieldComparer(IField field, int order)
        {
            this.field = field;
            this.order = order;
        }

        public int Compare(KeyValuePair<uint, string> pair1, KeyValuePair<uint, string> pair2)
        {
            if (order == 0) return 0;

            IComparable value1 = field.GetValue(pair1.Key) as IComparable;
            IComparable value2 = field.GetValue(pair2.Key) as IComparable;

            int cmp = 0;

            if (value1 == null && value2 == null)
                cmp = 0;
            else if (value1 == null)
                cmp = -1;
            else if (value2 == null)
                cmp = 1;
            else
                cmp = value1.CompareTo(value2);

            if (cmp == 0)
                cmp = pair1.Key.CompareTo(pair2.Key);

            return cmp * order;
        }
    }

    public enum Comparison
    {
        Comparison_Unknown,
        Comparison_Equal,
        Comparison_NotEqual,
        Comparison_Greater,
        Comparison_GreaterOrEqual,
        Comparison_Less,
        Comparison_LessOrEqual
    }

    public sealed class ComparisonUtil
    {
        public static string ComparisonToString(Comparison comparison)
        {
            switch (comparison)
            {
                case Comparison.Comparison_Equal:
                    return "=";
                case Comparison.Comparison_NotEqual:
                    return "!=";
                case Comparison.Comparison_Greater:
                    return ">";
                case Comparison.Comparison_GreaterOrEqual:
                    return ">=";
                case Comparison.Comparison_Less:
                    return "<";
                case Comparison.Comparison_LessOrEqual:
                    return "<=";
                default:
                    return null;
            }
        }

        public static Comparison StringToComparison(string comparison)
        {
            switch (comparison)
            {
                case "=":
                    return Comparison.Comparison_Equal;
                case "!=":
                case "<>":
                    return Comparison.Comparison_NotEqual;
                case ">":
                    return Comparison.Comparison_Greater;
                case ">=":
                    return Comparison.Comparison_GreaterOrEqual;
                case "<":
                    return Comparison.Comparison_Less;
                case "<=":
                    return Comparison.Comparison_LessOrEqual;
                default:
                    return Comparison.Comparison_Unknown;
            }
        }

    }

    public interface IFieldQueryBuilder
    {
        string GetPointQuery();
        string GetRangeQuery();
    }

    public abstract class FieldQueryBuilder<T> : IFieldQueryBuilder
    {
        protected readonly string fieldName;
        protected T minValue;
        protected T maxValue;
        protected readonly Comparison pointCmp;
        protected readonly Comparison lowerCmp;
        protected readonly Comparison upperCmp;
        protected T singleValue;
        protected Random rand;

        protected FieldQueryBuilder(string fieldName, T value, string point, T min, T max, string lower, string upper, int seed) :
            this(fieldName, value, ComparisonUtil.StringToComparison(point), min, max, ComparisonUtil.StringToComparison(lower), ComparisonUtil.StringToComparison(upper), seed)
        {
        }

        protected FieldQueryBuilder(string fieldName, T value, Comparison point, T min, T max, Comparison lower, Comparison upper, int seed)
        {
            this.fieldName = fieldName;
            this.singleValue = value;
            this.pointCmp = point;
            this.minValue = min;
            this.maxValue = max;
            this.lowerCmp = lower;
            this.upperCmp = upper;
            this.rand = new Random(seed);
        }

        public abstract string GetPointQuery();
        public abstract string GetRangeQuery();

        protected virtual string GetPointQuery(T value)
        {
            return GetPointQuery(
                pointCmp != Comparison.Comparison_Unknown ? ComparisonUtil.ComparisonToString(pointCmp) : rand.Next(10) > 0 ? "=" : "!=",
                value);
        }

        protected virtual string GetPointQuery(Comparison point, T value)
        {
            return GetPointQuery(ComparisonUtil.ComparisonToString(point), value);
        }

        protected virtual string GetPointQuery(string point, T value)
        {
            return String.Format(CultureInfo.InvariantCulture, "({0} {1} {2})", fieldName, point, value);
        }

        protected string GetRangeQuery(Comparison lower, T min, Comparison upper, T max)
        {
            return GetRangeQuery(ComparisonUtil.ComparisonToString(lower), min, ComparisonUtil.ComparisonToString(upper), max);
        }

        protected string GetRangeQuery(string lower, T min, string upper, T max)
        {
            return String.Format(
                CultureInfo.InvariantCulture,
                "(({0} {1} {2}) AND ({3} {4} {5}))",
                fieldName,
                lower,
                min,
                fieldName,
                upper,
                max);
        }

        public override string ToString()
        {
            string rangeQuery = GetRangeQuery();
            return rangeQuery == null || rand.Next(2) == 0 ? GetPointQuery() : rangeQuery;
        }
    }

    public sealed class DoubleFieldQueryBuilder : FieldQueryBuilder<Double?>
    {
        public DoubleFieldQueryBuilder(
            string fieldName,
            double min,
            double max,
            string lower = "",
            string upper = "",
            int seed = 0) :
            base(fieldName, null, String.Empty, Math.Min(min, max), Math.Max(min, max), lower, upper, seed)
        {
        }

        public DoubleFieldQueryBuilder(
            string fieldName,
            double min,
            double max,
            Comparison lower = Comparison.Comparison_Unknown,
            Comparison upper = Comparison.Comparison_Unknown,
            int seed = 0) :
            base(fieldName, null, Comparison.Comparison_Unknown, Math.Min(min, max), Math.Max(min, max), lower, upper, seed)
        {
        }

        public DoubleFieldQueryBuilder(string fieldName, double value, string point = "", int seed = 0) :
            base(fieldName, value, String.Empty, null, null, point, String.Empty, seed)
        {
        }

        public DoubleFieldQueryBuilder(string fieldName, double value, Comparison point = Comparison.Comparison_Unknown, int seed = 0) :
            base(fieldName, value, Comparison.Comparison_Unknown, null, null, point, Comparison.Comparison_Unknown, seed)
        {
        }

        public override string GetPointQuery()
        {
            return GetPointQuery(singleValue ?? rand.NextDouble() * (maxValue - minValue) + minValue);
        }

        public override string GetRangeQuery()
        {
            if (singleValue.HasValue) return null;

            double delta = (maxValue.Value - minValue.Value) / 4;
            return GetRangeQuery(
                lowerCmp != Comparison.Comparison_Unknown ? ComparisonUtil.ComparisonToString(lowerCmp) : rand.Next(2) == 0 ? ">" : ">=",
                rand.NextDouble() * (delta) + minValue,
                upperCmp != Comparison.Comparison_Unknown ? ComparisonUtil.ComparisonToString(upperCmp) : rand.Next(2) == 0 ? "<" : "<=",
                maxValue - rand.NextDouble() * (delta));
        }
    }

    public sealed class IntFieldQueryBuilder : FieldQueryBuilder<Int32?>
    {
        public IntFieldQueryBuilder(
            string fieldName,
            int min,
            int max,
            string lower = "",
            string upper = "",
            int seed = 0) :
            base(fieldName, null, String.Empty, Math.Min(min, max), Math.Max(min, max), lower, upper, seed)
        {
        }

        public IntFieldQueryBuilder(
            string fieldName,
            int min,
            int max,
            Comparison lower = Comparison.Comparison_Unknown,
            Comparison upper = Comparison.Comparison_Unknown,
            int seed = 0) :
            base(fieldName, null, Comparison.Comparison_Unknown, Math.Min(min, max), Math.Max(min, max), lower, upper, seed)
        {
        }

        public IntFieldQueryBuilder(string fieldName, int value, string point = "", int seed = 0) :
            base(fieldName, value, point, null, null, String.Empty, String.Empty, seed)
        {
        }

        public IntFieldQueryBuilder(string fieldName, int value, Comparison point = Comparison.Comparison_Unknown, int seed = 0) :
            base(fieldName, value, point, null, null, Comparison.Comparison_Unknown, Comparison.Comparison_Unknown, seed)
        {
        }

        public override string GetPointQuery()
        {
            return GetPointQuery(singleValue ?? rand.Next(maxValue.Value - minValue.Value + 1) + minValue);
        }

        public override string GetRangeQuery()
        {
            if (singleValue.HasValue) return null;

            int delta = (maxValue.Value - minValue.Value) / 4;
            return GetRangeQuery(
                lowerCmp != Comparison.Comparison_Unknown ? ComparisonUtil.ComparisonToString(lowerCmp) : rand.Next(2) == 0 ? ">" : ">=",
                rand.Next(minValue.Value, minValue.Value + delta / 4 + 1),
                upperCmp != Comparison.Comparison_Unknown ? ComparisonUtil.ComparisonToString(upperCmp) : rand.Next(2) == 0 ? "<" : "<=",
                rand.Next(maxValue.Value - delta / 4, maxValue.Value + 1));
        }
    }

    public sealed class BooleanFieldQueryBuilder : FieldQueryBuilder<Boolean?>
    {
        public BooleanFieldQueryBuilder(string fieldName, int seed = 0) :
            base(fieldName, null, String.Empty, null, null, String.Empty, String.Empty, seed)
        {
        }

        public BooleanFieldQueryBuilder(string fieldName, bool value, string point = "", int seed = 0) :
            base(fieldName, value, point, null, null, String.Empty, String.Empty, seed)
        {
        }

        public BooleanFieldQueryBuilder(string fieldName, bool value, Comparison point = Comparison.Comparison_Unknown, int seed = 0) :
            base(fieldName, value, point, null, null, Comparison.Comparison_Unknown, Comparison.Comparison_Unknown, seed)
        {
        }

        public override string GetPointQuery()
        {
            return String.Format(
                CultureInfo.InvariantCulture,
                "({0} {1} {2})",
                fieldName,
                pointCmp != Comparison.Comparison_Unknown ? ComparisonUtil.ComparisonToString(pointCmp) : rand.Next(2) == 0 ? "=" : "!=",
                singleValue.HasValue ? (singleValue.Value ? "true" : "false") : (rand.Next(2) > 0 ? "true" : "false"));
        }

        public override string GetRangeQuery()
        {
            return null;
        }
    }

    public sealed class StringFieldQueryBuilder : FieldQueryBuilder<String>
    {
        public StringFieldQueryBuilder(string fieldName, string value, string point = "", int seed = 0) :
            base(fieldName, value, point, null, null, String.Empty, String.Empty, seed)
        {
        }

        public StringFieldQueryBuilder(string fieldName, string value, Comparison point = Comparison.Comparison_Unknown, int seed = 0) :
            base(fieldName, value, point, null, null, Comparison.Comparison_Unknown, Comparison.Comparison_Unknown, seed)
        {
        }

        public override string GetPointQuery()
        {
            return GetPointQuery("\"" + singleValue + "\"");
        }

        public override string GetRangeQuery()
        {
            return null;
        }
    }

    public sealed class Query : IDisposable
    {
        private bool disposed;
        private IntPtr pFilterNode;
        private string query;
        private static readonly uint DISP_E_BUFFERTOOSMALL = 0x80020013;
        private FieldComparer comparer = null;
        public static string QueryString = "SELECT {0}.id FROM root {1}";

        public Query(string strFilter, string rootName = "r", FieldComparer comparer = null)
        {
            this.disposed = false;
            if (string.Equals(rootName, "root"))
            {
                this.query = String.Format(CultureInfo.InvariantCulture, QueryString, rootName, "");
            }
            else
            {
                this.query = String.Format(CultureInfo.InvariantCulture, QueryString, rootName, rootName);
            }

            // Create FilterNode
            uint errorCode = NativeMethods.CreateFilterNode(strFilter, rootName, out pFilterNode);
            Exception exception = Marshal.GetExceptionForHR((int)errorCode);
            if (exception != null) throw exception;

            // Get String
            StringBuilder filterBuffer = new StringBuilder(strFilter.Length * 2);
            uint resultSize;
            errorCode = NativeMethods.GetString(pFilterNode, filterBuffer, (uint)filterBuffer.Capacity, out resultSize);

            if (errorCode == DISP_E_BUFFERTOOSMALL)
            {
                filterBuffer.Capacity = (int)resultSize + 1;
                errorCode = NativeMethods.GetString(pFilterNode, filterBuffer, (uint)filterBuffer.Capacity, out resultSize);
            }

            exception = Marshal.GetExceptionForHR((int)errorCode);
            if (exception != null) throw exception;

            filterBuffer.Length = (int)resultSize;

            if (filterBuffer.Length > 0)
                query += " WHERE " + filterBuffer.ToString();

            if (comparer != null && comparer.field != null && comparer.order != 0)
            {
                query += " ORDER BY r." + comparer.field.Name + (comparer.order > 0 ? " ASC" : " DESC");
                this.comparer = comparer;
            }
        }

        ~Query()
        {
            Dispose(false);
        }

        public FieldComparer Comparer
        {
            get
            {
                return comparer;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposed) return;

            uint errorCode = NativeMethods.DeleteFilterNode(pFilterNode);
            Exception exception = Marshal.GetExceptionForHR((int)errorCode);
            if (exception != null) throw exception;

            disposed = true;
        }

        public bool Evaluate(string document)
        {
            bool result;
            uint errorCode = NativeMethods.Evaluate(pFilterNode, document, out result);
            Exception exception = Marshal.GetExceptionForHR((int)errorCode);
            if (exception != null) throw exception;

            return result;
        }

        public override string ToString()
        {
            return query;
        }
    }

    public abstract class QueryOracleUtil
    {
        protected List<string> documents = new List<string>();
        protected static readonly string DocumentFormat = "{{\"id\":\"{0}\"{1}}}";
        protected int seed;
        protected Random rand;
        public abstract IEnumerable<string> GetDocuments(uint numberOfDocuments);
        public abstract IEnumerable<Query> GetQueries(uint numberOfQueries, bool hasOrderBy = true);
        internal abstract Task<int> QueryAndVerifyDocuments(CosmosContainer container, IEnumerable<Query> queries, int pageSize = 1000, int retries = 0);

        protected QueryOracleUtil(int seed)
        {
            this.seed = seed;
            this.rand = new Random(seed);
        }

        protected virtual IEnumerable<string> GetDocuments(List<IField> fields, uint numberOfDocuments)
        {
            documents.Clear();

            for (var id = 1; id <= numberOfDocuments; ++id)
            {
                StringBuilder builder = new StringBuilder();
                foreach (var field in fields)
                    if (field.IsPresent((uint)id))
                        builder.Append(",").Append(field.ToString((uint)id));
                documents.Add(String.Format(CultureInfo.InvariantCulture, DocumentFormat, id, builder.ToString()));
            }

            return documents;
        }

        protected virtual string GetFilterString(List<IFieldQueryBuilder> fieldQueryBuilders, uint maxFilters = Int32.MaxValue)
        {
            var builder = new StringBuilder();
            var fieldQueryBuilderArray = fieldQueryBuilders.ToArray();

            for (var i = 0; i < Math.Min(fieldQueryBuilders.Count(), maxFilters); ++i)
            {
                var fieldQueryBuilder = fieldQueryBuilderArray[rand.Next(fieldQueryBuilderArray.Count())];
                // Random OR/AND
                if (builder.Length > 0)
                    builder.Append(rand.Next(2) == 0 ? " OR " : " AND ");

                builder.Append(fieldQueryBuilder);

                // Random grouping
                if (rand.Next(2) == 0)
                {
                    builder = new StringBuilder("(").Append(builder).Append(")");
                }
            }

            return builder.ToString();
        }

        protected virtual IEnumerable<Query> GetQueries(List<IFieldQueryBuilder> fieldQueryBuilders, uint numberOfQueries, uint maxFilters = Int32.MaxValue, List<IField> fields = null)
        {
            for (var i = 0; i < numberOfQueries; ++i)
            {
                yield return new Query(GetFilterString(fieldQueryBuilders, maxFilters), "r", (fields != null ? new FieldComparer(fields[rand.Next(fields.Count())], rand.Next(3) - 1) : null));
            }
        }

        public IEnumerable<string> Validate(IEnumerable<string> results, Query query, out bool valid)
        {
            var idList = new List<KeyValuePair<uint, string>>();
            for (uint i = 1; i <= documents.Count(); ++i)
                idList.Add(new KeyValuePair<uint, string>(i, documents[(int)(i - 1)]));

            if (query.Comparer != null)
            {
                idList.Sort(query.Comparer);
            }

            var expected = new List<string>();
            foreach (var pair in idList)
            {
                if (query.Evaluate(pair.Value))
                    expected.Add(String.Format(CultureInfo.InvariantCulture, DocumentFormat, pair.Key, String.Empty));
            }

            var actual = results as List<string> ?? results.ToList();

            if (query.Comparer == null)
            {
                var expectedIds = expected.Select(doc => JsonConvert.DeserializeObject<dynamic>(doc).id.ToString()).OrderBy(id => id).ToList();
                var actualIds = actual.Select(doc => JsonConvert.DeserializeObject<dynamic>(doc).id.ToString()).OrderBy(id => id).ToList();

                valid = expectedIds.Count == actualIds.Count && actualIds.SequenceEqual(expectedIds);
            }
            else
            {
                valid = expected.Count == actual.Count && expected.SequenceEqual(actual);
            }

            return expected;
        }

        public bool Validate(IEnumerable<string> results, Query query)
        {
            bool valid;
            Validate(results, query, out valid);
            return valid;
        }

        internal virtual async Task<int> QueryAndVerifyDocuments(CosmosContainer container, IEnumerable<Query> queries, int pageSize = 1000, int retries = 0, bool allowScan = false)
        {
            // First we make sure that all the queries are inserted
            {
                List<dynamic> queriedDocuments = new List<dynamic>();
                await foreach (Page<dynamic> queryResultsPage in container.GetItemQueryResultsAsync<dynamic>(requestOptions: new QueryRequestOptions() { MaxItemCount = pageSize }).AsPages())
                {
                    System.Diagnostics.Trace.TraceInformation("ReadFeed continuation token: {0}, SessionToken: {1}", queryResultsPage.ContinuationToken, queryResultsPage.GetRawResponse().Headers.GetSession());
                    queriedDocuments.AddRange(queryResultsPage.Values);
                }

                List<dynamic> expected = new List<dynamic>(documents.Count());
                for (int i = 0; i < documents.Count(); ++i)
                {
                    expected.Add(JsonConvert.DeserializeObject(String.Format(CultureInfo.InvariantCulture, DocumentFormat, i + 1, String.Empty)));
                }

                queriedDocuments.Sort((doc1, doc2) => int.Parse(doc1.id).CompareTo(int.Parse(doc2.id)));

                var expectedIds = expected.Select(doc => doc.id.ToString());

                var actualIds = queriedDocuments.Select(doc => doc.id.ToString());

                if (!expectedIds.SequenceEqual(actualIds))
                {
                    System.Diagnostics.Trace.TraceInformation("Failed to insert all the documents, queried documents are:" + Environment.NewLine + String.Join(Environment.NewLine, queriedDocuments));
                    return -1;
                }

                System.Diagnostics.Trace.TraceInformation("All the documents are inserted");
            }

            // Query and verify
            TimeSpan totalQueryLatencyAllPages = TimeSpan.FromSeconds(0);
            uint numberOfQueries = 0;
            var failedQueries = new List<Query>();

            List<Query> query_list = queries as List<Query> ?? queries.ToList();

            foreach (var query in query_list)
            {
                var queriedDocuments = new List<string>();
                List<string> activityIDsAllQueryPages = new List<string>();

                if (numberOfQueries > 0 && numberOfQueries % 100 == 0)
                {
                    System.Diagnostics.Trace.TraceInformation(DateTime.Now.ToString("HH:mm:ss.ffff") + @": Executing query {0} of {1}",
                                           (numberOfQueries + 1), query_list.Count());
                    System.Diagnostics.Trace.TraceInformation(@"    Query latency per query (avg ms) {0} after {1} queries",
                                           totalQueryLatencyAllPages.TotalMilliseconds / numberOfQueries, numberOfQueries);
                }

                await foreach (Page<dynamic> queryResultsPage in container.GetItemQueryResultsAsync<dynamic>(requestOptions: new QueryRequestOptions() { MaxItemCount = pageSize, EnableScanInQuery = allowScan }).AsPages())
                {
                    DateTime startTime = DateTime.Now;
                    activityIDsAllQueryPages.Add(queryResultsPage.GetRawResponse().Headers.GetActivityId());
                    totalQueryLatencyAllPages += (DateTime.Now - startTime);
                    foreach (JObject result in queryResultsPage.Values)
                    {
                        queriedDocuments.Add(result.ToString(Formatting.None));
                    }
                }
                numberOfQueries++;

                bool valid;
                var expected = Validate(queriedDocuments, query, out valid);
                if (!valid)
                {
                    System.Diagnostics.Trace.TraceInformation(
                        DateTime.Now.ToString("HH:mm:ss.ffff")
                        + @": Query {0} did not retrieve expected documents, query all pages activitiIDs: ({1})"
                        + Environment.NewLine
                        + "Expected:"
                        + Environment.NewLine
                        + "{2}"
                        + Environment.NewLine
                        + "Actual:"
                        + Environment.NewLine
                        + "{3}"
                        + Environment.NewLine,
                        query.ToString(),
                        String.Join(",", activityIDsAllQueryPages),
                        String.Join(",", expected),
                        String.Join(",", queriedDocuments));

                    failedQueries.Add(query);
                }
            }

            if (failedQueries.Count() == 0)
            {
                System.Diagnostics.Trace.TraceInformation(@"*** TEST PASSED ***");
                return 0;
            }
            else
            {
                System.Diagnostics.Trace.TraceInformation(@"*** TEST FAILED with seed {0}***", seed);
                int result = -1;
                //In case of a failure, retry only failed queries after sleeping for couple of minutes.
                if (retries > 0)
                {
                    System.Diagnostics.Trace.TraceInformation(@"*** Retrying Failed queries, {0} retries left ***", --retries);
                    Task.Delay(120 * 1000).Wait();
                    result = await QueryAndVerifyDocuments(container, failedQueries, pageSize, retries, allowScan);
                }

                return result;
            }
        }
    }

    public sealed class UserStoreExperiment : QueryOracleUtil
    {
        private uint numberOfUsers = 0;
        private int minDate;
        private int maxDate;
        private Func<uint, string> typeLambda;

        public UserStoreExperiment(int seed = 0)
            : base(seed)
        {
        }

        public override IEnumerable<string> GetDocuments(uint numberOfDocuments = (uint)1e6)
        {
            var fields = new List<IField>();

            // User
            numberOfUsers = (uint)(numberOfDocuments * 0.02);
            uint[] userIds = new uint[numberOfDocuments];

            int currentNumberOfUsers = (int)numberOfUsers;
            uint[] userPool = new uint[numberOfUsers];
            uint[] userDocs = new uint[numberOfUsers];

            for (var id = 0; id < numberOfUsers; ++id)
            {
                userPool[id] = (uint)id;

                if (id < numberOfUsers * 0.005)
                    userDocs[id] = (uint)(numberOfDocuments * 0.2 / (numberOfUsers * 0.005));
                else if (id < numberOfUsers * (0.005 + 0.045))
                    userDocs[id] = (uint)(numberOfDocuments * 0.45 / (numberOfUsers * 0.045));
                else if (id < numberOfUsers * (0.005 + 0.045 + 0.2))
                    userDocs[id] = (uint)(numberOfDocuments * 0.2 / (numberOfUsers * 0.2));
                else if (id < numberOfUsers * (0.005 + 0.045 + 0.2 + 0.25))
                    userDocs[id] = (uint)(numberOfDocuments * 0.1 / (numberOfUsers * 0.25));
                else
                    userDocs[id] = (uint)(numberOfDocuments * 0.05 / (numberOfUsers * 0.5));
            }

            for (var id = 0; id < numberOfDocuments; ++id)
            {
                var index = rand.Next(currentNumberOfUsers);
                var user = userPool[index];

                userIds[id] = user;

                if (--userDocs[user] <= 0)
                    userPool[index] = userPool[--currentNumberOfUsers];
            }

            fields.Add(new Field<string>(
                "User",
                id => userIds[(id - 1) % userIds.Length].ToString("D19", CultureInfo.InvariantCulture)));

            // Date
            minDate = GetUnixTime(new DateTime(2014, 8, 1));
            maxDate = GetUnixTime(new DateTime(2014, 10, 31));
            fields.Add(new Field<int>(
                "Date",
                id => (int)(rand.Next(maxDate - minDate + 1) + minDate)));

            // Type
            typeLambda = id =>
            {
                var value = rand.Next(10);
                if (value < 6)
                    return "0";
                if (value < 8)
                    return "1";
                if (value < 9)
                    return "2";
                else
                    return "3";
            };
            fields.Add(new Field<string>(
                "Type",
                typeLambda));

            // TombstoneState
            fields.Add(new Field<bool>(
                "TombstoneState",
                id =>
                {
                    return rand.Next(100) == 0;
                }));

            // Domain 
            fields.Add(new Field<string>(
                "Domain",
                id =>
                {
                    return rand.Next(100) > 94 ? "HNF" : "AAA";
                }));

            // Misc 
            for (var j = 0; j < 300; ++j)
                fields.Add(new Field<int>(
                    "Misc_" + j,
                    id =>
                    {
                        return rand.Next(10000);
                    }));

            return GetDocuments(fields, numberOfDocuments);
        }

        public override IEnumerable<Query> GetQueries(uint numberOfQueries, bool hasOrderBy = true)
        {
            Func<int, int> durationLambda = i =>
            {
                switch (i % 3)
                {
                    case 0:
                        return 7;
                    case 1:
                        return 30;
                    case 2:
                        return 60;
                }

                return 0;
            };

            for (var i = 0; i < Math.Ceiling(numberOfQueries / 3.0); ++i)
            {
                string userId = rand.Next((int)numberOfUsers).ToString("D19");
                for (var j = 0; j < 3; ++j)
                    yield return new Query(String.Format(
                        CultureInfo.InvariantCulture,
                        "(({0} AND {1}) AND (({2} AND {3}) AND {4}))",
                        String.Format(CultureInfo.InvariantCulture, "User = \"{0}\"", userId),
                        String.Format(CultureInfo.InvariantCulture, "(Date >= {0} AND Date <= {1})", minDate, minDate + 3600 * 24 * durationLambda(j)),
                        String.Format(CultureInfo.InvariantCulture, "Type = \"{0}\"", typeLambda(0)),
                        "TombstoneState = false",
                        "Domain = \"HNF\""));
            }
        }

        private int GetUnixTime(DateTime date)
        {
            return (int)date.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
        }

        internal override async Task<int> QueryAndVerifyDocuments(CosmosContainer container, IEnumerable<Query> queries, int pageSize = 1000, int retries = 0)
        {
            return await QueryAndVerifyDocuments(container, queries, pageSize, retries, false);
        }
    }

    public sealed class QueryOracle2 : QueryOracleUtil
    {
        private double[] uniquenessFactors = { 0.01, 0.02, 0.04, 0.08, 0.16, 0.32, 0.64, 0.80, 0.90, 1, 1 };
        private uint totalDocuments;
        private List<IField> fields;

        public QueryOracle2(int seed = 0)
            : base(seed)
        {
        }

        public override IEnumerable<string> GetDocuments(uint numberOfDocuments)
        {
            return GetDocuments(numberOfDocuments, null);
        }

        public IEnumerable<string> GetDocuments(uint numberOfDocuments, double[] factors = null)
        {
            fields = new List<IField>();
            totalDocuments = numberOfDocuments;

            if (factors != null)
                uniquenessFactors = factors;

            for (var i = 0; i < uniquenessFactors.Length; ++i)
                fields.Add(new Field<int>("field_" + i, id => rand.Next((int)(numberOfDocuments * uniquenessFactors[(id - 1) % uniquenessFactors.Length] + 1))));

            return GetDocuments(fields, numberOfDocuments);
        }

        public override IEnumerable<Query> GetQueries(uint numberOfQueries, bool hasOrderBy = true)
        {
            var fieldQueryBuilders = new List<IFieldQueryBuilder>();
            uint maxFilters = 3;

            for (var i = 0; i < uniquenessFactors.Length; ++i)
            {
                fieldQueryBuilders.Add(new IntFieldQueryBuilder("field_" + i, 0, (int)(totalDocuments * uniquenessFactors[i]), "", "", seed));
            }

            return GetQueries(fieldQueryBuilders, numberOfQueries, maxFilters, hasOrderBy ? fields : null);
        }

        internal override async Task<int> QueryAndVerifyDocuments(CosmosContainer container, IEnumerable<Query> queries, int pageSize = 1000, int retries = 0)
        {
            return await QueryAndVerifyDocuments(container, queries, pageSize, retries, false);
        }
    }

    public sealed class NativeMethods
    {
        [DllImport("FilterNodeDll.dll", SetLastError = true, CallingConvention = CallingConvention.Cdecl, BestFitMapping = false, ThrowOnUnmappableChar = true)]
        internal static extern
        UInt32 CreateFilterNode(
                [MarshalAs(UnmanagedType.LPWStr)] [In] string strFilter,
                [MarshalAs(UnmanagedType.LPStr)] [In] string strPrefix,
                [Out] out IntPtr pFilterNode);

        [DllImport("FilterNodeDll.dll", SetLastError = true, CallingConvention = CallingConvention.Cdecl, BestFitMapping = false, ThrowOnUnmappableChar = true)]
        internal static extern
        UInt32 Evaluate(
                [In] IntPtr pFilterNode,
                [MarshalAs(UnmanagedType.LPStr)] [In] string strDocument,
                [MarshalAs(UnmanagedType.I1)][Out] out bool bResult);

        [DllImport("FilterNodeDll.dll", SetLastError = true, CallingConvention = CallingConvention.Cdecl, BestFitMapping = false, ThrowOnUnmappableChar = true)]
        internal static extern
        UInt32 GetString(
                [In] IntPtr pFilterNode,
                [MarshalAs(UnmanagedType.LPStr)] StringBuilder filterBuffer,
                [MarshalAs(UnmanagedType.U4)] [In] UInt32 filterBufferSize,
                [Out] out UInt32 resultSize);

        [DllImport("FilterNodeDll.dll", SetLastError = true, CallingConvention = CallingConvention.Cdecl)]
        internal static extern
        UInt32 DeleteFilterNode([In] IntPtr pFilterNode);
    }
}