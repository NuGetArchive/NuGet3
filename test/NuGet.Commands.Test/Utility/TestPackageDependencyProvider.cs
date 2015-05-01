using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.DependencyResolver;
using NuGet.Frameworks;
using NuGet.LibraryModel;

namespace NuGet.Commands.Test
{
    internal class TestPackageDependencyProvider : IRemoteDependencyProvider
    {
        private readonly Dictionary<string, List<TestPackage>> _libraries;

        public bool IsHttp { get { return true; } }

        public TestPackageDependencyProvider(IEnumerable<TestPackage> libraries)
        {
            _libraries = libraries.GroupBy(l => l.Identity.Name).ToDictionary(g => g.Key, g => g.ToList());
        }

        public Task<LibraryIdentity> FindLibraryAsync(LibraryRange libraryRange, NuGetFramework targetFramework, CancellationToken cancellationToken)
        {
            List<TestPackage> libraries;
            if(!_libraries.TryGetValue(libraryRange.Name, out libraries))
            {
                return Task.FromResult((LibraryIdentity)null);
            }

            var version = libraryRange.VersionRange.FindBestMatch(libraries.Select(l => l.Identity.Version));
            return Task.FromResult(libraries.Select(l => l.Identity).FirstOrDefault(l => l.Version == version));
        }

        public Task<IEnumerable<LibraryDependency>> GetDependenciesAsync(LibraryIdentity match, NuGetFramework targetFramework, CancellationToken cancellationToken)
        {
            List<TestPackage> libraries;
            if(!_libraries.TryGetValue(match.Name, out libraries))
            {
                return Task.FromResult(Enumerable.Empty<LibraryDependency>());
            }
            var library = libraries.FirstOrDefault(l => l.Identity.Version == match.Version);
            if(library == null)
            {
                return Task.FromResult(Enumerable.Empty<LibraryDependency>());
            }

            TestDependencyGroupBuilder builder;
            if (!library.DependencySets.TryGetValue(targetFramework, out builder) && !library.DependencySets.TryGetValue(new NuGetFramework(FrameworkConstants.SpecialIdentifiers.Any), out builder))
            {
                return Task.FromResult(Enumerable.Empty<LibraryDependency>());
            }

            return Task.FromResult<IEnumerable<LibraryDependency>>(builder.Dependencies);
        }

        public Task CopyToAsync(LibraryIdentity match, Stream stream, CancellationToken cancellationTokqen)
        {
            throw new NotImplementedException();
        }
    }
}