using System;
using System.IO;
using ICSharpCode.SharpZipLib.Zip;
using Serilog;

namespace XExplorer.Utils;

public class ZipUtils
{
    public static string[] Passwords = new[] { "1024" };
    
	 /// <summary>
    /// Extracts a zip archive to a specified output path, trying passwords from the provided array if the archive is encrypted.
    /// </summary>
    /// <param name="archivePath">The path to the zip archive.</param>
    /// <param name="outputPath">The directory where files will be extracted.</param>
    /// <param name="passwords">An array of passwords to try if the archive is encrypted.</param>
    public static string Extract(string archivePath, string outputPath, string[] passwords)
    {
        // First, check if the archive is encrypted
        bool isEncrypted = false;
        using (ZipFile zipFile = new ZipFile(File.OpenRead(archivePath)))
        {
            foreach (ZipEntry entry in zipFile)
            {
                if (entry.IsCrypted)
                {
                    isEncrypted = true;
                    break;
                }
            }

            if (!isEncrypted)
            {
                // No encryption, extract directly
                zipFile.Password = null; // No password
                ExtractEntries(zipFile, outputPath); 
                return string.Empty;
            }
        }
 
        // Encrypted archive, try passwords
        foreach (string password in passwords)
        {
            try
            {
                using (ZipFile zipFile = new ZipFile(File.OpenRead(archivePath)))
                {
                    zipFile.Password = password;

                    // Test the password
                    zipFile.TestArchive(true);

                    // If no exception, password is correct
                    ExtractEntries(zipFile, outputPath);  
                    Log.Information("Extraction succeeded with password: " + password);
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                // Password incorrect or other error
                Log.Error($"Password '{password}' failed: {ex.Message}"); 
            }
        }
 
        // If we get here, none of the passwords worked
        Log.Error("Failed to extract archive. None of the passwords worked.");
        return $"{archivePath} Failed to extract archive. None of the passwords worked.";
    }

    /// <summary>
    /// Extracts all files from the zip archive to the specified output directory.
    /// </summary>
    /// <param name="zipFile">The zip file object.</param>
    /// <param name="outputPath">The directory where files will be extracted.</param>
    private static void ExtractEntries(ZipFile zipFile, string outputPath)
    {
        foreach (ZipEntry entry in zipFile)
        {
            if (!entry.IsFile)
            {
                // Skip directories
                continue;
            }

            string entryFileName = entry.Name;

            // Construct the full output path
            string fullZipToPath = Path.Combine(outputPath, entryFileName);
            if (File.Exists(fullZipToPath))
                continue;

            // Ensure the output directory exists
            string directoryName = Path.GetDirectoryName(fullZipToPath);
            if (!string.IsNullOrEmpty(directoryName))
            {
                Directory.CreateDirectory(directoryName);
            }

            // Extract the file
            using (Stream zipStream = zipFile.GetInputStream(entry))
            using (FileStream fsOutput = File.Create(fullZipToPath))
            {
                zipStream.CopyTo(fsOutput);
            }
        }
    }
}