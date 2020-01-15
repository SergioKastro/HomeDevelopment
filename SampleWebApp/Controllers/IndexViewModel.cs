namespace SampleWebApp.Controllers
{
    public class IndexViewModel
    {

        public string TestSetting { get; set; }

        public string EnvironmentName { get; set;}
        public string HostName { get; set;}

        public string TestFile { get; set; }
        public string TestFileLocation { get; set; }

        public string WebApiGetResult { get; set; }
        public string WebApiGetUri { get; set; }
        public bool WebApiGetSuccess { get; set; }
    }
}