using System;

namespace DcimIngester.Ingesting
{
    /// <summary>
    /// Represents event data about a file that has just been ingested.
    /// </summary>
    public class PostFileIngestedEventArgs : EventArgs
    {
        /// <summary>
        /// The new path of the ingested file.
        /// </summary>
        public string NewFilePath { get; private set; }

        /// <summary>
        /// The index of the file in the list of files to ingest.
        /// </summary>
        public int FileIndex { get; private set; }

        /// <summary>
        /// Initialises a new instance of the <see cref="PostFileIngestedEventArgs"/> class.
        /// </summary>
        /// <param name="newFilePath">The new path of the ingested file.</param>
        /// <param name="fileNumber">The index of the file in the list of files to ingest.</param>
        public PostFileIngestedEventArgs(string newFilePath, int fileNumber)
        {
            NewFilePath = newFilePath;
            FileIndex = fileNumber;
        }
    }
}
