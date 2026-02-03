using Microsoft.Win32;
using System;

namespace OSMBScriptManager.Services;

public class AutoStartService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "OSMBScriptManager";

    /// <summary>
    /// Enables or disables auto-start for this application.
    /// </summary>
    /// <param name="enable">True to enable auto-start, false to disable.</param>
    public bool SetAutoStart(bool enable)
    {
        try
        {
            if (!OperatingSystem.IsWindows())
                return false;

            using (var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true))
            {
                if (key == null)
                    return false;

                if (enable)
                {
                    var exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                    key.SetValue(AppName, exePath);
                }
                else
                {
                    key.DeleteValue(AppName, throwOnMissingValue: false);
                }

                return true;
            }
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if auto-start is currently enabled for this application.
    /// </summary>
    /// <returns>True if auto-start is enabled, false otherwise.</returns>
    public bool IsAutoStartEnabled()
    {
        try
        {
            if (!OperatingSystem.IsWindows())
                return false;

            using (var key = Registry.CurrentUser.OpenSubKey(RunKey))
            {
                if (key == null)
                    return false;

                var value = key.GetValue(AppName);
                return value != null;
            }
        }
        catch
        {
            return false;
        }
    }
}
