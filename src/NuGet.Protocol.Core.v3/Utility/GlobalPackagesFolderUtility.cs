using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Logging;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol.Core.v3
{
    public static class GlobalPackagesFolderUtility
    {
        private const int ChunkSize = 1024 * 4; // 4KB

        public static DownloadResourceResult GetPackage(PackageIdentity packageIdentity, ISettings settings)
        {
            if (packageIdentity == null)
            {
                throw new ArgumentNullException(nameof(packageIdentity));
            }

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var globalPackagesFolder = SettingsUtility.GetGlobalPackagesFolder(settings);
            var defaultPackagePathResolver = new DefaultPackagePathResolver(globalPackagesFolder);
            var hashPath = defaultPackagePathResolver.GetHashPath(packageIdentity.Id, packageIdentity.Version);

            if (File.Exists(hashPath))
            {
                var installPath = defaultPackagePathResolver.GetInstallPath(packageIdentity.Id, packageIdentity.Version);
                var nupkgPath = defaultPackagePathResolver.GetPackageFilePath(packageIdentity.Id, packageIdentity.Version);
                Stream stream = null;
                PackageReaderBase packageReader = null;
                try
                {
                    stream = File.Open(nupkgPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    packageReader = new PackageFolderReader(installPath);
                    return new DownloadResourceResult(stream, packageReader);
                }
                catch
                {
                    if (stream != null)
                    {
                        stream.Dispose();
                    }

                    if (packageReader != null)
                    {
                        packageReader.Dispose();
                    }

                    throw;
                }
            }

            return null;
        }

        public static async Task<DownloadResourceResult> AddPackageAsync(PackageIdentity packageIdentity,
            Stream packageStream,
            ISettings settings,
            DownloadResource resource,
            int length,
            CancellationToken token)
        {
            if (packageIdentity == null)
            {
                throw new ArgumentNullException(nameof(packageIdentity));
            }

            if (packageStream == null)
            {
                throw new ArgumentNullException(nameof(packageStream));
            }

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var globalPackagesFolder = SettingsUtility.GetGlobalPackagesFolder(settings);

            // The following call adds it to the global packages folder. Addition is performed using ConcurrentUtils, such that,
            // multiple processes may add at the same time
            await InstallFromStreamWithProgressAsync(packageStream,
                packageIdentity,
                globalPackagesFolder,
                resource,
                settings,
                length,
                NullLogger.Instance,
                fixNuspecIdCasing: false,
                token: token);

            var package = GetPackage(packageIdentity, settings);
            Debug.Assert(package.PackageStream.CanSeek);
            Debug.Assert(package.PackageReader != null);

            return package;
        }

        public static async Task InstallFromStreamWithProgressAsync(
            Stream stream,
            PackageIdentity packageIdentity,
            string packagesDirectory,
            DownloadResource resource,
            ISettings settings,
            int length,
            ILogger log,
            bool fixNuspecIdCasing,
            CancellationToken token)
        {
#if DNXCORE50
            await NuGetPackageUtils.InstallFromStreamAsync(stream, packageIdentity, packagesDirectory, log, fixNuspecIdCasing, token: token);
#endif

            var packagePathResolver = new DefaultPackagePathResolver(packagesDirectory);

            var targetPath = packagePathResolver.GetInstallPath(packageIdentity.Id, packageIdentity.Version);
            var targetNuspec = packagePathResolver.GetManifestFilePath(packageIdentity.Id, packageIdentity.Version);
            var targetNupkg = packagePathResolver.GetPackageFilePath(packageIdentity.Id, packageIdentity.Version);
            var hashPath = packagePathResolver.GetHashPath(packageIdentity.Id, packageIdentity.Version);

            // Acquire the lock on a nukpg before we extract it to prevent the race condition when multiple
            // processes are extracting to the same destination simultaneously
            await ConcurrencyUtilities.ExecuteWithFileLocked(targetNupkg,
                action: async () =>
                {
                    // If this is the first process trying to install the target nupkg, go ahead
                    // After this process successfully installs the package, all other processes
                    // waiting on this lock don't need to install it again.
                    if (!File.Exists(targetNupkg))
                    {
                        log.LogInformation($"Installing {packageIdentity.Id} {packageIdentity.Version}");

                        Directory.CreateDirectory(targetPath);
                        using (var nupkgStream = new FileStream(
                            targetNupkg,
                            FileMode.Create,
                            FileAccess.ReadWrite,
                            FileShare.ReadWrite | FileShare.Delete,
                            bufferSize: 4096,
                            useAsync: true))
                        {
                            // We read the response stream chunk by chunk (each chunk is 4KB). 
                            // After reading each chunk, we report the progress based on the total number bytes read so far.
                            int totalReadSoFar = 0;
                            byte[] buffer = new byte[ChunkSize];

                            while (totalReadSoFar < length)
                            {
                                int bytesRead = stream.Read(buffer, 0, Math.Min(length - totalReadSoFar, ChunkSize));
                                if (bytesRead == 0)
                                {
                                    break;
                                }
                                else
                                {
                                    await nupkgStream.WriteAsync(buffer, 0, bytesRead);

                                    totalReadSoFar += bytesRead;
                                    resource.OnProgressAvailable(packageIdentity, settings, (double)totalReadSoFar / (double)length);
                                }
                            }

                            nupkgStream.Seek(0, SeekOrigin.Begin);

                            NuGetPackageUtils.ExtractPackage(targetPath, nupkgStream);
                        }

                        if (fixNuspecIdCasing)
                        {
                            // DNU REFACTORING TODO: delete the hacky FixNuSpecIdCasing() and uncomment logic below after we
                            // have implementation of NuSpecFormatter.Read()
                            // Fixup the casing of the nuspec on disk to match what we expect
                            var nuspecFile = Directory.EnumerateFiles(targetPath, "*" + NuGetPackageUtils.ManifestExtension).Single();
                            NuGetPackageUtils.FixNuSpecIdCasing(nuspecFile, targetNuspec, packageIdentity.Id);
                        }

                        using (var nupkgStream = File.Open(targetNupkg, FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            string packageHash;
                            using (var sha512 = SHA512.Create())
                            {
                                packageHash = Convert.ToBase64String(sha512.ComputeHash(nupkgStream));
                            }

                            // Note: PackageRepository relies on the hash file being written out as the final operation as part of a package install
                            // to assume a package was fully installed.
                            File.WriteAllText(hashPath, packageHash);
                        }
                    }

                    return 0;
                },
                token: token);
        }
    }
}
