using Newtonsoft.Json;
using System.IO.Compression;
using static GenerateKnownHashes.Github;

namespace GenerateKnownHashes
{
    internal class Program
    {
        static void Main(string[] args)
        {
            string clientPath = GetClientPath();
            string knownHashesPath = Path.GetFullPath(Path.Combine(clientPath, "KnownFileHashes.json"));

            string key = GetCredentials();
            string repoOwner = "stayintarkov";
            string repoName = "SIT-Mod-Ports";
            Github github = new Github(key, repoOwner, repoName);
            List<GithubRelease> releases = github.ListReleases();
            Dictionary<string, HashData> hashData = LoadExistingKnownFileHashes(knownHashesPath);

            List<string> loadedHashVersions = new List<string>();
            foreach (KeyValuePair<string, HashData> entry in hashData)
            {
                // add the version if it's not already in the list
                if (entry.Value.ReleaseVersions != null)
                {
                    foreach (string version in entry.Value.ReleaseVersions)
                    {
                        if (!loadedHashVersions.Contains(version))
                        {
                            loadedHashVersions.Add(version);
                        }
                    }
                }
            }

            bool hashDataChanged = false;
            if (releases != null)
            {
                foreach (GithubRelease release in releases)
                {
                    string releaseVersion = release.TagName ?? release.Name ?? "BAD_VERSION";

                    if (loadedHashVersions.Contains(releaseVersion))
                    {
                        Console.WriteLine("Skipping release " + releaseVersion);
                        continue;
                    }

                    Console.WriteLine(release.TagName);
                    Console.WriteLine(release.Name);

                    if (release.Assets != null)
                    {
                        foreach (GithubAssets asset in release.Assets)
                        {
                            if (asset.ContentType != "application/x-zip-compressed" || asset.Name == null || !asset.Name.Contains("Collection"))
                            {
                                continue;
                            }

                            Console.WriteLine(asset.Name);
                            Console.WriteLine(asset.ContentType);
                            Console.WriteLine(asset.BrowserDownloadUrl);


                            byte[] data = asset.DownloadRelease();
                            if (data == null || data.Length <= 100)
                            {
                                Console.WriteLine("Failed to download release");
                                continue;
                            }

                            // write file to disk for debugging
                            //var currentPath = Directory.GetCurrentDirectory();
                            //File.WriteAllBytes(Path.Combine(currentPath, asset.Name), data);

                            // handle the zip in memory
                            using (MemoryStream stream = new MemoryStream(data))
                            {
                                using (ZipArchive archive = new ZipArchive(stream))
                                {
                                    foreach (ZipArchiveEntry entry in archive.Entries)
                                    {
                                        if (entry.FullName.EndsWith(".dll"))
                                        {
                                            using (Stream entryStream = entry.Open())
                                            {
                                                string fileName = entry.FullName.Split('/').Last();
                                                Console.WriteLine(fileName);

                                                // generate hash
                                                string hash = GenerateDataHash(entryStream);
                                                if (!hashData.ContainsKey(hash))
                                                {
                                                    hashData.Add(hash, new HashData {
                                                        Name = fileName,
                                                        ReleaseVersions = new List<string> { releaseVersion }
                                                    });
                                                }
                                                else
                                                {
                                                    hashData[hash].AddHashRelease(releaseVersion);
                                                }
                                                hashDataChanged = true;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    Console.WriteLine("---------------------");
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
            }
            else
            {
                Console.WriteLine("Failed to get releases");
                System.Environment.Exit(1);
            }
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

        private static string GenerateDataHash(Stream stream)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                return BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", string.Empty).ToLower();
            }
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

    internal class HashData
    {
        public string? Name { get; set; }
        public List<string>? ReleaseVersions { get; set; }

        public void AddHashRelease(string releaseVersion)
        {
            if (ReleaseVersions == null)
            {
                ReleaseVersions = new List<string>();
            }
            if (ReleaseVersions.Contains(releaseVersion))
            {
                return;
            }

            ReleaseVersions.Add(releaseVersion);
        }
    }
}
