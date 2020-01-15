using Microsoft.AspNetCore.Mvc;

namespace SampleWebApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GreetingController : ControllerBase
    {
        private static string greeting = "Hello";
        public GreetingController()
        {
        }

        [HttpGet]
        public ActionResult<string> Get()
        {
            return greeting;
        }

        [HttpPost]
        public ActionResult Post([FromBody]string newGreeting)
        {
            greeting = newGreeting;
            return new OkResult();
        }

    }
}