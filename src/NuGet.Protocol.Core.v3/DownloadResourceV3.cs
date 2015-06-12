// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol.Core.v3
{
    /// <summary>
    /// Provides the download metatdata for a given package from a V3 server endpoint.
    /// </summary>
    public class DownloadResourceV3 : DownloadResource
    {
        private readonly RegistrationResourceV3 _regResource;
        private readonly HttpClient _client;

        public DownloadResourceV3(HttpClient client, RegistrationResourceV3 regResource)
        {
            if (client == null)
            {
                throw new ArgumentNullException("client");
            }

            if (regResource == null)
            {
                throw new ArgumentNullException("regResource");
            }

            _regResource = regResource;
            _client = client;
        }

        private async Task<Uri> GetDownloadUrl(PackageIdentity identity, CancellationToken token)
        {
            Uri downloadUri = null;

            var blob = await _regResource.GetPackageMetadata(identity, token);

            if (blob != null
                && blob["packageContent"] != null)
            {
                downloadUri = new Uri(blob["packageContent"].ToString());
            }

            return downloadUri;
        }

        public override async Task<DownloadResourceResult> GetDownloadResourceResultAsync(PackageIdentity identity,
            ISettings settings,
            CancellationToken token)
        {
            if (identity == null)
            {
                throw new ArgumentNullException(nameof(identity));
            }

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }
            
            Uri uri = await GetDownloadUrl(identity, token);
            if (uri != null)
            {
                // Uri is not null, so the package exists in the source
                // Now, check if it is in the global packages folder, before, getting the package stream
                var packageFromGlobalPackages = GlobalPackagesFolderUtility.GetPackage(identity, settings);

                if (packageFromGlobalPackages != null)
                {
                    return packageFromGlobalPackages;
                }

                OnProgressAvailable(identity, settings, 0.0);

                using (var packageStream = await _client.GetStreamAsync(uri))
                {
                    int length = 0;
#if !DNXCORE50
                    // Get the length of the content
                    var request = WebRequest.Create(uri);
                    var response = request.GetResponse();
                    length = (int)response.ContentLength;
#endif

                    var downloadResult = await GlobalPackagesFolderUtility.AddPackageAsync(identity, packageStream, settings, this, length);
                    OnProgressAvailable(identity, settings, 1.0);

                    return downloadResult;
                }
            }

            return null;
        }
    }
}
