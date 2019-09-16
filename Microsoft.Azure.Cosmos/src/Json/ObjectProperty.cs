//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Json
{
    using System;

    /// <summary>
    /// Struct to hold the property name and property value for an object property.
    /// </summary>
#if INTERNAL
    public
#else
    internal
#endif
    struct ObjectProperty
    {
        /// <summary>
        /// Initializes a new instance of the ObjectProperty struct.
        /// </summary>
        /// <param name="nameNode">The IJsonNavigatorNode to the node that holds the object property name.</param>
        /// <param name="valueNode">The IJsonNavigatorNode to the node that holds the object property value.</param>
        public ObjectProperty(
            ReadOnlyMemory<byte> nameNode,
            ReadOnlyMemory<byte> valueNode)
        {
            this.NameNode = nameNode;
            this.ValueNode = valueNode;
        }

        /// <summary>
        /// The node that holds the object property name.
        /// </summary>
        public ReadOnlyMemory<byte> NameNode { get; }

        /// <summary>
        /// The node that holds the object property value.
        /// </summary>
        public ReadOnlyMemory<byte> ValueNode { get; }
    }
}
