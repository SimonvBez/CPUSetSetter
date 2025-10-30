using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;

namespace CPUSetSetter
{
    public class ThemeModeJsonConverter : JsonConverter<ThemeMode>
    {
        public override ThemeMode Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            string? value = reader.GetString();
            return value switch
            {
                "Light" => ThemeMode.Light,
                "Dark" => ThemeMode.Dark,
                "System" => ThemeMode.System,
                "None" => ThemeMode.None,
                _ => ThemeMode.System // Default fallback
            };
        }

        public override void Write(
            Utf8JsonWriter writer,
            ThemeMode themeMode,
            JsonSerializerOptions options)
        {
            writer.WriteStringValue(themeMode.Value);
        }
    }
}
