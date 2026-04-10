//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Net.Http;
    using System.Text;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class AuthorizationHelperTests
    {
        private readonly string[] HttpMethods = new string[]
        {
            HttpMethod.Get.ToString(),
            HttpMethod.Post.ToString(),
            HttpMethod.Put.ToString(),
            HttpMethod.Delete.ToString(),
            HttpMethod.Head.ToString(),
        };

        private readonly string[] ResourceTypesArray = new string[]
        {
            ResourceType.Document.ToString(),
            ResourceType.Collection.ToString(),
            ResourceType.Database.ToString()
        };

        private readonly string[] ResourceNameValues = new string[]
        {
            "dbs/dbName/Doc1",
            "dbs/dbName/DB1",
            "dbs/dbName/Foobar"
        };

        private readonly string[][] AuthorizationBaseline = new string[][]
        {
            new string[] { "http://localhost.sql:8901/", "", "GET", "", "Tue, 21 Jul 2020 17:55:31 GMT",  "" },
            new string[] { "http://localhost.sql:8901/dbs", "", "GET", "dbs", "Tue, 21 Jul 2020 17:55:33 GMT",  "" },
            new string[] { "http://localhost.sql:8901/dbs/02cae8a7583d41aca2a39d41fce2756c", "dbs/02cae8a7583d41aca2a39d41fce2756c", "DELETE", "dbs", "Tue, 21 Jul 2020 17:55:35 GMT",  "" },
            new string[] { "http://localhost.sql:8901/dbs", "", "GET", "dbs", "Tue, 21 Jul 2020 17:55:36 GMT",  "" },
            new string[] { "http://localhost.sql:8901/dbs", "", "POST", "dbs", "Tue, 21 Jul 2020 17:55:36 GMT",  "" },
            new string[] { "http://localhost.sql:8901/dbs", "", "GET", "dbs", "Tue, 21 Jul 2020 17:55:36 GMT",  "" },
            new string[] { "http://localhost.sql:8901/dbs/92dc837c73484349852a2c8f05777565", "dbs/92dc837c73484349852a2c8f05777565", "GET", "dbs", "Tue, 21 Jul 2020 17:55:36 GMT",  "" },
            new string[] { "http://localhost.sql:8901/dbs/92dc837c73484349852a2c8f05777565/colls", "dbs/92dc837c73484349852a2c8f05777565", "POST", "colls", "Tue, 21 Jul 2020 17:55:36 GMT",  "" },
            new string[] { "http://localhost.sql:8901/dbs/92dc837c73484349852a2c8f05777565/colls/ea9ebd7de6aa46d8bc0aa7e29b89236c", "dbs/92dc837c73484349852a2c8f05777565/colls/ea9ebd7de6aa46d8bc0aa7e29b89236c", "GET", "colls", "Tue, 21 Jul 2020 17:55:37 GMT",  "" },
            new string[] { "http://localhost.sql:8901/dbs/DPZtAA==/colls/DPZtAKyOj5g=/pkranges", "DPZtAKyOj5g=", "GET", "pkranges", "Tue, 21 Jul 2020 17:55:37 GMT",  "" },
            new string[] { "http://localhost.sql/dbs/DPZtAA==/colls/DPZtAKyOj5g=/docs", "DPZtAKyOj5g=", "GET", "docs", "Tue, 21 Jul 2020 17:55:37 GMT",  "" },
            new string[] { "http://localhost.sql:8901/dbs/92dc837c73484349852a2c8f05777565/colls/ea9ebd7de6aa46d8bc0aa7e29b89236c", "dbs/92dc837c73484349852a2c8f05777565/colls/ea9ebd7de6aa46d8bc0aa7e29b89236c", "GET", "colls", "Tue, 21 Jul 2020 17:55:37 GMT",  "" },
            new string[] { "http://localhost.sql:8901/dbs/92dc837c73484349852a2c8f05777565/colls/ea9ebd7de6aa46d8bc0aa7e29b89236c/docs", "dbs/92dc837c73484349852a2c8f05777565/colls/ea9ebd7de6aa46d8bc0aa7e29b89236c", "POST", "docs", "Tue, 21 Jul 2020 17:55:37 GMT",  "" },
            new string[] { "http://localhost.sql:8901/dbs/92dc837c73484349852a2c8f05777565/colls/ea9ebd7de6aa46d8bc0aa7e29b89236c/docs/9f39cb554e0f48e7a829dda4f441e147", "dbs/92dc837c73484349852a2c8f05777565/colls/ea9ebd7de6aa46d8bc0aa7e29b89236c/docs/9f39cb554e0f48e7a829dda4f441e147", "GET", "docs", "Tue, 21 Jul 2020 17:55:37 GMT",  "" },
            new string[] { "http://localhost.sql:8901/dbs/DPZtAA==/colls/DPZtAKyOj5g=/docs/DPZtAKyOj5gBAAAAAAAAAA==/attachments", "DPZtAKyOj5gBAAAAAAAAAA==", "POST", "attachments", "Tue, 21 Jul 2020 17:55:37 GMT",  "" },
            new string[] { "http://localhost.sql:8901/dbs/DPZtAA==/colls/DPZtAKyOj5g=/docs/DPZtAKyOj5gBAAAAAAAAAA==/attachments/DPZtAKyOj5gBAAAAAAAAACrX8zg=", "DPZtAKyOj5gBAAAAAAAAACrX8zg=", "GET", "attachments", "Tue, 21 Jul 2020 17:55:37 GMT",  "" },
            new string[] { "http://localhost.sql:8901/dbs/DPZtAA==/colls/DPZtAKyOj5g=/docs/DPZtAKyOj5gBAAAAAAAAAA==", "DPZtAKyOj5gBAAAAAAAAAA==", "PUT", "docs", "Tue, 21 Jul 2020 17:55:37 GMT",  "" },
            new string[] { "http://localhost.sql:8901/dbs/DPZtAA==/colls/DPZtAKyOj5g=/pkranges", "DPZtAKyOj5g=", "GET", "pkranges", "Tue, 21 Jul 2020 17:55:38 GMT",  "" },
            new string[] { "http://localhost.sql:8901/dbs/DPZtAA==/colls/DPZtAKyOj5g=/pkranges", "DPZtAKyOj5g=", "GET", "pkranges", "Tue, 21 Jul 2020 17:55:38 GMT",  "" },
            new string[] { "http://localhost.sql:8901/dbs/DPZtAA==/colls/DPZtAKyOj5g=/docs", "DPZtAKyOj5g=", "POST", "docs", "Tue, 21 Jul 2020 17:55:38 GMT",  "" },
            new string[] { "http://localhost.sql:8901/dbs/DPZtAA==/colls/DPZtAKyOj5g=/docs/DPZtAKyOj5gBAAAAAAAAAA==", "DPZtAKyOj5gBAAAAAAAAAA==", "DELETE", "docs", "Tue, 21 Jul 2020 17:55:38 GMT",  "" },
            new string[] { "http://localhost.sql:8901/dbs/DPZtAA==/colls/DPZtAKyOj5g=/docs/DPZtAKyOj5gBAAAAAAAAAA==", "DPZtAKyOj5gBAAAAAAAAAA==", "GET", "docs", "Tue, 21 Jul 2020 17:55:38 GMT",  "" },
            new string[] { "http://localhost.sql:8901/dbs/92dc837c73484349852a2c8f05777565/colls/ea9ebd7de6aa46d8bc0aa7e29b89236c", "dbs/92dc837c73484349852a2c8f05777565/colls/ea9ebd7de6aa46d8bc0aa7e29b89236c", "DELETE", "colls", "Tue, 21 Jul 2020 17:55:38 GMT",  "" },
            new string[] { "http://localhost.sql:8901/dbs/92dc837c73484349852a2c8f05777565/colls/ea9ebd7de6aa46d8bc0aa7e29b89236c", "dbs/92dc837c73484349852a2c8f05777565/colls/ea9ebd7de6aa46d8bc0aa7e29b89236c", "GET", "colls", "Tue, 21 Jul 2020 17:55:38 GMT",  "" },
            new string[] { "http://localhost.sql:8901/dbs/92dc837c73484349852a2c8f05777565", "dbs/92dc837c73484349852a2c8f05777565", "DELETE", "dbs", "Tue, 21 Jul 2020 17:55:38 GMT",  "" },
            new string[] { "http://localhost.sql:8901/dbs/92dc837c73484349852a2c8f05777565", "dbs/92dc837c73484349852a2c8f05777565", "GET", "dbs", "Tue, 21 Jul 2020 17:55:39 GMT",  "" },
            new string[] { "http://localhost.sql:8901/", "", "GET", "", "Tue, 21 Jul 2020 17:55:39 GMT",  "" },
            new string[] { "http://localhost.sql:8901/dbs", "", "GET", "dbs", "Tue, 21 Jul 2020 17:55:39 GMT",  "" },
            new string[] { "http://localhost.sql:8901/dbs", "", "POST", "dbs", "Tue, 21 Jul 2020 17:55:39 GMT",  "" },
            new string[] { "http://localhost.sql:8901/dbs/9c6f9f5b4495446695eefc0ab45863e2/colls", "dbs/9c6f9f5b4495446695eefc0ab45863e2", "POST", "colls", "Tue, 21 Jul 2020 17:55:39 GMT",  "" },
            new string[] { "http://localhost.sql:8901/dbs/9c6f9f5b4495446695eefc0ab45863e2/colls/67c1cfc78ba1479cae13c04c3c5ecf54", "dbs/9c6f9f5b4495446695eefc0ab45863e2/colls/67c1cfc78ba1479cae13c04c3c5ecf54", "GET", "colls", "Tue, 21 Jul 2020 17:55:39 GMT",  "" },
            new string[] { "http://localhost.sql:8901/dbs/9c6f9f5b4495446695eefc0ab45863e2/colls/67c1cfc78ba1479cae13c04c3c5ecf54/docs", "dbs/9c6f9f5b4495446695eefc0ab45863e2/colls/67c1cfc78ba1479cae13c04c3c5ecf54", "POST", "docs", "Tue, 21 Jul 2020 17:55:39 GMT",  "" },
            new string[] { "http://localhost.sql:8901/dbs/9c6f9f5b4495446695eefc0ab45863e2/colls/67c1cfc78ba1479cae13c04c3c5ecf54/docs", "dbs/9c6f9f5b4495446695eefc0ab45863e2/colls/67c1cfc78ba1479cae13c04c3c5ecf54", "POST", "docs", "Tue, 21 Jul 2020 17:55:39 GMT",  "" },
            new string[] { "http://localhost.sql:8901/dbs/9c6f9f5b4495446695eefc0ab45863e2/colls/67c1cfc78ba1479cae13c04c3c5ecf54/docs/nonexistentdocument", "dbs/9c6f9f5b4495446695eefc0ab45863e2/colls/67c1cfc78ba1479cae13c04c3c5ecf54/docs/nonexistentdocument", "GET", "docs", "Tue, 21 Jul 2020 17:55:39 GMT",  "" },
            new string[] { "http://localhost.sql:8901/", "", "GET", "", "Tue, 21 Jul 2020 17:55:39 GMT",  "" },
            new string[] { "http://localhost.sql:8901/dbs", "", "GET", "dbs", "Tue, 21 Jul 2020 17:55:39 GMT",  "" },
            new string[] { "http://localhost.sql:8901/dbs/9c6f9f5b4495446695eefc0ab45863e2", "dbs/9c6f9f5b4495446695eefc0ab45863e2", "DELETE", "dbs", "Tue, 21 Jul 2020 17:55:39 GMT",  "" },
            new string[] { "http://localhost.sql:8901/dbs", "", "POST", "dbs", "Tue, 21 Jul 2020 17:55:41 GMT",  "" },
            new string[] { "http://localhost.sql:8901/dbs/aba89649660340f0a27890f326b2846a/colls", "dbs/aba89649660340f0a27890f326b2846a", "POST", "colls", "Tue, 21 Jul 2020 17:55:41 GMT",  "" },
            new string[] { "http://localhost.sql:8901/dbs/aba89649660340f0a27890f326b2846a/colls/e892e9e3ba6c413298a3ad63529e4508", "dbs/aba89649660340f0a27890f326b2846a/colls/e892e9e3ba6c413298a3ad63529e4508", "GET", "colls", "Tue, 21 Jul 2020 17:55:41 GMT",  "" },
            new string[] { "http://localhost.sql:8901/dbs/aba89649660340f0a27890f326b2846a/colls/e892e9e3ba6c413298a3ad63529e4508/docs", "dbs/aba89649660340f0a27890f326b2846a/colls/e892e9e3ba6c413298a3ad63529e4508", "POST", "docs", "Tue, 21 Jul 2020 17:55:41 GMT",  "" },
            new string[] { "http://localhost.sql:8901/dbs/aba89649660340f0a27890f326b2846a/colls/e892e9e3ba6c413298a3ad63529e4508/docs", "dbs/aba89649660340f0a27890f326b2846a/colls/e892e9e3ba6c413298a3ad63529e4508", "POST", "docs", "Tue, 21 Jul 2020 17:55:41 GMT",  "" },
            new string[] { "http://localhost.sql:8901/dbs/aba89649660340f0a27890f326b2846a/colls/e892e9e3ba6c413298a3ad63529e4508/docs", "dbs/aba89649660340f0a27890f326b2846a/colls/e892e9e3ba6c413298a3ad63529e4508", "POST", "docs", "Tue, 21 Jul 2020 17:55:41 GMT",  "" },
            new string[] { "http://localhost.sql:8901/", "", "GET", "", "Tue, 21 Jul 2020 17:55:41 GMT",  "" },
            new string[] { "http://localhost.sql:8901/", "", "GET", "", "Tue, 21 Jul 2020 17:55:41 GMT",  "" },
        };

        private readonly string[][] AuthorizationBaselineResults = new string[][]
        {
            new string[] { "type%3dmaster%26ver%3d1.0%26sig%3d4ldo2CcZsh80o%2fVDqZFPS%2b3d2O0hTGfsVmhTfnSlE8g%3d", "get[n][n][n]tue, 21 jul 2020 17:55:31 gmt[n][n]", },
            new string[] { "type%3dmaster%26ver%3d1.0%26sig%3deGk7x5lvE1VKgZ4286y6%2baxcDJfDbfOFeCDHVg%2f9sZo%3d", "get[n]dbs[n][n]tue, 21 jul 2020 17:55:33 gmt[n][n]", },
            new string[] { "type%3dmaster%26ver%3d1.0%26sig%3d8NkuMW4MRhfcWRZCoyztUXMfIVMui8wNHIu6vd6sy3o%3d", "delete[n]dbs[n]dbs/02cae8a7583d41aca2a39d41fce2756c[n]tue, 21 jul 2020 17:55:35 gmt[n][n]", },
            new string[] { "type%3dmaster%26ver%3d1.0%26sig%3dsRFToUL1DKsWxKj9n3RtHJ1tryUlIVsBhp1PMFQ6OAU%3d", "get[n]dbs[n][n]tue, 21 jul 2020 17:55:36 gmt[n][n]", },
            new string[] { "type%3dmaster%26ver%3d1.0%26sig%3dNQA0D24qeSf1iGgtZn7gHtWg9BGQCWcE%2bt7NKnVOoEI%3d", "post[n]dbs[n][n]tue, 21 jul 2020 17:55:36 gmt[n][n]", },
            new string[] { "type%3dmaster%26ver%3d1.0%26sig%3dsRFToUL1DKsWxKj9n3RtHJ1tryUlIVsBhp1PMFQ6OAU%3d", "get[n]dbs[n][n]tue, 21 jul 2020 17:55:36 gmt[n][n]", },
            new string[] { "type%3dmaster%26ver%3d1.0%26sig%3dniv0JZU%2fT0s8tmaod36QbBmZJBCIbkJU31r5Rp%2brJGQ%3d", "get[n]dbs[n]dbs/92dc837c73484349852a2c8f05777565[n]tue, 21 jul 2020 17:55:36 gmt[n][n]", },
            new string[] { "type%3dmaster%26ver%3d1.0%26sig%3d2MIgOuCzkrWLI6%2bj5NYnmKYm8a7oS1dmcvETQAs3sEQ%3d", "post[n]colls[n]dbs/92dc837c73484349852a2c8f05777565[n]tue, 21 jul 2020 17:55:36 gmt[n][n]", },
            new string[] { "type%3dmaster%26ver%3d1.0%26sig%3dXJkV5QFSvzjviGYGuaK2D7umyjuo2huG15PNt7wNKCg%3d", "get[n]colls[n]dbs/92dc837c73484349852a2c8f05777565/colls/ea9ebd7de6aa46d8bc0aa7e29b89236c[n]tue, 21 jul 2020 17:55:37 gmt[n][n]", },
            new string[] { "type%3dmaster%26ver%3d1.0%26sig%3dqH73yd16s9pr9e3X4azaZc97zyqs4oyxkB1L%2bLdsGbk%3d", "get[n]pkranges[n]dpztakyoj5g=[n]tue, 21 jul 2020 17:55:37 gmt[n][n]", },
            new string[] { "type%3dmaster%26ver%3d1.0%26sig%3dnWZ29vBWEbWx7UZlJP%2bTzbgHoYCVVy6mz004thDJuPc%3d", "get[n]docs[n]dpztakyoj5g=[n]tue, 21 jul 2020 17:55:37 gmt[n][n]", },
            new string[] { "type%3dmaster%26ver%3d1.0%26sig%3dXJkV5QFSvzjviGYGuaK2D7umyjuo2huG15PNt7wNKCg%3d", "get[n]colls[n]dbs/92dc837c73484349852a2c8f05777565/colls/ea9ebd7de6aa46d8bc0aa7e29b89236c[n]tue, 21 jul 2020 17:55:37 gmt[n][n]", },
            new string[] { "type%3dmaster%26ver%3d1.0%26sig%3ddQGGlBUiVh2cX%2bLCTXt%2bfCyAkPRpChou%2f%2fplVECIuyA%3d", "post[n]docs[n]dbs/92dc837c73484349852a2c8f05777565/colls/ea9ebd7de6aa46d8bc0aa7e29b89236c[n]tue, 21 jul 2020 17:55:37 gmt[n][n]", },
            new string[] { "type%3dmaster%26ver%3d1.0%26sig%3d0QVIU7gETTwkjcRWYKwHUOYKKdXi6FyuV8KsDSnVdeA%3d", "get[n]docs[n]dbs/92dc837c73484349852a2c8f05777565/colls/ea9ebd7de6aa46d8bc0aa7e29b89236c/docs/9f39cb554e0f48e7a829dda4f441e147[n]tue, 21 jul 2020 17:55:37 gmt[n][n]", },
            new string[] { "type%3dmaster%26ver%3d1.0%26sig%3d6d7OgoRAWHZEo1DgvUMabsxXU6AIuVPdvoBIEixcuxg%3d", "post[n]attachments[n]dpztakyoj5gbaaaaaaaaaa==[n]tue, 21 jul 2020 17:55:37 gmt[n][n]", },
            new string[] { "type%3dmaster%26ver%3d1.0%26sig%3d3GdkZIIRiH4H8V85H1Af7lq4ddabXYosLVHfaf0kyQQ%3d", "get[n]attachments[n]dpztakyoj5gbaaaaaaaaacrx8zg=[n]tue, 21 jul 2020 17:55:37 gmt[n][n]", },
            new string[] { "type%3dmaster%26ver%3d1.0%26sig%3dF8ZE0qxkWE1DszV7y46GWQs42kdkFfT2%2bhGHK0zJp7U%3d", "put[n]docs[n]dpztakyoj5gbaaaaaaaaaa==[n]tue, 21 jul 2020 17:55:37 gmt[n][n]", },
            new string[] { "type%3dmaster%26ver%3d1.0%26sig%3dWnEeafF5Jf85Syax7OesO9iI4nPcfHIqI%2fYWoYyWgOQ%3d", "get[n]pkranges[n]dpztakyoj5g=[n]tue, 21 jul 2020 17:55:38 gmt[n][n]", },
            new string[] { "type%3dmaster%26ver%3d1.0%26sig%3dWnEeafF5Jf85Syax7OesO9iI4nPcfHIqI%2fYWoYyWgOQ%3d", "get[n]pkranges[n]dpztakyoj5g=[n]tue, 21 jul 2020 17:55:38 gmt[n][n]", },
            new string[] { "type%3dmaster%26ver%3d1.0%26sig%3dC%2fyNqDGvgnCTTE2VhalBGDOmMGTe%2fYalWjVENBmUBag%3d", "post[n]docs[n]dpztakyoj5g=[n]tue, 21 jul 2020 17:55:38 gmt[n][n]", },
            new string[] { "type%3dmaster%26ver%3d1.0%26sig%3dyw%2fThc12voLUXMZ7NMDp5VsCgAwRGNF2K38oulEfpGg%3d", "delete[n]docs[n]dpztakyoj5gbaaaaaaaaaa==[n]tue, 21 jul 2020 17:55:38 gmt[n][n]", },

            new string[] { "type%3dmaster%26ver%3d1.0%26sig%3dFrkHqwF9NwtGrh8EnKuGSk9gwe2E%2bzD8AECpVlF4z4g%3d", "get[n]docs[n]dpztakyoj5gbaaaaaaaaaa==[n]tue, 21 jul 2020 17:55:38 gmt[n][n]", },
            new string[] { "type%3dmaster%26ver%3d1.0%26sig%3dF35Jygmt7KZSLVceDDMZ3zIcTLntPaP7v0pn237%2bBsg%3d", "delete[n]colls[n]dbs/92dc837c73484349852a2c8f05777565/colls/ea9ebd7de6aa46d8bc0aa7e29b89236c[n]tue, 21 jul 2020 17:55:38 gmt[n][n]", },
            new string[] { "type%3dmaster%26ver%3d1.0%26sig%3dpQlz8RG9%2fPfERx6NLYC1xha95HzMbSiValGG4b27Y4E%3d", "get[n]colls[n]dbs/92dc837c73484349852a2c8f05777565/colls/ea9ebd7de6aa46d8bc0aa7e29b89236c[n]tue, 21 jul 2020 17:55:38 gmt[n][n]", },
            new string[] { "type%3dmaster%26ver%3d1.0%26sig%3dnhwWg%2bLA%2b4WrPIK5sOzcIhDhO6qDh6TCNxsCeq8kits%3d", "delete[n]dbs[n]dbs/92dc837c73484349852a2c8f05777565[n]tue, 21 jul 2020 17:55:38 gmt[n][n]", },
            new string[] { "type%3dmaster%26ver%3d1.0%26sig%3d3r7hJLdA0ZNtmFb2ONzkRDCmoTbcr%2f%2fmuNsJcas6Cpg%3d", "get[n]dbs[n]dbs/92dc837c73484349852a2c8f05777565[n]tue, 21 jul 2020 17:55:39 gmt[n][n]", },
            new string[] { "type%3dmaster%26ver%3d1.0%26sig%3dGNQ8GcpQ%2bMM54HsOVQsCdjXPSmp2kbUTq2yWpwbPUSo%3d", "get[n][n][n]tue, 21 jul 2020 17:55:39 gmt[n][n]", },
            new string[] { "type%3dmaster%26ver%3d1.0%26sig%3dDa8EeX9A3b3HdDcLGkR9GGr6gb88voxfD04hhAKmki8%3d", "get[n]dbs[n][n]tue, 21 jul 2020 17:55:39 gmt[n][n]", },
            new string[] { "type%3dmaster%26ver%3d1.0%26sig%3dsOKkc7a14LYoCakwDWw9Omngx3wDBsZtIII08TdFwgk%3d", "post[n]dbs[n][n]tue, 21 jul 2020 17:55:39 gmt[n][n]", },
            new string[] { "type%3dmaster%26ver%3d1.0%26sig%3dPAd8IyJ%2bvEQtZ%2bOUzGN1rsDSfPuyocYp2bDdrcvcigg%3d", "post[n]colls[n]dbs/9c6f9f5b4495446695eefc0ab45863e2[n]tue, 21 jul 2020 17:55:39 gmt[n][n]", },
            new string[] { "type%3dmaster%26ver%3d1.0%26sig%3dYDfwcLQbSgup%2fQsNUnWdPkpU8UAQMGmgyDUGIcOkEGc%3d", "get[n]colls[n]dbs/9c6f9f5b4495446695eefc0ab45863e2/colls/67c1cfc78ba1479cae13c04c3c5ecf54[n]tue, 21 jul 2020 17:55:39 gmt[n][n]", },
            new string[] { "type%3dmaster%26ver%3d1.0%26sig%3dGDEBkBGMy5ShF%2fNuXG%2fdgx75YLvHMEFHbTcr5VSyBH8%3d", "post[n]docs[n]dbs/9c6f9f5b4495446695eefc0ab45863e2/colls/67c1cfc78ba1479cae13c04c3c5ecf54[n]tue, 21 jul 2020 17:55:39 gmt[n][n]", },
            new string[] { "type%3dmaster%26ver%3d1.0%26sig%3dGDEBkBGMy5ShF%2fNuXG%2fdgx75YLvHMEFHbTcr5VSyBH8%3d", "post[n]docs[n]dbs/9c6f9f5b4495446695eefc0ab45863e2/colls/67c1cfc78ba1479cae13c04c3c5ecf54[n]tue, 21 jul 2020 17:55:39 gmt[n][n]", },
            new string[] { "type%3dmaster%26ver%3d1.0%26sig%3dXSYy9ViWlekpbZcizHYlX%2f5wfmamtld1T8Fgs0%2bixwM%3d", "get[n]docs[n]dbs/9c6f9f5b4495446695eefc0ab45863e2/colls/67c1cfc78ba1479cae13c04c3c5ecf54/docs/nonexistentdocument[n]tue, 21 jul 2020 17:55:39 gmt[n][n]", },
            new string[] { "type%3dmaster%26ver%3d1.0%26sig%3dGNQ8GcpQ%2bMM54HsOVQsCdjXPSmp2kbUTq2yWpwbPUSo%3d", "get[n][n][n]tue, 21 jul 2020 17:55:39 gmt[n][n]", },
            new string[] { "type%3dmaster%26ver%3d1.0%26sig%3dDa8EeX9A3b3HdDcLGkR9GGr6gb88voxfD04hhAKmki8%3d", "get[n]dbs[n][n]tue, 21 jul 2020 17:55:39 gmt[n][n]", },
            new string[] { "type%3dmaster%26ver%3d1.0%26sig%3ds22R62t09HTc30659y1AVoiDAUuxw9oCj4AJTr0wNe8%3d", "delete[n]dbs[n]dbs/9c6f9f5b4495446695eefc0ab45863e2[n]tue, 21 jul 2020 17:55:39 gmt[n][n]", },
            new string[] { "type%3dmaster%26ver%3d1.0%26sig%3dI3TPWNTTXAI0mcyrUk%2fKKUuicDGsfchR87BrHdTLenQ%3d", "post[n]dbs[n][n]tue, 21 jul 2020 17:55:41 gmt[n][n]", },
            new string[] { "type%3dmaster%26ver%3d1.0%26sig%3dwX2yEOEAXHw51Zd65TBGDZBIG%2fvZsm%2bZo5wfCUa2qrY%3d", "post[n]colls[n]dbs/aba89649660340f0a27890f326b2846a[n]tue, 21 jul 2020 17:55:41 gmt[n][n]", },
            new string[] { "type%3dmaster%26ver%3d1.0%26sig%3dOre1%2be3GjMMkb6kmIJ1n5m30ENg9mWMLcqXOxAQ9%2fog%3d", "get[n]colls[n]dbs/aba89649660340f0a27890f326b2846a/colls/e892e9e3ba6c413298a3ad63529e4508[n]tue, 21 jul 2020 17:55:41 gmt[n][n]", },

            new string[] { "type%3dmaster%26ver%3d1.0%26sig%3d5KudRjhJyqlOor0wknj%2fOaoD3lIyohzCzETLUGbfypo%3d", "post[n]docs[n]dbs/aba89649660340f0a27890f326b2846a/colls/e892e9e3ba6c413298a3ad63529e4508[n]tue, 21 jul 2020 17:55:41 gmt[n][n]", },
            new string[] { "type%3dmaster%26ver%3d1.0%26sig%3d5KudRjhJyqlOor0wknj%2fOaoD3lIyohzCzETLUGbfypo%3d", "post[n]docs[n]dbs/aba89649660340f0a27890f326b2846a/colls/e892e9e3ba6c413298a3ad63529e4508[n]tue, 21 jul 2020 17:55:41 gmt[n][n]", },
            new string[] { "type%3dmaster%26ver%3d1.0%26sig%3d5KudRjhJyqlOor0wknj%2fOaoD3lIyohzCzETLUGbfypo%3d", "post[n]docs[n]dbs/aba89649660340f0a27890f326b2846a/colls/e892e9e3ba6c413298a3ad63529e4508[n]tue, 21 jul 2020 17:55:41 gmt[n][n]", },
            new string[] { "type%3dmaster%26ver%3d1.0%26sig%3d74imyQlvO0n2VTOVkGwTDandzOtMGUUQ3SnY8gSmIgk%3d", "get[n][n][n]tue, 21 jul 2020 17:55:41 gmt[n][n]", },
            new string[] { "type%3dmaster%26ver%3d1.0%26sig%3d74imyQlvO0n2VTOVkGwTDandzOtMGUUQ3SnY8gSmIgk%3d", "get[n][n][n]tue, 21 jul 2020 17:55:41 gmt[n][n]", },
        };

        [TestMethod]
        public void AuthorizationGenerateAndCheckKeyAuthSignature()
        {
            Random r = new Random();
            byte[] hashKey = new byte[16];
            r.NextBytes(hashKey);
            string key = Convert.ToBase64String(hashKey);
            foreach (string method in this.HttpMethods)
            {
                foreach (string resourceType in this.ResourceTypesArray)
                {
                    foreach (string resourceName in this.ResourceNameValues)
                    {
                        RequestNameValueCollection nvc = new()
                        {
                            { HttpConstants.HttpHeaders.XDate, new DateTime(2020, 02, 01, 10, 00, 00).ToString("r") }
                        };
                        string authorizationKey = AuthorizationHelper.GenerateKeyAuthorizationSignature(
                            method,
                            resourceName,
                            resourceType,
                            nvc,
                            new StringHMACSHA256Hash(key),
                            out string _);
                        AuthorizationHelper.ParseAuthorizationToken(authorizationKey,
                            out ReadOnlyMemory<char> typeOutput,
                            out _,
                            out ReadOnlyMemory<char> tokenOutput);
                        Assert.AreEqual("master", typeOutput.ToString());

                        Assert.IsTrue(AuthorizationHelper.CheckPayloadUsingKey(
                            tokenOutput,
                            method,
                            resourceName,
                            resourceType,
                            nvc,
                            key));
                    }
                }
            }
        }

        [TestMethod]
        public void AuthorizationBaselineTests()
        {
            string key = "VGhpcyBpcyBhIHNhbXBsZSBzdHJpbmc=";

            for (int i = 0; i < this.AuthorizationBaseline.Length; i++)
            {
                string[] baseline = this.AuthorizationBaseline[i];
                string[] baselineResults = this.AuthorizationBaselineResults[i];
                RequestNameValueCollection nvc = new()
                {
                    { HttpConstants.HttpHeaders.XDate, baseline[4] }
                };
                Uri uri = new Uri(baseline[0]);
                string authorization = AuthorizationHelper.GenerateKeyAuthorizationSignature(
                    verb: baseline[2],
                    uri: uri,
                    headers: nvc,
                    stringHMACSHA256Helper: new StringHMACSHA256Hash(key));

                string authorization2 = AuthorizationHelper.GenerateKeyAuthorizationSignature(
                    verb: baseline[2],
                    resourceId: baseline[1],
                    resourceType: baseline[3],
                    headers: nvc,
                    new StringHMACSHA256Hash(key),
                    out string payload2);

                string authorization3 = AuthorizationHelper.GenerateKeyAuthorizationSignature(
                    verb: baseline[2],
                    resourceId: baseline[1],
                    resourceType: baseline[3],
                    headers: nvc,
                    key);

                Assert.AreEqual(authorization, baselineResults[0]);
                Assert.AreEqual(authorization2, baselineResults[0]);
                Assert.AreEqual(authorization3, baselineResults[0]);
                Assert.AreEqual(payload2.Replace("\n", "[n]"), baselineResults[1]);
                AuthorizationHelper.ParseAuthorizationToken(authorization, out ReadOnlyMemory<char> typeOutput1, out _, out ReadOnlyMemory<char> tokenOutput1);
                Assert.AreEqual("master", typeOutput1.ToString());
                AuthorizationHelper.ParseAuthorizationToken(authorization2, out ReadOnlyMemory<char> typeOutput2, out _, out ReadOnlyMemory<char> tokenOutput2);
                Assert.AreEqual("master", typeOutput2.ToString());
                AuthorizationHelper.ParseAuthorizationToken(authorization3, out ReadOnlyMemory<char> typeOutput3, out _, out ReadOnlyMemory<char> tokenOutput3);
                Assert.AreEqual("master", typeOutput3.ToString());

                Assert.IsTrue(AuthorizationHelper.CheckPayloadUsingKey(tokenOutput1, baseline[2], baseline[1], baseline[3], nvc, key));
                Assert.IsTrue(AuthorizationHelper.CheckPayloadUsingKey(tokenOutput2, baseline[2], baseline[1], baseline[3], nvc, key));
                Assert.IsTrue(AuthorizationHelper.CheckPayloadUsingKey(tokenOutput3, baseline[2], baseline[1], baseline[3], nvc, key));
            }
        }

        [TestMethod]
        public void Base64UrlEncoderFuzzTest()
        {
            Random random = new Random();
            for (int i = 0; i < 2000; i++)
            {
                Span<byte> randomBytes = new byte[random.Next(1, 500)];
                random.NextBytes(randomBytes);
                string randomBase64String = Convert.ToBase64String(randomBytes);
                byte[] randomBase64Bytes = Encoding.UTF8.GetBytes(randomBase64String);
                Span<byte> buffered = new byte[randomBase64Bytes.Length * 3];
                randomBase64Bytes.CopyTo(buffered);

                string baseline = null;
                string newResults = null;
                try
                {
                    baseline = HttpUtility.UrlEncode(randomBase64Bytes);
                    newResults = AuthorizationHelper.UrlEncodeBase64SpanInPlace(buffered, randomBase64Bytes.Length);
                }
                catch (Exception e)
                {
                    Assert.Fail($"Url encode failed with string {randomBase64String} ; Exception:{e}");
                }

                Assert.AreEqual(baseline, newResults);
            }
        }

        [TestMethod]
        public void Base64UrlEncoderEdgeCasesTest()
        {
            {
                Span<byte> singleInvalidChar = new byte[3];
                singleInvalidChar[0] = (byte)'=';
                string urlEncoded = AuthorizationHelper.UrlEncodeBase64SpanInPlace(singleInvalidChar, 1);
                Assert.AreEqual("%3d", urlEncoded);
            }

            {
                Span<byte> singleInvalidChar = new byte[3];
                singleInvalidChar[0] = (byte)'+';
                string urlEncoded = AuthorizationHelper.UrlEncodeBase64SpanInPlace(singleInvalidChar, 1);
                Assert.AreEqual("%2b", urlEncoded);
            }

            {
                Span<byte> singleInvalidChar = new byte[3];
                singleInvalidChar[0] = (byte)'/';
                string urlEncoded = AuthorizationHelper.UrlEncodeBase64SpanInPlace(singleInvalidChar, 1);
                Assert.AreEqual("%2f", urlEncoded);
            }

            {
                Span<byte> multipleInvalidChar = new byte[9];
                multipleInvalidChar[0] = (byte)'=';
                multipleInvalidChar[1] = (byte)'+';
                multipleInvalidChar[2] = (byte)'/';
                string urlEncoded = AuthorizationHelper.UrlEncodeBase64SpanInPlace(multipleInvalidChar, 3);
                Assert.AreEqual("%3d%2b%2f", urlEncoded);
            }

            {
                Span<byte> singleValidChar = new byte[3];
                singleValidChar[0] = (byte)'a';
                string urlEncoded = AuthorizationHelper.UrlEncodeBase64SpanInPlace(singleValidChar, 1);
                Assert.AreEqual("a", urlEncoded);
            }

            {
                byte[] singleInvalidChar = new byte[0];
                string result = HttpUtility.UrlEncode(singleInvalidChar);
                string urlEncoded = AuthorizationHelper.UrlEncodeBase64SpanInPlace(singleInvalidChar, 0);
                Assert.AreEqual(result, urlEncoded);
            }
        }

        [TestMethod]
        public void AuthorizationTokenLengthTest()
        {
            // Master Token (limit 1024)
            this.ValidateTokenParsing(Constants.Properties.MasterToken, 100, shouldParse: true);
            this.ValidateTokenParsing(Constants.Properties.MasterToken, 1024, shouldParse: true);
            this.ValidateTokenParsing(Constants.Properties.MasterToken, 1024 + 1, shouldParse: false);
            this.ValidateTokenParsing(Constants.Properties.MasterToken, 8 * 1024, shouldParse: false);
            this.ValidateTokenParsing(Constants.Properties.MasterToken, (8 * 1024) + 1, shouldParse: false);
            this.ValidateTokenParsing(Constants.Properties.MasterToken, 16 * 1024, shouldParse: false);
            this.ValidateTokenParsing(Constants.Properties.MasterToken, (16 * 1024) + 1, shouldParse: false);

            // Resource Token (limit 8*1024)
            this.ValidateTokenParsing(Constants.Properties.ResourceToken, 100, shouldParse: true);
            this.ValidateTokenParsing(Constants.Properties.ResourceToken, 1024, shouldParse: true);
            this.ValidateTokenParsing(Constants.Properties.ResourceToken, 1024 + 1, shouldParse: true);
            this.ValidateTokenParsing(Constants.Properties.ResourceToken, 8 * 1024, shouldParse: true);
            this.ValidateTokenParsing(Constants.Properties.ResourceToken, (8 * 1024) + 1, shouldParse: false);
            this.ValidateTokenParsing(Constants.Properties.ResourceToken, 16 * 1024, shouldParse: false);
            this.ValidateTokenParsing(Constants.Properties.ResourceToken, (16 * 1024) + 1, shouldParse: false);

            // AAD Token (limit 16*1024)
            this.ValidateTokenParsing(Constants.Properties.AadToken, 100, shouldParse: true);
            this.ValidateTokenParsing(Constants.Properties.AadToken, 1024, shouldParse: true);
            this.ValidateTokenParsing(Constants.Properties.AadToken, 1024 + 1, shouldParse: true);
            this.ValidateTokenParsing(Constants.Properties.AadToken, 8 * 1024, shouldParse: true);
            this.ValidateTokenParsing(Constants.Properties.AadToken, (8 * 1024) + 1, shouldParse: true);
            this.ValidateTokenParsing(Constants.Properties.AadToken, 16 * 1024, shouldParse: true);
            this.ValidateTokenParsing(Constants.Properties.AadToken, (16 * 1024) + 1, shouldParse: false);
        }

        private void ValidateTokenParsing(string tokenType, int length, bool shouldParse)
        {
            string token = this.GenerateSampleToken(tokenType, length, out string expectedParsedToken);

            try
            {
                AuthorizationHelper.ParseAuthorizationToken(
                    token,
                    out ReadOnlyMemory<char> type,
                    out ReadOnlyMemory<char> version,
                    out ReadOnlyMemory<char> parsedToken);

                if (shouldParse)
                {
                    Assert.AreEqual(tokenType, type.ToString());
                    Assert.AreEqual("1.0", version.ToString());
                    Assert.AreEqual(expectedParsedToken, parsedToken.ToString());
                }
                else
                {
                    Assert.Fail($"Parsing token of type [{tokenType}] and length [{length}] should have failed.");
                }
            }
            catch (Exception exception)
            {
                if (shouldParse)
                {
                    Assert.Fail($"Parsing token of type [{tokenType}] and length [{length}] should have succeeded.\n{exception}");
                }

                Assert.AreEqual(typeof(UnauthorizedException), exception.GetType());
                StringAssert.Contains(exception.Message, RMResources.InvalidAuthHeaderFormat);
            }
        }

        private string GenerateSampleToken(string tokenType, int length, out string tokenValue)
        {
            string tokenPrefix = $"type%3d{tokenType}%26ver%3d1.0%26sig%3d";
            tokenValue = new string('a', length - tokenPrefix.Length);
            return tokenPrefix + tokenValue;
        }
    }
}