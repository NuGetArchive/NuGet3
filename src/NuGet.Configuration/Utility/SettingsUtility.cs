// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;

namespace NuGet.Configuration
{
    public static class SettingsUtility
    {
        public static readonly string ConfigSection = "config";
        public static readonly string GlobalPackagesFolderKey = "globalPackagesFolder";
        public static readonly string GlobalPackagesFolderEnvironmentKey = "NUGET_PACKAGES";
        public static readonly string DefaultGlobalPackagesFolderPath = ".nuget\\packages\\";
        public static readonly string RepositoryPathKey = "repositoryPath";
        public static readonly string OfflineFeedKey = "offlineFeed";
        public static readonly string DefaultOfflineFeedFolderPath = ".nuget\\offlinefeed\\";

        public static string GetRepositoryPath(ISettings settings)
        {
            var path = settings.GetValue(ConfigSection, RepositoryPathKey, isPath: true);
            if (!String.IsNullOrEmpty(path))
            {
                path = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            }

            return path;
        }

        public static string GetDecryptedValue(ISettings settings, string section, string key, bool isPath = false)
        {
            if (String.IsNullOrEmpty(section))
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, "section");
            }

            if (String.IsNullOrEmpty(key))
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, "key");
            }

            var encryptedString = settings.GetValue(section, key, isPath);
            if (encryptedString == null)
            {
                return null;
            }

            if (String.IsNullOrEmpty(encryptedString))
            {
                return String.Empty;
            }
            return EncryptionUtility.DecryptString(encryptedString);
        }

        public static void SetEncryptedValue(ISettings settings, string section, string key, string value)
        {
            if (String.IsNullOrEmpty(section))
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, "section");
            }

            if (String.IsNullOrEmpty(key))
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, "key");
            }

            if (value == null)
            {
                throw new ArgumentNullException("value");
            }

            if (String.IsNullOrEmpty(value))
            {
                settings.SetValue(section, key, String.Empty);
            }
            else
            {
                var encryptedString = EncryptionUtility.EncryptString(value);
                settings.SetValue(section, key, encryptedString);
            }
        }

        /// <summary>
        /// Retrieves a config value for the specified key
        /// </summary>
        /// <param name="settings">The settings instance to retrieve </param>
        /// <param name="key">The key to look up</param>
        /// <param name="decrypt">Determines if the retrieved value needs to be decrypted.</param>
        /// <param name="isPath">Determines if the retrieved value is returned as a path.</param>
        /// <returns>Null if the key was not found, value from config otherwise.</returns>
        public static string GetConfigValue(ISettings settings, string key, bool decrypt = false, bool isPath = false)
        {
            return decrypt ?
                GetDecryptedValue(settings, ConfigSection, key, isPath) :
                settings.GetValue(ConfigSection, key, isPath);
        }

        /// <summary>
        /// Sets a config value in the setting.
        /// </summary>
        /// <param name="settings">The settings instance to store the key-value in.</param>
        /// <param name="key">The key to store.</param>
        /// <param name="value">The value to store.</param>
        /// <param name="encrypt">Determines if the value needs to be encrypted prior to storing.</param>
        public static void SetConfigValue(ISettings settings, string key, string value, bool encrypt = false)
        {
            if (encrypt == true)
            {
                SetEncryptedValue(settings, ConfigSection, key, value);
            }
            else
            {
                settings.SetValue(ConfigSection, key, value);
            }
        }

        /// <summary>
        /// Deletes a config value from settings
        /// </summary>
        /// <param name="settings">The settings instance to delete the key from.</param>
        /// <param name="key">The key to delete.</param>
        /// <returns>True if the value was deleted, false otherwise.</returns>
        public static bool DeleteConfigValue(ISettings settings, string key)
        {
            return settings.DeleteValue(ConfigSection, key);
        }

        public static string GetGlobalPackagesFolder(ISettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var path = Environment.GetEnvironmentVariable(GlobalPackagesFolderEnvironmentKey);
            if (string.IsNullOrEmpty(path))
            {
                // Environment variable for globalPackagesFolder is not set.
                // Try and get it from nuget settings

                // GlobalPackagesFolder path may be relative path. If so, it will be considered relative to
                // the solution directory, just like the 'repositoryPath' setting
                path = settings.GetValue(ConfigSection, GlobalPackagesFolderKey, isPath: false);
            }

            if (!string.IsNullOrEmpty(path))
            {
                path = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
                return path;
            }

#if !DNXCORE50
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
#else
            var userProfile = Environment.GetEnvironmentVariable("UserProfile");
#endif
            path = Path.Combine(userProfile, DefaultGlobalPackagesFolderPath);

            return path;
        }

        public static string GetOfflineFeed(ISettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var path = settings.GetValue(ConfigSection, OfflineFeedKey, isPath: false);
            if (!string.IsNullOrEmpty(path))
            {
                path = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
                ThrowIfNotValid(path, isAbsolute: true);
                return path;
            }

#if !DNXCORE50
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
#else
            var userProfile = Environment.GetEnvironmentVariable("UserProfile");
#endif
            path = Path.Combine(userProfile, DefaultOfflineFeedFolderPath);

            ThrowIfNotValid(path, isAbsolute: true);
            return path;
        }

        private static void ThrowIfNotValid(string path, bool isAbsolute)
        {
            Uri tempUri;
            if (isAbsolute && !Uri.TryCreate(path, UriKind.Absolute, out tempUri))
            {
                throw new ArgumentException(string.Format(Resources.Error_InvalidPathOrNotAbsolute, path));
            }

            if (!Uri.TryCreate(path, UriKind.RelativeOrAbsolute, out tempUri))
            {
                throw new ArgumentException(string.Format(Resources.Error_InvalidPath, path));
            }
        }
    }
}
