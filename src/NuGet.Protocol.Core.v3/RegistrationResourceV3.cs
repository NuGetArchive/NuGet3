// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3.DependencyInfo;
using NuGet.Versioning;

namespace NuGet.Protocol.Core.v3
{
    /// <summary>
    /// Registration blob reader
    /// </summary>
    public class RegistrationResourceV3 : INuGetResource
    {
        private readonly HttpClient _client;

        public RegistrationResourceV3(HttpClient client, Uri baseUrl)
        {
            if (client == null)
            {
                throw new ArgumentNullException("client");
            }

            if (baseUrl == null)
            {
                throw new ArgumentNullException("baseUrl");
            }

            _client = client;
            BaseUri = baseUrl;
        }

        /// <summary>
        /// Gets the <see cref="Uri"/> for the source backing this resource.
        /// </summary>
        public Uri BaseUri { get; }

        /// <summary>
        /// Returns the registration blob for the id and version
        /// </summary>
        /// <remarks>The inlined entries are potentially going away soon</remarks>
        public virtual async Task<JObject> GetPackageMetadata(PackageIdentity identity, CancellationToken token)
        {
            return (await GetPackageMetadata(identity.Id, new VersionRange(identity.Version, true, identity.Version, true), true, true, token)).SingleOrDefault();
        }

        /// <summary>
        /// Returns inlined catalog entry items for each registration blob
        /// </summary>
        /// <remarks>The inlined entries are potentially going away soon</remarks>
        public virtual async Task<IEnumerable<JObject>> GetPackageMetadata(string packageId, bool includePrerelease, bool includeUnlisted, CancellationToken token)
        {
            return await GetPackageMetadata(packageId, VersionRange.All, includePrerelease, includeUnlisted, token);
        }

        /// <summary>
        /// Returns inlined catalog entry items for each registration blob
        /// </summary>
        /// <remarks>The inlined entries are potentially going away soon</remarks>
        public virtual async Task<IEnumerable<JObject>> GetPackageMetadata(string packageId, VersionRange range, bool includePrerelease, bool includeUnlisted, CancellationToken token)
        {
            var results = new List<JObject>();

            var registrationUri = GetUri(packageId);

            var ranges = await Utils.LoadRanges(_client, registrationUri, range, token);

            foreach (var rangeObj in ranges)
            {
                if (rangeObj == null)
                {
                    throw new InvalidDataException(registrationUri.AbsoluteUri);
                }

                foreach (JObject packageObj in rangeObj["items"])
                {
                    var catalogEntry = (JObject)packageObj["catalogEntry"];
                    var version = NuGetVersion.Parse(catalogEntry["version"].ToString());

                    var listedToken = catalogEntry["listed"];

                    var listed = (listedToken != null) ? listedToken.Value<bool>() : true;

                    if (range.Satisfies(version)
                        && (includePrerelease || !version.IsPrerelease)
                        && (includeUnlisted || listed))
                    {
                        // add in the download url
                        if (packageObj["packageContent"] != null)
                        {
                            catalogEntry["packageContent"] = packageObj["packageContent"];
                        }

                        results.Add(catalogEntry);
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Returns all index entries of type Package within the given range and filters
        /// </summary>
        public virtual Task<IEnumerable<JObject>> GetPackageEntries(string packageId, bool includeUnlisted, CancellationToken token)
        {
            return GetPackageMetadata(packageId, VersionRange.All, true, includeUnlisted, token);
        }
    }
}
