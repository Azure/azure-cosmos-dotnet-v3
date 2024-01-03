//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    /// <summary>
    /// Serializer type to be used for LINQ query translations.
    /// </summary>
    /// <example>
    /// This example creates a <see cref="CosmosLinqSerializerOptions"/>, which specifies a Linq serialization
    /// strategy.  This is passed to <see cref="Container.GetItemLinqQueryable"/> to apply the specified
    /// serializer.
    /// <code language="c#">
    /// <![CDATA[
    /// CosmosLinqSerializerOptions options = new()
    /// {
    ///        LinqSerializerType = CosmosLinqSerializerType.CustomCosmosSerializer
    /// };
    /// 
    /// Book book = container.GetItemLinqQueryable<Book>(true, linqSerializerOptions: options)
    ///                      .Where(b => b.Title == "War and Peace")
    ///                     .AsEnumerable()
    ///                     .FirstOrDefault();
    /// ]]>
    /// </code>
    /// </example>
#if PREVIEW
    public
#else
    internal
#endif
        enum CosmosLinqSerializerType
    {
        /// <summary>
        /// This honors Newtonsoft attributes, followed by DataContract attributes. This will ignore System.Text.Json attributes.
        /// </summary>
        Default,

        /// <summary>
        /// Uses the provided custom CosmosSerializer.
        /// This requires:
        /// 1. a <see cref="CosmosSerializer"/> to be provided on a client, and
        /// 2. the custom CosmosSerializer implements <see cref="ICosmosLinqSerializer"/>
        /// </summary>
        CustomCosmosSerializer,
    }
}
