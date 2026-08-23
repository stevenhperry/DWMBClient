namespace DWMB_AIO.DWMB.Serialization
{
    /// <summary>
    /// Compiled-in DWMB server base URL, replacing the old loose
    /// <c>server_location.txt</c> file shipped next to the executable. This file is
    /// committed with a placeholder value; the release job in
    /// .github/workflows/installer.yml overwrites <see cref="ServerUrl"/> with the
    /// real production URL (from the DWMB_SERVER_URL repository secret) before
    /// compiling a tagged build, so the real address never lands in git history.
    /// See Installer/README.md for details.
    /// </summary>
    internal static class ServerConfig
    {
        public const string ServerUrl = "https://example.com";
    }
}
