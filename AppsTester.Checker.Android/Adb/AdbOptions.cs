using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppsTester.Checker.Android.Adb
{
    internal class AdbOptions
    {
        [Required]
        public string Host { get; set; }
        public string ExecutablePath { get; set; }
    }
}
