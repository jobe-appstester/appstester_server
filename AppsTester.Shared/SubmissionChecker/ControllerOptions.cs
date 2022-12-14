using System.ComponentModel.DataAnnotations;

namespace AppsTester.Shared.SubmissionChecker
{
    public class ControllerOptions
    {
        [Url]
        [Required]
        public string Url { get; set; }
    }
}