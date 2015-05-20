// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;

namespace NuGet.Packaging
{
    public class PackageExtractionContext
    {
        // TODO: Move PackagePathResolver into this context as well
        public bool CopySatelliteFiles { get; set; }

        public PackageExtractionContext()
        {
            CopySatelliteFiles = false;
        }
    }

    [Flags]
    public enum PackageSaveModes
    {
        None = 0,
        Nuspec = 1,

        [SuppressMessage(
            "Microsoft.Naming",
            "CA1704:IdentifiersShouldBeSpelledCorrectly",
            MessageId = "Nupkg",
            Justification = "nupkg is the file extension of the package file")]
        Nupkg = 2
    }
}
