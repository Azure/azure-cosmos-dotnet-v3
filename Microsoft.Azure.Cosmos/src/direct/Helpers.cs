//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Globalization;
    using System.Linq;
    using System.Net.Http.Headers;
    using Microsoft.Azure.Documents.Collections;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal static class Helpers
    {        
        internal static int ValidateNonNegativeInteger(string name, int value)
        {
            if (value < 0)
            {
                throw new BadRequestException(string.Format(CultureInfo.CurrentUICulture, RMResources.NegativeInteger, name));
            }

            return value;
        }

        internal static int ValidatePositiveInteger(string name, int value)
        {
            if (value <= 0)
            {
                throw new BadRequestException(string.Format(CultureInfo.CurrentUICulture, RMResources.PositiveInteger, name));
            }

            return value;
        }

        internal static void ValidateEnumProperties<TEnum>(TEnum enumValue)
        {
            foreach (TEnum e in Enum.GetValues(typeof(TEnum)))
            {
                if (e.Equals(enumValue)) return;
            }

            throw new BadRequestException(string.Format(CultureInfo.InvariantCulture, "Invalid value {0} for type{1}", enumValue.ToString(), enumValue.GetType().ToString()));
        }

        /// <summary>
        /// Gets the byte value for a header. If header not present, returns the defaultValue.
        /// </summary>
        /// <param name="headerValues"></param>
        /// <param name="headerName"></param>
        /// <param name="defaultValue">Pls do not set defaultValue to MinValue as MinValue carries valid meaning in some place</param>
        /// <returns></returns>
        public static byte GetHeaderValueByte(INameValueCollection headerValues, string headerName, byte defaultValue = byte.MaxValue)
        {
            byte result = defaultValue;
            string header = headerValues[headerName];
            if (!string.IsNullOrWhiteSpace(header))
            {
                if (!byte.TryParse(header, NumberStyles.None, CultureInfo.InvariantCulture, out result))
                {
                    result = defaultValue;
                }
            }
            return result;
        }

        public static string GetDateHeader(INameValueCollection headerValues)
        {
            if (headerValues == null)
            {
                return string.Empty;
            }

            // Since Date header is overridden by some proxies/http client libraries, we support
            // an additional date header 'x-ms-date' and prefer that to the regular 'date' header.
            string date = headerValues[HttpConstants.HttpHeaders.XDate];
            if (string.IsNullOrEmpty(date))
            {
                date = headerValues[HttpConstants.HttpHeaders.HttpDate];
            }

            return date ?? string.Empty;
        }

        public static long GetHeaderValueLong(INameValueCollection headerValues, string headerName, long defaultValue = -1)
        {
            long result = defaultValue;

            string header = headerValues[headerName];
            if (!string.IsNullOrEmpty(header))
            {
                if (!long.TryParse(header, NumberStyles.Number, CultureInfo.InvariantCulture, out result))
                {
                    result = defaultValue;
                }
            }
            return result;
        }

        public static double GetHeaderValueDouble(INameValueCollection headerValues, string headerName, double defaultValue = -1)
        {
            double result = defaultValue;

            string header = headerValues[headerName];
            if (!string.IsNullOrEmpty(header))
            {
                if (!double.TryParse(header, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out result))
                {
                    result = defaultValue;
                }
            }
            return result;
        }

        internal static string[] ExtractValuesFromHTTPHeaders(HttpHeaders httpHeaders, string[] keys)
        {
            string[] headerValues = Enumerable.Repeat("", keys.Length).ToArray();

            if (httpHeaders == null) return headerValues;

            foreach (KeyValuePair<string, IEnumerable<string> > pair in httpHeaders)
            {
                int pos = Array.FindIndex(keys, t => t.Equals(pair.Key, StringComparison.OrdinalIgnoreCase));
                if (pos < 0)
                {
                    continue;
                }

                if (pair.Value.Count() > 0)
                {
                    headerValues[pos] = pair.Value.First();
                }
            }
            return headerValues;
        }

        /// <summary>
        /// Helper method to set application specific user agent suffix for internal telemetry purposes
        /// </summary>
        /// <param name="appName"></param>
        /// <param name="appVersion"></param>
        /// <returns></returns>
        internal static string GetAppSpecificUserAgentSuffix(string appName, string appVersion)
        {
            if (string.IsNullOrEmpty(appName))
                throw new ArgumentNullException("appName");

            if (string.IsNullOrEmpty(appVersion))
                throw new ArgumentNullException("appVersion");

            return string.Format(CultureInfo.InvariantCulture, "{0}/{1}", appName, appVersion);
        }

        internal static void SetupJsonReader(JsonReader reader, JsonSerializerSettings serializerSettings)
        {
            if (serializerSettings != null)
            {
                if (serializerSettings.Culture != null)
                {
                    reader.Culture = serializerSettings.Culture;
                }
                reader.DateTimeZoneHandling = serializerSettings.DateTimeZoneHandling;
                reader.DateParseHandling = serializerSettings.DateParseHandling;
                reader.FloatParseHandling = serializerSettings.FloatParseHandling;
                if (serializerSettings.MaxDepth.HasValue)
                {
                    reader.MaxDepth = serializerSettings.MaxDepth;
                }
                if (serializerSettings.DateFormatString != null)
                {
                    reader.DateFormatString = serializerSettings.DateFormatString;
                }
            }
        }

        internal static string GetScriptLogHeader(INameValueCollection headerValues)
        {
            string urlEscapedLogResult = headerValues?[HttpConstants.HttpHeaders.LogResults];
            if (!string.IsNullOrEmpty(urlEscapedLogResult))
            {
                return Uri.UnescapeDataString(urlEscapedLogResult);
            }

            return urlEscapedLogResult;
        }

        internal static long ToUnixTime(DateTimeOffset dt)
        {
            TimeSpan dt2 = dt - new DateTimeOffset(1970, 1, 1, 0, 0, 0, new TimeSpan(0));
            return (long)dt2.TotalSeconds;
        }

        // This function should be same with IFXTraceProvider::GetStatusFromStatusCodeType
        internal static string GetStatusFromStatusCode(string statusCode)
        {
            int code;
            if (!Int32.TryParse(statusCode, out code))
            {
                return "Other";
            }

            if (code >= 200 && code < 300) return "Success";
            else if (code == 304) return "NotModified";
            else if (code == 400) return "BadRequestError";
            else if (code == 401) return "AuthorizationError";
            else if (code == 408) return "ServerTimeoutError";
            else if (code == 429) return "ClientThrottlingError";
            else if (code > 400 && code < 500) return "ClientOtherError";
            else if (code == 500) return "ServerOtherError";
            else if (code == 503) return "ServiceBusyError";
            else return "Other";
        }
    }
}
