//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Scripts;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Routing;

    /// <summary>
    /// Operations for reading, replacing, or deleting a specific, existing user by id.
    /// 
    /// <see cref="Cosmos.User"/> for creating new users, and reading/querying all user;
    /// </summary>
    public class UserCore : User
    {
        /// <summary>
        /// Only used for unit testing
        /// </summary>
        internal UserCore()
        {
        }

        internal UserCore(
            CosmosClientContext clientContext,
            DatabaseCore database,
            string userId)
        {
            this.Id = userId;
            this.ClientContext = clientContext;
            this.LinkUri = clientContext.CreateLink(
                parentLink: database.LinkUri.OriginalString,
                uriPathSegment: Paths.CollectionsPathSegment,
                id: userId);

            this.Database = database;
            this.cachedUriSegmentWithoutId = this.GetResourceSegmentUriWithoutId();
        }

        /// <summary>
        /// Cache the full URI segment without the last resource id.
        /// This allows only a single con-cat operation instead of building the full URI string each time.
        /// </summary>
        private string cachedUriSegmentWithoutId { get; }

        /// <inheritdoc/>
        public override string Id { get; }

        /// <summary>
        /// Returns a reference to a database object. 
        /// </summary>
        public Database Database { get; }

        internal virtual Uri LinkUri { get; }

        internal virtual CosmosClientContext ClientContext { get; }

        /// <inheritdoc/>
        public override Task<UserResponse> ReadUserAsync(RequestOptions requestOptions = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override Task<ResponseMessage> ReadUserStreamAsync(RequestOptions requestOptions = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override Task<UserResponse> ReplaceUserAsync(UserProperties userProperties, RequestOptions requestOptions = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override Task<ResponseMessage> ReplaceUserStreamAsync(UserProperties userProperties, RequestOptions requestOptions = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override Task<UserResponse> DeleteUserAsync(RequestOptions requestOptions = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override Task<ResponseMessage> DeleteUserStreamAsync(RequestOptions requestOptions = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new NotImplementedException();
        }

        private string GetResourceSegmentUriWithoutId()
        {
            // StringBuilder is roughly 2x faster than string.Format
            StringBuilder stringBuilder = new StringBuilder(this.LinkUri.OriginalString.Length +
                                                            Paths.DocumentsPathSegment.Length + 2);
            stringBuilder.Append(this.LinkUri.OriginalString);
            stringBuilder.Append("/");
            stringBuilder.Append(Paths.UsersPathSegment);
            stringBuilder.Append("/");
            return stringBuilder.ToString();
        }
    }
}
