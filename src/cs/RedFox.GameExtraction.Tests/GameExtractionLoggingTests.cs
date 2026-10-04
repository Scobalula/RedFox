using Microsoft.Extensions.Logging;

namespace RedFox.GameExtraction.Tests;

[TestClass]
public sealed class GameExtractionLoggingTests
{
    [TestMethod]
    public void VerboseSettingChangesLevelAfterConfigure()
    {
        string directory = Path.Combine(Path.GetTempPath(), "RedFoxLoggingTests", Guid.NewGuid().ToString("N"));
        ILoggerFactory factory = GameExtractionLogging.Configure("LoggingTests", new GameExtractionLogOptions { Directory = directory, WriteToDebug = false });

        try
        {
            ILogger logger = factory.CreateLogger("Test");

            logger.LogDebug("before-enabling");
            Assert.IsFalse(ReadLog(directory).Contains("before-enabling", StringComparison.Ordinal));

            GameExtractionSettings settings = new();
            settings.SetSettingValue(GameExtractionLogging.VerboseSetting, bool.TrueString);
            GameExtractionLogging.ApplySettings(settings);

            logger.LogDebug("after-enabling");
            Assert.IsTrue(ReadLog(directory).Contains("after-enabling", StringComparison.Ordinal));
        }
        finally
        {
            GameExtractionLogging.Factory = null;
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string ReadLog(string directory)
    {
        using FileStream stream = new(Directory.GetFiles(directory, "*.log").Single(), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using StreamReader reader = new(stream);

        return reader.ReadToEnd();
    }
}
