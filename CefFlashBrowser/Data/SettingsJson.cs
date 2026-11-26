using CefFlashBrowser.Models;
using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace CefFlashBrowser.Data
{
    internal static class SettingsJson
    {
        private static readonly JsonDocumentOptions DocumentOptions = new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        };

        private static readonly JsonSerializerOptions ReadOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.AllowNamedFloatingPointLiterals,
            Converters = { new JsonStringEnumConverter() }
        };

        private static readonly JsonSerializerOptions WriteOptions = new JsonSerializerOptions
        {
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
            WriteIndented = true
        };

        internal static Settings Deserialize(string json)
        {
            var root = ParseObject(json);

            // Older settings files could specify null for individual editor preferences.
            foreach (var property in root)
            {
                if (NameEquals(property.Key, nameof(Settings.ScriptEditorSettings)) && property.Value is JsonObject editor)
                {
                    foreach (var name in editor.Where(item => item.Value == null).Select(item => item.Key).ToArray())
                        editor.Remove(name);
                }
            }

            var settings = root.Deserialize<Settings>(ReadOptions);

            if (!root.Any(item => NameEquals(item.Key, nameof(Settings.DownloadDirectory)))
                && !string.IsNullOrWhiteSpace(settings.UserScriptDownloadDirectory))
            {
                settings.DownloadDirectory = settings.UserScriptDownloadDirectory;
            }

            settings.SetNullPropertiesToDefault();
            return settings;
        }

        internal static string Serialize(Settings settings)
        {
            return JsonSerializer.Serialize(settings, WriteOptions);
        }

        internal static string Merge(string json, Settings settings)
        {
            var root = ParseObject(json);
            var changes = (JsonObject)JsonSerializer.SerializeToNode(settings, WriteOptions);

            MergeObject(root, changes);

            if (root[nameof(Settings.ProxySettings)] is JsonObject proxy)
            {
                foreach (var name in proxy.Select(item => item.Key).Where(name => NameEquals(name, nameof(ProxySettings.PlainPassword))).ToArray())
                    proxy.Remove(name);
            }

            return root.ToJsonString(WriteOptions);
        }

        private static JsonObject ParseObject(string json)
        {
            using (var document = JsonDocument.Parse(json, DocumentOptions))
            {
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                    throw new JsonException("Settings must be a JSON object.");

                return (JsonObject)CopyNode(document.RootElement.Clone());
            }
        }

        private static JsonNode CopyNode(JsonElement value)
        {
            switch (value.ValueKind)
            {
                case JsonValueKind.Object:
                    var result = new JsonObject();
                    foreach (var property in value.EnumerateObject())
                    {
                        // Keep the last occurrence of duplicate keys, in file order.
                        result.Remove(property.Name);
                        result.Add(property.Name, CopyNode(property.Value));
                    }
                    return result;

                case JsonValueKind.Array:
                    return new JsonArray(value.EnumerateArray().Select(CopyNode).ToArray());

                case JsonValueKind.Null:
                    return null;

                default:
                    return JsonValue.Create(value);
            }
        }

        private static void MergeObject(JsonObject existing, JsonObject changes)
        {
            foreach (var change in changes)
            {
                var matchingKeys = existing.Select(item => item.Key).Where(name => NameEquals(name, change.Key)).ToArray();
                var lastKey = matchingKeys.LastOrDefault();
                var merged = MergeValue(lastKey == null ? null : existing[lastKey], change.Value);

                foreach (var alias in matchingKeys)
                {
                    if (alias != change.Key)
                        existing.Remove(alias);
                }

                existing[change.Key] = merged;
            }
        }

        private static JsonNode MergeValue(JsonNode existing, JsonNode change)
        {
            if (existing is JsonObject existingObject && change is JsonObject changedObject)
            {
                MergeObject(existingObject, changedObject);
                return existingObject;
            }

            if (existing is JsonArray existingArray && change is JsonArray changedArray)
            {
                foreach (var item in changedArray)
                    existingArray.Add(item?.DeepClone());
                return existingArray;
            }

            return change?.DeepClone();
        }

        private static bool NameEquals(string left, string right)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }
}
