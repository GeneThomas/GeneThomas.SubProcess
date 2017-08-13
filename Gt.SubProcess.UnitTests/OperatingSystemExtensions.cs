using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gt.SubProcess.UnitTests
{
    internal static class OperatingSystemExtensions
    {
        /// <summary>
        /// System.Environment.OSVersion.IsWindows()
        /// </summary>
        /// As opposed to Unix, Mac or XBox.
        /// <returns>Returns true if is running on windows
        /// </returns>
        public static bool IsWindows(this System.OperatingSystem os)
        {
            switch (os.Platform)
            {
                case PlatformID.Win32NT:
                case PlatformID.Win32S:
                case PlatformID.Win32Windows:
                case PlatformID.WinCE:
                    return true;
                default:
                    return false;
            }
        }
    }
}
