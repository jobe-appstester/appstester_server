using Microsoft.AspNetCore.Mvc;

namespace AppsTester.Controller.Submissions
{
    [ApiController]
    [Route("[controller]")]
    internal class SubmissionsController : ControllerBase
    {
        [HttpGet]
        public Submission GetSubmission(int id)
        {
            
        }
    }
}