using MetadataExtractor.Formats.Exif;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static DcimIngester.Utilities;

namespace DcimIngester.Ingesting
{
    /// <summary>
    /// Represents an ingest operation.
    /// </summary>
    public class IngestTask
    {
        /// <summary>
        /// Gets the work to do when the ingest is executed.
        /// </summary>
        public readonly IngestWork Work;

        /// <summary>
        /// Gets the status of the ingest.
        /// </summary>
        public IngestTaskStatus Status { get; private set; } = IngestTaskStatus.Ready;

        /// <summary>
        /// Gets or sets the base directory to ingest files into.
        /// </summary>
        public string DestinationDirectory { get; set; }
            = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

        /// <summary>
        /// Gets or sets the destination subfolder structure to ingest files into.
        /// </summary>
        public DestStructure DestinationStructure { get; set; } = DestStructure.Year_Month_Day;

        /// <summary>
        /// Gets or sets whether original files should be deleted after they has been successfully ingested.
        /// </summary>
        public bool DeleteAfterIngest { get; set; } = false;

        /// <summary>
        /// The index of the current or next file to be ingested.
        /// </summary>
        private int lastIngestedPos = 0;

        /// <summary>
        /// Occurs when the ingest of an individual file begins.
        /// </summary>
        public event EventHandler<PreFileIngestedEventArgs>? PreFileIngested;

        /// <summary>
        /// Occurs when the ingest of an individual file successfully completes.
        /// </summary>
        public event EventHandler<PostFileIngestedEventArgs>? PostFileIngested;


        /// <summary>
        /// Initialises a new instance of the <see cref="IngestTask"/> class.
        /// </summary>
        /// <param name="work">The work to do when the ingest is executed.</param>
        public IngestTask(IngestWork work)
        {
            Work = work;
        }

        /// <summary>
        /// Executes the ingest. If the ingest fails, this can be called again to attempt to continue.
        /// </summary>
        /// <param name="cancelToken">A cancellation token that can be used to cancel the ingest.</param>
        /// <exception cref="InvalidOperationException">Thrown if the ingest is completed, aborted or already in
        /// progress.</exception>
        public Task Ingest(CancellationToken cancelToken)
        {
            return Task.Run(() =>
            {
                if (Status == IngestTaskStatus.Ingesting)
                    throw new InvalidOperationException("Cannot start an already in-progress ingest.");
                else if (Status == IngestTaskStatus.Completed)
                    throw new InvalidOperationException("Cannot start a completed ingest.");
                else if (Status == IngestTaskStatus.Aborted)
                    throw new InvalidOperationException("Cannot start an aborted ingest.");

                try
                {
                    Status = IngestTaskStatus.Ingesting;

                    for (int i = lastIngestedPos; i < Work.FilesToIngest.Count; i++)
                    {
                        PreFileIngested?.Invoke(this, new PreFileIngestedEventArgs(i));

                        string destPath = IngestFile(Work.FilesToIngest.ElementAt(i));
                        if (DeleteAfterIngest)
                            File.Delete(Work.FilesToIngest.ElementAt(i));
                        lastIngestedPos++;

                        PostFileIngested?.Invoke(this, new PostFileIngestedEventArgs(destPath, i));

                        // Only cancel if the file we just ingested was not the final file
                        if (cancelToken.IsCancellationRequested && i < Work.FilesToIngest.Count - 1)
                        {
                            Status = IngestTaskStatus.Aborted;
                            cancelToken.ThrowIfCancellationRequested();
                        }
                    }

                    Status = IngestTaskStatus.Completed;
                }
                catch
                {
                    Status = IngestTaskStatus.Failed;
                    throw;
                }
            }, cancelToken);
        }

        /// <summary>
        /// Ingests a file into the appropriate destination directory based on the date in the EXIF data. If no date is
        /// available then the file is ingested into an "Unsorted" directory.
        /// </summary>
        /// <param name="path">The file to ingest.</param>
        /// <returns>The new path of the ingested file</returns>
        private string IngestFile(string path)
        {
            DateTime? dateTaken = GetDateTaken(path);
            string destination = "";

            if (dateTaken != null)
            {
                switch (DestinationStructure)
                {
                    case DestStructure.Year_Month_Day:
                        destination = "{0:D4}\\{1:D2}\\{2:D2}";
                        break;
                    case DestStructure.Year_YearMonthDay:
                        destination = "{0:D4}\\{0:D4}-{1:D2}-{2:D2}";
                        break;
                    case DestStructure.YearMonthDay:
                        destination = "{0:D4}-{1:D2}-{2:D2}";
                        break;
                }

                destination = Path.Combine(DestinationDirectory,
                    string.Format(destination, dateTaken?.Year, dateTaken?.Month, dateTaken?.Day));
            }
            else destination = Path.Combine(DestinationDirectory, "Unsorted");

            destination = CreateDestination(destination);
            return CopyFile(path, destination);
        }

