namespace Microsoft.Azure.Cosmos.Tests
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;

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

            
            using(MemoryStream dest = new MemoryStream())
            {
                foreach (FileInfo file in files)
                {

                    FileStream fileStream = File.OpenRead(file.FullName);
                    fileStream.CopyTo(dest);
                }

                text = Encoding.UTF8.GetString(dest.ToArray());
            }

            return text;
        }
    }
}
