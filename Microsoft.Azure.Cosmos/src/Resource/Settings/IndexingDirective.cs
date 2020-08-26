//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;

    /// <summary> 
    /// Specifies whether or not the resource in the Azure Cosmos DB database is to be indexed.
    /// </summary>
    public enum IndexingDirective
    {
        /// <summary>
        /// Use any pre-defined/pre-configured defaults.
        /// </summary>
        Default,

        /// <summary>
        /// Index the resource.
        /// </summary>
        Include,

        /// <summary>
        ///  Do not index the resource.
        /// </summary>
        Exclude
    }

#pragma warning disable SA1649 // File name should match first type name
    internal static class IndexingDirectiveStrings
#pragma warning restore SA1649 // File name should match first type name
    {
        public static readonly string Default = IndexingDirective.Default.ToString();
        public static readonly string Include = IndexingDirective.Include.ToString();
        public static readonly string Exclude = IndexingDirective.Exclude.ToString();

        public static string FromIndexingDirective(IndexingDirective directive)
        {
            return directive switch
            {
                IndexingDirective.Default => IndexingDirectiveStrings.Default,
                IndexingDirective.Exclude => IndexingDirectiveStrings.Exclude,
                IndexingDirective.Include => IndexingDirectiveStrings.Include,
                _ => throw new ArgumentException(string.Format("Missing indexing directive string for {0}", directive)),
            };
        }
    }
}
