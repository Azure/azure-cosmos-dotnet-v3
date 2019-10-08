namespace Microsoft.Azure.Cosmos.Tests
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    public static class TextFileConcatenation
    {
        public static string ReadMultipartFile(string path)
        {
            FileAttributes attr = File.GetAttributes(path);
            if (!attr.HasFlag(FileAttributes.Directory))
            {
                return File.ReadAllText(path);
            }

            DirectoryInfo dir = new DirectoryInfo(path);
            IEnumerable<FileInfo> files = dir.GetFiles().OrderBy(x => x.Name);

            string text = string.Empty;

            foreach(FileInfo file in files)
            {
                text = text + File.ReadAllText(file.FullName);
            }

            return text;
        }
    }
}
