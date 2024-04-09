using System.IO.Compression;
using static GenerateKnownHashes.Github;

namespace GenerateKnownHashes
{
    internal class GithubRepo
    {
        private string Credentials;
        private string Owner;
        private string Repo;

        private Github github;

        public GithubRepo(string credentials, string owner, string repo)
        {
            Credentials = credentials;
            Owner = owner;
            Repo = repo;

            github = new Github(credentials, owner, repo);
        }

        public bool AppendReleasesHashData(Dictionary<string, HashData> hashData)
        {
            string repoPrefix = generateReleaseVersion("");
            List<string> loadedHashVersions = new List<string>();
            foreach (KeyValuePair<string, HashData> entry in hashData)
            {
                // add the version if it's not already in the list
                if (entry.Value.ReleaseVersions != null)
                {
                    foreach (string version in entry.Value.ReleaseVersions)
                    {
                        if (!version.StartsWith(repoPrefix))
                        {
                            continue;
                        }
                        if (!loadedHashVersions.Contains(version))
                        {
                            loadedHashVersions.Add(version);
                        }
                    }
                }
            }

            List<GithubRelease> releases = github.ListReleases();
            bool hashDataChanged = false;
            if (releases != null)
            {
                foreach (GithubRelease release in releases)
                {
                    string assetVersion = release.TagName ?? release.Name ?? "BAD_VERSION";

                    string releaseVersion = generateReleaseVersion(assetVersion);

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
                            if (asset.ContentType == null || !isValidContentType(asset.ContentType))
                            {
                                Console.WriteLine("Skipping asset " + asset.Name + " invalid content-type: " + asset.ContentType);
                                continue;
                            }
                            if (asset.BrowserDownloadUrl == null || asset.BrowserDownloadUrl.Contains("/archive/"))
                            {
                                Console.WriteLine("Skipping asset " + asset.Name + " invalid download url: " + asset.BrowserDownloadUrl);
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
                                                string hash = Utilities.GenerateHash(entryStream);
                                                if (!hashData.ContainsKey(hash))
                                                {
                                                    hashData.Add(hash, new HashData
                                                    {
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
            }
            else
            {
                Console.WriteLine("Failed to get releases");
                Environment.Exit(1);
            }

            return hashDataChanged;
        }
    
        private string generateReleaseVersion(string assetVersion)
        {
            return Owner + "/" + Repo + ":" + assetVersion;
        }
    
        private bool isValidContentType(string contentType)
        {
            return contentType == "application/zip" || contentType == "application/x-zip-compressed";
        }
    }
}
