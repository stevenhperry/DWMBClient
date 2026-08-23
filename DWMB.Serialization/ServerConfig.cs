namespace DWMB_AIO.DWMB.Serialization
{
    /// <summary>
    /// Which DWMB server environment an <see cref="ApiManager"/> should talk to.
    /// </summary>
    internal enum ServerEnvironment
    {
        Production,
        Development
    }

    /// <summary>
    /// Compiled-in DWMB server base URLs, replacing the old loose
    /// <c>server_location.txt</c> file shipped next to the executable. This file is
    /// committed with placeholder values; the release job in
    /// .github/workflows/installer.yml overwrites <see cref="ServerUrl"/> and
    /// <see cref="ServerUrlDev"/> with the real production/development URLs (from the
    /// DWMB_SERVER_URL and DWMB_SERVER_URL_DEV repository secrets) before compiling a
    /// tagged build, so the real addresses never land in git history.
    /// See Installer/README.md for details.
    /// </summary>
    internal static class ServerConfig
    {
        public const string ServerUrl = "https://example.com";
        public const string ServerUrlDev = "https://example.com";
    }
}
