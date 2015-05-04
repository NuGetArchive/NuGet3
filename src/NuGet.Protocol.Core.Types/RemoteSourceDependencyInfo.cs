using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NuGet.Packaging;
using NuGet.Packaging.Core;

namespace NuGet.Protocol.Core.Types
{
    /// <summary>
    /// A collection of package dependency groups with the content (nupkg url).
    /// </summary>
    public class RemoteSourceDependencyInfo : IEquatable<RemoteSourceDependencyInfo>
    {
        /// <summary>
        /// DependencyInfo
        /// </summary>
        /// <param name="identity">package identity</param>
        /// <param name="dependencyGroups">package dependency groups</param>
        /// <param name="frameworkReferenceGroups">Sequence of <see cref="FrameworkSpecificGroup"/>s.</param>
        public RemoteSourceDependencyInfo(
            PackageIdentity identity, 
            IEnumerable<PackageDependencyGroup> dependencyGroups)
        {
            if (identity == null)
            {
                throw new ArgumentNullException(nameof(identity));
            }

            if (dependencyGroups == null)
            {
                throw new ArgumentNullException(nameof(dependencyGroups));
            }

            DependencyGroups = dependencyGroups.ToList();
        }

        /// <summary>
        /// Package identity
        /// </summary>
        public PackageIdentity Identity { get; }

        /// <summary>
        /// Package dependency groups
        /// </summary>
        public IEnumerable<PackageDependencyGroup> DependencyGroups { get; }

        /// <summary>
        /// The content url of this resource.
        /// </summary>
        public string ContentUri { get; }

        public bool Equals(RemoteSourceDependencyInfo other)
        {
            return other != null &&
                Identity.Equals(other.Identity) &&
                new HashSet<PackageDependencyGroup>(DependencyGroups).SetEquals(other.DependencyGroups) &&
                string.Equals(ContentUri, other.ContentUri, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => Equals(obj as PackageDependencyInfo);

        public override int GetHashCode()
        {
            var combiner = HashCodeCombiner.Start();

            combiner.AddObject(Identity);

            foreach (int hash in DependencyGroups.Select(group => group.GetHashCode()).OrderBy(x => x))
            {
                combiner.AddInt32(hash);
            }

            combiner.AddObject(ContentUri);

            return combiner.CombinedHash;
        }

        public override string ToString()
        {
            return String.Format(CultureInfo.InvariantCulture, "{0} : {1}", Identity, String.Join(" ,", DependencyGroups));
        }
    }
}
