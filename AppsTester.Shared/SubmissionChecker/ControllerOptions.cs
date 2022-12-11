using System.ComponentModel.DataAnnotations;

namespace AppsTester.Shared.SubmissionChecker
{
    public class ControllerOptions
    {
        [Required]
        [Url]
        public string Url { get; set; }
    }
}