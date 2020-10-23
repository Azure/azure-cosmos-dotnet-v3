//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Net;
    using System.Net.NetworkInformation;
    using System.Text;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Documents.Collections;

    internal static class UrlUtility
    {
        internal static string ConcatenateUrlsString(string baseUrl, params string[] relativeParts)
        {
            StringBuilder urlStringBuilder = new StringBuilder(RemoveTrailingSlash(baseUrl));
            foreach (string urlPart in relativeParts)
            {
                urlStringBuilder.Append(RuntimeConstants.Separators.Url[0]);
                urlStringBuilder.Append(RemoveLeadingSlash(urlPart));
            }
            return urlStringBuilder.ToString();
        }

        internal static string ConcatenateUrlsString(string baseUrl, string relativePart)
        {
            return AddTrailingSlash(baseUrl) + RemoveLeadingSlash(relativePart);
        }

        internal static string ConcatenateUrlsString(Uri baseUrl, string relativePart)
        {
            return UrlUtility.ConcatenateUrlsString(GetLeftPartOfPath(baseUrl), relativePart);
        }

        internal static void ExtractTargetInfo(Uri uri, out string tenantId, out string applicationName, out string serviceId, out string partitionKey, out string replicaId)
        {
            if (uri.Segments == null || uri.Segments.Length < 9)
            {
                // uri could contains more segments (e.g. replicas etc), hence
                // for the purpose of this call, it is safe to relax from != 9 to < 9
                DefaultTrace.TraceError("Uri {0} is invalid", uri);
                throw new ArgumentException("uri");
            }

            //net.tcp: (or) http://cluster.docdb.cloudapp.net:[Port]/apps/<appId>/services/databases0/partitions/<PartitionKey>/replicas/<replicaId>
            tenantId = ExtractTenantIdFromUri(uri);
            applicationName = uri.Segments[2].Substring(0, uri.Segments[2].Length - 1);
            serviceId = uri.Segments[4].Substring(0, uri.Segments[4].Length - 1);
            partitionKey = uri.Segments[6].Substring(0, uri.Segments[6].Length - 1); //Not PartitionId.
            replicaId = uri.Segments[8].Substring(0, uri.Segments[8].Length);
        }

        internal static string ConcatenateUrlsString(Uri baseUrl, Uri relativePart)
        {
            if (relativePart.IsAbsoluteUri)
                return relativePart.ToString();

            return ConcatenateUrlsString(GetLeftPartOfPath(baseUrl), relativePart.OriginalString);
        }

        internal static Uri ConcatenateUrls(string baseUrl, string relativePart)
        {
            return new Uri(ConcatenateUrlsString(baseUrl, relativePart));
        }

        internal static Uri ConcatenateUrls(Uri baseUrl, string relativePart)
        {
            return new Uri(ConcatenateUrlsString(baseUrl, relativePart));
        }

        internal static Uri ConcatenateUrls(Uri baseUrl, Uri relativePart)
        {
            if (relativePart.IsAbsoluteUri)
                return relativePart;

            return new Uri(ConcatenateUrlsString(baseUrl, relativePart));
        }

        internal static NameValueCollection ParseQuery(string queryString)
        {
            NameValueCollection result = null;

            queryString = UrlUtility.RemoveLeadingQuestionMark(queryString);
            if (string.IsNullOrEmpty(queryString))
                result = new NameValueCollection(0);
            else
            {
                string[] queries = SplitAndRemoveEmptyEntries(queryString, new char[] { RuntimeConstants.Separators.Query[1] });
                result = new NameValueCollection(queries.Length);
                for (int index = 0; index < queries.Length; ++index)
                {
                    string[] nameValue = SplitAndRemoveEmptyEntries(queries[index], new char[] { RuntimeConstants.Separators.Query[2] }, 2);
                    result.Add(nameValue[0], nameValue.Length > 1 ? nameValue[1] : null);
                }
            }

            return result;
        }

        internal static string CreateQuery(INameValueCollection parsedQuery)
        {
            if (parsedQuery == null)
                return string.Empty;

            StringBuilder queryString = new StringBuilder();

            int count = parsedQuery.Count();
            foreach (string key in parsedQuery)
            {
                string value = parsedQuery[key];
                if (!string.IsNullOrEmpty(key))
                {
                    if (queryString.Length > 0)
                        queryString.Append(RuntimeConstants.Separators.Query[1]);

                    queryString.Append(key);

                    if (value != null)
                    {
                        queryString.Append(RuntimeConstants.Separators.Query[2]);
                        queryString.Append(value);
                    }
                }
            }

            return queryString.ToString();
        }

        internal static Uri SetQuery(Uri url, string query)
        {
            if (url == null) throw new ArgumentNullException("url");

            string urlSansQuery;
            UriKind resultKind;
            if (url.IsAbsoluteUri)
            {
                urlSansQuery = url.GetComponents(UriComponents.AbsoluteUri & (~UriComponents.Query) & (~UriComponents.Fragment), UriFormat.Unescaped);
                resultKind = UriKind.Absolute;
            }
            else
            {
                resultKind = UriKind.Relative;
                urlSansQuery = url.ToString();
                int lastQuestionMarkIndex = urlSansQuery.LastIndexOf(RuntimeConstants.Separators.Query[0]);
                if (lastQuestionMarkIndex >= 0)
                    urlSansQuery = urlSansQuery.Remove(lastQuestionMarkIndex, urlSansQuery.Length - lastQuestionMarkIndex);
            }

            query = UrlUtility.RemoveLeadingQuestionMark(query);
            if (!string.IsNullOrEmpty(query))
                return new Uri(UrlUtility.AddTrailingSlash(urlSansQuery) + RuntimeConstants.Separators.Query[0] + query, resultKind);
            else
                return new Uri(UrlUtility.AddTrailingSlash(urlSansQuery), resultKind);
        }

        internal static string RemoveLeadingQuestionMark(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            if (path[0] == RuntimeConstants.Separators.Query[0])
                return path.Remove(0, 1);

            return path;
        }

        internal static string RemoveTrailingSlash(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            int length = path.Length;
            if (path[length - 1] == RuntimeConstants.Separators.Url[0])
                return path.Remove(length - 1, 1);

            return path;
        }
        internal static StringSegment RemoveTrailingSlashes(StringSegment path)
        {
            if (path.IsNullOrEmpty())
                return path;

            return path.TrimEnd(RuntimeConstants.Separators.Url);
        }

        internal static string RemoveTrailingSlashes(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            return path.TrimEnd(RuntimeConstants.Separators.Url);
        }

        internal static StringSegment RemoveLeadingSlashes(StringSegment path)
        {
            if (path.IsNullOrEmpty())
                return path;

            return path.TrimStart(RuntimeConstants.Separators.Url);
        }

        internal static string RemoveLeadingSlash(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            if (path[0] == RuntimeConstants.Separators.Url[0])
                return path.Remove(0, 1);

            return path;
        }

        internal static string RemoveLeadingSlashes(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            return path.TrimStart(RuntimeConstants.Separators.Url);
        }

        internal static string AddTrailingSlash(string path)
        {
            if (string.IsNullOrEmpty(path))
                path = new string(RuntimeConstants.Separators.Url);
            else if (path[path.Length - 1] != RuntimeConstants.Separators.Url[0])
                path = path + RuntimeConstants.Separators.Url[0];

            return path;
        }

        internal static string AddLeadingSlash(string path)
        {
            if (string.IsNullOrEmpty(path))
                path = new string(RuntimeConstants.Separators.Url);
            else if (path[0] != RuntimeConstants.Separators.Url[0])
                path = RuntimeConstants.Separators.Url[0] + path;

            return path;
        }

        internal static string GetLeftPartOfAuthority(Uri uri)
        {
            return uri.GetLeftPart(UriPartial.Authority);
        }

        internal static string GetLeftPartOfPath(Uri uri)
        {
            return uri.GetLeftPart(UriPartial.Path);
        }

        public static string[] SplitAndRemoveEmptyEntries(string str, char[] seperators)
        {
            return SplitAndRemoveEmptyEntries(str, seperators, int.MaxValue);
        }

        public static string[] SplitAndRemoveEmptyEntries(string str, char[] seperators, int count)
        {
            return str.Split(seperators, count, StringSplitOptions.RemoveEmptyEntries);
        }

        //ID Segment Parsing Helper.
        internal static string ExtractIdFromItemUri(Uri uri, int i)
        {
            string id = uri.Segments[i];
            return UrlUtility.RemoveTrailingSlash(id);
        }

        // Extract the tenant id from the dns name
        // All our supported domain names are have more then one dns zone so they we only need accept names with at least one zone in them
        // ie, we do not have to supoprt http://localhost/ as a value uri with tenant information but we will for ease of use
        internal static string ExtractTenantIdFromUri(Uri uri)
        {
            string hostName = uri.DnsSafeHost;
            int firstPeriod = hostName.IndexOf('.');
            
            if (firstPeriod != -1)
            {
                return hostName.Substring(0, firstPeriod);
            }
            else
            {
                return hostName;
            }
        }

        internal static string ExtractIdOrFullNameFromUri(string path, out bool isNameBased)
        {
            bool isFeed;
            string resourceIdOrFullName;
            string resourcePath;
            if (PathsHelper.TryParsePathSegments(path, out isFeed, out resourcePath, out resourceIdOrFullName, out isNameBased))
            {
                return resourceIdOrFullName;
            }

            return null;
        }

        internal static string ExtractIdFromItemUri(Uri uri)
        {
            string id = uri.Segments[uri.Segments.Length - 1];
            return UrlUtility.RemoveTrailingSlash(id);
        }

        internal static string ExtractIdFromCollectionUri(Uri uri)
        {
            string ownerId = uri.Segments[uri.Segments.Length - 2];
            return UrlUtility.RemoveTrailingSlash(ownerId);      
        }

        internal static string ExtractItemIdAndCollectionIdFromUri(Uri uri, out string collectionId)
        {
            collectionId = UrlUtility.RemoveTrailingSlash(uri.Segments[uri.Segments.Length - 3]);
            return UrlUtility.RemoveTrailingSlash(uri.Segments[uri.Segments.Length - 1]);
        }

        internal static string ExtractFileNameFromUri(Uri uri)
        {
            return UrlUtility.RemoveTrailingSlash(uri.Segments[uri.Segments.Length - 1]);
        }

        // expected uri http (or) tcp://<ip>:<port>/<restOfThePath>
        internal static bool IsLocalHostUri(Uri uri)
        {
            IPAddress ipAddress;
            if (!IPAddress.TryParse(uri.DnsSafeHost, out ipAddress))
            {
                throw new ArgumentException("uri");
            }

            if (IPAddress.IsLoopback(ipAddress))
            {
                return true;
            }

            List<IPAddress> Addresses = new List<IPAddress>();
            NetworkInterface[] adapters = NetworkInterface.GetAllNetworkInterfaces();
            foreach (NetworkInterface adapter in adapters)
            {
                IPInterfaceProperties properties = adapter.GetIPProperties();
                foreach (IPAddressInformation localIpInfo in properties.UnicastAddresses)
                {
                    if (localIpInfo.Address.Equals(ipAddress))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        #region Astoria Url Parsing functions
        internal static bool IsAstoriaUrl(Uri url)
        {
            return (url.AbsolutePath.IndexOf(RuntimeConstants.Separators.Parenthesis[0]) != -1 && url.AbsolutePath.IndexOf(RuntimeConstants.Separators.Parenthesis[1]) != -1);
        }

        internal static Uri ToNativeUrl(Uri astoriaUrl)
        {
            Uri baseUri = null;

            if (astoriaUrl.IsAbsoluteUri)
                baseUri = new Uri(UrlUtility.GetLeftPartOfAuthority(astoriaUrl));

            string query = astoriaUrl.Query;
            string path = astoriaUrl.AbsolutePath;
            string nativeUrl = null;
            Element[] elements = null;
            if (!UrlUtility.ParseAstoriaUrl(path, out elements))
            {
                // Astoria Parse Failed, we return the same Url
                return astoriaUrl;
            }

            List<string> parts = new List<string>();
            foreach (Element element in elements)
            {
                if (!string.IsNullOrEmpty(element.Name))
                    parts.Add(element.Name);

                if (!string.IsNullOrEmpty(element.Id))
                {
                    string elementId = element.Id.Trim(RuntimeConstants.Separators.Quote);

                    if (elementId.StartsWith(RuntimeConstants.Schemes.UuidScheme, StringComparison.Ordinal))
                    {
                        elementId = elementId.Substring(RuntimeConstants.Schemes.UuidScheme.Length);
                    }
                    parts.Add(elementId);
                }
            }

            string firstPart = parts[0];
            parts.RemoveAt(0);
            nativeUrl = UrlUtility.ConcatenateUrlsString(firstPart, parts.ToArray());

            Uri nativeUri = null;
            if (baseUri != null)
                nativeUri = new Uri(baseUri, nativeUrl);
            else
                nativeUri = new Uri(nativeUrl, UriKind.Relative);

            if (!string.IsNullOrEmpty(query))
                UrlUtility.SetQuery(nativeUri, query);

            return nativeUri;
        }

        private static bool ParseAstoriaUrl(string astoriaUrl, out Element[] urlElements)
        {
            urlElements = null;

            if (astoriaUrl == null)
                return false;

            string[] segments = UrlUtility.SplitAndRemoveEmptyEntries(astoriaUrl, RuntimeConstants.Separators.Url);
            if ((segments == null) || (segments.Length < 1))
                return false; // InValid Url

            List<Element> elements = new List<Element>();
            foreach (string segment in segments)
            {
                string name;
                string id;
                if (!UrlUtility.ParseAstoriaUrlPart(segment, out name, out id))
                    return false; // InValid Url

                elements.Add(new Element(name, id));
            }

            urlElements = elements.ToArray();
            return true;
        }

        private static bool ParseAstoriaUrlPart(string urlPart, out string name, out string id)
        {
            name = null;
            id = null;

            int parameterStart = urlPart.IndexOf(RuntimeConstants.Separators.Parenthesis[0]);
            int parameterEnd = urlPart.IndexOf(RuntimeConstants.Separators.Parenthesis[1]);

            if (parameterStart == -1)
            {
                if (parameterEnd != -1)
                    return false;

                name = urlPart;
            }
            else
            {
                if (parameterEnd == -1 || parameterEnd != urlPart.Length - 1)
                    return false;

                name = urlPart.Substring(0, parameterStart);
                id = urlPart.Substring(parameterStart, parameterEnd - parameterStart).Trim(RuntimeConstants.Separators.Parenthesis);
            }

            return true;
        }

        private class Element
        {
            public Element() { }
            public Element(string name, string id)
            {
                this.Name = name;
                this.Id = id;
            }

            public string Name { get; set; }
            public string Id { get; set; }
        }
        #endregion
    }
}
