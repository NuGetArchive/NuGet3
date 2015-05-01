using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.ProjectModel;

namespace NuGet.Commands.Test
{
    internal class TestPackageSpecResolver : IPackageSpecResolver
    {
        private IDictionary<string, PackageSpec> _projects;

        public TestPackageSpecResolver(IEnumerable<PackageSpec> projects)
        {
            _projects = projects.ToDictionary(p => p.Name);
        }

        public IEnumerable<string> SearchPaths
        {
            get
            {
                return Enumerable.Empty<string>();
            }
        }

        public bool TryResolvePackageSpec(string name, out PackageSpec packageSpec)
        {
            return _projects.TryGetValue(name, out packageSpec);
        }
    }
}