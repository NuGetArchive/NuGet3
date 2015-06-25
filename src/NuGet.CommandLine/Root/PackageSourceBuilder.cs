using System.Collections.Generic;
using NuGet.Configuration;

namespace NuGet
{
    internal static class PackageSourceBuilder
    {
        internal static PackageSourceProvider CreateSourceProvider(ISettings settings)
        {
            var defaultPackageSource = new PackageSource(NuGetConstants.V2FeedUrl);

            var officialPackageSource = new PackageSource(NuGetConstants.V2FeedUrl, LocalizedResourceManager.GetString("OfficialPackageSourceName"));
            var v1PackageSource = new PackageSource(NuGetConstants.V1FeedUrl, LocalizedResourceManager.GetString("OfficialPackageSourceName"));
            var legacyV2PackageSource = new PackageSource(NuGetConstants.V2LegacyFeedUrl, LocalizedResourceManager.GetString("OfficialPackageSourceName"));

            var packageSourceProvider = new PackageSourceProvider(
                settings,
                new[] { defaultPackageSource },
                new Dictionary<PackageSource, PackageSource> { 
                            { v1PackageSource, officialPackageSource },
                            { legacyV2PackageSource, officialPackageSource }
                        }
            );
            return packageSourceProvider;
        }
    }
}
