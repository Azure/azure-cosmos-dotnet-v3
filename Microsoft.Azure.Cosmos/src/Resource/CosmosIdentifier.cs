//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Globalization;
    using System.Text;

    /// <summary>
    /// A base class for a the client reference objects like CosmosDatabase.
    /// This generates the URI needed for the resource and caches it.
    /// </summary>
    public abstract class CosmosIdentifier
    {
        private static readonly char[] InvalidCharacters = new char[] { '/', '\\', '?', '#' };

        /// <summary>
        /// The Id of the cosmos resource
        /// </summary>
        public abstract string Id { get; }

        /// <summary>
        /// The Cosmos client that is used for the request
        /// </summary>
        internal abstract CosmosClient Client { get; }

        /// <summary>
        /// The Cosmos resource URI
        /// </summary>
        internal abstract Uri LinkUri { get; }

        /// <summary>
        /// Generates the URI link for the resource
        /// </summary>
        /// <param name="parentLink">The parent link URI (/dbs/mydbId) </param>
        /// <param name="uriPathSegment">The URI path segment</param>
        /// <returns>A resource link in the format of {parentLink}/this.UriPathSegment/this.Name with this.Name being a Uri escaped version</returns>
        protected Uri GetLink(string parentLink, string uriPathSegment)
        {
            int parentLinkLength = parentLink?.Length ?? 0;
            string idUriEscaped = Uri.EscapeUriString(this.Id);

            StringBuilder stringBuilder = new StringBuilder(parentLinkLength + 2 + uriPathSegment.Length + idUriEscaped.Length);
            if (parentLinkLength > 0)
            {
                stringBuilder.Append(parentLink);
            }

            stringBuilder.Append("/");
            stringBuilder.Append(uriPathSegment);
            stringBuilder.Append("/");
            stringBuilder.Append(idUriEscaped);
            return new Uri(stringBuilder.ToString(), UriKind.Relative);
        }

        internal static void ValidateResource(CosmosResource resource)
        {
            if (!string.IsNullOrEmpty(resource.Id))
            {
                int match = resource.Id.IndexOfAny(CosmosIdentifier.InvalidCharacters);
                if (match != -1)
                {
                    throw new ArgumentException(string.Format(
                                CultureInfo.CurrentUICulture,
                                RMResources.InvalidCharacterInResourceName,
                                resource.Id[match]
                                ));
                }

                if (resource.Id[resource.Id.Length - 1] == ' ')
                {
                    throw new ArgumentException(RMResources.InvalidSpaceEndingInResourceName);
                }
            }
        }
    }
}
