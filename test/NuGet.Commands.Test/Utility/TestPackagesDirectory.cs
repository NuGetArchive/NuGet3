using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using NuGet.DependencyResolver;
using NuGet.Packaging;
using NuGet.RuntimeModel;
using NuGet.Versioning;

namespace NuGet.Commands.Test
{
    internal class TestPackagesDirectory : PackagesDirectory
    {
        public IList<RemoteMatch> Installed { get; } = new List<RemoteMatch>();
        public IList<TestPackage> Packages { get; } = new List<TestPackage>();

        public TestPackagesDirectory(IEnumerable<TestPackage> testPackages) : base()
        {
            DirectoryPath = @"C:\\Fake\Packages\Directory";

            Packages = testPackages.ToList();
        }

        public override Task InstallPackage(RemoteMatch match)
        {
            Installed.Add(match);
            return Task.FromResult(0);
        }

        public override RuntimeGraph LoadRuntimeGraph(string name, NuGetVersion version)
        {
            var package = Packages.FirstOrDefault(p => string.Equals(name, p.Identity.Name) && version == p.Identity.Version);
            if(package == null)
            {
                return RuntimeGraph.Empty;
            }
            return package.RuntimeGraph;
        }

        public bool IsInstalled(string packageId, string version)
        {
            var ver = NuGetVersion.Parse(version);
            return Installed.Any(m => string.Equals(m.Library.Name, packageId, StringComparison.Ordinal) && m.Library.Version == ver);
        }

        public override LocalPackageContent ReadPackage(string name, NuGetVersion version, SHA512 hashAlgorithm)
        {
            var package = Packages.FirstOrDefault(p => string.Equals(name, p.Identity.Name) && version == p.Identity.Version);
            if(package == null)
            {
                return null;
            }

            string sha512 = Convert.ToBase64String(hashAlgorithm.ComputeHash(Encoding.UTF8.GetBytes(name + "/" + version.ToNormalizedString())));

            var dependencies = package.DependencySets.Select(p =>
                new PackageDependencyGroup(p.Key, p.Value.Dependencies.Select(d =>
                    new Packaging.Core.PackageDependency(d.LibraryRange.Name, d.LibraryRange.VersionRange))));

            return new LocalPackageContent()
            {
                Sha512 = sha512,
                Files = package.FileNames,
                Dependencies = dependencies.ToList(),
                FrameworkAssemblies = package.FrameworkAssemblySets.Select(p => new FrameworkSpecificGroup(p.Key, p.Value)),
                References = package.ReferencedAssemblySets.Select(p => new FrameworkSpecificGroup(p.Key, p.Value))
            };
        }
    }
}