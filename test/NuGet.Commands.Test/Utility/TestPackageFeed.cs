using System;
using System.Collections.Generic;
using NuGet.LibraryModel;
using NuGet.Versioning;

namespace NuGet.Commands.Test
{
    public class TestPackageFeed
    {
        public IList<TestPackage> Packages { get; }

        public TestPackageFeed()
        {
            Packages = new List<TestPackage>();
        }

        public TestPackageFeed Package(string id, string version, Action<TestPackage> packageBuilder)
        {
            return Package(id, NuGetVersion.Parse(version), packageBuilder);
        }

        public TestPackageFeed Package(string id, NuGetVersion version, Action<TestPackage> packageBuilder)
        {
            var package = new TestPackage()
            {
                Identity = new LibraryIdentity()
                {
                    Name = id,
                    Version = version,
                    Type = LibraryTypes.Package
                }
            };
            packageBuilder(package);
            Packages.Add(package);

            return this;
        }
    }
}