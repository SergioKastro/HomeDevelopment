using System;
using Kognifai.File;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Kognifai.WindowsService.Tests
{
    [TestClass]
    public class FileMessageBuilderTests
    {
        private MessageBuilder _messageBuilder;

        [TestInitialize]
        public void Initialize()
        {
            _messageBuilder = new MessageBuilder();
        }


        [TestMethod]
        public void CreateSuccessMessageToWriteInResultFile_PassValueAsString_WriteValueWithQuotes()
        {
            //Arrange
            const string monitoredItemName = "ns=4;s=0:UI-18-1406A:Y.Parameters.Unit";
            object value = "%/time";
            const string statusCode = "Good";
            var dateTime = DateTime.Parse("2020-07-22T01:23:25.2660000Z");

            const string expectedResult = "ns=4;s=0:UI-18-1406A:Y.Parameters.Unit,\"%/time\",Good,2020-07-22T03:23:25.2660000+02:00\n";

            //Act
            var actual =
                _messageBuilder.CreateSuccessMessageToWriteInResultFile(monitoredItemName, value, statusCode, dateTime);

            //Assert
            Assert.AreEqual(expectedResult, actual);
        }

       
        [TestMethod]
        public void CreateSuccessMessageToWriteInResultFile_PassValueAsStringWithComma_WriteValueWithQuotesAndWithOutComma()
        {
            //Arrange
            const string monitoredItemName = "ns=4;s=0:UI-18-1406A:Y.Parameters.Unit";
            object value = "This test has ,a comma";
            const string statusCode = "Good";
            var dateTime = DateTime.Parse("2020-07-22T01:23:25.2660000Z");

            const string expectedResult = "ns=4;s=0:UI-18-1406A:Y.Parameters.Unit,\"This test has ,a comma\",Good,2020-07-22T03:23:25.2660000+02:00\n";

            //Act
            var actual =
                _messageBuilder.CreateSuccessMessageToWriteInResultFile(monitoredItemName, value, statusCode, dateTime);

            //Assert
            Assert.AreEqual(expectedResult, actual);
        }

        [TestMethod]
        public void CreateSuccessMessageToWriteInResultFile_PassValueAsStringWithDoubleQuotes_WriteValueWithDoubles()
        {
            //Arrange
            const string monitoredItemName = "ns=4;s=0:UI-18-1406A:Y.Parameters.Unit";
            object value = " Description with \"something\" quoted";
            const string statusCode = "Good";
            var dateTime = DateTime.Parse("2020-07-22T01:23:25.2660000Z");

            const string expectedResult = "ns=4;s=0:UI-18-1406A:Y.Parameters.Unit,\" Description with \"\"something\"\" quoted\",Good,2020-07-22T03:23:25.2660000+02:00\n"
                ;

            //Act
            var actual =
                _messageBuilder.CreateSuccessMessageToWriteInResultFile(monitoredItemName, value, statusCode, dateTime);

            //Assert
            Assert.AreEqual(expectedResult, actual);
        }


        [TestMethod]
        public void CreateSuccessMessageToWriteInResultFile_PassValueAsNumeric_WriteValueWithoutQuotes()
        {
            //Arrange
            const string monitoredItemName = "ns=4;s=0:UI-18-1406A:Y.Parameters.Max";
            object value = 100.0;
            const string statusCode = "Good";
            var dateTime = DateTime.Parse("2020-07-22T01:23:25.2660000Z");

            const string expectedResult = "ns=4;s=0:UI-18-1406A:Y.Parameters.Max,100,Good,2020-07-22T03:23:25.2660000+02:00\n";

            //Act
            var actual =
                _messageBuilder.CreateSuccessMessageToWriteInResultFile(monitoredItemName, value, statusCode, dateTime);

            //Assert
            Assert.AreEqual(expectedResult, actual);
        }

        [TestMethod]
        public void CreateSuccessMessageToWriteInResultFile_PassValueAsNumericDouble_WriteValueWithoutQuotes()
        {
            //Arrange
            const string monitoredItemName = "ns=4;s=0:UI-18-1406A:Y.Parameters.Max";
            object value = 100.54;
            const string statusCode = "Good";
            var dateTime = DateTime.Parse("2020-07-22T01:23:25.2660000Z");

            const string expectedResult = "ns=4;s=0:UI-18-1406A:Y.Parameters.Max,100.54,Good,2020-07-22T03:23:25.2660000+02:00\n";

            //Act
            var actual =
                _messageBuilder.CreateSuccessMessageToWriteInResultFile(monitoredItemName, value, statusCode, dateTime);

            //Assert
            Assert.AreEqual(expectedResult, actual);
        }

        [TestMethod]
        public void CreateSuccessMessageToWriteInResultFile_PassValueAsBoolean_WriteValueWithoutQuotes()
        {
            //Arrange
            const string monitoredItemName = "ns=4;s=0:UI-18-1406A:PNE";
            object value = false;
            const string statusCode = "Good";
            var dateTime = DateTime.Parse("2020-07-22T01:23:25.2660000Z");

            const string expectedResult = "ns=4;s=0:UI-18-1406A:PNE,False,Good,2020-07-22T03:23:25.2660000+02:00\n";

            //Act
            var actual =
                _messageBuilder.CreateSuccessMessageToWriteInResultFile(monitoredItemName, value, statusCode, dateTime);

            //Assert
            Assert.AreEqual(expectedResult, actual);
        }

        [TestMethod]
        public void CreateSuccessMessageToWriteInResultFile_PassValueAsNull_WriteValueWithoutQuotes()
        {
            //Arrange
            const string monitoredItemName = "ns=4;s=0:UI-18-1406A:PNE";
            const string statusCode = "Good";
            var dateTime = DateTime.Parse("2020-07-22T01:23:25.2660000Z");

            const string expectedResult = "ns=4;s=0:UI-18-1406A:PNE,,Good,2020-07-22T03:23:25.2660000+02:00\n";

            //Act
            var actual =
                _messageBuilder.CreateSuccessMessageToWriteInResultFile(monitoredItemName, null, statusCode, dateTime);

            //Assert
            Assert.AreEqual(expectedResult, actual);
        }

        [TestMethod]
        public void CreateSuccessMessageToWriteInResultFile_PassValueAsEmptyString_WriteValueWithoutQuotes()
        {
            //Arrange
            const string monitoredItemName = "ns=4;s=0:UI-18-1406A:PNE";
            object value = string.Empty;
            const string statusCode = "Good";
            var dateTime = DateTime.Parse("2020-07-22T01:23:25.2660000Z");

            const string expectedResult = "ns=4;s=0:UI-18-1406A:PNE,,Good,2020-07-22T03:23:25.2660000+02:00\n";

            //Act
            var actual =
                _messageBuilder.CreateSuccessMessageToWriteInResultFile(monitoredItemName, value, statusCode, dateTime);

            //Assert
            Assert.AreEqual(expectedResult, actual);
        }
    }
}