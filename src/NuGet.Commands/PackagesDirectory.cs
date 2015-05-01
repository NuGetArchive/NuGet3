using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Framework.Logging;
using NuGet.DependencyResolver;
using NuGet.Packaging;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3;
using NuGet.Repositories;
using NuGet.RuntimeModel;
using NuGet.Versioning;

namespace NuGet.Commands
{
    public class PackagesDirectory
    {
        private readonly NuGetv3LocalRepository _localRepository;

        private ConcurrentDictionary<Tuple<string, NuGetVersion>, LocalPackageContent> _contentCache = new ConcurrentDictionary<Tuple<string, NuGetVersion>, LocalPackageContent>();

        public SourceRepository SourceRepository { get; }
        public string DirectoryPath { get; protected set; }

        protected PackagesDirectory() { }
        public PackagesDirectory(string packageDirectory) : this()
        {
            DirectoryPath = packageDirectory;
            SourceRepository = Repository.Factory.GetCoreV3(packageDirectory);
            _localRepository = new NuGetv3LocalRepository(packageDirectory, checkPackageIdCase: true);
        }

        public virtual async Task InstallPackage(RemoteMatch match)
        {
            using (var memoryStream = new MemoryStream())
            {
                await match.Provider.CopyToAsync(match.Library, memoryStream, CancellationToken.None);

                memoryStream.Seek(0, SeekOrigin.Begin);
                await NuGetPackageUtils.InstallFromStream(memoryStream, match.Library, DirectoryPath);
            }
        }

        public virtual RuntimeGraph LoadRuntimeGraph(string name, NuGetVersion version)
        {
            var package = _localRepository.FindPackagesById(name).FirstOrDefault(p => p.Version == version);
            if (package != null)
            {
                return LoadRuntimeGraph(package);
            }
            return null;
        }

        public virtual LocalPackageContent ReadPackage(string name, NuGetVersion version, SHA512 hashAlgorithm)
        {
            return _contentCache.GetOrAdd(Tuple.Create(name, version), _ =>
            {
                var localPackage = _localRepository.FindPackagesById(name).FirstOrDefault(p => p.Version == version);
                if (localPackage == null)
                {
                    return null;
                }

                using (var nupkgStream = File.OpenRead(localPackage.ZipPath))
                {
                    // Compute the hash
                    var sha512 = Convert.ToBase64String(hashAlgorithm.ComputeHash(nupkgStream));
                    nupkgStream.Seek(0, SeekOrigin.Begin);

                    // Read the contents
                    var reader = new PackageReader(nupkgStream);
                    return new LocalPackageContent()
                    {
                        Sha512 = sha512,
                        Files = reader.GetFiles(),
                        References = reader.GetReferenceItems(),
                        Dependencies = reader.GetPackageDependencies(),
                        FrameworkAssemblies = reader.GetFrameworkItems()
                    };
                }
            });
        }

        private RuntimeGraph LoadRuntimeGraph(LocalPackageInfo package)
        {
            var runtimeGraphFile = Path.Combine(package.ExpandedPath, RuntimeGraph.RuntimeGraphFileName);
            if (File.Exists(runtimeGraphFile))
            {
                using (var stream = new FileStream(runtimeGraphFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    return JsonRuntimeFormat.ReadRuntimeGraph(stream);
                }
            }
            return null;
        }
    }
}