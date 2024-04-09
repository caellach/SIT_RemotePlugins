namespace GenerateKnownHashes
{
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