        /// <summary>
        /// Gets the date and time an image file was taken.
        /// </summary>
        /// <param name="path">The file to read.</param>
        /// <returns>The date and time the image was taken, or <see langword="null"/> if the file does not contain that
        /// information.</returns>
        private static DateTime? GetDateTaken(string path)
        {
            IEnumerable<MetadataExtractor.Directory> metadata;
            try
            {
                metadata = MetadataExtractor.ImageMetadataReader.ReadMetadata(path);
            }
            catch (MetadataExtractor.ImageProcessingException) { return null; }

            ExifSubIfdDirectory? exif = metadata.OfType<ExifSubIfdDirectory>().SingleOrDefault();
            string? dto = exif?.GetDescription(ExifDirectoryBase.TagDateTimeOriginal);

            if (dto == null)
                return null;

            try
            {
                return DateTime.ParseExact(dto, "yyyy:MM:dd HH:mm:ss", null);
            }
            catch (FormatException) { return null; }
        }

        /// <summary>
        /// Creates a directory if it does not exist. If the directory exists but has additional text (following a
        /// space) appended to the name of the final directory in the path then it is not created.
        /// </summary>
        /// <param name="path">The directory to create.</param>
        /// <returns>The created or already existing directory.</returns>
        private static string CreateDestination(string path)
        {
            DirectoryInfo dirInfo = new(path);

            // No parent means we're at a root, which isn't something that can be created
            if (dirInfo.Parent == null)
                return path;

            try
            {
                string[] directories = Directory.GetDirectories(dirInfo.Parent.FullName, dirInfo.Name + " *");

                if (directories.Length > 0)
                    return Path.Combine(dirInfo.Parent.FullName, new DirectoryInfo(directories[0]).Name);
            }
            catch (DirectoryNotFoundException) { }

            return Directory.CreateDirectory(path).FullName;
        }

        /// <summary>
        /// Copies a file to a directory. If the file already exists in the directory and the contents are not identical
        /// then a counter is added to the file name.
        /// </summary>
        /// <param name="sourcePath">The file to copy.</param>
        /// <param name="destDirectory">The directory to copy the file to.</param>
        /// <returns>The destination file path, which may be different if the file was renamed to deduplicate.</returns>
        public static string CopyFile(string sourcePath, string destDirectory)
        {
            int duplicateCount = 0;
            string destFileName = "";

            // If file already exists then add incrementing counter until file doesn't exist
            while (true)
            {
                try
                {
                    if (duplicateCount == 0)
                        destFileName = Path.GetFileName(sourcePath);
                    else if (duplicateCount == 1)
                    {
                        destFileName = string.Format("{0} - Copy{2}", Path.GetFileNameWithoutExtension(sourcePath),
                            duplicateCount, Path.GetExtension(sourcePath));
                    }
                    else
                    {
                        destFileName = string.Format("{0} - Copy ({1}){2}", Path.GetFileNameWithoutExtension(sourcePath),
                            duplicateCount, Path.GetExtension(sourcePath));
                    }

                    File.Copy(sourcePath, Path.Combine(destDirectory, destFileName));
                    return Path.Combine(destDirectory, destFileName);
                }
                catch (IOException ex)
                when (ex.HResult == unchecked((int)0x80070050) || ex.HResult == unchecked((int)0x80070050))
                {
                    // File already exists

                    if (AreFilesEqual(sourcePath, Path.Combine(destDirectory, destFileName)))
                        return Path.Combine(destDirectory, destFileName);
                    else duplicateCount++;
                }
            }
        }


        /// <summary>
        /// Specifies the status of an <see cref="IngestTask"/>.
        /// </summary>
        public enum IngestTaskStatus { Ready, Ingesting, Completed, Failed, Aborted }

        /// <summary>
        /// Specifies the destination subfolder structure to ingest files into.
        /// </summary>
        public enum DestStructure { Year_Month_Day, Year_YearMonthDay, YearMonthDay }
    }
}
