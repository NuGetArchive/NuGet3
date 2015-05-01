using System.Collections.Generic;
using NuGet.Packaging;

namespace NuGet.Commands
{
    public class LocalPackageContent
    {
        public IEnumerable<string> Files { get; set; }
        public string Sha512 { get; set; }
        public IEnumerable<PackageDependencyGroup> Dependencies { get; set; }
        public IEnumerable<FrameworkSpecificGroup> FrameworkAssemblies { get; set; }
        public IEnumerable<FrameworkSpecificGroup> References { get; set; }
    }
}