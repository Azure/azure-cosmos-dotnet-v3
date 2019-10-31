//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos
{
    /// <summary>
    /// The cosmos permission response
    /// </summary>
    public class PermissionResponse : Response<PermissionProperties>
    {
        private readonly Response rawResponse;

        /// <summary>
        /// Create a <see cref="PermissionResponse"/> as a no-op for mock testing
        /// </summary>
        protected PermissionResponse()
            : base()
        {
        }

        /// <summary>
        /// A private constructor to ensure the factory is used to create the object.
        /// This will prevent memory leaks when handling the HttpResponseMessage
        /// </summary>
        internal PermissionResponse(
            Response response,
            PermissionProperties permissionProperties,
            Permission permission)
        {
            this.rawResponse = response;
            this.Value = permissionProperties;
            this.Permission = permission;
        }

        /// <inheritdoc/>
        public override Response GetRawResponse() => this.rawResponse;

        /// <inheritdoc/>
        public override PermissionProperties Value { get; }

        /// <summary>
        /// The reference to the cosmos permission. This allows additional operations on the permission
        /// or for easy access permissions
        /// </summary>
        public virtual Permission Permission { get; private set; }

        /// <summary>
        /// Get <see cref="Cosmos.Permission"/> implicitly from <see cref="PermissionResponse"/>
        /// </summary>
        /// <param name="response">PermissionResponse</param>
        public static implicit operator Permission(PermissionResponse response)
        {
            return response.Permission;
        }
    }
}
