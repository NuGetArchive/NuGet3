﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Framework.Logging;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.Protocol.Core.v3.RemoteRepositories
{
    public class RemoteV3FindPackageByIdResource : FindPackageByIdResource
    {
        private readonly SemaphoreSlim _dependencyInfoSemaphore = new SemaphoreSlim(initialCount: 1);
        private readonly Dictionary<string, Task<IEnumerable<RemoteSourceDependencyInfo>>> _packageVersionsCache =
            new Dictionary<string, Task<IEnumerable<RemoteSourceDependencyInfo>>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Task<NupkgEntry>> _nupkgCache = new Dictionary<string, Task<NupkgEntry>>(StringComparer.OrdinalIgnoreCase);
        private readonly HttpSource _httpSource;

        private DependencyInfoResource _dependencyInfoResource;
        private TimeSpan _cacheAgeLimitList;
        private TimeSpan _cacheAgeLimitNupkg;

        public RemoteV3FindPackageByIdResource(SourceRepository sourceRepository)
        {
            SourceRepository = sourceRepository;
            _httpSource = new HttpSource(sourceRepository.PackageSource);
        }

        public SourceRepository SourceRepository { get; }

        public override ILogger Logger
        {
            get
            {
                return base.Logger;
            }
            set
            {
                base.Logger = value;
                _httpSource.Logger = value;
            }
        }

        public override bool NoCache
        {
            get
            {
                return base.NoCache;
            }
            set
            {
                base.NoCache = value;
                if (value)
                {
                    _cacheAgeLimitList = TimeSpan.Zero;
                    _cacheAgeLimitNupkg = TimeSpan.Zero;
                }
                else
                {
                    _cacheAgeLimitList = TimeSpan.FromMinutes(30);
                    _cacheAgeLimitNupkg = TimeSpan.FromHours(24);
                }
            }
        }

        public override async Task<IEnumerable<NuGetVersion>> GetAllVersionsAsync(string id, CancellationToken cancellationToken)
        {
            var result = await EnsurePackagesAsync(id, cancellationToken);
            return result.Select(item => item.Identity.Version);
        }

        public override async Task<FindPackageByIdDependencyInfo> GetDependencyInfoAsync(string id, NuGetVersion version, CancellationToken cancellationToken)
        {
            var packageInfos = await EnsurePackagesAsync(id, cancellationToken);
            var packageInfo = packageInfos.FirstOrDefault(p => p.Identity.Version == version);
            if (packageInfo == null)
            {
                return null;
            }

            using (var stream = await PackageUtilities.OpenNuspecStreamFromNupkgAsync(
                packageInfo.Identity.Id,
                OpenNupkgStreamAsync(packageInfo, cancellationToken),
                Logger))
            {
                return GetDependencyInfo(new NuspecReader(stream));
            }
        }

        public override async Task<Stream> GetNupkgStreamAsync(string id, NuGetVersion version, CancellationToken cancellationToken)
        {
            var packageInfos = await EnsurePackagesAsync(id, cancellationToken);
            var packageInfo = packageInfos.FirstOrDefault(p => p.Identity.Version == version);
            if (packageInfo == null)
            {
                return null;
            }

            return await OpenNupkgStreamAsync(packageInfo, cancellationToken);
        }

        private Task<IEnumerable<RemoteSourceDependencyInfo>> EnsurePackagesAsync(string id, CancellationToken cancellationToken)
        {
            Task<IEnumerable<RemoteSourceDependencyInfo>> task;

            lock (_packageVersionsCache)
            {
                if (!_packageVersionsCache.TryGetValue(id, out task))
                {
                    task = FindPackagesByIdAsyncCore(id, cancellationToken);
                    _packageVersionsCache[id] = task;
                }
            }

            return task;
        }

        private async Task<IEnumerable<RemoteSourceDependencyInfo>> FindPackagesByIdAsyncCore(string id, CancellationToken cancellationToken)
        {
            // This is invoked from inside a lock.
            await EnsureDependencyProvider(cancellationToken);

            return await _dependencyInfoResource.ResolvePackages(id, cancellationToken);
        }

        private async Task EnsureDependencyProvider(CancellationToken cancellationToken)
        {
            if (_dependencyInfoResource == null)
            {
                try
                {
                    await _dependencyInfoSemaphore.WaitAsync(cancellationToken);
                    if (_dependencyInfoResource == null)
                    {
                        _dependencyInfoResource = await SourceRepository.GetResourceAsync<DependencyInfoResource>();
                    }
                }
                finally
                {
                    _dependencyInfoSemaphore.Release();
                }

            }
        }

        private async Task<Stream> OpenNupkgStreamAsync(RemoteSourceDependencyInfo package, CancellationToken cancellationToken)
        {
            await EnsureDependencyProvider(cancellationToken);

            Task<NupkgEntry> task;
            lock (_nupkgCache)
            {
                if (!_nupkgCache.TryGetValue(package.ContentUri, out task))
                {
                    task = _nupkgCache[package.ContentUri] = OpenNupkgStreamAsyncCore(package, cancellationToken);
                }
            }

            var result = await task;
            if (result == null)
            {
                return null;
            }

            // Acquire the lock on a file before we open it to prevent this process
            // from opening a file deleted by the logic in HttpSource.GetAsync() in another process
            return await ConcurrencyUtilities.ExecuteWithFileLocked(result.TempFileName, _ =>
            {
                return Task.FromResult(
                    new FileStream(result.TempFileName, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete));
            });
        }

        private async Task<NupkgEntry> OpenNupkgStreamAsyncCore(RemoteSourceDependencyInfo package, CancellationToken cancellationToken)
        {
            for (int retry = 0; retry != 3; ++retry)
            {
                try
                {
                    using (var data = await _httpSource.GetAsync(
                        package.ContentUri,
                        "nupkg_" + package.Identity.Id + "." + package.Identity.Version.ToNormalizedString(),
                        retry == 0 ? _cacheAgeLimitNupkg : TimeSpan.Zero,
                        cancellationToken))
                    {
                        return new NupkgEntry
                        {
                            TempFileName = data.CacheFileName
                        };
                    }
                }
                catch (Exception ex) when (retry < 2)
                {
                    var message = Strings.FormatLog_FailedToDownloadPackage(package.ContentUri) + Environment.NewLine + ex.Message;
                    Logger.LogInformation(message.Yellow().Bold());

                }
                catch (Exception ex) when (retry == 2)
                {
                    var message = Strings.FormatLog_FailedToDownloadPackage(package.ContentUri) + Environment.NewLine + ex.Message;
                    Logger.LogError(message.Red().Bold());
                }
            }
            return null;
        }

        private class NupkgEntry
        {
            public string TempFileName { get; set; }
        }
    }
}