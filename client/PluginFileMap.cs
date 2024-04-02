using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RemotePlugins
{
    internal class PluginFileMap
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
    }
}
