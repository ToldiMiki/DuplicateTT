using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace SmartpageTimetableDuplicateV1
{
    /// <summary>
    /// Összeveti a szerver nyers válaszát a modellosztályokkal, és jelzi azokat a mezőket,
    /// amiket a modell nem ismer.
    ///
    /// Erre azért van szükség, mert a backendnek nincs dokumentációja: ha egy új mező megjelenik
    /// a válaszban, a deszerializálás csendben eldobja, a mentés pedig már nem küldi vissza - a
    /// másolat így észrevétlenül eltér az eredetitől. Pontosan így veszett el a DynamicCell
    /// delayThreshold mezője és az Announcement items tömbje.
    /// </summary>
    public static class ModelFieldGuard
    {
        private static readonly Dictionary<Type, Dictionary<string, PropertyInfo>> PropertyCache = new();

        /// <summary>
        /// Végigjárja a JSON fát a modell szerkezete mentén, és visszaadja az ismeretlen mezők
        /// útvonalát (pl. "dynamicRows[].dynamicCells[].delayThreshold").
        /// </summary>
        public static List<string> FindUnknownFields(JsonNode? node, Type modelType)
        {
            var unknown = new List<string>();
            Walk(node, modelType, "", unknown);
            // Ugyanaz a mező minden során/celláján megjelenik; egyszer érdemes jelenteni.
            return unknown.Distinct().ToList();
        }

        private static void Walk(JsonNode? node, Type type, string path, List<string> unknown)
        {
            if (node is JsonArray array)
            {
                foreach (var item in array)
                {
                    Walk(item, type, path + "[]", unknown);
                }
                return;
            }

            if (node is not JsonObject obj) return;

            var properties = GetJsonProperties(type);
            foreach (var kv in obj)
            {
                string childPath = string.IsNullOrEmpty(path) ? kv.Key : $"{path}.{kv.Key}";

                if (!properties.TryGetValue(kv.Key, out PropertyInfo? property))
                {
                    unknown.Add(childPath);
                    continue;
                }

                // Csak a saját modellosztályainkba megyünk bele; a primitívek és a nyers
                // JSON-ként tárolt mezők (object, JsonElement) tartalma nem ellenőrizhető.
                Type? nested = GetModelType(property.PropertyType);
                if (nested != null)
                {
                    Walk(kv.Value, nested, childPath, unknown);
                }
            }
        }

        /// <summary>A típus JSON-neveinek térképe, a [JsonPropertyName] attribútumot figyelembe véve.</summary>
        private static Dictionary<string, PropertyInfo> GetJsonProperties(Type type)
        {
            lock (PropertyCache)
            {
                if (PropertyCache.TryGetValue(type, out var cached)) return cached;

                var map = new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase);
                foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    var attribute = property.GetCustomAttribute<JsonPropertyNameAttribute>();
                    map[attribute?.Name ?? property.Name] = property;
                }
                PropertyCache[type] = map;
                return map;
            }
        }

        /// <summary>
        /// Ha a property egy saját modellosztály (vagy azok listája), visszaadja azt a típust;
        /// egyébként null.
        /// </summary>
        private static Type? GetModelType(Type type)
        {
            if (type.IsGenericType && typeof(System.Collections.IEnumerable).IsAssignableFrom(type))
            {
                Type[] arguments = type.GetGenericArguments();
                if (arguments.Length == 1) return GetModelType(arguments[0]);
                return null;
            }

            Type? underlying = Nullable.GetUnderlyingType(type) ?? type;
            return IsModelType(underlying) ? underlying : null;
        }

        private static bool IsModelType(Type type)
            => type.Namespace != null
               && type.Namespace.StartsWith("SmartpageTimetableDuplicateV1.Models", StringComparison.Ordinal);
    }
}
