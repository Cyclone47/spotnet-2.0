using System;
using System.Collections.Generic;
using Squirrel;

namespace SquirrelTest
{
    class Program
    {
        static void Main(string[] args)
        {
            UpdateManager mgr = null;
            UpdateInfo info = null;
            ReleaseEntry entry = null;
            Console.WriteLine("Compiled OK!");
        }
    }

    internal class UpdateInfoEx : UpdateInfo
    {
        public UpdateInfoEx(ReleaseEntry currentlyInstalledVersion, IEnumerable<ReleaseEntry> releasesToApply, string packageDirectory, FrameworkVersion appFrameworkVersion)
            : base(currentlyInstalledVersion, releasesToApply, packageDirectory, appFrameworkVersion)
        {
        }
    }
}
