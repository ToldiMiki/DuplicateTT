using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmartPageDuplicate
{
    /// <summary>
    /// A backend strukturált hibaválasza. A szerver magyar nyelvű, mezőnév-szintű üzeneteket küld
    /// (pl. "A megadott érték nem szerepel a nyilvántartásban!") - ezeket kibontva sokkal
    /// használhatóbb a hibajelzés, mint a nyers JSON-t a felhasználó elé tenni.
    /// </summary>
    public class ApiError
    {
        [JsonPropertyName("fieldErrors")]
        public List<FieldError>? FieldErrors { get; set; }

        [JsonPropertyName("logicalErrors")]
        public List<LogicalError>? LogicalErrors { get; set; }

        [JsonPropertyName("statusCode")]
        public int StatusCode { get; set; }
    }

    public class FieldError
    {
        [JsonPropertyName("fieldName")]
        public string? FieldName { get; set; }

        [JsonPropertyName("errorMsg")]
        public string? ErrorMsg { get; set; }
    }

    public class LogicalError
    {
        [JsonPropertyName("errorMsg")]
        public string? ErrorMsg { get; set; }
    }

    public static class ApiErrorFormatter
    {
        /// <summary>
        /// Olvasható hibaüzenetet készít a válaszból.
        /// </summary>
        /// <param name="elementNameResolver">
        /// Opcionális: az "elements[3].imageId" alakú mezőnevekben szereplő indexet elemnévre
        /// fordítja, hogy a felhasználó tudja, melyik elemről van szó.
        /// </param>
        public static string Format(HttpStatusCode status, string? body, Func<int, string?>? elementNameResolver = null)
        {
            int code = (int)status;

            // A hitelesítési hibát külön érdemes kezelni: a felhasználónak nem a státuszkód
            // a hasznos információ, hanem hogy újra be kell lépnie.
            if (status == HttpStatusCode.Unauthorized || IsTokenProblem(body))
            {
                return "A munkamenet lejárt vagy érvénytelen. Válaszd ki újra a szervert a legördülőből, "
                     + "és jelentkezz be, majd próbáld újra.";
            }

            // Nyers HTML jön, ha a kérés el sem jut a backendig (tipikusan VPN nélkül).
            if (LooksLikeHtml(body))
            {
                return code == 404
                    ? $"{code} - a szerver nem található (ellenőrizd a VPN-kapcsolatot)."
                    : $"{code} {status} - a szerver váratlan választ adott (nem JSON).";
            }

            ApiError? error = TryParse(body);
            if (error == null)
            {
                return string.IsNullOrWhiteSpace(body) ? $"{code} {status}" : $"{code} {status} - {body}";
            }

            var lines = new List<string>();
            foreach (var logical in error.LogicalErrors ?? new List<LogicalError>())
            {
                if (!string.IsNullOrWhiteSpace(logical.ErrorMsg))
                    lines.Add("• " + logical.ErrorMsg);
            }
            foreach (var field in error.FieldErrors ?? new List<FieldError>())
            {
                if (string.IsNullOrWhiteSpace(field.ErrorMsg)) continue;
                string where = DescribeField(field.FieldName, elementNameResolver);
                lines.Add(string.IsNullOrEmpty(where) ? "• " + field.ErrorMsg : $"• {where}: {field.ErrorMsg}");
            }

            if (lines.Count == 0)
            {
                return string.IsNullOrWhiteSpace(body) ? $"{code} {status}" : $"{code} {status} - {body}";
            }

            var sb = new StringBuilder();
            sb.Append(code == 422 ? "a szerver elutasította a mentést:" : $"{code} {status}:");
            foreach (var line in lines)
            {
                sb.AppendLine();
                sb.Append("      ").Append(line);
            }
            return sb.ToString();
        }

        /// <summary>Az "elements[3].imageId" mezőnevet emberi leírássá alakítja.</summary>
        private static string DescribeField(string? fieldName, Func<int, string?>? elementNameResolver)
        {
            if (string.IsNullOrWhiteSpace(fieldName)) return "";

            var match = System.Text.RegularExpressions.Regex.Match(fieldName, @"^elements\[(\d+)\]\.(.+)$");
            if (!match.Success) return fieldName;

            int index = int.Parse(match.Groups[1].Value);
            string property = match.Groups[2].Value;
            string? name = elementNameResolver?.Invoke(index);

            return string.IsNullOrEmpty(name)
                ? $"{index + 1}. elem, {property}"
                : $"{index + 1}. elem (\"{name}\"), {property}";
        }

        private static ApiError? TryParse(string? body)
        {
            if (string.IsNullOrWhiteSpace(body)) return null;
            if (!body.TrimStart().StartsWith("{")) return null;
            try
            {
                var error = JsonSerializer.Deserialize<ApiError>(body,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                bool hasContent = (error?.FieldErrors?.Count ?? 0) > 0 || (error?.LogicalErrors?.Count ?? 0) > 0;
                return hasContent ? error : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static bool IsTokenProblem(string? body)
            => body != null && body.Contains("TOKEN_MISSING", StringComparison.OrdinalIgnoreCase);

        private static bool LooksLikeHtml(string? body)
        {
            if (string.IsNullOrWhiteSpace(body)) return false;
            string start = body.TrimStart();
            return start.StartsWith("<html", StringComparison.OrdinalIgnoreCase)
                || start.StartsWith("<!doctype", StringComparison.OrdinalIgnoreCase);
        }
    }
}
