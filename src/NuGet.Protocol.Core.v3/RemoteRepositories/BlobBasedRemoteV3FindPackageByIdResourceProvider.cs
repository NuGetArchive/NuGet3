using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol.Core.v3.RemoteRepositories
{
    public class BlobBasedRemoteV3FindPackageByIdResourceProvider : ResourceProvider
    {
        public BlobBasedRemoteV3FindPackageByIdResourceProvider()
            : base(typeof(FindPackageByIdResource),
                   nameof(BlobBasedRemoteV3FindPackageByIdResourceProvider), 
                   before: nameof(RemoteV3FindPackagePackageByIdResourceProvider))
        {
        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository sourceRepository, CancellationToken token)
        {
            INuGetResource resource = null;

            if (sourceRepository.PackageSource.IsHttp &&
                (sourceRepository.PackageSource.ProtocolVersion == 3 ||
                sourceRepository.PackageSource.Source.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
            {
                var serviceIndexResource = await sourceRepository.GetResourceAsync<ServiceIndexResourceV3>();
                var packageBaseAddress = GetPackageBaseAddress(serviceIndexResource.Index);

                if (packageBaseAddress != null)
                {
                    resource = new BlobBasedRemoteV3FindPackageByIdResource(packageBaseAddress);
                }
            }

            return Tuple.Create(resource != null, resource);
        }

        private Uri GetPackageBaseAddress(JObject indexJson)
        {
            foreach (var resource in indexJson["resources"])
            {
                var type = resource.Value<string>("@type");
                var id = resource.Value<string>("@id");

                Uri uri;
                if (id != null && 
                    string.Equals(type, "PackageBaseAddress/3.0.0", StringComparison.Ordinal) &&
                    Uri.TryCreate(id, UriKind.Absolute, out uri))
                {
                    return uri;
                }
            }

            return null;
        }
    }
}