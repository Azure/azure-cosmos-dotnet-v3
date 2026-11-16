//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;

    /// <summary> 
    /// Specifies whether or not the resource in the Azure Cosmos DB database is to be indexed.
    /// </summary>
#if COSMOSCLIENT
    internal
#else
    public
#endif
    enum IndexingDirective
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

    internal static class IndexingDirectiveStrings
    {
        public static readonly string Default = IndexingDirective.Default.ToString();
        public static readonly string Include = IndexingDirective.Include.ToString();
        public static readonly string Exclude = IndexingDirective.Exclude.ToString();

        public static string FromIndexingDirective(IndexingDirective directive)
        {
            switch (directive)
            {
                case IndexingDirective.Default:
                    return IndexingDirectiveStrings.Default;
                case IndexingDirective.Exclude:
                    return IndexingDirectiveStrings.Exclude;
                case IndexingDirective.Include:
                    return IndexingDirectiveStrings.Include;
                default:
                    throw new ArgumentException(string.Format("Missing indexing directive string for {0}", directive));
            }
        }
    }
}
