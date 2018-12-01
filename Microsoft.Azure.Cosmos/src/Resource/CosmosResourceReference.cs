//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Text;

    /// <summary>
    /// A base class for a the client reference objects like CosmosDatabase.
    /// This generates the URI needed for the resource and caches it.
    /// </summary>
    public abstract class CosmosIdentifier
    {
        internal abstract string UriPathSegment { get; }

        internal CosmosIdentifier(
            CosmosClient cosmosClient,
            string parentLink,
            string id)
        {
            this.Id = id;
            this.Client = cosmosClient;
            this.Link = GetLinkString(parentLink);
            this.LinkUri = GetLinkUri();
        }

        /// <summary>
        /// The Id of the cosmos resource
        /// </summary>
        public virtual string Id { get; }

        /// <summary>
        /// The Cosmos client that is used for the request
        /// </summary>
        internal CosmosClient Client { get; }

        /// <summary>
        /// The Cosmos resource URI as a string
        /// </summary>
        internal string Link { get; }

        /// <summary>
        /// The Cosmos resource URI
        /// </summary>
        internal Uri LinkUri { get; }

        /// <summary>
        /// Generates the URI link for the resource
        /// </summary>
        /// <param name="parentLink">The parent link URI (/dbs/mydbId) </param>
        /// <returns>A resource link in the format of {parentLink}/this.UriPathSegment/this.Name with this.Name being a Uri escaped version</returns>
        private string GetLinkString(string parentLink)
        {
            int parentLinkLength = parentLink?.Length ?? 0;
            string idUriEscaped = Uri.EscapeUriString(this.Id);

            StringBuilder stringBuilder = new StringBuilder(parentLinkLength + 2 + this.UriPathSegment.Length + idUriEscaped.Length);
            if (parentLinkLength > 0)
            {
                stringBuilder.Append(parentLink);
            }

            stringBuilder.Append("/");
            stringBuilder.Append(this.UriPathSegment);
            stringBuilder.Append("/");
            stringBuilder.Append(idUriEscaped);
            return stringBuilder.ToString();
        }

        private Uri GetLinkUri()
        {
            return new Uri(this.Link, UriKind.Relative);
        }
    }
}
