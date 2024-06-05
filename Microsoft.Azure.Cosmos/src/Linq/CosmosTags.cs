//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// Tag query options for CosmosTags.Match
    /// </summary>
    [Flags]
    public enum TagsQueryOptions
    {
        /// <summary>
        /// Basic tag matching (Exact match, explicit wildcards, implicit wildcards, not and required query tags)
        /// </summary>
        Basic = 0,

        /// <summary>
        /// Support the ! operator on tags in documents
        /// </summary>
        DocumentNotTags = 4,

        /// <summary>
        /// Support the * operator on tags in documents. NOTE: This is extremely expensive for large data sets.
        /// </summary>
        DocumentRequiredTags = 8,

        /// <summary>
        /// Support basic tag matching, and not tags on the documents.
        /// </summary>
        Default = Basic | DocumentNotTags,
    }
    
    /// <summary>
    /// Tag matching for LINQ
    /// </summary>
    public static class CosmosTags
    {
        /// <summary>
        /// The default UdfNane "TagsMatch"
        /// </summary>
        public const string UdfNameDefault = "TagsMatch";
    
        /// <summary>
        /// Tag matching for LINQ
        /// </summary>
        /// <param name="dataTags">The data tags</param>
        /// <param name="queryTags">An XTags instance</param>
        /// <param name="queryOptions">Tag query options for performance </param>
        /// <returns>throws Exception</returns>
        public static bool Match(object dataTags, object queryTags, TagsQueryOptions queryOptions) => throw new Exception("CosmosTags.Match is only for linq expressions");

        /// <summary>
        /// Tag matching for LINQ
        /// </summary>
        /// <param name="dataTags">The data tags</param>
        /// <param name="queryTags">An XTags instance</param>
        /// <param name="queryOptions">Tag query options for performance </param>
        /// <param name="udfName">The name of the tag matching UDF when TagsQueryOptions.DocumentRequiredTags is specified</param>
        /// <returns>throws Exception</returns>
        public static bool Match(object dataTags, object queryTags, TagsQueryOptions queryOptions, string udfName = UdfNameDefault) => throw new Exception("CosmosTags.Match is only for linq expressions");

        /// <summary>
        /// Matches any of the Filters using OR.
        /// Each FilterList is evaluated using AND
        /// </summary>
        /// <param name="filters">List of MatchObjectList</param>
        /// <returns>True if any Filter matches</returns>
        /// <exception cref="Exception">Used in LINQ</exception>
        public static bool MatchAny(IEnumerable<MatchObjectList> filters) => throw new Exception("CosmosTags.MatchAny is only for linq expressions");

        /// <summary>
        /// Matches any of the Filters using OR.
        /// Each FilterList is evaluated using AND
        /// </summary>
        /// <param name="filters">List of MatchObjectList</param>
        /// <returns>True if any Filter matches</returns>
        /// <exception cref="Exception">Used in LINQ</exception>
        public static bool MatchAny(params MatchObjectList[] filters) => throw new Exception("CosmosTags.MatchAny is only for linq expressions");

        /// <summary>
        /// Creates and SQL WHERE condition string from a tag match expression.
        /// </summary>
        /// <param name="tagsProp"></param>
        /// <param name="tags"></param>
        /// <param name="queryOptions"></param>
        /// <param name="udfName">The name of the tag matching UDF when TagsQueryOptions.DocumentRequiredTags is specified</param>
        /// <returns>The tag expression as an SQL condition</returns>
        public static string Condition(string tagsProp, IEnumerable<string> tags, TagsQueryOptions queryOptions, string udfName = UdfNameDefault)
        {
            const char nullOperator = '\0';
            const char requiredOperator = '*';
            const char requiredExactOperator = '@';
            const char notOperator = '!';

            (char Operator, string Namespace, string Name, string Value, string Tag, bool IsNot, bool IsRequired, bool IsRequiredExact, bool IsWildcard) Parse(string tag)
            {
                int indexOfColon = tag.IndexOf(':');
                int indexOfEquals = tag.IndexOf('=');

                if (indexOfColon < 1 || indexOfEquals < 3 || indexOfColon > indexOfEquals)
                    throw new ArgumentException("Tag is not a machine tag");

                char op = tag[0] == notOperator || tag[0] == requiredOperator || tag[0] == requiredExactOperator ? tag[0] : nullOperator;
                string ns = tag.Substring(op == nullOperator ? 0 : 1, op == nullOperator ? indexOfColon : indexOfColon - 1);
                string name = tag.Substring(indexOfColon + 1, indexOfEquals - (indexOfColon + 1));
                string value = tag.Substring(indexOfEquals + 1);

                return (op, ns, name, value, tag, op == notOperator, op == requiredOperator, op == requiredExactOperator, value.Length == 0);
            }

            var sb = new StringBuilder();
            var tagProp = $"{tagsProp}[\"tag\"]";

            if (queryOptions.HasFlag(TagsQueryOptions.DocumentRequiredTags))
            {
                var tagsExpression = $"[{string.Join(",", tags.Select(x => $"\"{x}\""))}]";
                sb.Append($"udf.{udfName}({tagProp}, {tagsExpression})");
            }
            else if (tags.Any())
            {
                var needsLoopAnd = false;
                var machineTags = tags.Select(Parse);
                var tagsByGroup = machineTags.GroupBy(x => x.Namespace + ":" + x.Name);
                foreach (var grouping in tagsByGroup)
                {
                    var needsRegularsAnd = false;
                    var needsNotsAnd = false;
                    var tagName = grouping.Key;
                    var regulars = grouping.Where(x => x.Operator == nullOperator).ToArray();
                    var nots = grouping.Where(x => x.IsNot);
                    var requireds = grouping.Where(x => x.IsRequired || x.IsRequiredExact);
                    var wildcardTag = grouping.Key + "=";
                    var regularProp = $"{tagsProp}[\"tags\"][\"{tagName}\"]";
                    var notProp = $"{tagsProp}[\"tags\"][\"!{tagName}\"]";

                    if (nots.Any())
                    {
                        if (nots.Any(x => x.IsWildcard))
                        {
                            if (needsLoopAnd)
                                sb.Append(" AND ");

                            sb.Append($"NOT(IS_DEFINED({regularProp}))");
                            needsNotsAnd = true;
                            needsLoopAnd = true;
                        }
                        else
                        {
                            if (needsLoopAnd)
                                sb.Append(" AND ");

                            sb.Append($"NOT(ARRAY_CONTAINS({tagProp}, \"{wildcardTag}\"))");
                            foreach (var not in nots)
                                sb.Append($" AND NOT(ARRAY_CONTAINS({tagProp}, \"{not.Tag.Substring(1)}\"))");
                            needsNotsAnd = true;
                            needsLoopAnd = true;
                        }
                    }
                    if (regulars.Any())
                    {
                        if (regulars.Any(x => x.IsWildcard))
                        {
                            if (queryOptions.HasFlag(TagsQueryOptions.DocumentNotTags))
                            {
                                if (needsLoopAnd || needsNotsAnd)
                                    sb.Append(" AND ");

                                sb.Append($"NOT(IS_DEFINED({notProp}))");

                                needsRegularsAnd = true;
                                needsLoopAnd = true;
                            }
                        }
                        else
                        {
                            if (needsLoopAnd || needsNotsAnd)
                                sb.Append(" AND ");

                            if (queryOptions.HasFlag(TagsQueryOptions.DocumentNotTags))
                            {
                                sb.Append("(");

                                // No document "not" tags set
                                sb.Append($"NOT(IS_DEFINED({notProp}))");
                                sb.Append(" OR ");
                                // Explicit document "not" wildcard is set
                                sb.Append($"NOT(ARRAY_CONTAINS({tagProp}, \"!{wildcardTag}\"))");

                                // Explicit document "not" tag is set
                                foreach (var regular in regulars)
                                {
                                    sb.Append(" AND ");
                                    sb.Append($"NOT(ARRAY_CONTAINS({tagProp}, \"!{regular.Tag}\"))");
                                }

                                sb.Append(")");
                                sb.Append(" AND ");
                            }

                            sb.Append("(");
                            // Implicit document wildcard
                            sb.Append($"NOT(IS_DEFINED({regularProp}))");
                            sb.Append(" OR ");
                            // Explicit document wildcard
                            sb.Append($"ARRAY_CONTAINS({tagProp}, \"{wildcardTag}\")");

                            // Explicit document tag is set
                            foreach (var regular in regulars)
                                sb.Append($" OR ARRAY_CONTAINS({tagProp}, \"{regular.Tag}\")");

                            sb.Append(")");

                            needsRegularsAnd = true;
                            needsLoopAnd = true;
                        }
                    }
                    if (requireds.Any())
                    {
                        if (needsLoopAnd || needsNotsAnd || needsRegularsAnd)
                            sb.Append(" AND ");

                        if (requireds.Any(x => x is { IsWildcard: true, IsRequiredExact: false }))
                        {
                            sb.Append($"IS_DEFINED({regularProp})");
                        }
                        else
                        {
                            var needsOr = false;
                            sb.Append("(");
                            if (requireds.Any(x => x is { IsRequiredExact: false }) || requireds.All(x => x is { IsWildcard: true }))
                            {
                                sb.Append($"ARRAY_CONTAINS({tagProp}, \"{wildcardTag}\")");
                                needsOr = true;
                            }
                            foreach (var required in requireds.Where(x => !x.IsWildcard))
                            {
                                if (needsOr)
                                    sb.Append(" OR ");
                                sb.Append($"ARRAY_CONTAINS({tagProp}, \"{required.Tag.Substring(1)}\")");
                                needsOr = true;
                            }

                            sb.Append(")");
                        }

                        needsLoopAnd = true;
                    }
                }

                if (sb.Length > 0)
                {
                    sb.Insert(0, "(");
                    sb.Append(")");
                }
            }
            else
            {
                sb.Append("true");
            }

            if (sb.Length == 0)
                sb.Append("true");

            return sb.ToString();
        }
    }
    
    /// <summary>
    /// List of MatchObjects
    /// </summary>
    public class MatchObjectList : IEnumerable<MatchObject>
    {
        private readonly IEnumerable<MatchObject> matchObjects;

        /// <summary>
        /// Creates a new MatchObjectList
        /// </summary>
        public MatchObjectList() => matchObjects = Enumerable.Empty<MatchObject>();

        /// <summary>
        /// Creates a new MatchObjectList from an existing list
        /// </summary>
        /// <param name="matchObjects"></param>
        public MatchObjectList(IEnumerable<MatchObject> matchObjects) => this.matchObjects = matchObjects;

        /// <summary>
        /// Get the Enumerator
        /// </summary>
        /// <returns>The Enumerator</returns>
        public IEnumerator<MatchObject> GetEnumerator() => matchObjects.GetEnumerator();

        /// <summary>
        /// Get the Enumerator
        /// </summary>
        /// <returns>The Enumerator</returns>
        IEnumerator IEnumerable.GetEnumerator() => matchObjects.GetEnumerator();
    }
    
    /// <summary>
    /// Used for TagMatchAny
    /// </summary>
    public class MatchObject
    {
        /// <summary>
        /// Creates a new MatchObject
        /// </summary>
        /// <param name="dataTags"></param>
        /// <param name="queryTags"></param>
        /// <param name="tagsQueryOptions"></param>
        /// <returns>new MatchObject</returns>
        public static MatchObject Create(object dataTags, IEnumerable<string> queryTags, TagsQueryOptions tagsQueryOptions)
            => Create(dataTags, queryTags, tagsQueryOptions, CosmosTags.UdfNameDefault);

        /// <summary>
        /// Creates a new MatchObject
        /// </summary>
        /// <param name="dataTags"></param>
        /// <param name="queryTags"></param>
        /// <param name="tagsQueryOptions"></param>
        /// <param name="udfName"></param>
        /// <returns>new MatchObject</returns>
        public static MatchObject Create(object dataTags, IEnumerable<string> queryTags, TagsQueryOptions tagsQueryOptions, string udfName = CosmosTags.UdfNameDefault)
            => new (dataTags, queryTags, tagsQueryOptions, udfName);

        /// <summary>
        /// Creates a new MatchObject
        /// </summary>
        public MatchObject()
        {
        }

        /// <summary>
        /// Creates a new MatchObject
        /// </summary>
        /// <param name="dataTags"></param>
        /// <param name="queryTags"></param>
        /// <param name="tagsQueryOptions"></param>
        public MatchObject(object dataTags, IEnumerable<string> queryTags, TagsQueryOptions tagsQueryOptions)
            : this(dataTags, queryTags, tagsQueryOptions, CosmosTags.UdfNameDefault)
        {
        }

        /// <summary>
        /// Creates a new MatchObject
        /// </summary>
        /// <param name="dataTags"></param>
        /// <param name="queryTags"></param>
        /// <param name="tagsQueryOptions"></param>
        /// <param name="udfName"></param>
        public MatchObject(object dataTags, IEnumerable<string> queryTags, TagsQueryOptions tagsQueryOptions, string udfName)
        {
            DataTags = dataTags;
            QueryTags = queryTags;
            QueryOptions = tagsQueryOptions;
            UdfName = udfName;
        }
        
        /// <summary>
        /// The expression for the tags on the document
        /// </summary>
        public object DataTags { get; set; }
        /// <summary>
        /// The list of tags used to filter as string[]
        /// </summary>
        public IEnumerable<string> QueryTags { get; set; }
        /// <summary>
        /// The TagsQueryOptions 
        /// </summary>
        public TagsQueryOptions QueryOptions { get; set; }
        /// <summary>
        /// Optionally the name of the UdfName to use
        /// </summary>
        public string UdfName { get; set; }
    }
}