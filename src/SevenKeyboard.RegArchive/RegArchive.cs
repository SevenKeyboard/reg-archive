//==============================================================
// RegArchive — Registry import/export helper using reg.exe with optional RegView selection
//
// GitHub: https://github.com/SevenKeyboard/reg-archive
// Author: SevenKeyboard Ltd. (2026)
// License: The Unlicense
//==============================================================

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security;
using Microsoft.Win32;

namespace SevenKeyboard;

public static class RegArchive
{
    public static bool Import(string filePath, RegistryView view = RegistryView.Default)
    {
        if (string.IsNullOrEmpty(filePath) || filePath.Contains('"') || !File.Exists(filePath))
            return false;

        return Run("import", filePath, null, view);
    }

    public static bool Export(string keyPath, string filePath, bool overwrite = true,
        RegistryView view = RegistryView.Default)
    {
        if (string.IsNullOrEmpty(keyPath) || string.IsNullOrEmpty(filePath)
            || keyPath.Contains('"') || filePath.Contains('"') || !IsValidView(view))
            return false;

        string? temporaryPath = null;
        try
        {
            string? directory = Path.GetDirectoryName(filePath);
            if (string.IsNullOrEmpty(directory))
                return false;

            // Resolve once so the export and move use the same destination.
            filePath = Path.GetFullPath(filePath);
            directory = Path.GetDirectoryName(filePath)!;
            Directory.CreateDirectory(directory);
            if (overwrite)
                return Run("export", keyPath, filePath, view);
            if (File.Exists(filePath))
                return false;

            string candidate = Path.Combine(directory, $".RegArchive-{Guid.NewGuid():N}.reg");
            using (new FileStream(candidate, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                temporaryPath = candidate;

            // Never let reg.exe prompt or race to overwrite the caller's destination.
            if (!Run("export", keyPath, temporaryPath, view))
                return false;

            File.Move(temporaryPath, filePath, overwrite: false);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException
            or ArgumentException or NotSupportedException or SecurityException)
        {
            return false;
        }
        finally
        {
            if (temporaryPath is not null)
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException
                    or SecurityException)
                {
                    // Cleanup failure must not replace the operation's bool result.
                }
            }
        }
    }

    private static bool IsValidView(RegistryView view) =>
        view is RegistryView.Default or RegistryView.Registry32 or RegistryView.Registry64;

    private static bool Run(string operation, string firstPath, string? secondPath, RegistryView view)
    {
        if (!IsValidView(view))
            return false;

        try
        {
            string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string system = Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess
                && File.Exists(Path.Combine(windows, "Sysnative", "reg.exe"))
                ? "Sysnative" : "System32";
            ProcessStartInfo start = new(Path.Combine(windows, system, "reg.exe"))
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            start.ArgumentList.Add(operation);
            start.ArgumentList.Add(firstPath);
            if (secondPath is not null)
            {
                start.ArgumentList.Add(secondPath);
                start.ArgumentList.Add("/y");
            }
            if (view != RegistryView.Default)
                start.ArgumentList.Add(view == RegistryView.Registry32 ? "/reg:32" : "/reg:64");

            using Process? process = Process.Start(start);
            if (process is null)
                return false;
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch (Exception error) when (error is Win32Exception or IOException
            or UnauthorizedAccessException or InvalidOperationException or SecurityException)
        {
            return false;
        }
    }
}
