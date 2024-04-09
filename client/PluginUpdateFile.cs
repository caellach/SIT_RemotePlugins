namespace RemotePlugins
{
    internal class PluginUpdateFile
    {
        private string fileName;
        private bool isFileNameSet;
        public string FileName
        {
            get { return fileName; }
            set
            {
                if (!isFileNameSet)
                {
                    fileName = value.Trim();
                    isFileNameSet = true;
                }
            }
        }

        private string filePath;
        private bool isFilePathSet;
        public string FilePath
        {
            get { return filePath; }
            set
            {
                if (!isFilePathSet)
                {
                    filePath = value.Trim();
                    isFilePathSet = true;
                }
            }
        }

        private int fileSize;
        private bool isFileSizeSet;
        public int FileSize
        {
            get { return fileSize; }
            set
            {
                if (!isFileSizeSet)
                {
                    fileSize = value;
                    isFileSizeSet = true;
                }
            }
        }
    }
}
