//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Security.Cryptography;
    using System.Text;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;

    // This class is used by both client (for generating the auth header with master/system key) and 
    // by the G/W when verifying the auth header. Some additional logic is also used by management service.
    internal sealed class AuthorizationHelper
    {
        public const int MaxAuthorizationHeaderSize = 1024;

        // This API is a helper method to create auth header based on client request.
        // Uri is split into resourceType/resourceId - 
        // For feed/post/put requests, resourceId = parentId,
        // For point get requests,     resourceId = last segment in URI
        public static string GenerateKeyAuthorizationSignature(string verb,
               Uri uri,
               INameValueCollection headers,
               IComputeHash stringHMACSHA256Helper,
               string clientVersion = "")
        {
            if (string.IsNullOrEmpty(verb))
            {
                throw new ArgumentException(RMResources.StringArgumentNullOrEmpty, "verb");
            }

            if (uri == null)
            {
                throw new ArgumentNullException("uri");
            }

            if (stringHMACSHA256Helper == null)
            {
                throw new ArgumentNullException("stringHMACSHA256Helper");
            }

            if (headers == null)
            {
                throw new ArgumentNullException("headers");
            }

            string resourceType = string.Empty;
            string resourceIdValue = string.Empty;
            bool isNameBased = false;

            AuthorizationHelper.GetResourceTypeAndIdOrFullName(uri, out isNameBased, out resourceType, out resourceIdValue, clientVersion);

            string payload;
            return AuthorizationHelper.GenerateKeyAuthorizationSignature(verb,
                         resourceIdValue,
                         resourceType,
                         headers,
                         stringHMACSHA256Helper,
                         out payload);
        }

        // This is a helper for both system and master keys
        [SuppressMessage("Microsoft.Globalization", "CA1308:NormalizeStringsToUppercase", Justification = "HTTP Headers are ASCII")]
        public static string GenerateKeyAuthorizationSignature(string verb,
            string resourceId,
            string resourceType,
            INameValueCollection headers,
            string key,
            bool bUseUtcNowForMissingXDate = false)
        {
            string payload;
            return AuthorizationHelper.GenerateKeyAuthorizationSignature(verb,
                resourceId,
                resourceType,
                headers,
                key,
                out payload,
                bUseUtcNowForMissingXDate);
        }

        // This is a helper for both system and master keys
        [SuppressMessage("Microsoft.Globalization", "CA1308:NormalizeStringsToUppercase", Justification = "HTTP Headers are ASCII")]
        public static string GenerateKeyAuthorizationSignature(string verb,
            string resourceId,
            string resourceType,
            INameValueCollection headers,
            string key,
            out string payload,
            bool bUseUtcNowForMissingXDate = false)
        {
            // resourceId can be null for feed-read of /dbs

            if (string.IsNullOrEmpty(verb))
            {
                throw new ArgumentException(RMResources.StringArgumentNullOrEmpty, "verb");
            }

            if (resourceType == null)
            {
                throw new ArgumentNullException("resourceType"); // can be empty
            }

            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException(RMResources.StringArgumentNullOrEmpty, "key");
            }

            if (headers == null)
            {
                throw new ArgumentNullException("headers");
            }

            byte[] keyBytes = Convert.FromBase64String(key);
            using (HMACSHA256 hmacSha256 = new HMACSHA256(keyBytes))
            {
                // Order of the values included in the message payload is a protocol that clients/BE need to follow exactly.
                // More headers can be added in the future.
                // If any of the value is optional, it should still have the placeholder value of ""
                // OperationType -> ResourceType -> ResourceId/OwnerId -> XDate -> Date
                string verbInput = verb ?? string.Empty;
                string resourceIdInput = resourceId ?? string.Empty;
                string resourceTypeInput = resourceType ?? string.Empty;

                string authResourceId = AuthorizationHelper.GetAuthorizationResourceIdOrFullName(resourceTypeInput, resourceIdInput);
                payload = GenerateMessagePayload(verbInput,
                     authResourceId,
                     resourceTypeInput,
                     headers,
                     bUseUtcNowForMissingXDate);

                byte[] hashPayLoad = hmacSha256.ComputeHash(Encoding.UTF8.GetBytes(payload));
                string authorizationToken = Convert.ToBase64String(hashPayLoad);

                return HttpUtility.UrlEncode(String.Format(CultureInfo.InvariantCulture, Constants.Properties.AuthorizationFormat,
                            Constants.Properties.MasterToken,
                            Constants.Properties.TokenVersion,
                            authorizationToken));
            }
        }
        // This is a helper for both system and master keys
        [SuppressMessage("Microsoft.Globalization", "CA1308:NormalizeStringsToUppercase", Justification = "HTTP Headers are ASCII")]
        public static string GenerateKeyAuthorizationSignature(string verb,
            string resourceId,
            string resourceType,
            INameValueCollection headers,
            IComputeHash stringHMACSHA256Helper)
        {
            string payload;
            return AuthorizationHelper.GenerateKeyAuthorizationSignature(verb,
                resourceId,
                resourceType,
                headers,
                stringHMACSHA256Helper,
                out payload);
        }

        // This is a helper for both system and master keys
        [SuppressMessage("Microsoft.Globalization", "CA1308:NormalizeStringsToUppercase", Justification = "HTTP Headers are ASCII")]
        public static string GenerateKeyAuthorizationSignature(string verb,
            string resourceId,
            string resourceType,
            INameValueCollection headers,
            IComputeHash stringHMACSHA256Helper,
            out string payload)
        {
            // resourceId can be null for feed-read of /dbs

            if (string.IsNullOrEmpty(verb))
            {
                throw new ArgumentException(RMResources.StringArgumentNullOrEmpty, "verb");
            }

            if (resourceType == null)
            {
                throw new ArgumentNullException("resourceType"); // can be empty
            }

            if (stringHMACSHA256Helper == null)
            {
                throw new ArgumentNullException("stringHMACSHA256Helper");
            }

            if (headers == null)
            {
                throw new ArgumentNullException("headers");
            }

            // Order of the values included in the message payload is a protocol that clients/BE need to follow exactly.
            // More headers can be added in the future.
            // If any of the value is optional, it should still have the placeholder value of ""
            // OperationType -> ResourceType -> ResourceId/OwnerId -> XDate -> Date
            string verbInput = verb ?? string.Empty;
            string resourceIdInput = resourceId ?? string.Empty;
            string resourceTypeInput = resourceType ?? string.Empty;

            string authResourceId = AuthorizationHelper.GetAuthorizationResourceIdOrFullName(resourceTypeInput, resourceIdInput);
            payload = GenerateMessagePayload(verbInput,
                 authResourceId,
                 resourceTypeInput,
                 headers);

            byte[] hashPayLoad = stringHMACSHA256Helper.ComputeHash(Encoding.UTF8.GetBytes(payload));
            string authorizationToken = Convert.ToBase64String(hashPayLoad);

            return HttpUtility.UrlEncode(String.Format(CultureInfo.InvariantCulture, Constants.Properties.AuthorizationFormat,
                        Constants.Properties.MasterToken,
                        Constants.Properties.TokenVersion,
                        authorizationToken));
        }

        internal static void GetResourceTypeAndIdOrFullName(Uri uri,
            out bool isNameBased,
            out string resourceType,
            out string resourceId,
            string clientVersion = "")
        {
            if (uri == null)
            {
                throw new ArgumentNullException("uri");
            }

            resourceType = string.Empty;
            resourceId = string.Empty;

            int uriSegmentsCount = uri.Segments.Length;
            if (uriSegmentsCount < 1)
            {
                throw new ArgumentException(RMResources.InvalidUrl);
            }

            // Authorization code is fine with Uri not having resource id and path. 
            // We will just return empty in that case
            bool isFeed = false;
            if (!PathsHelper.TryParsePathSegments(uri.PathAndQuery, out isFeed, out resourceType, out resourceId, out isNameBased, clientVersion))
            {
                resourceType = string.Empty;
                resourceId = string.Empty;
            }
        }

        public static string GenerateMessagePayload(string verb,
               string resourceId,
               string resourceType,
               INameValueCollection headers,
               bool bUseUtcNowForMissingXDate = false)
        {
            string xDate = AuthorizationHelper.GetHeaderValue(headers, HttpConstants.HttpHeaders.XDate);
            string date = AuthorizationHelper.GetHeaderValue(headers, HttpConstants.HttpHeaders.HttpDate);

            // At-least one of date header should present
            // https://docs.microsoft.com/en-us/rest/api/documentdb/access-control-on-documentdb-resources 
            if (string.IsNullOrEmpty(xDate) && string.IsNullOrWhiteSpace(date))
            {
                if (!bUseUtcNowForMissingXDate)
                {
                    throw new UnauthorizedException(RMResources.InvalidDateHeader);
                }

                headers[HttpConstants.HttpHeaders.XDate] = DateTime.UtcNow.ToString("r", CultureInfo.InvariantCulture);
                xDate = AuthorizationHelper.GetHeaderValue(headers, HttpConstants.HttpHeaders.XDate);
            }

            // for name based, it is case sensitive, we won't use the lower case
            if (!PathsHelper.IsNameBased(resourceId))
            {
                resourceId = resourceId.ToLowerInvariant();
            }

            string payLoad = string.Format(CultureInfo.InvariantCulture,
                "{0}\n{1}\n{2}\n{3}\n{4}\n",
                verb.ToLowerInvariant(),
                resourceType.ToLowerInvariant(),
                resourceId,
                xDate.ToLowerInvariant(),
                xDate.Equals(string.Empty, StringComparison.OrdinalIgnoreCase) ? date.ToLowerInvariant() : string.Empty);

            return payLoad;
        }

        public static bool IsResourceToken(string token)
        {
            int typeSeparatorPosition = token.IndexOf('&');
            if (typeSeparatorPosition == -1)
            {
                return false;
            }
            string authType = token.Substring(0, typeSeparatorPosition);

            int typeKeyValueSepartorPosition = authType.IndexOf('=');
            if (typeKeyValueSepartorPosition == -1 ||
                !authType.Substring(0, typeKeyValueSepartorPosition).Equals(Constants.Properties.AuthSchemaType, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            string authTypeValue = authType.Substring(typeKeyValueSepartorPosition + 1);

            return authTypeValue.Equals(Constants.Properties.ResourceToken, StringComparison.OrdinalIgnoreCase);
        }

        internal static string GetHeaderValue(INameValueCollection headerValues, string key)
        {
            if (headerValues == null)
            {
                return string.Empty;
            }

            return headerValues[key] ?? string.Empty;
        }

        internal static string GetAuthorizationResourceIdOrFullName(string resourceType, string resourceIdOrFullName)
        {
            if (string.IsNullOrEmpty(resourceType) || string.IsNullOrEmpty(resourceIdOrFullName))
            {
                return resourceIdOrFullName;
            }

            if (PathsHelper.IsNameBased(resourceIdOrFullName))
            {
                // resource fullname is always end with name (not type segment like docs/colls).
                return resourceIdOrFullName;
            }

            if (resourceType.Equals(Paths.OffersPathSegment, StringComparison.OrdinalIgnoreCase) ||
                resourceType.Equals(Paths.PartitionsPathSegment, StringComparison.OrdinalIgnoreCase) ||
                resourceType.Equals(Paths.TopologyPathSegment, StringComparison.OrdinalIgnoreCase) ||
                resourceType.Equals(Paths.RidRangePathSegment, StringComparison.OrdinalIgnoreCase))
            {
                return resourceIdOrFullName;
            }

            ResourceId parsedRId = ResourceId.Parse(resourceIdOrFullName);
            if (resourceType.Equals(Paths.DatabasesPathSegment, StringComparison.OrdinalIgnoreCase))
            {
                return parsedRId.DatabaseId.ToString();
            }
            else if (resourceType.Equals(Paths.UsersPathSegment, StringComparison.OrdinalIgnoreCase))
            {
                return parsedRId.UserId.ToString();
            }
            else if (resourceType.Equals(Paths.UserDefinedTypesPathSegment, StringComparison.OrdinalIgnoreCase))
            {
                return parsedRId.UserDefinedTypeId.ToString();
            }
            else if (resourceType.Equals(Paths.CollectionsPathSegment, StringComparison.OrdinalIgnoreCase))
            {
                return parsedRId.DocumentCollectionId.ToString();
            }
            else if (resourceType.Equals(Paths.DocumentsPathSegment, StringComparison.OrdinalIgnoreCase))
            {
                return parsedRId.DocumentId.ToString();
            }
            else
            {
                // leaf node 
                return resourceIdOrFullName;
            }
        }
    }
}
