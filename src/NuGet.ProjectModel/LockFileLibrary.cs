// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace NuGet.ProjectModel
{
    public class LockFileLibrary
    {
        public string Name { get; set; }

        public NuGetVersion Version { get; set; }

        public bool IsServiceable { get; set; }

        public string Sha512 { get; set; }

        public IList<string> Files { get; set; } = new List<string>();
    }

    public class LockFileTarget
    {
        public NuGetFramework TargetFramework { get; set; }

        public string RuntimeIdentifier { get; set; }

        public IList<LockFileTargetLibrary> Libraries { get; set; } = new List<LockFileTargetLibrary>();
    }

    public class LockFileTargetLibrary
    {
        public string Name { get; set; }

        public NuGetVersion Version { get; set; }

        public IList<PackageDependency> Dependencies { get; set; } = new List<PackageDependency>();

        public IList<string> FrameworkAssemblies { get; set; } = new List<string>();

        public IList<LockFileItem> RuntimeAssemblies { get; set; } = new List<LockFileItem>();
        
        public IList<LockFileItem> ResourceAssemblies { get; set; } = new List<LockFileItem>();

        public IList<LockFileItem> CompileTimeAssemblies { get; set; } = new List<LockFileItem>();

        public IList<LockFileItem> NativeLibraries { get; set; } = new List<LockFileItem>();
    }
    public class LockFileItem
    {
        public LockFileItem(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public IDictionary<string, string> Properties { get; } = new Dictionary<string, string>();

        public override string ToString() => Path;
    }
}
