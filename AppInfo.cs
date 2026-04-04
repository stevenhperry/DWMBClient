using System.Reflection;

namespace DWMB_AIO
{
    public static class AppInfo
    {
        private static Assembly Assembly => Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();

        public static string DisplayVersion
        {
            get
            {
                // Prefer informational version (may contain SemVer + metadata), then file version, then assembly version
                var info = Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
                if (!string.IsNullOrWhiteSpace(info)) return info.Split('+')[0]; // Strip build metadata

                var file = Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
                if (!string.IsNullOrWhiteSpace(file)) return file;

                var ver = Assembly.GetName().Version?.ToString();
                return ver ?? string.Empty;
            }
        }
    }
}