using System.ComponentModel.DataAnnotations;

namespace AppsTester.Controller.Moodle
{
    public class MoodleOptions
    {
        [Required]
        public string Token { get; set; }
        public string BasicToken { get; set; }
        [Required]
        [Url]
        public string Url { get; set; }
    }
}
