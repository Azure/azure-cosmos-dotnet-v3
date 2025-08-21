namespace AOTSample
{
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using static AOTSample.Program;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(ToDoItem))]
public partial class JsonContext : JsonSerializerContext { }
}
