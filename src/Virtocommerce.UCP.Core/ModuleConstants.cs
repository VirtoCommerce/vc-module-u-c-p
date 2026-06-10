using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Settings;

namespace Virtocommerce.UCP.Core;

public static class ModuleConstants
{
    public static class Security
    {
        public static class Permissions
        {
            public const string Access = "ucp:access";
            public const string Create = "ucp:create";
            public const string Read = "ucp:read";
            public const string Update = "ucp:update";
            public const string Delete = "ucp:delete";

            public static string[] AllPermissions { get; } =
            [
                Access,
                Create,
                Read,
                Update,
                Delete,
            ];
        }
    }

    public static class Settings
    {
        public static class General
        {
            public static SettingDescriptor UCPEnabled { get; } = new()
            {
                Name = "UCP.Enabled",
                GroupName = "UCP|General",
                ValueType = SettingValueType.Boolean,
                DefaultValue = false,
            };

            public static IEnumerable<SettingDescriptor> AllGeneralSettings
            {
                get
                {
                    yield return UCPEnabled;
                }
            }
        }

        public static IEnumerable<SettingDescriptor> AllSettings
        {
            get
            {
                return General.AllGeneralSettings;
            }
        }
    }
}
