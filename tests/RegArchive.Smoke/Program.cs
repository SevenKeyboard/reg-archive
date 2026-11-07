using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Win32;
using SevenKeyboard;

internal static class Program
{
    private static int checks;

    private static int Main()
    {
        string id = Guid.NewGuid().ToString("N");
        string keyName = @"Software\SevenKeyboard.RegArchive.Smoke\" + id + " 한글 space";
        string directory = Path.Combine(Path.GetTempPath(), "RegArchive smoke 한글 " + id);
        RegistryView[] views = Environment.Is64BitOperatingSystem
            ? [RegistryView.Default, RegistryView.Registry32, RegistryView.Registry64]
            : [RegistryView.Default, RegistryView.Registry32];
        try
        {
            Directory.CreateDirectory(directory);
            foreach (RegistryView view in views)
                RoundTrip(keyName, directory, view);

            if (Environment.Is64BitOperatingSystem)
                CheckSeparateViews(directory);

            string path = Path.Combine(directory, "preserve.reg");
            byte[] sentinel = [0, 255, 13, 10, 42];
            File.WriteAllBytes(path, sentinel);
            Check(!RegArchive.Export("HKCU\\" + keyName, path, false), "Existing destination fails quietly.");
            Check(File.ReadAllBytes(path).SequenceEqual(sentinel), "Existing bytes preserved.");

            foreach (string invalid in new[] { "", "missing.reg", directory, "bad\"file.reg", "bad\0file" })
                Check(!RegArchive.Import(invalid), "Invalid import fails.");
            Check(!RegArchive.Import(null!), "Null import fails.");
            Check(!RegArchive.Export("", path), "Empty key fails.");
            Check(!RegArchive.Export("HKCU", ""), "Empty file fails.");
            Check(!RegArchive.Export(null!, path), "Null key fails.");
            Check(!RegArchive.Export("HKCU", null!), "Null file fails.");
            Check(!RegArchive.Export("HKCU", "bare.reg"), "Directory required.");
            Check(!RegArchive.Export("HKCU", directory), "Existing directory is not an export file.");
            Check(!RegArchive.Export("HKCU\"bad", path), "Quoted key fails.");
            Check(!RegArchive.Export("HKCU", Path.Combine(directory, "bad\"file.reg")), "Quoted file fails.");
            Check(!RegArchive.Export("HKCU", Path.Combine(directory, "bad\0file")), "Invalid path fails.");
            Check(!RegArchive.Export("HKCU", path, view: (RegistryView)123), "Invalid view fails.");
            Check(!RegArchive.Import(path, (RegistryView)123), "Invalid import view fails.");
            Check(!RegArchive.Import(path), "Malformed reg file fails.");
            Check(!RegArchive.Export("HKCU\\" + keyName + "\\missing", Path.Combine(directory, "missing.reg"), false),
                "Missing key fails and temporary file is cleaned.");

            using RegistryKey root = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default);
            using (RegistryKey key = root.CreateSubKey(keyName))
                key.SetValue("race", "winner 한글");
            string registryPath = "HKCU\\" + keyName;
            string racePath = Path.Combine(directory, "race.reg");
            Task<bool>[] writers = Enumerable.Range(0, 8)
                .Select(_ => Task.Run(() => RegArchive.Export(registryPath, racePath, false))).ToArray();
            Task.WaitAll(writers);
            Check(writers.Count(task => task.Result) == 1, "Exactly one concurrent no-overwrite export succeeds.");
            byte[] winner = File.ReadAllBytes(racePath);
            Check(!RegArchive.Export(registryPath, racePath, false), "Winner remains protected.");
            Check(File.ReadAllBytes(racePath).SequenceEqual(winner), "Winner bytes unchanged.");

            string native = Path.Combine(directory, "native.reg");
            string defaultFile = Path.Combine(directory, "default.reg");
            Check(RegArchive.Export(registryPath, defaultFile), "Default export succeeds.");
            Check(RegArchive.Export(registryPath, native, view: Environment.Is64BitOperatingSystem
                ? RegistryView.Registry64 : RegistryView.Registry32), "Native view export succeeds.");
            Check(File.ReadAllBytes(defaultFile).SequenceEqual(File.ReadAllBytes(native)), "Default matches OS-native output.");

            using (FileStream locked = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                Check(!RegArchive.Export(registryPath, path), "Exclusive destination lock returns false.");
            Check(File.ReadAllBytes(path).SequenceEqual(sentinel), "Locked destination bytes preserved.");
            File.SetAttributes(path, FileAttributes.ReadOnly);
            try
            {
                Check(!RegArchive.Export(registryPath, path), "Read-only destination returns false.");
            }
            finally
            {
                File.SetAttributes(path, FileAttributes.Normal);
            }

            string blocker = Path.Combine(directory, "blocker");
            File.WriteAllText(blocker, "file");
            Check(!RegArchive.Export(registryPath, Path.Combine(blocker, "output.reg")),
                "File blocking directory creation returns false.");
            Check(!Directory.EnumerateFiles(directory, ".RegArchive-*", SearchOption.AllDirectories).Any(),
                "No temporary exports remain.");
            Console.WriteLine($"PASS: {checks} checks, {RuntimeInformation.ProcessArchitecture}.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
        finally
        {
            foreach (RegistryView view in views)
            {
                using RegistryKey root = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, view);
                root.DeleteSubKeyTree(keyName, throwOnMissingSubKey: false);
                if (root.OpenSubKey(keyName) is not null)
                    throw new InvalidOperationException("Owned registry key remains.");
            }
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
            if (Directory.Exists(directory))
                throw new InvalidOperationException("Owned temporary directory remains.");
        }
    }

    private static void CheckSeparateViews(string directory)
    {
        // HKCU Software is shared; Classes/CLSID is redirected and distinguishes the views.
        string name = @"Software\Classes\CLSID\{" + Guid.NewGuid() + "}";
        using RegistryKey root32 = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry32);
        using RegistryKey root64 = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
        try
        {
            using (RegistryKey key = root32.CreateSubKey(name))
                key.SetValue("view", "32");
            using (RegistryKey key = root64.CreateSubKey(name))
                key.SetValue("view", "64");
            foreach (RegistryView view in new[] { RegistryView.Registry32, RegistryView.Registry64, RegistryView.Default })
            {
                string expected = view == RegistryView.Registry32 ? "32" : "64";
                RegistryKey target = expected == "32" ? root32 : root64;
                string file = Path.Combine(directory, $"separate-{view}.reg");
                Check(RegArchive.Export("HKCU\\" + name, file, view: view), $"{view}: redirected export.");
                using (RegistryKey key = target.OpenSubKey(name, writable: true)!)
                    key.SetValue("view", "changed");
                Check(RegArchive.Import(file, view), $"{view}: redirected import.");
                using RegistryKey restored = target.OpenSubKey(name)!;
                Check((string?)restored.GetValue("view") == expected, $"{view}: correct view restored.");
            }
            using RegistryKey key32 = root32.OpenSubKey(name)!;
            using RegistryKey key64 = root64.OpenSubKey(name)!;
            Check((string?)key32.GetValue("view") == "32" && (string?)key64.GetValue("view") == "64",
                "Explicit views stay independent; Default selects 64-bit even in an x86 process.");
        }
        finally
        {
            root32.DeleteSubKeyTree(name, throwOnMissingSubKey: false);
            root64.DeleteSubKeyTree(name, throwOnMissingSubKey: false);
            Check(root32.OpenSubKey(name) is null && root64.OpenSubKey(name) is null,
                "Redirected owned keys removed.");
        }
    }
    private static void RoundTrip(string keyName, string directory, RegistryView view)
    {
        using RegistryKey root = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, view);
        using (RegistryKey key = root.CreateSubKey(keyName))
        {
            key.SetValue("", "default 한글 😀");
            key.SetValue("text 한글", "spaces & unicode 日本語 😀", RegistryValueKind.String);
            key.SetValue("dword", unchecked((int)0xfedcba98), RegistryValueKind.DWord);
            key.SetValue("binary", new byte[] { 0, 1, 128, 255 }, RegistryValueKind.Binary);
            key.SetValue("multi", new[] { "first space", "한글", "日本語 😀" }, RegistryValueKind.MultiString);
            key.SetValue("expand", "%TEMP%\\한글", RegistryValueKind.ExpandString);
            using RegistryKey child = key.CreateSubKey("child space");
            child.SetValue("nested", "yes");
        }
        string path = Path.Combine(directory, view.ToString(), "archive 한글 space.reg");
        Check(RegArchive.Export("HKCU\\" + keyName, path, false, view), $"{view}: export creates directory and absent file.");
        root.DeleteSubKeyTree(keyName);
        Check(RegArchive.Import(path, view), $"{view}: import completes synchronously.");
        using (RegistryKey key = root.OpenSubKey(keyName)!)
        {
            Check((string?)key.GetValue("") == "default 한글 😀", "Default value round trip.");
            Check((string?)key.GetValue("text 한글") == "spaces & unicode 日本語 😀", "Unicode string round trip.");
            Check((int)key.GetValue("dword")! == unchecked((int)0xfedcba98), "DWORD round trip.");
            Check(((byte[])key.GetValue("binary")!).SequenceEqual(new byte[] { 0, 1, 128, 255 }), "Binary round trip.");
            Check(((string[])key.GetValue("multi")!).SequenceEqual(new[] { "first space", "한글", "日本語 😀" }), "Multi-string round trip.");
            Check((string?)key.GetValue("expand", null, RegistryValueOptions.DoNotExpandEnvironmentNames)
                == "%TEMP%\\한글", "Expand string remains unexpanded.");
            using RegistryKey child = key.OpenSubKey("child space")!;
            Check((string?)child.GetValue("nested") == "yes", "Subkey round trip.");
        }
        File.WriteAllText(path, "replace me");
        Check(RegArchive.Export("HKCU\\" + keyName, path, view: view), "Default overwrite replaces existing file.");
        Check(RegArchive.Import(path, view), "Replaced archive is valid.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
        checks++;
    }
}
