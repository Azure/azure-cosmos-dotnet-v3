//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos
{
    /// <summary>
    /// The cosmos user response
    /// </summary>
    public class UserResponse : Response<UserProperties>
    {
        private readonly Response rawResponse;

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
            Response response,
            UserProperties userProperties,
            User user)
        {
            this.rawResponse = response;
            this.Value = userProperties;
            this.User = user;
        }

        /// <inheritdoc/>
        public override Response GetRawResponse() => this.rawResponse;

        /// <inheritdoc/>
        public override UserProperties Value { get; }

        /// <summary>
        /// The reference to the cosmos user. This allows additional operations on the user
        /// or for easy access permissions
        /// </summary>
        public virtual User User { get; private set; }

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
