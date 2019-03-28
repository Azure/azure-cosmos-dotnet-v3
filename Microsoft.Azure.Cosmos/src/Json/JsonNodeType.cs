//-----------------------------------------------------------------------
// <copyright file="JsonNodeType.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Json
{
    /// <summary>
    /// The enumeration of JSON node types
    /// </summary>
    internal enum JsonNodeType
    {
        /// <summary>
        /// Corresponds to the 'null' value in JSON.
        /// </summary>
        Null,

        /// <summary>
        /// Corresponds to the 'false' value in JSON.
        /// </summary>
        False,

        /// <summary>
        /// Corresponds to the 'true' value in JSON.
        /// </summary>
        True,

        /// <summary>
        /// Corresponds to the number type in JSON (number = [ minus ] integer [ fraction ] [ exponent ])
        /// </summary>
        Number,

        /// <summary>
        /// Corresponds to the string type in JSON (string = quotation-mark *char quotation-mark)
        /// </summary>
        String,

        /// <summary>
        /// Corresponds to the array type in JSON ( begin-array [ value *( value-separator value ) ] end-array)
        /// </summary>
        Array,

        /// <summary>
        /// Corresponds to the object type in JSON (begin-object [ member *( value-separator member ) ] end-object)
        /// </summary>
        Object,

        /// <summary>
        /// Corresponds to the property name of a JSON object property (which is also a string).
        /// </summary>
        FieldName,

        /// <summary>
        /// Unknown JsonNodeType.
        /// </summary>
        Unknown,
    }
}
