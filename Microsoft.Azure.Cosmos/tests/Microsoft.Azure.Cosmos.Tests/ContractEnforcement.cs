using DiffMatchPatch;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.Cosmos.Tests
{
    [TestClass]
    public class ContractEnforcement
    {
        private const string BaselinePath = "DotNetSDKAPI.json";
        private const string BreakingChangesPath = "DotNetSDKAPIChanges.json";

        [TestMethod]
        public void ContractChanges()
        {
            Tuple<string, string> result = ContractEnforcement.CheckBreakingChanges("Microsoft.Azure.Cosmos.Client", BaselinePath, BreakingChangesPath);
            string baselineJson = result.Item1;
            string localJson = result.Item2;

            diff_match_patch dmp = new diff_match_patch();
            dmp.Diff_Timeout = 0;
            List<Diff> diffs = dmp.diff_main(localJson, baselineJson);

            Assert.IsTrue(
                baselineJson == localJson,
                $@"Public API has changed. If this is expected, then refresh {BaselinePath} with {Environment.NewLine} Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Tests/testbaseline.cmd /update after this test is run locally. To see the differences run testbaselines.cmd /diff. Diff = {string.Join(string.Empty, diffs)}"
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
                !x.AttributeType.Name.Contains("DebuggerStepThroughAttribute")
            );
        }

        private static TypeTree BuildTypeTree(TypeTree root, Type[] types)
        {
            IEnumerable<Type> subclassTypes = types.Where((type) => type.IsSubclassOf(root.Type)).OrderBy(o => o.FullName);
            foreach (Type subclassType in subclassTypes)
            {
                root.Subclasses[subclassType.Name] = (ContractEnforcement.BuildTypeTree(new TypeTree(subclassType), types));
            }

            IEnumerable<KeyValuePair<string, MemberInfo>> memberInfos =
                root.Type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                    .Select(memberInfo => new KeyValuePair<string, MemberInfo>($"{memberInfo.ToString()}{string.Join("-", ContractEnforcement.RemoveDebugSpecificAttributes(memberInfo.CustomAttributes))}", memberInfo))
                    .OrderBy(o => o.Key);
            foreach (KeyValuePair<string, MemberInfo> memberInfo in memberInfos)
            {
                List<string> attributes = ContractEnforcement.RemoveDebugSpecificAttributes(memberInfo.Value.CustomAttributes)
                        .Select((customAttribute) => customAttribute.AttributeType.Name)
                        .ToList();
                attributes.Sort();

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
                root.NestedTypes[nestedType.Name] = (ContractEnforcement.BuildTypeTree(new TypeTree(nestedType), types));
            }

            return root;
        }

        private static Tuple<string, string> CheckBreakingChanges(string dllName, string baselinePath, string breakingChangesPath)
        {
            TypeTree locally = new TypeTree(typeof(object));
            ContractEnforcement.BuildTypeTree(locally, ContractEnforcement.GetAssemblyLocally(dllName).GetExportedTypes());

            TypeTree baseline = JsonConvert.DeserializeObject<TypeTree>(File.ReadAllText(baselinePath));

            string localJson = JsonConvert.SerializeObject(locally, Formatting.Indented);
            File.WriteAllText($"{breakingChangesPath}", localJson, Encoding.UTF8);
            string baselineJson = JsonConvert.SerializeObject(baseline, Formatting.Indented);

            return new Tuple<string, string>(baselineJson, localJson);
        }
    }
}
