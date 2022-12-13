using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using VerifyTests;

namespace AppsTester.Controller.Tests.Unit
{
    internal class StaticSettingsUsage
    {
        [ModuleInitializer]
        public static void Initialize()
        {
            VerifierSettings.UseStrictJson();
        }
    }
}
