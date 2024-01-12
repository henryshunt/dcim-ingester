using System;
using System.IO;

namespace DcimIngester
{
    public static class Utilities
    {
        /// <summary>
        /// Formats a numerical storage size into a string with units based on the magnitude of the value.
        /// </summary>
        /// <param name="bytes">The storage size in bytes.</param>
        /// <returns>The formatted storage size with units based on the magnitude of the value.</returns>
        public static string FormatStorageSize(long bytes)
        {
            string[] units = ["B", "KB", "MB", "GB", "TB"];
            double bytesDouble = bytes;

            int i;
            for (i = 0; i < units.Length && bytes >= 1024; i++, bytes /= 1024)
                bytesDouble = bytes / 1024.0;

            if (bytesDouble > 100)
                bytesDouble = Math.Round(bytesDouble);

            return string.Format("{0:0.#} {1}", bytesDouble, units[i]);
        }

        /// <summary>
        /// Determines whether the size and contents of two files are identical.
        /// </summary>
        /// <param name="path1">The first file to compare.</param>
        /// <param name="path2">The second file to compare.</param>
        /// <returns><see langword="true"/> if the files are identical, otherwise <see langword="false"/>.</returns>
        public static bool AreFilesEqual(string path1, string path2)
        {
            if (string.Equals(path1, path2, StringComparison.OrdinalIgnoreCase))
                return true;

            FileInfo fileInfo1 = new(path1);
            FileInfo fileInfo2 = new(path2);

            if (fileInfo1.Length != fileInfo2.Length)
                return false;

            const int BYTES_TO_READ = sizeof(Int64); // Faster than one byte at a time
            long chunkCount = (long)Math.Ceiling((double)fileInfo1.Length / BYTES_TO_READ);

            using (FileStream stream1 = fileInfo1.OpenRead())
            using (FileStream stream2 = fileInfo2.OpenRead())
            {
                byte[] data1 = new byte[BYTES_TO_READ];
                byte[] data2 = new byte[BYTES_TO_READ];

                for (int i = 0; i < chunkCount; i++)
                {
                    stream1.Read(data1, 0, BYTES_TO_READ);
                    stream2.Read(data2, 0, BYTES_TO_READ);

                    if (BitConverter.ToInt64(data1, 0) != BitConverter.ToInt64(data2, 0))
                        return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Returns "s" or <see cref="string.Empty"/> depending on if a number is singular or plural.
        /// </summary>
        /// <param name="number">The number to use.</param>
        /// <returns>The corresponding suffix for the value of <paramref name="number"/>.</returns>
        public static string GetPluralSuffix(int number)
        {
            return number != 1 ? "s" : string.Empty;
        }
    }
}