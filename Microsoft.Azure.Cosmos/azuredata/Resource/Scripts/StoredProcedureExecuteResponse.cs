//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.Scripts
{
    using System;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// The cosmos stored procedure response
    /// </summary>
    public class StoredProcedureExecuteResponse<T> : Response<T>
    {
        private readonly Response rawResponse;

        /// <summary>
        /// Create a <see cref="StoredProcedureExecuteResponse{T}"/> as a no-op for mock testing
        /// </summary>
        protected StoredProcedureExecuteResponse()
            : base()
        {
        }

        /// <summary>
        /// A private constructor to ensure the factory is used to create the object.
        /// This will prevent memory leaks when handling the HttpResponseMessage
        /// </summary>
        internal StoredProcedureExecuteResponse(
            Response response,
            T storedProcedureResponse)
        {
            this.rawResponse = response;
            this.Value = storedProcedureResponse;
        }

        /// <inheritdoc/>
        public override Response GetRawResponse() => this.rawResponse;

        /// <inheritdoc/>
        public override T Value { get; }

        /// <summary>
        /// Gets the output from stored procedure console.log() statements.
        /// </summary>
        /// <value>
        /// Output from console.log() statements in a stored procedure.
        /// </value>
        /// <seealso cref="StoredProcedureRequestOptions.EnableScriptLogging"/>
        public virtual string ScriptLog
        {
            get
            {
                if (this.rawResponse != null
                    && this.rawResponse.Headers.TryGetValue(HttpConstants.HttpHeaders.LogResults, out string logResults))
                {
                    return Uri.UnescapeDataString(logResults);
                }

                return null;
            }
        }
    }
}