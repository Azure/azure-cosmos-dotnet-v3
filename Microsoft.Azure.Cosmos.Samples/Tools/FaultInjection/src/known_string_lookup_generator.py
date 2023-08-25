def variable_name(str):
    return str[0].upper() + str[1:]

strings = [
    "documentLoadTimeInMs",
    "writeOutputTimeInMs",
    "indexUtilizationRatio",
    "indexLookupTimeInMs",
    "queryLogicalPlanBuildTimeInMs",
    "outputDocumentCount",
    "outputDocumentSize",
    "queryPhysicalPlanBuildTimeInMs",
    "queryCompileTimeInMs",
    "queryOptimizationTimeInMs",
    "retrievedDocumentCount",
    "retrievedDocumentSize",
    "systemFunctionExecuteTimeInMs",
    "totalExecutionTimeInMs",
    "userFunctionExecuteTimeInMs",
    "VMExecutionTimeInMs",
]

token_name = "BackendMetrics"

print(strings)

d = {}
for string in strings:
    d.setdefault(len(string), []).append(string)

print(d)
print(f"public static class {token_name}TokenLookupTable")
print("{")
print()
print("\tpublic enum TokenType")
print("\t{")
for str in strings:
    print(f"\t\t{variable_name(str)},")
print("\t}")
print()
print("\tpublic static class TokenBuffers")
print("\t{")
for str in strings:
    print(f"\t\tpublic static readonly ReadOnlyMemory<byte> {variable_name(str)} = Encoding.UTF8.GetBytes(\"{str}\");")
print("\t}")
print()
print("\tprivate static (TokenType? tokenType, ReadOnlyMemory<byte> tokenBuffer) GetTokenType(ReadOnlySpan<byte> buffer)")
print("\t{")
print("\t\tswitch(buffer.Length)")
print("\t\t{")
for key in d:
    print(f"\t\t\tcase {key}:")
    print(f"\t\t\t\treturn GetTokenTypeLength{key}(buffer);")
print("\t\t}")
print("\t\treturn (default, default);")
print("\t}")
print()
for key in d:
    print(f"\tprivate static (TokenType? tokenType, ReadOnlyMemory<byte> tokenBuffer) GetTokenTypeLength{key}(ReadOnlySpan<byte> buffer)")
    print("\t{")
    for str in d[key]:
        print(f"\t\tif (buffer.SequenceEqual(TokenBuffers.{variable_name(str)}.Span))")
        print("\t\t{")
        print(f"\t\t\treturn (TokenType.{variable_name(str)}, TokenBuffers.{variable_name(str)});")
        print("\t\t}")
        print()
    print("\t\treturn (default, default);")
    print("\t}")
    print()
print("}")
