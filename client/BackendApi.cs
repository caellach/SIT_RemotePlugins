using Newtonsoft.Json;
using System;
using System.IO;
using System.Net.Http;

namespace RemotePlugins
{
    internal class BackendApi
    {
        private HttpClient httpClient = new HttpClient();

        private BackendUrlObj backendUrlObj;
        private bool canConnect = false;
        public bool CanConnect { get { return canConnect; } }


        public class BackendUrlObj
        {
            public string BackendUrl { get; set; }
            public string Version { get; set; }
        }

        public BackendApi()
        {
            Initialize();
            TestConnection();
        }

        private void Initialize()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (args == null)
            {
                Logger.LogFatal("No command line arguments found");
                return;
            }

            foreach (string arg in args)
            { // -config={'BackendUrl':'http://tarky.carothers.me:6969/','Version':'live'}
                if (arg.Contains("BackendUrl"))
                {
                    string json = arg.Replace("-config=", string.Empty);
                    backendUrlObj = JsonConvert.DeserializeObject<BackendUrlObj>(json);
                    Logger.LogInfo("BackendUrl: " + backendUrlObj.BackendUrl);
                }
            }
        }

        private void TestConnection()
        {
            if (backendUrlObj == null || string.IsNullOrWhiteSpace(backendUrlObj.BackendUrl))
            {
                Logger.LogFatal("BackendUrl is not set");
                return;
            }

            httpClient.Timeout = TimeSpan.FromSeconds(60 * 15); // 15 minutes, needed for downloading the plugin zip; calculated assuming 1mbps download speed
            httpClient.BaseAddress = new Uri(backendUrlObj.BackendUrl);
            string assemblyName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
            string assemblyVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            string userAgent = "SITarkov-" + assemblyName + "-" + assemblyVersion;
            httpClient.DefaultRequestHeaders.Add("User-Agent", userAgent);
            httpClient.DefaultRequestHeaders.Add("debug", "1"); // if we don't set this then bad requests are zlib'd
            
            // Send a request to the backend to see if we can connect
            try
            {
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "launcher/ping");
                request.Headers.Add("Accept", "application/json");

                HttpResponseMessage response = httpClient.SendAsync(request).Result;
                response.EnsureSuccessStatusCode();
                byte[] responseBytes = response.Content.ReadAsByteArrayAsync().Result;
                
                // compressed using zlib but they don't return the Content-Encoding header... oy
                // decompress the response
                string responseBody = Utilities.DecompressZlibString(responseBytes);
                Logger.LogInfo("Backend responded: " + responseBody);
                if (responseBody != "\"pong!\"")
                {
                    Logger.LogFatal("Backend responded different than expected: " + responseBody);
                    return;
                }

                canConnect = true;
            }
            catch (Exception e)
            {
                Logger.LogFatal("Failed to connect to backend: " + e.Message);
                return;
            }
        }

        internal ClientOptions GetClientOptions()
        {
            return GetJsonData<ClientOptions>("RemotePlugins/ClientOptions");
        }

        internal PluginFileMap GetFileList()
        {
            return GetJsonData<PluginFileMap>("RemotePlugins/FileMap").CleanFileNames();
        }

        private static string RemotePluginsFilename = "RemotePlugins.zip";
        internal PluginUpdateFile GetPluginUpdateFile(string downloadPath, string expectedHash)
        {
            try
            {
                string downloadFilePath = Path.GetFullPath(Path.Combine(downloadPath, RemotePluginsFilename));
                if (File.Exists(downloadFilePath))
                {
                    string existingZipHash = Utilities.GenerateHash(downloadFilePath);
                    if (existingZipHash == expectedHash)
                    {
                        Logger.LogInfo("Existing plugin update file found. Skipping download");
                        return new PluginUpdateFile
                        {
                            FileName = RemotePluginsFilename,
                            FilePath = downloadFilePath,
                            FileSize = (int)new FileInfo(downloadFilePath).Length
                        };
                    }

                    File.Delete(downloadFilePath);
                }

                HttpResponseMessage response = httpClient.GetAsync("RemotePlugins/File").Result;
                response.EnsureSuccessStatusCode();
                if (!response.Content.Headers.ContentType.ToString().Equals("application/zip"))
                {
                    Logger.LogFatal("Failed to get file: Content-Type is not application/zip");
                    return null;
                }
                // this is the file, store it in the download folder
                byte[] fileBytes = response.Content.ReadAsByteArrayAsync().Result;
                if (fileBytes == null || fileBytes.Length <= 100)
                {
                    Logger.LogFatal("Failed to get file");
                    return null;
                }
                string filePath = Path.GetFullPath(Path.Combine(downloadPath, RemotePluginsFilename));

                // create the download folder if it doesn't exist
                if (!Directory.Exists(downloadPath))
                {
                    Directory.CreateDirectory(downloadPath);
                }


                File.WriteAllBytes(filePath, fileBytes);
                return new PluginUpdateFile
                {
                    FileName = RemotePluginsFilename,
                    FilePath = filePath,
                    FileSize = fileBytes.Length
                };
            }
            catch (AggregateException e)
            {
                for (int i = 0; i < e.InnerExceptions.Count; i++)
                {
                    Logger.LogFatal("Failed to get file: " + e.InnerExceptions[i].Message);
                }
                return null;
            }
            catch (Exception e)
            {
                Logger.LogFatal("Failed to get file: " + e.Message);
                return null;
            }
        }
    
        private T GetJsonData<T>(string urlPath) where T: RemoteObject
        {
            try
            {
                HttpResponseMessage response = httpClient.GetAsync(urlPath).Result;
                response.EnsureSuccessStatusCode();
                string responseBody = response.Content.ReadAsStringAsync().Result;
                // convert the JSON to a ClientOptions object
                T jsonObject = JsonConvert.DeserializeObject<T>(responseBody);
                return jsonObject;
            }
            catch (Exception e)
            {
                Logger.LogFatal("Failed to get data from path [" + urlPath + "]: " + e.Message);
            }
            return default;
        }
    }

    internal class RemoteObject { }
}
