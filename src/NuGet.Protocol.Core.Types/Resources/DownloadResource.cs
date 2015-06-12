// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.Packaging.Core;

namespace NuGet.Protocol.Core.Types
{
    /// <summary>
    /// Finds the download url of a nupkg
    /// </summary>
    public abstract class DownloadResource : INuGetResource
    {
        public abstract Task<DownloadResourceResult> GetDownloadResourceResultAsync(PackageIdentity identity, ISettings settings, CancellationToken token);

        public event EventHandler<PackageProgressEventArgs> Progress;

        public void OnProgressAvailable(PackageIdentity identity, ISettings settings, double percentage)
        {
            string sourceName = string.Empty;

            if (settings != null)
            {
                var sourceProvider = new PackageSourceProvider(settings);
                sourceName = sourceProvider?.ActivePackageSourceName ?? string.Empty;
            }

            var packageSource = new PackageSource(sourceName);

            if (Progress != null)
            {
                Progress(this, new PackageProgressEventArgs(identity, packageSource, percentage));
            }
        }
    }
}
