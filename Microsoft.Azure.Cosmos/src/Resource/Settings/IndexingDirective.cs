//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
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

    internal static class IndexingDirectiveStrings
    {
        public static readonly string Default = IndexingDirective.Default.ToString();
        public static readonly string Include = IndexingDirective.Include.ToString();
        public static readonly string Exclude = IndexingDirective.Exclude.ToString();
    }
}
