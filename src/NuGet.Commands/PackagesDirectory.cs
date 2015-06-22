using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Logging;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3;
using NuGet.Repositories;

namespace NuGet.Commands
{
    /// <summary>
    /// Represents the location into which packages are installed
    /// </summary>
    public class PackagesDirectory
    {
        public string Path { get; }

        public PackagesDirectory(string path)
        {
            Path = path;
        }

        public virtual NuGetv3LocalRepository GetLocalRepository()
        {
            return new NuGetv3LocalRepository(Path, checkPackageIdCase: false);
        }

        public virtual SourceRepository GetSourceRepository()
        {
            return Repository.Factory.GetCoreV3(Path);
        }

        public virtual Task InstallPackage(Func<Stream, Task> copyToAsync, PackageIdentity packageIdentity, ILogger _log)
        {
            return NuGetPackageUtils.InstallFromSourceAsync(
                copyToAsync,
                packageIdentity,
                Path,
                _log,
                fixNuspecIdCasing: true,
                token: CancellationToken.None);
        }
    }
}
