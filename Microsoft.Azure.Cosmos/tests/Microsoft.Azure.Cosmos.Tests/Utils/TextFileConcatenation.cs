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
            if (!path.EndsWith(".json"))
            {
                path += ".json";
            }

            try
            {
                // Try as a file
                FileAttributes attr = File.GetAttributes(path);
                if (!attr.HasFlag(FileAttributes.Directory))
                {
                    return File.ReadAllText(path);
                }
            }
            catch (FileNotFoundException)
            {
                // try as a directory
                path = path.Replace(".json", string.Empty);
            }

            DirectoryInfo dir = new DirectoryInfo(path);
            IEnumerable<FileInfo> files = dir.GetFiles().OrderBy(x => x.Name);

            string text;
            using (MemoryStream dest = new MemoryStream())
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