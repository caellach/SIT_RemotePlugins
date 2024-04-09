namespace GenerateKnownHashes
{
    internal class Utilities
    {
        public static string GenerateHash(string filePath)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                using (var stream = File.OpenRead(filePath))
                {
                    return BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", string.Empty).ToLower();
                }
            }
        }

        public static string GenerateHash(byte[] fileData)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                return BitConverter.ToString(sha256.ComputeHash(fileData)).Replace("-", string.Empty).ToLower();
            }
        }

        public static string GenerateHash(Stream stream)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                return BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", string.Empty).ToLower();
            }
        }
    }
}
