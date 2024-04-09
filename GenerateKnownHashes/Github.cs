using Newtonsoft.Json;

namespace GenerateKnownHashes
{
    internal class Github
    {
        private string githubBaseUrl = "https://api.github.com/repos/";
        private HttpClient githubHttpClient = new HttpClient();
        private static HttpClient genericHttpClient = new HttpClient();

        private string RepoOwner { get; set; }
        private string RepoName { get; set; }
        public Github(string authKey, string repoOwner, string repoName)
        {
            RepoOwner = repoOwner;
            RepoName = repoName;
            githubHttpClient.BaseAddress = new Uri(githubBaseUrl);
            githubHttpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + authKey);
            githubHttpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
            githubHttpClient.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
            githubHttpClient.DefaultRequestHeaders.Add("User-Agent", "GenerateKnownHashes");
        }

        public List<GithubRelease> ListReleases()
        {
            return GetJsonData<List<GithubRelease>>("releases") ?? new List<GithubRelease>();
        }

        private T? GetJsonData<T>(string urlPath)
        {
            try
            {
                HttpResponseMessage response = githubHttpClient.GetAsync(RepoOwner + "/" + RepoName + "/" + urlPath).Result;
                response.EnsureSuccessStatusCode();
                string responseBody = response.Content.ReadAsStringAsync().Result;
                T? deserializedJson = JsonConvert.DeserializeObject<T>(responseBody);
                if (deserializedJson == null)
                {
                    throw new Exception("No data returned from path [" + urlPath + "]");
                }
                return deserializedJson;
            }
            catch (Exception e)
            {
                throw new Exception("Failed to get data from path [" + urlPath + "]: " + e.Message);
            }
        }



        internal class GithubRelease
        {
            [JsonProperty("tag_name")]
            public string? TagName { get; set; }

            [JsonProperty("name")]
            public string? Name { get; set; }

            [JsonProperty("assets")]
            public List<GithubAssets>? Assets { get; set; }
        }

        internal class GithubAssets
        {
            [JsonProperty("name")]
            public string? Name { get; set; }

            [JsonProperty("content_type")]
            public string? ContentType { get; set; }

            [JsonProperty("browser_download_url")]
            public string? BrowserDownloadUrl { get; set; }

            public byte[] DownloadRelease()
            {
                byte[] data = Array.Empty<byte>();
                try
                {
                    HttpResponseMessage response = genericHttpClient.GetAsync(BrowserDownloadUrl).Result;
                    response.EnsureSuccessStatusCode();
                    data = response.Content.ReadAsByteArrayAsync().Result;
                }
                catch (Exception e)
                {
                    Console.WriteLine("Failed to download asset [" + Name + "]: " + e.Message);
                }
                return data;
            }
        }
    }

    
}
