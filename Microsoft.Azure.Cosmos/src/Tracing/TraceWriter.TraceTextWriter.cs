// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.Cosmos.Query.Core.Metrics;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using static Microsoft.Azure.Cosmos.Tracing.TraceData.ClientSideRequestStatisticsTraceDatum;

    internal static partial class TraceWriter
    {
        private static class TraceTextWriter
        {
            private const string space = "  ";

            private static readonly Dictionary<AsciiType, AsciiTreeCharacters> asciiTreeCharactersMap = new Dictionary<AsciiType, AsciiTreeCharacters>()
            {
                {
                    AsciiType.Default,
                    new AsciiTreeCharacters(
                        blank: ' ',
                        child: '├',
                        dash: '─',
                        last: '└',
                        parent: '│',
                        root: '.')
                },
                {
                    AsciiType.DoubleLine,
                    new AsciiTreeCharacters(
                        blank: ' ',
                        child: '╠',
                        dash: '═',
                        last: '╚',
                        parent: '║',
                        root: '╗')
                },
                {
                    AsciiType.Classic,
                    new AsciiTreeCharacters(
                        blank: ' ',
                        child: '|',
                        dash: '-',
                        last: '+',
                        parent: '|',
                        root: '+')
                },
                {
                    AsciiType.ClassicRounded,
                    new AsciiTreeCharacters(
                        blank: ' ',
                        child: '|',
                        dash: '-',
                        last: '`',
                        parent: '|',
                        root: '+')
                },
                {
                    AsciiType.ExclamationMarks,
                    new AsciiTreeCharacters(
                        blank: ' ',
                        child: '#',
                        dash: '=',
                        last: '*',
                        parent: '!',
                        root: '#')
                },
            };
            private static readonly Dictionary<AsciiType, AsciiTreeIndents> asciiTreeIndentsMap = new Dictionary<AsciiType, AsciiTreeIndents>()
            {
                { AsciiType.Default, AsciiTreeIndents.Create(asciiTreeCharactersMap[AsciiType.Default]) },
                { AsciiType.DoubleLine, AsciiTreeIndents.Create(asciiTreeCharactersMap[AsciiType.DoubleLine]) },
                { AsciiType.Classic, AsciiTreeIndents.Create(asciiTreeCharactersMap[AsciiType.Classic]) },
                { AsciiType.ClassicRounded, AsciiTreeIndents.Create(asciiTreeCharactersMap[AsciiType.ClassicRounded]) },
                { AsciiType.ExclamationMarks, AsciiTreeIndents.Create(asciiTreeCharactersMap[AsciiType.ExclamationMarks]) },
            };

            private static readonly string[] newLines = new string[] { Environment.NewLine };
            private static readonly char[] newLineCharacters = Environment.NewLine.ToCharArray();

            private static class AddressResolutionStatisticsTextTable
            {
                private static class Headers
                {
                    public const string StartTime = "Start Time (utc)";
                    public const string EndTime = "End Time (utc)";
                    public const string Endpoint = "Endpoint";
                }

                private static class HeaderLengths
                {
                    public static readonly int StartTime = Math.Max(Headers.StartTime.Length, DateTime.MaxValue.ToString("hh:mm:ss:fff", CultureInfo.InvariantCulture).Length);
                    public static readonly int EndTime = Math.Max(Headers.EndTime.Length, DateTime.MaxValue.ToString("hh:mm:ss:fff", CultureInfo.InvariantCulture).Length);
                    public static readonly int Endpoint = 80 - (StartTime + EndTime);
                }

                private static readonly TextTable.Column[] Columns = new TextTable.Column[]
                {
                    new TextTable.Column(Headers.StartTime, HeaderLengths.StartTime),
                    new TextTable.Column(Headers.EndTime, HeaderLengths.EndTime),
                    new TextTable.Column(Headers.Endpoint, HeaderLengths.Endpoint),
                };

                public static readonly TextTable Singleton = new TextTable(Columns);
            }

            public static void WriteTrace(
                TextWriter writer,
                ITrace trace,
                TraceLevel level = TraceLevel.Verbose,
                AsciiType asciiType = AsciiType.Default)
            {
                if (writer == null)
                {
                    throw new ArgumentNullException(nameof(writer));
                }

                if (trace == null)
                {
                    throw new ArgumentNullException(nameof(trace));
                }

                if ((int)trace.Level > (int)level)
                {
                    return;
                }

                AsciiTreeCharacters asciiTreeCharacter = asciiTreeCharactersMap[asciiType];
                AsciiTreeIndents asciiTreeIndents = asciiTreeIndentsMap[asciiType];

                writer.WriteLine(asciiTreeCharacter.Root);
                WriteTraceRecursive(writer, trace, level, asciiTreeIndents, isLastChild: true);
            }

            private static void WriteTraceRecursive(
                TextWriter writer,
                ITrace trace,
                TraceLevel level,
                AsciiTreeIndents asciiTreeIndents,
                bool isLastChild)
            {
                ITrace parent = trace.Parent;
                Stack<string> indentStack = new Stack<string>();
                while (parent != null)
                {
                    bool parentIsLastChild = (parent.Parent == null) || parent.Equals(parent.Parent.Children.Last());
                    if (parentIsLastChild)
                    {
                        indentStack.Push(asciiTreeIndents.Blank);
                    }
                    else
                    {
                        indentStack.Push(asciiTreeIndents.Parent);
                    }

                    parent = parent.Parent;
                }

                WriteIndents(writer, indentStack, asciiTreeIndents, isLastChild);

                writer.Write(trace.Name);
                writer.Write('(');
                writer.Write(trace.Id);
                writer.Write(')');
                writer.Write(space);

                writer.Write(trace.Component);
                writer.Write('-');
                writer.Write("Component");
                writer.Write(space);

                writer.Write(trace.CallerInfo.MemberName);

                writer.Write('@');
                writer.Write(GetFileNameFromPath(trace.CallerInfo.FilePath));
                writer.Write(':');
                writer.Write(trace.CallerInfo.LineNumber);
                writer.Write(space);

                writer.Write(trace.StartTime.ToString("hh:mm:ss:fff", CultureInfo.InvariantCulture));
                writer.Write(space);

                writer.Write(trace.Duration.TotalMilliseconds.ToString("0.00"));
                writer.Write(" milliseconds");
                writer.Write(space);

                writer.WriteLine();

                if (trace.Data.Count > 0)
                {
                    bool isLeaf = trace.Children.Count == 0;

                    WriteInfoIndents(writer, indentStack, asciiTreeIndents, isLastChild: isLastChild, isLeaf: isLeaf);
                    writer.WriteLine('(');

                    foreach (KeyValuePair<string, object> kvp in trace.Data)
                    {
                        string key = kvp.Key;
                        object value = kvp.Value;

                        WriteInfoIndents(writer, indentStack, asciiTreeIndents, isLastChild: isLastChild, isLeaf: isLeaf);
                        writer.Write(asciiTreeIndents.Blank);
                        writer.Write('[');
                        writer.Write(key);
                        writer.Write(']');
                        writer.WriteLine();

                        string traceDatumToString;
                        if (value is TraceDatum traceDatum)
                        {
                            TraceDatumTextWriter traceDatumTextWriter = new TraceDatumTextWriter();
                            traceDatum.Accept(traceDatumTextWriter);

                            traceDatumToString = traceDatumTextWriter.ToString();
                        }
                        else
                        {
                            traceDatumToString = value.ToString();
                        }

                        string[] infoLines = traceDatumToString
                            .TrimEnd(newLineCharacters)
                            .Split(newLines, StringSplitOptions.None);
                        foreach (string infoLine in infoLines)
                        {
                            WriteInfoIndents(writer, indentStack, asciiTreeIndents, isLastChild: isLastChild, isLeaf: isLeaf);
                            writer.Write(asciiTreeIndents.Blank);
                            writer.WriteLine(infoLine);
                        }
                    }

                    WriteInfoIndents(writer, indentStack, asciiTreeIndents, isLastChild: isLastChild, isLeaf: isLeaf);
                    writer.WriteLine(')');
                }

                for (int i = 0; i < trace.Children.Count - 1; i++)
                {
                    ITrace child = trace.Children[i];
                    WriteTraceRecursive(writer, child, level, asciiTreeIndents, isLastChild: false);
                }

                if (trace.Children.Count != 0)
                {
                    ITrace child = trace.Children[trace.Children.Count - 1];
                    WriteTraceRecursive(writer, child, level, asciiTreeIndents, isLastChild: true);
                }
            }

            private static void WriteIndents(
                TextWriter writer,
                Stack<string> indentStack,
                AsciiTreeIndents asciiTreeIndents,
                bool isLastChild)
            {
                foreach (string indent in indentStack)
                {
                    writer.Write(indent);
                }

                if (isLastChild)
                {
                    writer.Write(asciiTreeIndents.Last);
                }
                else
                {
                    writer.Write(asciiTreeIndents.Child);
                }
            }

            private static void WriteInfoIndents(
                TextWriter writer,
                Stack<string> indentStack,
                AsciiTreeIndents asciiTreeIndents,
                bool isLastChild,
                bool isLeaf)
            {
                foreach (string indent in indentStack)
                {
                    writer.Write(indent);
                }

                if (isLastChild)
                {
                    writer.Write(asciiTreeIndents.Blank);
                }
                else
                {
                    writer.Write(asciiTreeIndents.Parent);
                }

                if (isLeaf)
                {
                    writer.Write(asciiTreeIndents.Blank);
                }
                else
                {
                    writer.Write(asciiTreeIndents.Parent);
                }
            }

            private sealed class TraceDatumTextWriter : ITraceDatumVisitor
            {
                private string toStringValue;

                public TraceDatumTextWriter()
                {
                }

                public void Visit(QueryMetricsTraceDatum queryMetricsTraceDatum)
                {
                    this.toStringValue = queryMetricsTraceDatum.QueryMetrics.ToString();
                }

                public void Visit(PointOperationStatisticsTraceDatum pointOperationStatisticsTraceDatum)
                {
                    StringBuilder stringBuilder = new StringBuilder();
                    stringBuilder.AppendLine($"Activity ID: {pointOperationStatisticsTraceDatum.ActivityId ?? "<null>"}");
                    stringBuilder.AppendLine($"Status Code: {pointOperationStatisticsTraceDatum.StatusCode}/{pointOperationStatisticsTraceDatum.SubStatusCode}");
                    stringBuilder.AppendLine($"Response Time: {pointOperationStatisticsTraceDatum.ResponseTimeUtc.ToString("hh:mm:ss:fff", CultureInfo.InvariantCulture)}");
                    stringBuilder.AppendLine($"Request Charge: {pointOperationStatisticsTraceDatum.RequestCharge}");
                    stringBuilder.AppendLine($"Request URI: {pointOperationStatisticsTraceDatum.RequestUri ?? "<null>"}");
                    stringBuilder.AppendLine($"Session Tokens: {pointOperationStatisticsTraceDatum.RequestSessionToken ?? "<null>"} / {pointOperationStatisticsTraceDatum.ResponseSessionToken ?? "<null>"}");
                    if (pointOperationStatisticsTraceDatum.ErrorMessage != null)
                    {
                        stringBuilder.AppendLine($"Error Message: {pointOperationStatisticsTraceDatum.ErrorMessage}");
                    }

                    this.toStringValue = stringBuilder.ToString();
                }

                public void Visit(ClientSideRequestStatisticsTraceDatum clientSideRequestStatisticsTraceDatum)
                {
                    StringBuilder stringBuilder = new StringBuilder();
                    stringBuilder.AppendLine($"Start Time: {clientSideRequestStatisticsTraceDatum.RequestStartTimeUtc.ToString("hh:mm:ss:fff", CultureInfo.InvariantCulture)}");
                    if (clientSideRequestStatisticsTraceDatum.RequestEndTimeUtc.HasValue)
                    {
                        stringBuilder.AppendLine($"End Time: {clientSideRequestStatisticsTraceDatum.RequestEndTimeUtc.Value.ToString("hh:mm:ss:fff", CultureInfo.InvariantCulture)}");
                    }

                    stringBuilder.AppendLine("Contacted Replicas");
                    Dictionary<Uri, int> uriAndCounts = new Dictionary<Uri, int>();
                    foreach (Uri uri in clientSideRequestStatisticsTraceDatum.ContactedReplicas)
                    {
                        if (uri == null)
                        {
                            continue;
                        }

                        if (!uriAndCounts.TryGetValue(uri, out int count))
                        {
                            count = 0;
                        }

                        uriAndCounts[uri] = ++count;
                    }

                    foreach (KeyValuePair<Uri, int> uriAndCount in uriAndCounts)
                    {
                        stringBuilder.AppendLine($"{space}{uriAndCount.Key?.ToString() ?? "<null>"}: {uriAndCount.Value}");
                    }

                    stringBuilder.AppendLine("Failed to Contact Replicas");
                    foreach (Uri failedToContactReplica in clientSideRequestStatisticsTraceDatum.FailedReplicas)
                    {
                        stringBuilder.AppendLine($"{space}{failedToContactReplica?.ToString() ?? "<null>"}");
                    }

                    stringBuilder.AppendLine("Regions Contacted");
                    foreach (Uri regionContacted in clientSideRequestStatisticsTraceDatum.ContactedReplicas)
                    {
                        stringBuilder.AppendLine($"{space}{regionContacted?.ToString() ?? "<null>"}");
                    }

                    stringBuilder.AppendLine("Address Resolution Statistics");
                    stringBuilder.AppendLine(AddressResolutionStatisticsTextTable.Singleton.TopLine);
                    stringBuilder.AppendLine(AddressResolutionStatisticsTextTable.Singleton.Header);
                    stringBuilder.AppendLine(AddressResolutionStatisticsTextTable.Singleton.MiddleLine);
                    foreach (AddressResolutionStatistics stat in clientSideRequestStatisticsTraceDatum.EndpointToAddressResolutionStatistics.Values)
                    {
                        string row = AddressResolutionStatisticsTextTable.Singleton.GetRow(
                            stat.StartTime.ToString("hh:mm:ss:fff", CultureInfo.InvariantCulture),
                            stat.EndTime.HasValue ? stat.EndTime.Value.ToString("hh:mm:ss:fff", CultureInfo.InvariantCulture) : "NO END TIME",
                            stat.TargetEndpoint);
                        stringBuilder.AppendLine(row);
                    }

                    stringBuilder.AppendLine(AddressResolutionStatisticsTextTable.Singleton.BottomLine);

                    stringBuilder.AppendLine("Store Response Statistics");
                    foreach (StoreResponseStatistics stat in clientSideRequestStatisticsTraceDatum.StoreResponseStatisticsList)
                    {
                        if (stat.RequestStartTime.HasValue)
                        {
                            stringBuilder.AppendLine($"{space}Start Time: {stat.RequestStartTime.Value.ToString("hh:mm:ss:fff", CultureInfo.InvariantCulture)}");
                        }
                        else
                        {
                            stringBuilder.AppendLine("{space}Start Time Not Found");
                        }

                        stringBuilder.AppendLine($"{space}End Time: {stat.RequestResponseTime.ToString("hh:mm:ss:fff", CultureInfo.InvariantCulture)}");

                        stringBuilder.AppendLine($"{space}Resource Type: {stat.RequestResourceType}");
                        stringBuilder.AppendLine($"{space}Operation Type: {stat.RequestOperationType}");

                        if (stat.StoreResult != null)
                        {
                            stringBuilder.AppendLine($"{space}Store Result");
                            stringBuilder.AppendLine($"{space}{space}Activity Id: {stat.StoreResult.ActivityId ?? "<null>"}");
                            stringBuilder.AppendLine($"{space}{space}Store Physical Address: {stat.StoreResult.StorePhysicalAddress?.ToString() ?? "<null>"}");
                            stringBuilder.AppendLine($"{space}{space}Status Code: {stat.StoreResult.StatusCode}/{stat.StoreResult.SubStatusCode}");
                            stringBuilder.AppendLine($"{space}{space}Is Valid: {stat.StoreResult.IsValid}");
                            stringBuilder.AppendLine($"{space}{space}LSN Info");
                            stringBuilder.AppendLine($"{space}{space}{space}LSN: {stat.StoreResult.LSN}");
                            stringBuilder.AppendLine($"{space}{space}{space}Item LSN: {stat.StoreResult.ItemLSN}");
                            stringBuilder.AppendLine($"{space}{space}{space}Global LSN: {stat.StoreResult.GlobalCommittedLSN}");
                            stringBuilder.AppendLine($"{space}{space}{space}Quorum Acked LSN: {stat.StoreResult.QuorumAckedLSN}");
                            stringBuilder.AppendLine($"{space}{space}{space}Using LSN: {stat.StoreResult.UsingLocalLSN}");
                            stringBuilder.AppendLine($"{space}{space}Session Token: {stat.StoreResult.SessionToken?.ConvertToString() ?? "<null>"}");
                            stringBuilder.AppendLine($"{space}{space}Quorum Info");
                            stringBuilder.AppendLine($"{space}{space}{space}Current Replica Set Size: {stat.StoreResult.CurrentReplicaSetSize}");
                            stringBuilder.AppendLine($"{space}{space}{space}Current Write Quorum: {stat.StoreResult.CurrentWriteQuorum}");
                            stringBuilder.AppendLine($"{space}{space}Is Client CPU Overloaded: {stat.StoreResult.IsClientCpuOverloaded}");
                            stringBuilder.AppendLine($"{space}{space}Exception");
                            try
                            {
                                stringBuilder.AppendLine($"{space}{space}{stat.StoreResult.GetException()}");
                            }
                            catch (Exception)
                            {
                                // This method throws if there is no exception.
                            }
                        }
                    }

                    if (clientSideRequestStatisticsTraceDatum.HttpResponseStatisticsList.Count > 0)
                    {
                        stringBuilder.AppendLine("Http Response Statistics");
                        foreach (HttpResponseStatistics stat in clientSideRequestStatisticsTraceDatum.HttpResponseStatisticsList)
                        {
                            stringBuilder.AppendLine($"{space}HttpResponse");
                            stringBuilder.AppendLine($"{space}{space}RequestStartTime: {stat.RequestStartTime.ToString("o", CultureInfo.InvariantCulture)}");
                            stringBuilder.AppendLine($"{space}{space}RequestEndTime: {stat.RequestEndTime.ToString("o", CultureInfo.InvariantCulture)}");
                            stringBuilder.AppendLine($"{space}{space}RequestUri: {stat.RequestUri}");
                            stringBuilder.AppendLine($"{space}{space}ResourceType: {stat.ResourceType}");
                            stringBuilder.AppendLine($"{space}{space}HttpMethod: {stat.HttpMethod}");

                            if (stat.Exception != null)
                            {
                                stringBuilder.AppendLine($"{space}{space}ExceptionType: {stat.Exception.GetType()}");
                                stringBuilder.AppendLine($"{space}{space}ExceptionMessage: {stat.Exception.Message}");
                            }

                            if (stat.HttpResponseMessage != null)
                            {
                                stringBuilder.AppendLine($"{space}{space}StatusCode: {stat.HttpResponseMessage.StatusCode}");
                                if (!stat.HttpResponseMessage.IsSuccessStatusCode)
                                {
                                    stringBuilder.AppendLine($"{space}{space}ReasonPhrase: {stat.HttpResponseMessage.ReasonPhrase}");
                                }
                            }
                        }
                    }

                    this.toStringValue = stringBuilder.ToString();
                }

                public void Visit(CpuHistoryTraceDatum cpuHistoryTraceDatum)
                {
                    StringBuilder stringBuilder = new StringBuilder();
                    stringBuilder.AppendLine(cpuHistoryTraceDatum.Value.ToString());

                    // TODO: Expose the raw data so we can custom format the string.

                    this.toStringValue = stringBuilder.ToString();
                }

                public void Visit(ClientConfigurationTraceDatum clientConfigurationTraceDatum)
                {
                    StringBuilder stringBuilder = new StringBuilder();
                    stringBuilder.AppendLine("Client Configuration");
                    stringBuilder.AppendLine($"Client Created Time: {clientConfigurationTraceDatum.ClientCreatedDateTimeUtc.ToString("o", CultureInfo.InvariantCulture)}");
                    stringBuilder.AppendLine($"Number Of Clients Created: {CosmosClient.numberOfClientsCreated}");
                    stringBuilder.AppendLine($"User Agent: {clientConfigurationTraceDatum.UserAgentContainer.UserAgent}");
                    stringBuilder.AppendLine("Connection Config:");
                    stringBuilder.AppendLine($"{space}'gw': {clientConfigurationTraceDatum.GatewayConnectionConfig}");
                    stringBuilder.AppendLine($"{space}'rntbd': {clientConfigurationTraceDatum.RntbdConnectionConfig}");
                    stringBuilder.AppendLine($"{space}'other': {clientConfigurationTraceDatum.OtherConnectionConfig}");
                    stringBuilder.AppendLine($"Consistency Config: {clientConfigurationTraceDatum.ConsistencyConfig}");

                    this.toStringValue = stringBuilder.ToString();
                }

                public override string ToString()
                {
                    return this.toStringValue;
                }
            }

            /// <summary>
            /// Character set to generate an Ascii Tree (https://marketplace.visualstudio.com/items?itemName=aprilandjan.ascii-tree-generator)
            /// </summary>
            private readonly struct AsciiTreeCharacters
            {
                public AsciiTreeCharacters(char blank, char child, char dash, char last, char parent, char root)
                {
                    this.Blank = blank;
                    this.Child = child;
                    this.Dash = dash;
                    this.Last = last;
                    this.Parent = parent;
                    this.Root = root;
                }

                /// <summary>
                /// For blanks / spaces
                /// </summary>
                public char Blank { get; }

                /// <summary>
                /// For intermediate child elements
                /// </summary>
                public char Child { get; }

                /// <summary>
                /// For horizontal dashes
                /// </summary>
                public char Dash { get; }

                /// <summary>
                /// For the last element of a path
                /// </summary>
                public char Last { get; }

                /// <summary>
                /// For vertical parent elements
                /// </summary>
                public char Parent { get; }

                /// <summary>
                /// For the root element (on top)
                /// </summary>
                public char Root { get; }
            }

            private readonly struct AsciiTreeIndents
            {
                private AsciiTreeIndents(string child, string parent, string last, string blank)
                {
                    this.Child = child;
                    this.Parent = parent;
                    this.Last = last;
                    this.Blank = blank;
                }

                public string Child { get; }

                public string Parent { get; }

                public string Last { get; }

                public string Blank { get; }

                public static AsciiTreeIndents Create(AsciiTreeCharacters asciiTreeCharacters)
                {
                    return new AsciiTreeIndents(
                        child: new string(
                            new char[]
                            {
                                asciiTreeCharacters.Child,
                                asciiTreeCharacters.Dash,
                                asciiTreeCharacters.Dash,
                                asciiTreeCharacters.Blank
                            }),
                        parent: new string(
                            new char[]
                            {
                                asciiTreeCharacters.Parent,
                                asciiTreeCharacters.Blank,
                                asciiTreeCharacters.Blank,
                                asciiTreeCharacters.Blank
                            }),
                        last: new string(
                            new char[]
                            {
                                asciiTreeCharacters.Last,
                                asciiTreeCharacters.Dash,
                                asciiTreeCharacters.Dash,
                                asciiTreeCharacters.Blank
                            }),
                        blank: new string(
                            new char[]
                            {
                                asciiTreeCharacters.Blank,
                                asciiTreeCharacters.Blank,
                                asciiTreeCharacters.Blank,
                                asciiTreeCharacters.Blank
                            }));
                }
            }
        }
    }
}
