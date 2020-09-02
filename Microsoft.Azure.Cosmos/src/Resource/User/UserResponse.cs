//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Net;

    /// <summary>
    /// The cosmos user response
    /// </summary>
    public class UserResponse : Response<UserProperties>
    {
        /// <summary>
        /// Create a <see cref="UserResponse"/> as a no-op for mock testing
        /// </summary>
        protected UserResponse()
            : base()
        {
        }

        /// <summary>
        /// A private constructor to ensure the factory is used to create the object.
        /// This will prevent memory leaks when handling the HttpResponseMessage
        /// </summary>
        internal UserResponse(
            HttpStatusCode httpStatusCode,
            Headers headers,
            UserProperties userProperties,
            User user,
            CosmosDiagnostics diagnostics)
        {
            this.StatusCode = httpStatusCode;
            this.Headers = headers;
            this.Resource = userProperties;
            this.User = user;
            this.Diagnostics = diagnostics;
        }

        /// <summary>
        /// The reference to the cosmos user. This allows additional operations on the user
        /// or for easy access permissions
        /// </summary>
        public virtual User User { get; private set; }

        /// <inheritdoc/>
        public override Headers Headers { get; }

        /// <inheritdoc/>
        public override UserProperties Resource { get; }

        /// <inheritdoc/>
        public override HttpStatusCode StatusCode { get; }

        /// <inheritdoc/>
        public override CosmosDiagnostics Diagnostics { get; }

        /// <inheritdoc/>
        public override double RequestCharge => this.Headers?.RequestCharge ?? 0;

        /// <inheritdoc/>
        public override string ActivityId => this.Headers?.ActivityId;

        /// <inheritdoc/>
        public override string ETag => this.Headers?.ETag;

        /// <summary>
        /// Get <see cref="Cosmos.User"/> implicitly from <see cref="UserResponse"/>
        /// </summary>
        /// <param name="response">UserResponse</param>
        public static implicit operator User(UserResponse response)
        {
            return response.User;
        }
    }
}
