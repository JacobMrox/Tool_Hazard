using System;
using System.Linq;
using System.Reflection;

namespace IntelOrca.Biohazard
{
    public class Program
    {
        public static Assembly CurrentAssembly => Assembly.GetEntryAssembly();
        public static Version CurrentVersion = GetCurrentVersion();
        public static string CurrentVersionNumber => $"{CurrentVersion.Major}.{CurrentVersion.Minor}.{CurrentVersion.Build}";
        public static string CurrentVersionInfo => $"BioRand {CurrentVersion.Major}.{CurrentVersion.Minor}.{CurrentVersion.Build} ({GitHash})";
        public static string GitHash
        {
            get
            {
                var assembly = CurrentAssembly;
                if (assembly == null)
                    return "";

                var attribute = assembly
                    .GetCustomAttributes<AssemblyInformationalVersionAttribute>()
                    .FirstOrDefault();
                var rev = attribute.InformationalVersion;
                var plusIndex = rev.IndexOf('+');
                if (plusIndex != -1)
                {
                    return rev.Substring(plusIndex + 1);
                }
                return rev;
            }
        }

        private static Version GetCurrentVersion()
        {
            var version = CurrentAssembly?.GetName().Version ?? new Version();
            if (version.Revision == -1)
                return version;
            return new Version(version.Major, version.Minor, version.Build);
        }
    }
}
