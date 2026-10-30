using System.Globalization;

namespace RedFox.AppHost;

/// <summary>
/// Controls the splash screen shown by the RedFox AppHost launcher.
/// </summary>
public static unsafe class AppHostSplash
{
    private static readonly delegate* unmanaged<char*, void> SetStatusCallback = (delegate* unmanaged<char*, void>)GetCallback("REDFOX_APPHOST_SPLASH_STATUS");
    private static readonly delegate* unmanaged<void> CloseCallback = (delegate* unmanaged<void>)GetCallback("REDFOX_APPHOST_SPLASH_CLOSE");

    /// <summary>
    /// Gets whether the application was started by the RedFox AppHost launcher.
    /// </summary>
    public static bool IsHosted => SetStatusCallback != null;

    /// <summary>
    /// Sets the status text shown on the splash screen.
    /// </summary>
    /// <param name="status">The status text to show.</param>
    public static void SetStatus(string status)
    {
        if (SetStatusCallback == null)
        {
            return;
        }

        fixed (char* text = status)
        {
            SetStatusCallback(text);
        }
    }

    /// <summary>
    /// Closes the splash screen.
    /// </summary>
    public static void Close()
    {
        if (CloseCallback != null)
        {
            CloseCallback();
        }
    }

    private static nint GetCallback(string name)
    {
        return AppContext.GetData(name) is string value ? nint.Parse(value, CultureInfo.InvariantCulture) : 0;
    }
}
