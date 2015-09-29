// <auto-generated />
namespace NuGet.Configuration
{
    using System.Globalization;
    using System.Reflection;
    using System.Resources;

    internal static class Resources
    {
        private static readonly ResourceManager _resourceManager
            = new ResourceManager("NuGet.Configuration.Resources", typeof(Resources).GetTypeInfo().Assembly);

        /// <summary>
        /// Value cannot be null or empty string.
        /// </summary>
        internal static string Argument_Cannot_Be_Null_Or_Empty
        {
            get { return GetString("Argument_Cannot_Be_Null_Or_Empty"); }
        }

        /// <summary>
        /// Value cannot be null or empty string.
        /// </summary>
        internal static string FormatArgument_Cannot_Be_Null_Or_Empty()
        {
            return GetString("Argument_Cannot_Be_Null_Or_Empty");
        }

        /// <summary>
        /// There are no writable config files.
        /// </summary>
        internal static string Error_NoWritableConfig
        {
            get { return GetString("Error_NoWritableConfig"); }
        }

        /// <summary>
        /// There are no writable config files.
        /// </summary>
        internal static string FormatError_NoWritableConfig()
        {
            return GetString("Error_NoWritableConfig");
        }

        /// <summary>
        /// File '{0}' does not exist.
        /// </summary>
        internal static string FileDoesNotExist
        {
            get { return GetString("FileDoesNotExist"); }
        }

        /// <summary>
        /// File '{0}' does not exist.
        /// </summary>
        internal static string FormatFileDoesNotExist(object p0)
        {
            return string.Format(CultureInfo.CurrentCulture, GetString("FileDoesNotExist"), p0);
        }

        /// <summary>
        /// "{0}" cannot be called on a NullSettings. This may be caused on account of insufficient permissions to read or write to "%AppData%\NuGet\NuGet.config".
        /// </summary>
        internal static string InvalidNullSettingsOperation
        {
            get { return GetString("InvalidNullSettingsOperation"); }
        }

        /// <summary>
        /// "{0}" cannot be called on a NullSettings. This may be caused on account of insufficient permissions to read or write to "%AppData%\NuGet\NuGet.config".
        /// </summary>
        internal static string FormatInvalidNullSettingsOperation(object p0)
        {
            return string.Format(CultureInfo.CurrentCulture, GetString("InvalidNullSettingsOperation"), p0);
        }

        /// <summary>
        /// The package source does not belong to the collection of available sources.
        /// </summary>
        internal static string PackageSource_Invalid
        {
            get { return GetString("PackageSource_Invalid"); }
        }

        /// <summary>
        /// The package source does not belong to the collection of available sources.
        /// </summary>
        internal static string FormatPackageSource_Invalid()
        {
            return GetString("PackageSource_Invalid");
        }

        /// <summary>
        /// Parameter 'fileName' to Settings must be just a fileName and not a path
        /// </summary>
        internal static string Settings_FileName_Cannot_Be_A_Path
        {
            get { return GetString("Settings_FileName_Cannot_Be_A_Path"); }
        }

        /// <summary>
        /// Parameter 'fileName' to Settings must be just a fileName and not a path
        /// </summary>
        internal static string FormatSettings_FileName_Cannot_Be_A_Path()
        {
            return GetString("Settings_FileName_Cannot_Be_A_Path");
        }

        /// <summary>
        /// Hash algorithm '{0}' is unsupported. Supported algorithms include: SHA512 and SHA256.
        /// </summary>
        internal static string UnsupportedHashAlgorithm
        {
            get { return GetString("UnsupportedHashAlgorithm"); }
        }

        /// <summary>
        /// Hash algorithm '{0}' is unsupported. Supported algorithms include: SHA512 and SHA256.
        /// </summary>
        internal static string FormatUnsupportedHashAlgorithm(object p0)
        {
            return string.Format(CultureInfo.CurrentCulture, GetString("UnsupportedHashAlgorithm"), p0);
        }

        /// <summary>
        /// The environment variable 'UserProfile' is empty or invalid. Set 'NUGET_PACKAGES' to a valid directory to override the global packages folder location.
        /// </summary>
        internal static string UserProfileMissingUseNuGetPackages
        {
            get { return GetString("UserProfileMissingUseNuGetPackages"); }
        }

        /// <summary>
        /// The environment variable 'UserProfile' is empty or invalid. Set 'NUGET_PACKAGES' to a valid directory to override the global packages folder location.
        /// </summary>
        internal static string FormatUserProfileMissingUseNuGetPackages()
        {
            return GetString("UserProfileMissingUseNuGetPackages");
        }

        /// <summary>
        /// Unable to parse config file '{0}'.
        /// </summary>
        internal static string UserSettings_UnableToParseConfigFile
        {
            get { return GetString("UserSettings_UnableToParseConfigFile"); }
        }

        /// <summary>
        /// Unable to parse config file '{0}'.
        /// </summary>
        internal static string FormatUserSettings_UnableToParseConfigFile(object p0)
        {
            return string.Format(CultureInfo.CurrentCulture, GetString("UserSettings_UnableToParseConfigFile"), p0);
        }

        private static string GetString(string name, params string[] formatterNames)
        {
            var value = _resourceManager.GetString(name);

            System.Diagnostics.Debug.Assert(value != null);

            if (formatterNames != null)
            {
                for (var i = 0; i < formatterNames.Length; i++)
                {
                    value = value.Replace("{" + formatterNames[i] + "}", "{" + i + "}");
                }
            }

            return value;
        }
    }
}
