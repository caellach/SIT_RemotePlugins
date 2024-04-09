using System.Collections.Generic;

namespace RemotePlugins
{
    internal class PluginFileMap : RemoteObject
    {
        public class PluginFile
        {
            public string Name { get; set; }
            public string Hash { get; set; }
            public int Size { get; set; }
        }

        public List<PluginFile> Files { get; set; } = new List<PluginFile>();
        public string FilesHash { get; set; }
        public PluginFile Zip { get; set; }


        public PluginFileMap CleanFileNames()
        {
            // prevent remote directory escape
            foreach (PluginFile file in Files)
            {
                file.Name = file.Name.Replace("..", string.Empty);
            }
            return this;
        }
    }
}
