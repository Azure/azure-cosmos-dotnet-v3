//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    /// <summary>
    /// API for JSON processing
    /// </summary>
    internal enum JsonProcessor
    {
        /// <summary>
        /// Newtonsoft.Json
        /// </summary>
        Newtonsoft,

#if NET8_0_OR_GREATER
        /// <summary>
        /// Ut8JsonReader/Writer
        /// </summary>
        /// <remarks>
        /// Available with .NET8.0 package only.
        ///
        /// <para><b>Observable exception type difference vs <see cref="Newtonsoft"/>:</b>
        /// the streaming MDE decrypt path that this value selects (<c>SystemTextJsonStreamAdapter</c>)
        /// surfaces <see cref="System.Text.Json.JsonException"/> for malformed inputs where the
        /// default <see cref="Newtonsoft"/> path would surface
        /// <c>Newtonsoft.Json.JsonException</c> or <see cref="System.FormatException"/>.
        /// Both paths reject the same set of inputs; only the exception type differs because the
        /// two adapters fail at different layers (STJ model deserializer vs Newtonsoft base64
        /// decoder). Callers should catch <c>Exception</c> or match broadly on JSON-family
        /// exceptions when handling decrypt failures on a <see cref="Stream"/>-opt-in code path.</para>
        /// </remarks>
        Stream,
#endif
    }
}