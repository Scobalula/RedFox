namespace RedFox.AppHost.Packer;

/// <summary>
/// Describes how the launcher locates the application files.
/// </summary>
internal enum AppHostMode : uint
{
    /// <summary>
    /// The application files are compressed into the executable and extracted to a cache on first run.
    /// </summary>
    Packed = 0,

    /// <summary>
    /// The application files sit next to the executable, which only provides the splash screen.
    /// </summary>
    SplashOnly = 1,
}
