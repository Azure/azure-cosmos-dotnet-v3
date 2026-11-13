//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    /// <summary> 
    /// Specifies the supported schema builder modes.
    /// </summary>
    internal enum SchemaBuilderMode
    {
        /// <summary>
        /// SchemaBuilder is active and running lazily.
        /// </summary>
        /// <remarks>
        /// Setting the SchemaBuilderMode to "Lazy" ensures the schema builder will be running in the background either 
        /// as it's own process (consistent indexing) or with the lazy index processor (lazy indexing)
        /// </remarks>
        Lazy,

        /// <summary>
        /// Schema builder is not active.
        /// </summary>
        /// <remarks>
        /// Setting SchemaBuilderMode to "None" ensures that the schema builder will be disabled and no longer functioning.
        /// </remarks>
        None
    }
}
