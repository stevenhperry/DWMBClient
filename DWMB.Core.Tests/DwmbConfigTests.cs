using System;
using System.IO;
using DWMB.Core.Api;
using DWMB.Core.Config;
using Xunit;

namespace DWMB.Core.Tests
{
    public class DwmbConfigTests : IDisposable
    {
        private readonly string _tempDir;

        public DwmbConfigTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "dwmb-config-tests-" + Guid.NewGuid());
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }

        private string WriteConfig(string contents)
        {
            string path = Path.Combine(_tempDir, "dwmb.config.json");
            File.WriteAllText(path, contents);
            return path;
        }

        [Fact]
        public void Load_ReadsServerAndTokenFromInjectedBaseDirectory()
        {
            // This is the regression test for the CWD bug: baseDirectory is passed
            // explicitly (as each host adapter does, using its own executing assembly's
            // directory), not resolved from Environment.CurrentDirectory.
            WriteConfig("""{ "server": "https://dontwallopmebro.com", "token": "abc123" }""");

            var config = DwmbConfig.Load(_tempDir);

            Assert.Equal("https://dontwallopmebro.com", config.Server);
            Assert.Equal("abc123", config.Token);
        }

        [Fact]
        public void Load_IsCaseInsensitiveOnPropertyNames()
        {
            WriteConfig("""{ "Server": "https://x.test", "Token": "tok" }""");

            var config = DwmbConfig.Load(_tempDir);

            Assert.Equal("https://x.test", config.Server);
            Assert.Equal("tok", config.Token);
        }

        [Fact]
        public void Load_MissingFile_ThrowsDwmbApiExceptionWithClearMessage()
        {
            var ex = Assert.Throws<DwmbApiException>(() => DwmbConfig.Load(_tempDir));
            Assert.Contains("dwmb.config.json", ex.Message);
            Assert.Contains(_tempDir, ex.Message);
        }

        [Fact]
        public void Load_MalformedJson_ThrowsDwmbApiException_NotDownstreamException()
        {
            WriteConfig("{ not valid json ");

            var ex = Assert.Throws<DwmbApiException>(() => DwmbConfig.Load(_tempDir));
            Assert.Contains("malformed", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Load_MissingTokenField_DoesNotThrow()
        {
            // Only `server` is required at the config-file level: the npcap
            // fallback deliberately has no token in its config file (it keeps the
            // v1 UI callsign+token entry instead) and overwrites Token itself
            // after loading. Plugins are the ones that must check config.Token
            // themselves, since they have no other source for it.
            WriteConfig("""{ "server": "https://x.test" }""");

            var config = DwmbConfig.Load(_tempDir);

            Assert.Equal("https://x.test", config.Server);
            Assert.Equal(string.Empty, config.Token);
        }

        [Fact]
        public void Load_EmptyServerField_ThrowsDwmbApiException()
        {
            WriteConfig("""{ "server": "", "token": "abc" }""");

            var ex = Assert.Throws<DwmbApiException>(() => DwmbConfig.Load(_tempDir));
            Assert.Contains("server", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
    }
}
