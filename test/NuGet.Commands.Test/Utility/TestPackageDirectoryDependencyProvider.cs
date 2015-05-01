using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.DependencyResolver;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging;

namespace NuGet.Commands.Test
{
    internal class TestPackageDirectoryDependencyProvider : IRemoteDependencyProvider
    {
        private TestPackagesDirectory _packagesDirectory;

        public TestPackageDirectoryDependencyProvider(TestPackagesDirectory packagesDirectory)
        {
            _packagesDirectory = packagesDirectory;
        }

        public bool IsHttp
        {
            get
            {
                return false;
            }
        }

        public Task<LibraryIdentity> FindLibraryAsync(LibraryRange libraryRange, NuGetFramework targetFramework, CancellationToken cancellationToken)
        {
            var packages = _packagesDirectory.Packages.Where(p => string.Equals(p.Identity.Name, libraryRange.Name));
            var version = libraryRange.VersionRange.FindBestMatch(packages.Select(p => p.Identity.Version));
            return Task.FromResult(packages.Select(p => p.Identity).FirstOrDefault(i => i.Version == version));
        }

        public Task<IEnumerable<LibraryDependency>> GetDependenciesAsync(LibraryIdentity match, NuGetFramework targetFramework, CancellationToken cancellationToken)
        {
            var package = _packagesDirectory.Packages.FirstOrDefault(p => string.Equals(p.Identity.Name, match.Name) && p.Identity.Version == match.Version);
            if(package == null)
            {
                throw new InvalidOperationException("Unknown test package: " + match.Name + "/" + match.Version);
            }

            TestDependencyGroupBuilder group;
            if(!package.DependencySets.TryGetValue(targetFramework, out group) && !package.DependencySets.TryGetValue(new NuGetFramework(FrameworkConstants.SpecialIdentifiers.Any), out group))
            {
                return Task.FromResult(Enumerable.Empty<LibraryDependency>());
            }
            return Task.FromResult<IEnumerable<LibraryDependency>>(group.Dependencies);
        }

        public Task CopyToAsync(LibraryIdentity match, Stream stream, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}