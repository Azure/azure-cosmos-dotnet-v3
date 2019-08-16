//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Json
{
    /// <summary>
    /// Enum of JsonTokenType
    /// </summary>
#if INTERNAL
    public
#else
    internal
#endif
    enum JsonTokenType
    {
        /// <summary>
        /// Reserved for no other value
        /// </summary>
        NotStarted,

        /// <summary>
        /// Corresponds to the beginning of a JSON array ('[')
        /// </summary>
        BeginArray,

        /// <summary>
        /// Corresponds to the end of a JSON array (']')
        /// </summary>
        EndArray,

        /// <summary>
        /// Corresponds to the beginning of a JSON object ('{')
        /// </summary>
        BeginObject,

        /// <summary>
        /// Corresponds to the end of a JSON object ('}')
        /// </summary>
        EndObject,

        /// <summary>
        /// Corresponds to a JSON string.
        /// </summary>
        String,

        /// <summary>
        /// Corresponds to a JSON number.
        /// </summary>
        Number,

        /// <summary>
        /// Corresponds to the JSON 'true' value.
        /// </summary>
        True,

        /// <summary>
        /// Corresponds to the JSON 'false' value.
        /// </summary>
        False,

        /// <summary>
        /// Corresponds to the JSON 'null' value.
        /// </summary>
        Null,

        /// <summary>
        /// Corresponds to the JSON fieldname in a JSON object.
        /// </summary>
        FieldName,

        /// <summary>
        /// Corresponds to an arbitrary sequence of bytes in an object.
        /// </summary>
        Binary,
    }
}
