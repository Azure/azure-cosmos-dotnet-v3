//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    internal static class RuntimeConstants
    {
        public const string IncludeExceptionDetails = "includeExceptionDetails";
                
        internal static class Serialization
        {
            public const int ChunkSize512 = 512;
            public const int ChunkSize1K = 1024;
            public const int ChunkSize8K = 8192;
        }

        internal static class Separators
        {
            public static readonly char[] Url = new char[] { '/' };
            public static readonly char[] Quote = new char[] { '\'' };
            public static readonly char[] DomainId = new char[] { '-' };
            public static readonly char[] Query = new char[] { '?', '&', '=' };
            public static readonly char[] Parenthesis = new char[] { '(', ')' };
            public static readonly char[] UserAgentHeader = new char[] { '(', ')', ';', ',' };


            //Note that the accept header separator here is ideally comma. Semicolon is used for separators within individual
            //header for now cloud moe does not recognize such accept header hence we allow both semicolon or comma separated
            //accept header
            public static readonly char[] Header = new char[] { ';', ',' };
            public static readonly char[] CookieSeparator = new char[] { ';' };
            public static readonly char[] CookieValueSeparator = new char[] { '=' };
            public static readonly char[] PPMUserToken = new char[] { ':' };
            public static readonly char[] Identifier = new char[] { '-' };
            public static readonly char[] Host = new char[] { '.' };
            public static readonly char[] Version = new char[] { ',' };
            public static readonly char[] Pair = new char[] { ';' };
            public static readonly char[] ETag = new char[] { '#' };
            public static readonly char[] MemberQuery = new char[] { '+' };

            public const string HeaderEncodingBegin = "=?";
            public const string HeaderEncodingEnd = "?=";
            public const string HeaderEncodingSeparator = "?";
        }

        internal static class MediaTypes
        {
            // http://www.iana.org/assignments/media-types/media-types.xhtml
            public const string Any = "*/*";
            public const string Http = "application/http";
            public const string Json = "application/json";
            public const string Xml = "application/xml";
            public const string AtomXml = "application/atom+xml";
            public const string AtomXmlEntry = "application/atom+xml;type=entry";
            public const string OctetStream = "application/octet-stream";
            public const string SQL = "application/sql";
            public const string QueryJson = "application/query+json";
            public const string ImageJpeg = "image/jpeg";
            public const string ImagePng = "image/png";
            public const string TextHtml = "text/html";
            public const string TextPlain = "text/plain";
            public const string JavaScript = "application/x-javascript";
            public const string JsonNoOdataMetadata = "application/json;odata=nometadata";
            public const string JsonMinimalOdataMetadata = "application/json;odata=minimalmetadata";
            public const string JsonFullOdataMetadata = "application/json;odata=fullmetadata";
            public const string MutlipartBatchPrefix = "multipart/mixed";
            public const string FormUrlEncoded = "application/x-www-form-urlencoded";
            public const string MultipartFormData = "multipart/form-data";
            public const string JsonPatch = "application/json-patch+json";
        }

        internal static class Schemes
        {
            internal const string UuidScheme = "urn:uuid:";
        }

        internal static class Protocols
        {
            internal const string HTTP = "http";
            internal const string HTTPS = "https";
            internal const string TCP = "net.tcp";
            internal const string RNTBD = "rntbd";
        }
    }
}
