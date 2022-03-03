//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Buffers;
    using System.Buffers.Text;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.IO;
    using System.Security.Cryptography;
    using System.Text;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;

    // This class is used by both client (for generating the auth header with master/system key) and 
    // by the G/W when verifying the auth header. Some additional logic is also used by management service.
    internal static class AuthorizationHelper
    {
        public const int MaxAuthorizationHeaderSize = 1024;
        public const int DefaultAllowedClockSkewInSeconds = 900;
        public const int DefaultMasterTokenExpiryInSeconds = 900;
        private const int MaxAadAuthorizationHeaderSize = 16 * 1024;
        private const int MaxResourceTokenAuthorizationHeaderSize = 8 * 1024;
        private static readonly string AuthorizationFormatPrefixUrlEncoded = HttpUtility.UrlEncode(string.Format(CultureInfo.InvariantCulture, Constants.Properties.AuthorizationFormat,
                Constants.Properties.MasterToken,
                Constants.Properties.TokenVersion,
                string.Empty));

        private static readonly Encoding AuthorizationEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        // This API is a helper method to create auth header based on client request.
        // Uri is split into resourceType/resourceId - 
        // For feed/post/put requests, resourceId = parentId,
        // For point get requests,     resourceId = last segment in URI
        public static string GenerateGatewayAuthSignatureWithAddressResolution(string verb,
            Uri uri,
            INameValueCollection headers,
            IComputeHash stringHMACSHA256Helper,
            string clientVersion = "")
        {
            if (uri == null)
            {
                throw new ArgumentNullException(nameof(uri));
            }

            // Address request has the URI fragment (dbs/dbid/colls/colId...) as part of
            // either $resolveFor 'or' $generate queries of the context.RequestUri.
            // Extracting out the URI in the form https://localhost/dbs/dbid/colls/colId/docs to generate the signature.
            // Authorizer uses the same URI to verify signature.
            if (uri.AbsolutePath.Equals(Paths.Address_Root, StringComparison.OrdinalIgnoreCase))
            {
                uri = AuthorizationHelper.GenerateUriFromAddressRequestUri(uri);
            }

            return GenerateKeyAuthorizationSignature(verb, uri, headers, stringHMACSHA256Helper, clientVersion);
        }

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
                throw new ArgumentException(RMResources.StringArgumentNullOrEmpty, nameof(verb));
            }

            if (uri == null)
            {
                throw new ArgumentNullException(nameof(uri));
            }

            if (stringHMACSHA256Helper == null)
            {
                throw new ArgumentNullException(nameof(stringHMACSHA256Helper));
            }

            if (headers == null)
            {
                throw new ArgumentNullException(nameof(headers));
            }

            AuthorizationHelper.GetResourceTypeAndIdOrFullName(uri, out _, out string resourceType, out string resourceIdValue, clientVersion);

            string authorizationToken = AuthorizationHelper.GenerateKeyAuthorizationSignature(verb,
                         resourceIdValue,
                         resourceType,
                         headers,
                         stringHMACSHA256Helper,
                         out ArrayOwner arrayOwner);
            using (arrayOwner)
            {
                return authorizationToken;
            }
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
            string authorizationToken = AuthorizationHelper.GenerateKeyAuthorizationCore(
                verb,
                resourceId,
                resourceType,
                headers,
                key);
            return AuthorizationHelper.AuthorizationFormatPrefixUrlEncoded + HttpUtility.UrlEncode(authorizationToken);
        }

        // This is a helper for both system and master keys
        [SuppressMessage("Microsoft.Globalization", "CA1308:NormalizeStringsToUppercase", Justification = "HTTP Headers are ASCII")]
        public static string GenerateKeyAuthorizationSignature(string verb,
            string resourceId,
            string resourceType,
            INameValueCollection headers,
            IComputeHash stringHMACSHA256Helper)
        {
            string authorizationToken = AuthorizationHelper.GenerateUrlEncodedAuthorizationTokenWithHashCore(
                verb,
                resourceId,
                resourceType,
                headers,
                stringHMACSHA256Helper,
                out ArrayOwner payloadStream);
            using (payloadStream)
            {
                return AuthorizationHelper.AuthorizationFormatPrefixUrlEncoded + authorizationToken;
            }
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
            string authorizationToken = AuthorizationHelper.GenerateUrlEncodedAuthorizationTokenWithHashCore(
                verb,
                resourceId,
                resourceType,
                headers,
                stringHMACSHA256Helper,
                out ArrayOwner payloadStream);
            using (payloadStream)
            {
                payload = AuthorizationHelper.AuthorizationEncoding.GetString(payloadStream.Buffer.Array, payloadStream.Buffer.Offset, (int)payloadStream.Buffer.Count);
                return AuthorizationHelper.AuthorizationFormatPrefixUrlEncoded + authorizationToken;
            }
        }

        // This is a helper for both system and master keys
        [SuppressMessage("Microsoft.Globalization", "CA1308:NormalizeStringsToUppercase", Justification = "HTTP Headers are ASCII")]
        public static string GenerateKeyAuthorizationSignature(string verb,
            string resourceId,
            string resourceType,
            INameValueCollection headers,
            IComputeHash stringHMACSHA256Helper,
            out ArrayOwner payload)
        {
            string authorizationToken = AuthorizationHelper.GenerateUrlEncodedAuthorizationTokenWithHashCore(
                verb: verb,
                resourceId: resourceId,
                resourceType: resourceType,
                headers: headers,
                stringHMACSHA256Helper: stringHMACSHA256Helper,
                payload: out payload);
            try
            {
                return AuthorizationHelper.AuthorizationFormatPrefixUrlEncoded + authorizationToken;
            }
            catch
            {
                payload.Dispose();
                throw;
            }
        }

        // used in Compute
        public static void ParseAuthorizationToken(
            string authorizationTokenString,
            out ReadOnlyMemory<char> typeOutput,
            out ReadOnlyMemory<char> versionOutput,
            out ReadOnlyMemory<char> tokenOutput)
        {
            typeOutput = default;
            versionOutput = default;
            tokenOutput = default;

            if (string.IsNullOrEmpty(authorizationTokenString))
            {
                DefaultTrace.TraceError("Auth token missing");
                throw new UnauthorizedException(RMResources.MissingAuthHeader);
            }

            int authorizationTokenLength = authorizationTokenString.Length;

            authorizationTokenString = HttpUtility.UrlDecode(authorizationTokenString);
 
            // Format of the token being deciphered is 
            // type=<master/resource/system>&ver=<version>&sig=<base64encodedstring>

            // Step 1. split the tokens into type/ver/token.
            // when parsing for the last token, I use , as a separator to skip any redundant authorization headers

            ReadOnlyMemory<char> authorizationToken = authorizationTokenString.AsMemory();
            int typeSeparatorPosition = authorizationToken.Span.IndexOf('&');
            if (typeSeparatorPosition == -1)
            {
                throw new UnauthorizedException(RMResources.InvalidAuthHeaderFormat);
            }
            ReadOnlyMemory<char> authType = authorizationToken.Slice(0, typeSeparatorPosition);

            authorizationToken = authorizationToken.Slice(typeSeparatorPosition + 1, authorizationToken.Length - typeSeparatorPosition - 1);
            int versionSepartorPosition = authorizationToken.Span.IndexOf('&');
            if (versionSepartorPosition == -1)
            {
                throw new UnauthorizedException(RMResources.InvalidAuthHeaderFormat);
            }
            ReadOnlyMemory<char> version = authorizationToken.Slice(0, versionSepartorPosition);

            authorizationToken = authorizationToken.Slice(versionSepartorPosition + 1, authorizationToken.Length - versionSepartorPosition - 1);
            ReadOnlyMemory<char> token = authorizationToken;
            int tokenSeparatorPosition = authorizationToken.Span.IndexOf(',');
            if (tokenSeparatorPosition != -1)
            {
                token = authorizationToken.Slice(0, tokenSeparatorPosition);
            }

            // Step 2. For each token, split to get the right half of '='
            // Additionally check for the left half to be the expected scheme type
            int typeKeyValueSepartorPosition = authType.Span.IndexOf('=');
            if (typeKeyValueSepartorPosition == -1
                || !authType.Span.Slice(0, typeKeyValueSepartorPosition).SequenceEqual(Constants.Properties.AuthSchemaType.AsSpan())
                || !authType.Span.Slice(0, typeKeyValueSepartorPosition).ToString().Equals(Constants.Properties.AuthSchemaType, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedException(RMResources.InvalidAuthHeaderFormat);
            }

            ReadOnlyMemory<char> authTypeValue = authType.Slice(typeKeyValueSepartorPosition + 1);

            if (MemoryExtensions.Equals(authTypeValue.Span, Constants.Properties.AadToken.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                if (authorizationTokenLength > AuthorizationHelper.MaxAadAuthorizationHeaderSize)
                {
                    DefaultTrace.TraceError($"Token of type [{authTypeValue.Span.ToString()}] was of size [{authorizationTokenLength}] while the max allowed size is [{AuthorizationHelper.MaxAadAuthorizationHeaderSize}].");
                    throw new UnauthorizedException(RMResources.InvalidAuthHeaderFormat, SubStatusCodes.InvalidAuthHeaderFormat);
                }
            }
            else if (MemoryExtensions.Equals(authTypeValue.Span, Constants.Properties.ResourceToken.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                if (authorizationTokenLength > AuthorizationHelper.MaxResourceTokenAuthorizationHeaderSize)
                {
                    DefaultTrace.TraceError($"Token of type [{authTypeValue.Span.ToString()}] was of size [{authorizationTokenLength}] while the max allowed size is [{AuthorizationHelper.MaxResourceTokenAuthorizationHeaderSize}].");
                    throw new UnauthorizedException(RMResources.InvalidAuthHeaderFormat, SubStatusCodes.InvalidAuthHeaderFormat);
                }
            }
            else if (authorizationTokenLength > AuthorizationHelper.MaxAuthorizationHeaderSize)
            {
                DefaultTrace.TraceError($"Token of type [{authTypeValue.Span.ToString()}] was of size [{authorizationTokenLength}] while the max allowed size is [{AuthorizationHelper.MaxAuthorizationHeaderSize}].");
                throw new UnauthorizedException(RMResources.InvalidAuthHeaderFormat, SubStatusCodes.InvalidAuthHeaderFormat);
            }

            int versionKeyValueSeparatorPosition = version.Span.IndexOf('=');
            if (versionKeyValueSeparatorPosition == -1
                || !version.Span.Slice(0, versionKeyValueSeparatorPosition).SequenceEqual(Constants.Properties.AuthVersion.AsSpan())
                || !version.Slice(0, versionKeyValueSeparatorPosition).ToString().Equals(Constants.Properties.AuthVersion, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedException(RMResources.InvalidAuthHeaderFormat);
            }

            ReadOnlyMemory<char> versionValue = version.Slice(versionKeyValueSeparatorPosition + 1);

            int tokenKeyValueSeparatorPosition = token.Span.IndexOf('=');
            if (tokenKeyValueSeparatorPosition == -1
                || !token.Slice(0, tokenKeyValueSeparatorPosition).Span.SequenceEqual(Constants.Properties.AuthSignature.AsSpan())
                || !token.Slice(0, tokenKeyValueSeparatorPosition).ToString().Equals(Constants.Properties.AuthSignature, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedException(RMResources.InvalidAuthHeaderFormat);
            }

            ReadOnlyMemory<char> tokenValue = token.Slice(tokenKeyValueSeparatorPosition + 1);

            if (authTypeValue.IsEmpty ||
                versionValue.IsEmpty ||
                tokenValue.IsEmpty)
            {
                throw new UnauthorizedException(RMResources.InvalidAuthHeaderFormat);
            }

            typeOutput = authTypeValue;
            versionOutput = versionValue;
            tokenOutput = tokenValue;
        }

        // used in Compute
        public static bool CheckPayloadUsingKey(
               ReadOnlyMemory<char> inputToken,
               string verb,
               string resourceId,
               string resourceType,
               INameValueCollection headers,
               string key)
        {
            string requestBasedToken = AuthorizationHelper.GenerateKeyAuthorizationCore(
                verb,
                resourceId,
                resourceType,
                headers,
                key);

            return inputToken.Span.SequenceEqual(requestBasedToken.AsSpan())
                || inputToken.ToString().Equals(requestBasedToken, StringComparison.OrdinalIgnoreCase);
        }

        // used by Compute
        public static void ValidateInputRequestTime(
            INameValueCollection requestHeaders,
            int masterTokenExpiryInSeconds,
            int allowedClockSkewInSeconds)
        {
            ValidateInputRequestTime(
                requestHeaders,
                (headers, field) => AuthorizationHelper.GetHeaderValue(headers, field),
                masterTokenExpiryInSeconds,
                allowedClockSkewInSeconds);
        }

        public static void ValidateInputRequestTime<T>(
            T requestHeaders,
            Func<T, string, string> headerGetter,
            int masterTokenExpiryInSeconds,
            int allowedClockSkewInSeconds)
        {
            if (requestHeaders == null)
            {
                DefaultTrace.TraceError("Null request headers for validating auth time");
                throw new UnauthorizedException(RMResources.MissingDateForAuthorization);
            }

            // Fetch the date in the headers to compare against the correct time.
            // Since Date header is overridden by some proxies/http client libraries, we support
            // an additional date header 'x-ms-date' and prefer that to the regular 'date' header.
            string dateToCompare = headerGetter(requestHeaders, HttpConstants.HttpHeaders.XDate);
            if (string.IsNullOrEmpty(dateToCompare))
            {
                dateToCompare = headerGetter(requestHeaders, HttpConstants.HttpHeaders.HttpDate);
            }

            ValidateInputRequestTime(dateToCompare, masterTokenExpiryInSeconds, allowedClockSkewInSeconds);
        }

        public static void CheckTimeRangeIsCurrent(
            int allowedClockSkewInSeconds,
            DateTime startDateTime,
            DateTime expiryDateTime)
        {
            // Check if time ranges provided are beyond DateTime.MinValue or DateTime.MaxValue
            bool outOfRange = startDateTime <= DateTime.MinValue.AddSeconds(allowedClockSkewInSeconds)
                || expiryDateTime >= DateTime.MaxValue.AddSeconds(-allowedClockSkewInSeconds);

            // Adjust for a time lag between various instances upto 5 minutes i.e. allow [start-5, end+5]
            if (outOfRange ||
                startDateTime.AddSeconds(-allowedClockSkewInSeconds) > DateTime.UtcNow ||
                expiryDateTime.AddSeconds(allowedClockSkewInSeconds) < DateTime.UtcNow)
            {
                string message = string.Format(CultureInfo.InvariantCulture,
                    RMResources.InvalidTokenTimeRange,
                    startDateTime.ToString("r", CultureInfo.InvariantCulture),
                    expiryDateTime.ToString("r", CultureInfo.InvariantCulture),
                    DateTime.UtcNow.ToString("r", CultureInfo.InvariantCulture));

                DefaultTrace.TraceError(message);

                throw new ForbiddenException(message);
            }
        }

        internal static void GetResourceTypeAndIdOrFullName(Uri uri,
            out bool isNameBased,
            out string resourceType,
            out string resourceId,
            string clientVersion = "")
        {
            if (uri == null)
            {
                throw new ArgumentNullException(nameof(uri));
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
            if (!PathsHelper.TryParsePathSegments(uri.PathAndQuery, out _, out resourceType, out resourceId, out isNameBased, clientVersion))
            {
                resourceType = string.Empty;
                resourceId = string.Empty;
            }
        }

        public static bool IsUserRequest(string resourceType)
        {
            if (string.Compare(resourceType, Paths.Root, StringComparison.OrdinalIgnoreCase) == 0
                || string.Compare(resourceType, Paths.PartitionKeyRangePreSplitSegment, StringComparison.OrdinalIgnoreCase) == 0
                || string.Compare(resourceType, Paths.PartitionKeyRangePostSplitSegment, StringComparison.OrdinalIgnoreCase) == 0
                || string.Compare(resourceType, Paths.ControllerOperations_BatchGetOutput, StringComparison.OrdinalIgnoreCase) == 0
                || string.Compare(resourceType, Paths.ControllerOperations_BatchReportCharges, StringComparison.OrdinalIgnoreCase) == 0
                || string.Compare(resourceType, Paths.Operations_GetStorageAccountKey, StringComparison.OrdinalIgnoreCase) == 0)
            {
                return false;
            }

            return true;
        }

        public static AuthorizationTokenType GetSystemOperationType(bool readOnlyRequest, string resourceType)
        {
            if (!AuthorizationHelper.IsUserRequest(resourceType))
            {
                if (readOnlyRequest)
                {
                    return AuthorizationTokenType.SystemReadOnly;
                }
                else
                {
                    return AuthorizationTokenType.SystemAll;
                }
            }

            // operations on user resources
            if (readOnlyRequest)
            {
                return AuthorizationTokenType.SystemReadOnly;
            }
            else
            {
                return AuthorizationTokenType.SystemReadWrite;
            }

        }

        public static int SerializeMessagePayload(
               Span<byte> stream,
               string verb,
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

            int totalLength = 0;
            int length = stream.Write(verb.ToLowerInvariant());
            totalLength += length;
            stream = stream.Slice(length);
            length = stream.Write("\n");
            totalLength += length;
            stream = stream.Slice(length);
            length = stream.Write(resourceType.ToLowerInvariant());
            totalLength += length;
            stream = stream.Slice(length);
            length = stream.Write("\n");
            totalLength += length;
            stream = stream.Slice(length);
            length = stream.Write(resourceId);
            totalLength += length;
            stream = stream.Slice(length);
            length = stream.Write("\n");
            totalLength += length;
            stream = stream.Slice(length);
            length = stream.Write(xDate.ToLowerInvariant());
            totalLength += length;
            stream = stream.Slice(length);
            length = stream.Write("\n");
            totalLength += length;
            stream = stream.Slice(length);
            length = stream.Write(xDate.Equals(string.Empty, StringComparison.OrdinalIgnoreCase) ? date.ToLowerInvariant() : string.Empty);
            totalLength += length;
            stream = stream.Slice(length);
            length = stream.Write("\n");
            totalLength += length;
            return totalLength;
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

        internal static string GetHeaderValue(IDictionary<string, string> headerValues, string key)
        {
            if (headerValues == null)
            {
                return string.Empty;
            }

            headerValues.TryGetValue(key, out string value);
            return value;
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
                resourceType.Equals(Paths.RidRangePathSegment, StringComparison.OrdinalIgnoreCase) ||
                resourceType.Equals(Paths.SnapshotsPathSegment, StringComparison.OrdinalIgnoreCase))
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
            else if (resourceType.Equals(Paths.ClientEncryptionKeysPathSegment, StringComparison.OrdinalIgnoreCase))
            {
                return parsedRId.ClientEncryptionKeyId.ToString();
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

        public static Uri GenerateUriFromAddressRequestUri(Uri uri)
        {
            // Address request has the URI fragment (dbs/dbid/colls/colId...) as part of
            // either $resolveFor 'or' $generate queries of the context.RequestUri.
            // Extracting out the URI in the form https://localhost/dbs/dbid/colls/colId/docs to generate the signature.
            // Authorizer uses the same URI to verify signature.
            string addressFeedUri = UrlUtility.ParseQuery(uri.Query)[HttpConstants.QueryStrings.Url]
                ?? UrlUtility.ParseQuery(uri.Query)[HttpConstants.QueryStrings.GenerateId]
                ?? UrlUtility.ParseQuery(uri.Query)[HttpConstants.QueryStrings.GetChildResourcePartitions];

            if (string.IsNullOrEmpty(addressFeedUri))
            {
                throw new BadRequestException(RMResources.BadUrl);
            }

            return new Uri(uri.Scheme + "://" + uri.Host + "/" + HttpUtility.UrlDecode(addressFeedUri).Trim('/'));
        }

        private static void ValidateInputRequestTime(
            string dateToCompare,
            int masterTokenExpiryInSeconds,
            int allowedClockSkewInSeconds)
        {
            if (string.IsNullOrEmpty(dateToCompare))
            {
                throw new UnauthorizedException(RMResources.MissingDateForAuthorization);
            }

            if (!DateTime.TryParse(dateToCompare, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal | DateTimeStyles.AllowWhiteSpaces, out DateTime utcStartTime))
            {
                throw new UnauthorizedException(RMResources.InvalidDateHeader);
            }

            // Check if time range is beyond DateTime.MaxValue
            bool outOfRange = utcStartTime >= DateTime.MaxValue.AddSeconds(-masterTokenExpiryInSeconds);

            if (outOfRange)
            {
                string message = string.Format(CultureInfo.InvariantCulture,
                    RMResources.InvalidTokenTimeRange,
                    utcStartTime.ToString("r", CultureInfo.InvariantCulture),
                    DateTime.MaxValue.ToString("r", CultureInfo.InvariantCulture),
                    DateTime.UtcNow.ToString("r", CultureInfo.InvariantCulture));

                DefaultTrace.TraceError(message);

                throw new ForbiddenException(message);
            }

            DateTime utcEndTime = utcStartTime + TimeSpan.FromSeconds(masterTokenExpiryInSeconds);

            AuthorizationHelper.CheckTimeRangeIsCurrent(allowedClockSkewInSeconds, utcStartTime, utcEndTime);
        }

        // This function is used by Compute
        internal static string GenerateAuthorizationTokenWithHashCore(
            string verb,
            string resourceId,
            string resourceType,
            INameValueCollection headers,
            IComputeHash stringHMACSHA256Helper,
            out ArrayOwner payload)
        {
            return AuthorizationHelper.GenerateAuthorizationTokenWithHashCore(
                verb,
                resourceId,
                resourceType,
                headers,
                stringHMACSHA256Helper,
                urlEncode: false,
                out payload);
        }

        private static string GenerateUrlEncodedAuthorizationTokenWithHashCore(
            string verb,
            string resourceId,
            string resourceType,
            INameValueCollection headers,
            IComputeHash stringHMACSHA256Helper,
            out ArrayOwner payload)
        {
            return AuthorizationHelper.GenerateAuthorizationTokenWithHashCore(
                verb,
                resourceId,
                resourceType,
                headers,
                stringHMACSHA256Helper,
                urlEncode: true,
                out payload);
        }

        private static string GenerateAuthorizationTokenWithHashCore(
            string verb,
            string resourceId,
            string resourceType,
            INameValueCollection headers,
            IComputeHash stringHMACSHA256Helper,
            bool urlEncode,
            out ArrayOwner payload)
        {
            // resourceId can be null for feed-read of /dbs
            if (string.IsNullOrEmpty(verb))
            {
                throw new ArgumentException(RMResources.StringArgumentNullOrEmpty, nameof(verb));
            }

            if (resourceType == null)
            {
                throw new ArgumentNullException(nameof(resourceType)); // can be empty
            }

            if (stringHMACSHA256Helper == null)
            {
                throw new ArgumentNullException(nameof(stringHMACSHA256Helper));
            }

            if (headers == null)
            {
                throw new ArgumentNullException(nameof(headers));
            }

            // Order of the values included in the message payload is a protocol that clients/BE need to follow exactly.
            // More headers can be added in the future.
            // If any of the value is optional, it should still have the placeholder value of ""
            // OperationType -> ResourceType -> ResourceId/OwnerId -> XDate -> Date
            string verbInput = verb ?? string.Empty;
            string resourceIdInput = resourceId ?? string.Empty;
            string resourceTypeInput = resourceType ?? string.Empty;

            string authResourceId = AuthorizationHelper.GetAuthorizationResourceIdOrFullName(resourceTypeInput, resourceIdInput);
            int capacity = AuthorizationHelper.ComputeMemoryCapacity(verbInput, authResourceId, resourceTypeInput);
            byte[] buffer = ArrayPool<byte>.Shared.Rent(capacity);
            try
            {
                Span<byte> payloadBytes = buffer;
                int length = AuthorizationHelper.SerializeMessagePayload(
                    payloadBytes,
                    verbInput,
                    authResourceId,
                    resourceTypeInput,
                    headers);

                payload = new ArrayOwner(ArrayPool<byte>.Shared, new ArraySegment<byte>(buffer, 0, length));
                byte[] hashPayLoad = stringHMACSHA256Helper.ComputeHash(payload.Buffer);
                return AuthorizationHelper.OptimizedConvertToBase64string(hashPayLoad, urlEncode);
            }
            catch
            {
                ArrayPool<byte>.Shared.Return(buffer);
                throw;
            }
        }

        /// <summary>
        /// This an optimized version of doing Convert.ToBase64String(hashPayLoad) with an optional wrapping HttpUtility.UrlEncode.
        /// This avoids the over head of converting it to a string and back to a byte[].
        /// </summary>
        private static unsafe string OptimizedConvertToBase64string(byte[] hashPayLoad, bool urlEncode)
        {
            // Create a large enough buffer that URL encode can use it.
            // Increase the buffer by 3x so it can be used for the URL encoding
            int capacity = Base64.GetMaxEncodedToUtf8Length(hashPayLoad.Length) * 3;
            byte[] rentedBuffer = ArrayPool<byte>.Shared.Rent(capacity);

            try
            {
                Span<byte> encodingBuffer = rentedBuffer;
                // This replaces the Convert.ToBase64String
                OperationStatus status = Base64.EncodeToUtf8(
                    hashPayLoad,
                    encodingBuffer,
                    out int _,
                    out int bytesWritten);

                if (status != OperationStatus.Done)
                {
                    throw new ArgumentException($"Authorization key payload is invalid. {status}");
                }

                return urlEncode 
                    ? AuthorizationHelper.UrlEncodeBase64SpanInPlace(encodingBuffer, bytesWritten)
                    : Encoding.UTF8.GetString(encodingBuffer.Slice(0, bytesWritten));
            }
            finally
            {
                if (rentedBuffer != null)
                {
                    ArrayPool<byte>.Shared.Return(rentedBuffer);
                }
            }
        }

        // This function is used by Compute
        internal static int ComputeMemoryCapacity(string verbInput, string authResourceId, string resourceTypeInput)
        {
            return
                verbInput.Length
                + AuthorizationHelper.AuthorizationEncoding.GetMaxByteCount(authResourceId.Length)
                + resourceTypeInput.Length
                + 5 // new line characters
                + 30; // date header length;
        }

        private static string GenerateKeyAuthorizationCore(
            string verb,
            string resourceId,
            string resourceType,
            INameValueCollection headers,
            string key)
        {
            // resourceId can be null for feed-read of /dbs
            if (string.IsNullOrEmpty(verb))
            {
                throw new ArgumentException(RMResources.StringArgumentNullOrEmpty, nameof(verb));
            }

            if (resourceType == null)
            {
                throw new ArgumentNullException(nameof(resourceType)); // can be empty
            }

            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException(RMResources.StringArgumentNullOrEmpty, nameof(key));
            }

            if (headers == null)
            {
                throw new ArgumentNullException(nameof(headers));
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
                int memoryStreamCapacity = AuthorizationHelper.ComputeMemoryCapacity(verbInput, authResourceId, resourceTypeInput);
                byte[] arrayPoolBuffer = ArrayPool<byte>.Shared.Rent(memoryStreamCapacity);

                try
                {
                    int length = AuthorizationHelper.SerializeMessagePayload(
                        arrayPoolBuffer,
                        verbInput,
                        authResourceId,
                        resourceTypeInput,
                        headers);

                    byte[] hashPayLoad = hmacSha256.ComputeHash(arrayPoolBuffer, 0, length);
                    return Convert.ToBase64String(hashPayLoad);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(arrayPoolBuffer);
                }
            }
        }

        /// <summary>
        /// This does HttpUtility.UrlEncode functionality with Span buffer. It does an in place update to avoid
        /// creating the new buffer.
        /// </summary>
        /// <param name="base64Bytes">The buffer that include the bytes to url encode.</param>
        /// <param name="length">The length of bytes used in the buffer</param>
        /// <returns>The URLEncoded string of the bytes in the buffer</returns>
        public unsafe static string UrlEncodeBase64SpanInPlace(Span<byte> base64Bytes, int length)
        {
            if (base64Bytes == default)
            {
                throw new ArgumentNullException(nameof(base64Bytes));
            }

            if (base64Bytes.Length < length * 3)
            {
                throw new ArgumentException($"{nameof(base64Bytes)} should be 3x to avoid running out of space in worst case scenario where all characters are special");
            }

            if (length == 0)
            {
                return string.Empty;
            }

            int escapeBufferPosition = base64Bytes.Length - 1;
            for (int i = length - 1; i >= 0; i--)
            { 
                byte curr = base64Bytes[i];
                // Base64 is limited to Alphanumeric characters and '/' '=' '+'
                switch (curr)
                {
                    case (byte)'/':
                        base64Bytes[escapeBufferPosition--] = (byte)'f';
                        base64Bytes[escapeBufferPosition--] = (byte)'2';
                        base64Bytes[escapeBufferPosition--] = (byte)'%';
                        break;
                    case (byte)'=':
                        base64Bytes[escapeBufferPosition--] = (byte)'d';
                        base64Bytes[escapeBufferPosition--] = (byte)'3';
                        base64Bytes[escapeBufferPosition--] = (byte)'%';
                        break;
                    case (byte)'+':
                        base64Bytes[escapeBufferPosition--] = (byte)'b';
                        base64Bytes[escapeBufferPosition--] = (byte)'2';
                        base64Bytes[escapeBufferPosition--] = (byte)'%';
                        break;
                    default:
                        base64Bytes[escapeBufferPosition--] = curr;
                        break;
                }
            }

            Span<byte> endSlice = base64Bytes.Slice(escapeBufferPosition + 1);
            fixed (byte* bp = endSlice)
            {
                return Encoding.UTF8.GetString(bp, endSlice.Length);
            }
        }

        private static int Write(this Span<byte> stream, string contentToWrite)
        {
            int actualByteCount = AuthorizationHelper.AuthorizationEncoding.GetBytes(
                contentToWrite,
                stream);
            return actualByteCount;
        }

        public struct ArrayOwner : IDisposable
        {
            private readonly ArrayPool<byte> pool;

            public ArrayOwner(ArrayPool<byte> pool, ArraySegment<byte> buffer)
            {
                this.pool = pool;
                this.Buffer = buffer;
            }

            public ArraySegment<byte> Buffer { get; private set; }

            public void Dispose()
            {
                if (this.Buffer.Array != null)
                {
                    this.pool?.Return(this.Buffer.Array);
                    this.Buffer = default;
                }
            }
        }
    }
}
