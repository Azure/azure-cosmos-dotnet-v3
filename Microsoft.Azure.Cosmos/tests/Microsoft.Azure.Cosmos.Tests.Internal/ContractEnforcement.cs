namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    [TestCategory("Windows")]
    [TestClass]
    public class ContractEnforcement
    {
        private const string BaselinePath = "DotNetSDKAPI.json";
        private const string BreakingChangesPath = "DotNetSDKAPIChanges.json";

        private static readonly InvariantComparer invariantComparer = new InvariantComparer();

        [TestMethod]
        public void InternalContractChanges()
        {
            System.Diagnostics.Process process = System.Diagnostics.Process.Start("CMD.exe", $"/c dotnet build ../../../../../src/Microsoft.Azure.Cosmos.csproj -o . /p:IncludeInternals=true");
            process.WaitForExit();
            Assert.IsFalse(
                ContractEnforcement.DoesContractContainBreakingChanges("Microsoft.Azure.Cosmos.Client", BaselinePath, BreakingChangesPath),
                $@"Internal API has changed. If this is expected, then refresh {BaselinePath} with {Environment.NewLine} Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Tests.Internal/testbaseline.cmd /update after this test is run locally. To see the differences run testbaselines.cmd /diff"
            );
        }

        private static Assembly GetAssemblyLocally(string name)
        {
            Assembly.Load(name);
            Assembly[] loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            return loadedAssemblies
                .Where((candidate) => candidate.FullName.Contains(name + ","))
                .FirstOrDefault();
        }

        private sealed class MemberMetadata
        {
            [JsonConverter(typeof(StringEnumConverter))]
            public MemberTypes Type { get; }
            public List<string> Attributes { get; }
            public string MethodInfo { get; }
            public MemberMetadata(MemberTypes type, List<string> attributes, string methodInfo)
            {
                this.Type = type;
                this.Attributes = attributes;
                this.MethodInfo = methodInfo;
            }
        }

        private sealed class TypeTree
        {
            [JsonIgnore]
            public Type Type { get; }
            public Dictionary<string, TypeTree> Subclasses { get; } = new Dictionary<string, TypeTree>();
            public Dictionary<string, MemberMetadata> Members { get; } = new Dictionary<string, MemberMetadata>();
            public Dictionary<string, TypeTree> NestedTypes { get; } = new Dictionary<string, TypeTree>();

            public TypeTree(Type type)
            {
                this.Type = type;
            }
        }

        private static IEnumerable<CustomAttributeData> RemoveDebugSpecificAttributes(IEnumerable<CustomAttributeData> attributes)
        {
            return attributes.Where(x =>
                !x.AttributeType.Name.Contains("SuppressMessageAttribute") &&
                !x.AttributeType.Name.Contains("DynamicallyInvokableAttribute") &&
                !x.AttributeType.Name.Contains("NonVersionableAttribute") &&
                !x.AttributeType.Name.Contains("ReliabilityContractAttribute") &&
                !x.AttributeType.Name.Contains("NonVersionableAttribute") &&
                !x.AttributeType.Name.Contains("DebuggerStepThroughAttribute") &&
                !x.AttributeType.Name.Contains("IsReadOnlyAttribute")
            );
        }

        private static TypeTree BuildTypeTree(TypeTree root, Type[] types)
        {
            IEnumerable<Type> subclassTypes = types.Where((type) => type.IsSubclassOf(root.Type)).OrderBy(o => o.FullName, invariantComparer);
            foreach (Type subclassType in subclassTypes)
            {
                root.Subclasses[subclassType.Name] = ContractEnforcement.BuildTypeTree(new TypeTree(subclassType), types);
            }

            IEnumerable<KeyValuePair<string, MemberInfo>> memberInfos =
                root.Type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                    .Select(memberInfo => new KeyValuePair<string, MemberInfo>($"{memberInfo.ToString()}{string.Join("-", ContractEnforcement.RemoveDebugSpecificAttributes(memberInfo.CustomAttributes))}", memberInfo))
                    .OrderBy(o => o.Key, invariantComparer);
            foreach (KeyValuePair<string, MemberInfo> memberInfo in memberInfos)
            {
                List<string> attributes = ContractEnforcement.RemoveDebugSpecificAttributes(memberInfo.Value.CustomAttributes)
                        .Select((customAttribute) => customAttribute.AttributeType.Name)
                        .ToList();
                attributes.Sort(invariantComparer);

                string methodSignature = null;

                if (memberInfo.Value.MemberType == MemberTypes.Constructor | memberInfo.Value.MemberType == MemberTypes.Method | memberInfo.Value.MemberType == MemberTypes.Event)
                {
                    methodSignature = memberInfo.Value.ToString();
                }

                root.Members[
                        memberInfo.Key
                    ] = new MemberMetadata(
                    memberInfo.Value.MemberType,
                    attributes,
                    methodSignature);
            }

            foreach (Type nestedType in root.Type.GetNestedTypes().OrderBy(o => o.FullName))
            {
                root.NestedTypes[nestedType.Name] = ContractEnforcement.BuildTypeTree(new TypeTree(nestedType), types);
            }

            return root;
        }

        private static bool DoesContractContainBreakingChanges(string dllName, string baselinePath, string breakingChangesPath)
        {
            TypeTree locally = new TypeTree(typeof(object));
            ContractEnforcement.BuildTypeTree(locally, ContractEnforcement.GetAssemblyLocally(dllName).GetExportedTypes());

            TypeTree baseline = JsonConvert.DeserializeObject<TypeTree>(File.ReadAllText(baselinePath));

            string localJson = JsonConvert.SerializeObject(locally, Formatting.Indented);
            File.WriteAllText($"{breakingChangesPath}", localJson);
            string baselineJson = JsonConvert.SerializeObject(baseline, Formatting.Indented);

            System.Diagnostics.Trace.TraceWarning($"String length Expected: {baselineJson.Length};Actual:{localJson.Length}");
            if (string.Equals(localJson, baselineJson, StringComparison.InvariantCulture))
            {
                return false;
            }
            else
            {
                System.Diagnostics.Trace.TraceWarning($"Expected: {baselineJson}");
                System.Diagnostics.Trace.TraceWarning($"Actual: {localJson}");
                return true;
            }
        }

        private class InvariantComparer : IComparer<string>
        {
            public int Compare(string a, string b) => Comparer.DefaultInvariant.Compare(a, b);
        }
    }
}
