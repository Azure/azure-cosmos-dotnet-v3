//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Linq
{
    /// <summary>
    /// Serializer to be used for LINQ query translations
    /// </summary>
    public enum LinqSerializerType
    {
        /// <summary>
        /// TODO
        /// </summary>
        Default,

        /// <summary>
        /// TODO
        /// </summary>
        Newtonsoft,

        /// <summary>
        /// TODO
        /// </summary>
        DataContract,

        /// <summary>
        /// TODO
        /// </summary>
        DotNet,
    }
}
