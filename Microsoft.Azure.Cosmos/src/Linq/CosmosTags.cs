//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
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
        public static bool Match(object dataTags, object queryTags, TagsQueryOptions queryOptions, string udfName = "TagsMatch") => throw new Exception("CosmosTags.Match is only for linq expressions");

        /// <summary>
        /// Creates and SQL WHERE condition string from a tag match expression.
        /// </summary>
        /// <param name="tagsProp"></param>
        /// <param name="tags"></param>
        /// <param name="queryOptions"></param>
        /// <param name="udfName">The name of the tag matching UDF when TagsQueryOptions.DocumentRequiredTags is specified</param>
        /// <returns>The tag expression as an SQL condition</returns>
        public static string Condition(string tagsProp, IEnumerable<string> tags, TagsQueryOptions queryOptions, string udfName = "TagsMatch")
        {
            const char nullOperator = '\0';
            const char requiredOperator = '*';
            const char notOperator = '!';

            (char Operator, string Namespace, string Name, string Value, string Tag, bool IsNot, bool IsRequired, bool IsWildcard) Parse(string tag)
            {
                int indexOfColon = tag.IndexOf(':');
                int indexOfEquals = tag.IndexOf('=');

                if (indexOfColon < 1 || indexOfEquals < 3 || indexOfColon > indexOfEquals)
                    throw new ArgumentException("Tag is not a machine tag");

                char op = tag[0] == notOperator || tag[0] == requiredOperator ? tag[0] : nullOperator;
                string ns = tag.Substring(op == nullOperator ? 0 : 1, op == nullOperator ? indexOfColon : indexOfColon - 1);
                string name = tag.Substring(indexOfColon + 1, indexOfEquals - (indexOfColon + 1));
                string value = tag.Substring(indexOfEquals + 1);

                return (op, ns, name, value, tag, op == '!', op == '*', value.Length == 0);
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
                    var requireds = grouping.Where(x => x.IsRequired);
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
                        sb.Append("(");
                        sb.Append($"ARRAY_CONTAINS({tagProp}, \"{wildcardTag}\")");
                        sb.Append(" OR ");
                        foreach ((var value, int index) in requireds.Select((v, i) => (v, i)))
                        {
                            if (index > 0)
                                sb.Append(" OR ");
                            sb.Append($"ARRAY_CONTAINS({tagProp}, \"{value.Tag.Substring(1)}\")");
                        }
                        sb.Append(")");

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
}