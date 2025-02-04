using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Pannella.Models.Settings;

namespace Pannella.Helpers
{
    public class ArchiveContractResolver : DefaultContractResolver
    {
        public static readonly ArchiveContractResolver INSTANCE = new();

        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            JsonProperty property = base.CreateProperty(member, memberSerialization);

            if (property.DeclaringType == typeof(Archive) && property.PropertyName == "enabled")
            {
                property.ShouldSerialize =
                    instance =>
                    {
                        Archive archive = (Archive)instance;

                        return archive.type is ArchiveType.core_specific_archive or ArchiveType.core_specific_custom_archive;
                    };
            }

            return property;
        }
    }
}
