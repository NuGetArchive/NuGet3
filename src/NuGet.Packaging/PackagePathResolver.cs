// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using NuGet.Packaging.Core;

namespace NuGet.Packaging
{
    public class PackagePathResolver
    {
        private readonly bool _useSideBySidePaths;
        private readonly string _rootDirectory;

        public PackagePathResolver(string rootDirectory, bool useSideBySidePaths = true)
        {
            if (String.IsNullOrEmpty(rootDirectory))
            {
                throw new ArgumentException(String.Format(Strings.StringCannotBeNullOrEmpty, nameof(rootDirectory)));
            }
            _rootDirectory = rootDirectory;
            _useSideBySidePaths = useSideBySidePaths;
        }

        protected internal string Root => _rootDirectory;

        public virtual string GetPackageDirectoryName(PackageIdentity packageIdentity)
        {
            var directoryName = packageIdentity.Id;
            if (_useSideBySidePaths)
            {
                directoryName += "." + packageIdentity.Version.ToString();
            }

            return directoryName;
        }

        public virtual string GetPackageFileName(PackageIdentity packageIdentity)
        {
            return GetPackageDirectoryName(packageIdentity) + PackagingCoreConstants.NupkgExtension;
        }

        public virtual string GetInstallPath(PackageIdentity packageIdentity)
        {
            return Path.Combine(_rootDirectory, GetPackageDirectoryName(packageIdentity));
        }

        public virtual string GetInstalledPath(PackageIdentity packageIdentity)
        {
            var installedPackageFilePath = GetInstalledPackageFilePath(packageIdentity);
            return String.IsNullOrEmpty(installedPackageFilePath) ? null : Path.GetDirectoryName(installedPackageFilePath);
        }

        public virtual string GetInstalledPackageFilePath(PackageIdentity packageIdentity)
        {
            return PackagePathHelper.GetInstalledPackageFilePath(packageIdentity, this);
        }
    }
}
