using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Spotnet.Setup;
using Xunit;

namespace Spotnet.Tests;

public sealed class ShortcutMigrationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "spotnet-shortcut-tests-" + Guid.NewGuid().ToString("N"));
    private string Desktop => Path.Combine(_root, "Desktop");
    private string Programs => Path.Combine(_root, "Programs");
    private string Profile => Path.Combine(_root, "Profile");
    private string Exe => Path.Combine(_root, "NewApp", "Spotnet.exe");
    private ShortcutManager Manager => new ShortcutManager(Desktop, Programs, Profile);

    public ShortcutMigrationTests()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Exe));
        File.WriteAllBytes(Exe, new byte[] { 0 }); // Never executed; only a shell-link target.
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

    private static void SeedLink(string path, string target, string args = "")
    {
        ShortcutManager.Write(path, target);
        if (args == "") return;
        object shell = null, link = null;
        try
        {
            shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell", true));
            link = ((dynamic)shell).CreateShortcut(path);
            ((dynamic)link).Arguments = args;
            ((dynamic)link).Save();
        }
        finally
        {
            if (link != null) Marshal.FinalReleaseComObject(link);
            if (shell != null) Marshal.FinalReleaseComObject(shell);
        }
    }

    [Theory]
    [InlineData(@"C:\Legacy\Spotnet.exe", "", true)]
    [InlineData(@"C:\Legacy\SPOTNET.EXE", "--old-launch-flag", true)]
    [InlineData(@"C:\Legacy\Spotnet.exe", "--uninstall", false)]
    [InlineData(@"C:\Legacy\Spotnet.exe", "--exitOnUninstall", false)]
    [InlineData(@"C:\Legacy\Spotnet.exe", "--squirrel-uninstall", false)]
    [InlineData(@"C:\Users\Test\Spotnet\Update.exe", "--processStart Spotnet.exe", true)]
    [InlineData(@"C:\Users\Test\Spotnet 2.0\Update.exe", "--processStartAndWait \"Spotnet.exe\"", true)]
    [InlineData(@"C:\Users\Test\Spotnet\Update.exe", "--processStart Spotnet.exe --process-start-args old", true)]
    [InlineData(@"C:\Users\Test\Other\Update.exe", "--processStart Spotnet.exe", false)]
    [InlineData(@"C:\Users\Test\Spotnet\Update.exe", "--uninstall", false)]
    [InlineData(@"C:\Users\Test\Spotnet\Update.exe", "--processStart Other.exe", false)]
    [InlineData(@"C:\Other\Other.exe", "", false)]
    public void MatchesOnlySpotnetLaunchers(string target, string arguments, bool expected)
    {
        Assert.Equal(expected, ShortcutManager.IsSpotnetLauncher(new ShortcutInfo { Target = target, Arguments = arguments }));
    }

    [Fact]
    public void FreshInstallCreatesBothLinksAndUninstallRemovesThem()
    {
        Manager.Install(Exe);
        foreach (string root in new[] { Desktop, Programs })
        {
            string path = Assert.Single(Directory.GetFiles(root, "*.lnk"));
            Assert.Equal("Spotnet.lnk", Path.GetFileName(path));
            var link = ShortcutManager.Read(path);
            Assert.Equal(Exe, link.Target, ignoreCase: true);
            Assert.Equal("", link.Arguments);
            Assert.Equal(Path.GetDirectoryName(Exe), link.WorkingDirectory, ignoreCase: true);
            Assert.StartsWith(Exe, link.Icon, StringComparison.OrdinalIgnoreCase);
        }
        Manager.Restore();
        Assert.Empty(Directory.GetFiles(Desktop, "*.lnk"));
        Assert.Empty(Directory.GetFiles(Programs, "*.lnk"));
    }

    [Fact]
    public void ReplacesOldNewRenamedAndSquirrelLinksInPlaceWithoutDuplicates()
    {
        string[] paths = { Path.Combine(Desktop, "My spots.lnk"), Path.Combine(Desktop, "Spotnet 3.0.lnk"),
            Path.Combine(Programs, "Spotnet", "Spotnet 2.0.lnk") };
        SeedLink(paths[0], Path.Combine(_root, "Old", "Spotnet.exe"), "--old-flag");
        SeedLink(paths[1], Path.Combine(_root, "Previous3", "Spotnet.exe"));
        SeedLink(paths[2], Path.Combine(_root, "Spotnet", "Update.exe"), "--processStart Spotnet.exe");
        var originals = paths.Select(File.ReadAllBytes).ToArray();
        Manager.Install(Exe);
        Manager.Install(Exe); // Must not replace the original backups with 3.0 links.
        Assert.Equal(2, Directory.GetFiles(Desktop, "*.lnk", SearchOption.AllDirectories).Length);
        Assert.Single(Directory.GetFiles(Programs, "*.lnk", SearchOption.AllDirectories));
        foreach (string path in paths)
        {
            Assert.Equal(Exe, ShortcutManager.Read(path).Target, ignoreCase: true);
            Assert.Equal("", ShortcutManager.Read(path).Arguments);
        }
        Manager.Restore();
        for (int index = 0; index < paths.Length; index++) Assert.Equal(originals[index], File.ReadAllBytes(paths[index]));
    }

    [Fact]
    public void PreservesUnrelatedCanonicalAndUninstallLinks()
    {
        string unrelated = Path.Combine(Desktop, "Spotnet.lnk");
        string uninstall = Path.Combine(Programs, "Uninstall Spotnet.lnk");
        SeedLink(unrelated, Path.Combine(_root, "Other.exe"));
        SeedLink(uninstall, Path.Combine(_root, "Spotnet.exe"), "--uninstall");
        byte[] unrelatedBytes = File.ReadAllBytes(unrelated), uninstallBytes = File.ReadAllBytes(uninstall);
        Manager.Install(Exe);
        Assert.Equal(unrelatedBytes, File.ReadAllBytes(unrelated));
        Assert.Equal(uninstallBytes, File.ReadAllBytes(uninstall));
        Assert.Equal(Exe, ShortcutManager.Read(Path.Combine(Desktop, "Spotnet 3.0.lnk")).Target, ignoreCase: true);
        Manager.Restore();
        Assert.Equal(unrelatedBytes, File.ReadAllBytes(unrelated));
        Assert.Equal(uninstallBytes, File.ReadAllBytes(uninstall));
    }

    [Fact]
    public void UninstallPreservesUserEditedLinks()
    {
        Manager.Install(Exe);
        string path = Path.Combine(Desktop, "Spotnet.lnk");
        SeedLink(path, Path.Combine(_root, "UserChoice.exe"));
        byte[] edited = File.ReadAllBytes(path);
        Manager.Restore();
        Assert.Equal(edited, File.ReadAllBytes(path));
        Assert.Empty(Directory.GetFiles(Programs, "*.lnk"));
    }

    [Fact]
    public void LockedExistingLinkReportsFailureWithoutOverwritingIt()
    {
        string path = Path.Combine(Desktop, "Spotnet.lnk");
        SeedLink(path, Path.Combine(_root, "Legacy", "Spotnet.exe"));
        byte[] original = File.ReadAllBytes(path);
        using (var guard = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.ThrowsAny<Exception>(() => Manager.Install(Exe));
        Assert.Equal(original, File.ReadAllBytes(path));
    }

    [Fact]
    public void BackupManifestCannotRestoreOutsideShellRoots()
    {
        Manager.Install(Exe);
        string manifest = Path.Combine(Profile, "ShortcutBackups", "shortcuts.xml");
        var xml = new System.Xml.XmlDocument { XmlResolver = null };
        xml.Load(manifest);
        ((System.Xml.XmlElement)xml.DocumentElement.FirstChild).SetAttribute("path", Path.Combine(_root, "outside.lnk"));
        xml.Save(manifest);
        Assert.Throws<IOException>(() => Manager.Restore());
    }
}
