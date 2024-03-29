﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace DcimIngester.Ingesting
{
    /// <summary
    /// Represents the work to do during an ingest operation.
    /// </summary>
    public class IngestWork
    {
        /// <summary>
        /// Gets the letter of the volume to ingest from.
        /// </summary>
        public readonly char VolumeLetter;

        /// <summary>
        /// Gets the label of the volume to ingest from.
        /// </summary>
        public readonly string VolumeLabel = "Unnamed";

        /// <summary>
        /// The paths of the files to ingest from the volume.
        /// </summary>
        private readonly List<string> filesToIngest = [];

        /// <summary>
        /// Gets the paths of the files to ingest from the volume.
        /// </summary>
        public IReadOnlyCollection<string> FilesToIngest
        {
            get { return filesToIngest.AsReadOnly(); }
        }

        /// <summary>
        /// Gets or sets the total size of the files to ingest from the volume.
        /// </summary>
        public long TotalIngestSize { get; private set; } = 0;

        /// <summary>
        /// Indicates whether file discovery is in progress.
        /// </summary>
        private bool isDiscovering = false;


        /// <summary>
        /// Initialises a new instance of the <see cref="IngestWork"/> class.
        /// </summary>
        /// <param name="volumeLetter">The letter of the volume to ingest from.</param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="volumeLetter"/> is not a letter.</exception>
        public IngestWork(char volumeLetter)
        {
            if (!char.IsLetter(volumeLetter))
                throw new ArgumentException(nameof(volumeLetter) + " must be a letter");

            VolumeLetter = volumeLetter;

            string label = new DriveInfo(VolumeLetter.ToString() + ":").VolumeLabel;
            if (label.Length > 0)
                VolumeLabel = label;
        }

        /// <summary>
        /// Searches the volume for files to ingest. Only files within a directory whose name conforms to the DCF
        /// specification, which in turn is within the DCIM directory, will be found. The paths of the files found
        /// are placed in <see cref="FilesToIngest"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if file discovery is already in progress.</exception>
        /// <returns><see langword="true"/> if any files were found, otherwise <see langword="false"/>.</returns>
        public bool DiscoverFiles()
        {
            if (isDiscovering)
            {
                throw new InvalidOperationException(
                    "Cannot execute file discovery because it is already in progress.");
            }

            try
            {
                isDiscovering = true;
                filesToIngest.Clear();

                if (!Directory.Exists(Path.Combine(VolumeLetter + ":", "DCIM")))
                    return false;

                string[] directories = Directory.GetDirectories(Path.Combine(VolumeLetter + ":", "DCIM"));

                foreach (string directory in directories)
                {
                    // Ignore directory names not conforming to DCF spec to avoid non-image directories
                    if (Regex.IsMatch(Path.GetFileName(directory),
                        "^([1-8][0-9]{2}|9[0-8][0-9]|99[0-9])[0-9A-Z]{5}$"))
                    {
                        foreach (string file in Directory.GetFiles(directory))
                        {
                            filesToIngest.Add(file);
                            TotalIngestSize += new FileInfo(file).Length;
                        }
                    }
                }

                isDiscovering = false;
                return filesToIngest.Count > 0;
            }
            catch
            {
                filesToIngest.Clear();
                TotalIngestSize = 0;
                isDiscovering = false;

                throw;
            }
        }
    }
}
