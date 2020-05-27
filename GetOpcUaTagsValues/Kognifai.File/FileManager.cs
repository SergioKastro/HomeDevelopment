using log4net;
using System;
using System.Collections.Generic;
using System.IO;


namespace Kognifai.File
{
    public static class FileManager
    {
        private static readonly ILog SysLog = LogManager.GetLogger(typeof(FileManager));


        public static List<string> DataReading(string filePath)
        {
            List<string> result = new List<string>(); // A list of strings 

            try
            {
                // Create a stream reader object to read a text file.
                using (StreamReader reader = new StreamReader(new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read)))
                {
                    string line = string.Empty;

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

        public static void WriteToFile(string Message, string fileName, string directoryPath = null, string header = null)
        {
            if (string.IsNullOrEmpty(directoryPath))
            {
                directoryPath = "C:\\Logs";
            }

            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            string filepath = directoryPath + "\\" + fileName;
            try
            {
                if (!System.IO.File.Exists(filepath))
                {
                    // Create a file to write to.   
                    using (StreamWriter sw = System.IO.File.CreateText(filepath))
                    {
                        if (!string.IsNullOrEmpty(header))
                        {
                            sw.WriteLine(header);
                        }

                        sw.WriteLine(Message);
                    }
                }
                else
                {
                    using (StreamWriter sw = System.IO.File.AppendText(filepath))
                    {
                        sw.WriteLine(Message);
                    }
                }
            }
            catch (Exception ex)
            {
                SysLog.Error($"Unable to write data to the file {fileName}. ", ex);
            }

            SysLog.Debug("Writing completed");
        }

        private static string CreateHeaderFile()
        {
            string header = "Tagid  ,\t Value ,\t StatusCode ,\t Timestamp ,\t Error messages";

            return header;
        }
    }
}
