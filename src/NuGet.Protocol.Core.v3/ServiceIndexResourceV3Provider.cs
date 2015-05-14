// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3.Data;
using NuGet.Versioning;

namespace NuGet.Protocol.Core.v3
{
    /// <summary>
    /// Retrieves and caches service index.json files
    /// ServiceIndexResourceV3 stores the json, all work is done in the provider
    /// </summary>
    public class ServiceIndexResourceV3Provider : ResourceProvider
    {
        private readonly ConcurrentDictionary<string, ServiceIndexResourceV3> _cache;

        public ServiceIndexResourceV3Provider()
            : base(typeof(ServiceIndexResourceV3), "ServiceIndexResourceV3Provider", NuGetResourceProviderPositions.Last)
        {
            _cache = new ConcurrentDictionary<string, ServiceIndexResourceV3>();
        }

        // TODO: refresh the file when it gets old
        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            ServiceIndexResourceV3 index = null;

            var url = source.PackageSource.Source;

            // the file type can easily rule out if we need to request the url
            if (source.PackageSource.ProtocolVersion == 3 ||

                (source.PackageSource.IsHttp &&
                url.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
            {
                // check the cache before downloading the file
                if (!_cache.TryGetValue(url, out index))
                {
                    var messageHandlerResource = await source.GetResourceAsync<HttpHandlerResource>(token);

                    var client = new DataClient(messageHandlerResource.MessageHandler);

                    JObject json;

                    try
                    {
                        json = await client.GetJObjectAsync(new Uri(url), token);
                    }
                    catch (JsonReaderException)
                    {
                        _cache.TryAdd(url, value: null);
                        return new Tuple<bool, INuGetResource>(index != null, index);
                    }
                    catch (HttpRequestException)
                    {
                        _cache.TryAdd(url, value: null);
                        return new Tuple<bool, INuGetResource>(index != null, index);
                    }

                    if (json != null)
                    {
                        // Use SemVer instead of NuGetVersion, the service index should always be
                        // in strict SemVer format
                        SemanticVersion version;
                        JToken versionToken;
                        if (json.TryGetValue("version", out versionToken) &&
                            versionToken.Type == JTokenType.String &&
                            SemanticVersion.TryParse((string)versionToken, out version) &&
                            version.Major == 3)
                        {
                            index = new ServiceIndexResourceV3(json, DateTime.UtcNow);
                        }
                    }
                }

                // cache the value even if it is null to avoid checking it again later
                _cache.TryAdd(url, index);
            }

            return new Tuple<bool, INuGetResource>(index != null, index);
        }
    }
}
