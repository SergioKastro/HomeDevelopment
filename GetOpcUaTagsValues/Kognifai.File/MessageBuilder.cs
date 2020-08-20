using System;
using log4net;


namespace Kognifai.File
{
    public class MessageBuilder
    {
        private static readonly ILog SysLog = LogManager.GetLogger(typeof(MessageBuilder));

        public string GetHeaderForFile()
        {
            const string header = "TagId,Value,StatusCode,Timestamp";

            return header;
        }

        public string CreateSuccessMessageToWriteInResultFile(string monitoredItemName, object value, string statusCode, DateTime sourceTimestamp)
        {
            var valueToWrite = IsValueString(value) ? $"\"{value.ToString().Replace("\"", "\"\"")}\"" : value;
            var message = $"{monitoredItemName},{valueToWrite},{statusCode},{sourceTimestamp:o}\n";

            SysLog.Debug($"Read value for: {message}");

            return message;
        }

        private static bool IsValueString(object value)
        {
            try
            {
                return value is string && !string.IsNullOrEmpty(value.ToString());
            }
            catch (Exception)
            {
                return false;
            }
        }

        public void LogFailureMessage(string monitoredItemName, string statusCode)
        {
            SysLog.Debug($"Bad status code: {statusCode} from server for this nodeId: {monitoredItemName}. Please check OPC Server status.");
        }
    }
}
