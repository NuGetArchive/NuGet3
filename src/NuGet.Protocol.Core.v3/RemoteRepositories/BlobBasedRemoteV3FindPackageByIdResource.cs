// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Framework.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.Protocol.Core.v3.RemoteRepositories
{
    public class BlobBasedRemoteV3FindPackageByIdResource : FindPackageByIdResource
    {
        private readonly HttpSource _httpSource;
        private readonly Dictionary<string, Task<IEnumerable<PackageInfo>>> _packageInfoCache = new Dictionary<string, Task<IEnumerable<PackageInfo>>>();
        private readonly Dictionary<string, Task<NupkgEntry>> _nupkgCache = new Dictionary<string, Task<NupkgEntry>>();
        private bool _ignored;

        private TimeSpan _cacheAgeLimitList;
        private TimeSpan _cacheAgeLimitNupkg;

        public BlobBasedRemoteV3FindPackageByIdResource(Uri baseUri)
        {
            BaseUri = baseUri;
            _httpSource = new HttpSource(baseUri, userName: null, password: null);
        }

        public Uri BaseUri { get; }

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

        public bool IgnoreFailure { get; set; }

        public override async Task<IEnumerable<NuGetVersion>> GetAllVersionsAsync(string id, CancellationToken cancellationToken)
        {
            var packageInfos = await EnsurePackagesAsync(id, cancellationToken);
            return packageInfos.Select(p => p.Version);
        }

        public override async Task<FindPackageByIdDependencyInfo> GetDependencyInfoAsync(string id, NuGetVersion version, CancellationToken cancellationToken)
        {
            var packageInfos = await EnsurePackagesAsync(id, cancellationToken);
            var packageInfo = packageInfos.FirstOrDefault(p => p.Version == version);
            if (packageInfo == null)
            {
                Logger.LogWarning($"Unable to find package {id}{version}".Yellow());
                return null;
            }

            using (var stream = await PackageUtilities.OpenNuspecStreamFromNupkgAsync(
                packageInfo.Id,
                OpenNupkgStreamAsync(packageInfo, cancellationToken),
                Logger))
            {
                return GetDependencyInfo(new NuspecReader(stream));
            }
        }

        public override async Task<Stream> GetNupkgStreamAsync(string id, NuGetVersion version, CancellationToken cancellationToken)
        {
            var packageInfos = await EnsurePackagesAsync(id, cancellationToken);
            var packageInfo = packageInfos.FirstOrDefault(p => p.Version == version);
            if (packageInfo == null)
            {
                return null;
            }

            return await OpenNupkgStreamAsync(packageInfo, cancellationToken);
        }

        private Task<IEnumerable<PackageInfo>> EnsurePackagesAsync(string id, CancellationToken cancellationToken)
        {
            Task<IEnumerable<PackageInfo>> task;

            lock (_packageInfoCache)
            {
                if (!_packageInfoCache.TryGetValue(id, out task))
                {
                    task = FindPackagesByIdAsyncCore(id, cancellationToken);
                    _packageInfoCache[id] = task;
                }
            }

            return task;
        }

        private async Task<IEnumerable<PackageInfo>> FindPackagesByIdAsyncCore(string id, CancellationToken cancellationToken)
        {
            for (int retry = 0; retry != 3; ++retry)
            {
                if (_ignored)
                {
                    return Enumerable.Empty<PackageInfo>();
                }

                try
                {
                    var uri = id.ToLowerInvariant() + "/index.json";
                    var results = new List<PackageInfo>();
                    using (var data = await _httpSource.GetAsync(uri,
                        $"list_{id}",
                        retry == 0 ? _cacheAgeLimitList : TimeSpan.Zero,
                        ignoreNotFounds: true,
                        cancellationToken: cancellationToken))
                    {
                        if (data.Stream == null)
                        {
                            return Enumerable.Empty<PackageInfo>();
                        }

                        try
                        {
                            JObject doc;
                            using (var reader = new StreamReader(data.Stream))
                            {
                                doc = JObject.Load(new JsonTextReader(reader));
                            }

                            var result = doc["versions"]
                                .Select(x => BuildModel(id, x.ToString()))
                                .Where(x => x != null);

                            results.AddRange(result);
                        }
                        catch
                        {
                            Logger.LogInformation("The file {0} is corrupt", data.CacheFileName.Yellow().Bold());
                            throw;
                        }
                    }

                    return results;
                }
                catch (Exception ex) when (retry < 2)
                {
                    Logger.LogInformation($"Warning: FindPackagesById: {id}{Environment.NewLine}  {ex.Message}".Yellow().Bold());
                }
                catch (Exception ex) when (retry == 2)
                {
                    // Fail silently by returning empty result list
                    if (IgnoreFailure)
                    {
                        _ignored = true;
                        Logger.LogWarning(
                            $"Failed to retrieve information from remote source '{BaseUri}'".Yellow().Bold());
                        return Enumerable.Empty<PackageInfo>();
                    }

                    Logger.LogError($"Error: FindPackagesById: {id}{Environment.NewLine}  {ex.Message}".Red().Bold());
                    throw;
                }
            }

            return null;
        }

        private PackageInfo BuildModel(string id, string version)
        {
            return new PackageInfo
            {
                // If 'Id' element exist, use its value as accurate package Id
                // Otherwise, use the value of 'title' if it exist
                // Use the given Id as final fallback if all elements above don't exist
                Id = id,
                Version = NuGetVersion.Parse(version),
                ContentUri = BaseUri + id.ToLowerInvariant() + "/" + version.ToLowerInvariant() + "/" + id.ToLowerInvariant() + "." + version.ToLowerInvariant() + ".nupkg",
            };
        }

        private async Task<Stream> OpenNupkgStreamAsync(PackageInfo package, CancellationToken cancellationToken)
        {
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

        private async Task<NupkgEntry> OpenNupkgStreamAsyncCore(PackageInfo package, CancellationToken cancellationToken)
        {
            for (int retry = 0; retry != 3; ++retry)
            {
                try
                {
                    using (var data = await _httpSource.GetAsync(
                        package.ContentUri,
                        "nupkg_" + package.Id + "." + package.Version,
                        retry == 0 ? _cacheAgeLimitNupkg : TimeSpan.Zero,
                        cancellationToken))
                    {
                        return new NupkgEntry
                        {
                            TempFileName = data.CacheFileName
                        };
                    }
                }
                catch (Exception ex)
                {
                    if (retry == 2)
                    {
                        Logger.LogError(string.Format("Error: DownloadPackageAsync: {1}\r\n  {0}", ex.Message, package.ContentUri.Red().Bold()));
                    }
                    else
                    {
                        Logger.LogInformation(string.Format("Warning: DownloadPackageAsync: {1}\r\n  {0}".Yellow().Bold(), ex.Message, package.ContentUri.Yellow().Bold()));
                    }
                }
            }
            return null;
        }

        private class NupkgEntry
        {
            public string TempFileName { get; set; }
        }

        private class PackageInfo
        {
            public string Id { get; set; }

            public string Path { get; set; }

            public string ContentUri { get; set; }

            public NuGetVersion Version { get; set; }
        }
    }
}