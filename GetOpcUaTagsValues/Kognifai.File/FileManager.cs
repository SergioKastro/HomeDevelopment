using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using log4net;

namespace Kognifai.File
{
    public static class FileManager
    {
        private static readonly ILog SysLog = LogManager.GetLogger(typeof(FileManager));


        public static List<string> DataReading(string filePath)
        {
            var result = new List<string>(); // A list of strings 

            try
            {
                // Create a stream reader object to read a text file.
                using (var reader =
                    new StreamReader(new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read)))
                {
                    string line;

                    // While there are lines in the file, read a line into the line variable.
                    while ((line = reader.ReadLine()) != null)
                    {
                        // If the line is not empty, add it to the list.
                        if (!string.IsNullOrEmpty(line))
                        {
                            result.Add(line.Trim());
                        }
                    }
                        
                }
            }
            catch (Exception ex)
            {
                SysLog.Error($"An error occured when attempting to read file {filePath}.", ex);
            }


            return result;
        }

        public static void WriteToFile(string message, string fileName, string directoryPath = null, string header = null)
        {
            if (string.IsNullOrEmpty(directoryPath))
            {
                directoryPath = "C:\\Logs";
            }

            if (!Directory.Exists(directoryPath)) Directory.CreateDirectory(directoryPath);

            var filepath = Path.Combine(directoryPath, fileName); 

            try
            {
                if (!System.IO.File.Exists(filepath))
                    // Create a file to write to.   
                    using (var streamWriter = System.IO.File.CreateText(filepath))
                    {
                        if (!string.IsNullOrEmpty(header))
                        {
                            streamWriter.WriteLine(header);
                        }
                    }

                using (var streamWriter = System.IO.File.AppendText(filepath))
                {
                    streamWriter.WriteLine(message);
                }

                //Remove any empty line from file
                System.IO.File.WriteAllLines(filepath, System.IO.File.ReadAllLines(filepath).Where(l => !string.IsNullOrWhiteSpace(l)));
            }
            catch (Exception ex)
            {
                SysLog.Error($"Unable to write data to the file {fileName}. ", ex);
            }

            SysLog.Info($"\n\n Writing data in result file \"{filepath}\" completed.\n");
        }

    }
}