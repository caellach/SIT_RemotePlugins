using Newtonsoft.Json;

namespace GenerateKnownHashes
{
    internal class Program
    {
        private static List<string> repoList = new List<string> { 
            "stayintarkov/SIT-Mod-Ports",
            "stayintarkov/StayInTarkov.Client"
        };

        static void Main(string[] args)
        {
            string clientPath = GetClientPath();
            string key = GetCredentials();
            string knownHashesPath = Path.GetFullPath(Path.Combine(clientPath, "KnownFileHashes.json"));
            Dictionary<string, HashData> hashData = LoadExistingKnownFileHashes(knownHashesPath);

            bool hashDataChanged = false;
            for (int i = 0; i < repoList.Count; i++)
            {
                string[] repoParts = repoList[i].Split('/');
                string repoOwner = repoParts[0];
                string repoName = repoParts[1];

                GithubRepo repo = new GithubRepo(key, repoOwner, repoName);
                bool changedData = repo.AppendReleasesHashData(hashData);
                if (changedData)
                {
                    hashDataChanged = true;
                }
            }

            if (hashDataChanged)
            {
                // write to file
                var sortedDict = hashData.OrderBy(x => x.Value.Name).ToList();
                string json = JsonConvert.SerializeObject(hashData, Formatting.Indented);

                // write to the client project
                File.WriteAllText(knownHashesPath, json);

                Console.WriteLine("Wrote known file hashes to " + knownHashesPath);
            }
            else
            {
                Console.WriteLine("No changes to known file hashes");
            }

            Console.WriteLine("Done");
        }

        private static string GetClientPath()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (args != null)
            {
                // find -c then the value is the next arg
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] == "-c" && i + 1 < args.Length)
                    {
                        return Path.GetFullPath(args[i + 1]);
                    }
                }
            }

            Console.WriteLine("Failed to get client path: -c");
            throw new ArgumentNullException(nameof(args));
        }

        private static string GetCredentials()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (args != null)
            {
                foreach (string arg in args)
                {
                    if (arg.StartsWith("-key="))
                    {
                        string key = arg.Replace("-key=", string.Empty);
                        return key;
                    }
                }
            }
            Console.WriteLine("Failed to get credentials: -key");
            throw new ArgumentNullException(nameof(args));
        }

        private static Dictionary<string, HashData> LoadExistingKnownFileHashes(string path)
        {
            // read file in this directory KnownFileHashes.json
            if (!File.Exists(path))
            {
                return new Dictionary<string, HashData>();
            }

            try
            {
                string json = File.ReadAllText(path);
                var deserializedJson = JsonConvert.DeserializeObject<Dictionary<string, HashData>>(json);
                if (deserializedJson == null)
                {
                    return new Dictionary<string, HashData>();
                }
                return deserializedJson;
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to load existing known file hashes: " + e.Message);
                return new Dictionary<string, HashData>();
            }
        }
    }
}
