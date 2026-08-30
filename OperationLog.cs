using System;
using System.IO;
using System.Text;

namespace SmartPageDuplicate
{
    /// <summary>
    /// Fájlba írt művelet-napló. A státuszmező tartalma az ablak bezárásával elveszik, így ma
    /// semmi nyoma nem marad annak, hogy egy másolás mit fordított le, mit hagyott ki, és
    /// pontosan mit küldött a szerverre. Egy hetekkel későbbi "miért más ez a tábla?" kérdésre
    /// ez a napló az egyetlen visszakereshető válasz.
    ///
    /// A nagy JSON-payloadok külön fájlba kerülnek, hogy a napló olvasható maradjon; a
    /// főnaplóba csak a hivatkozás kerül.
    /// </summary>
    public static class OperationLog
    {
        // A UI-ban a JSON-néző 32 767 karakternél levág; a naplóban ilyen korlát nincs, de a
        // több százezer karakteres base64-tartalmakat sem érdemes a napló közé keverni.
        private const int InlinePayloadLimit = 4096;

        private static readonly object Sync = new object();
        private static string? _logDirectory;
        private static string? _payloadDirectory;
        private static bool _disabled;
        private static string? _failureReason;

        /// <summary>A napló könyvtára, vagy null, ha a naplózás nem indult el.</summary>
        public static string? Directory
        {
            get { EnsureInitialized(); return _logDirectory; }
        }

        /// <summary>Az utolsó hiba oka, ha a naplózás nem működik (csak egyszer jelezzük).</summary>
        public static string? FailureReason => _failureReason;

        private static bool EnsureInitialized()
        {
            if (_disabled) return false;
            if (_logDirectory != null) return true;

            try
            {
                string root = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SmartPageDuplicate", "logs");
                System.IO.Directory.CreateDirectory(root);
                string payloads = Path.Combine(root, "payloads");
                System.IO.Directory.CreateDirectory(payloads);

                _logDirectory = root;
                _payloadDirectory = payloads;
                return true;
            }
            catch (Exception ex)
            {
                // A naplózás soha ne akadályozza a munkát: ha nem tud írni, csendben kimarad.
                _disabled = true;
                _failureReason = ex.Message;
                return false;
            }
        }

        private static string CurrentLogFile()
            => Path.Combine(_logDirectory!, $"smartpageduplicate-{DateTime.Now:yyyy-MM-dd}.log");

        private static void Write(string line)
        {
            if (!EnsureInitialized()) return;
            try
            {
                lock (Sync)
                {
                    File.AppendAllText(CurrentLogFile(), line + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                _disabled = true;
                _failureReason = ex.Message;
            }
        }

        private static string Stamp() => DateTime.Now.ToString("HH:mm:ss.fff");

        /// <summary>Új művelet kezdete - elválasztó fejléc a naplóban.</summary>
        public static void BeginOperation(string operation, string loadServer, string saveServer, bool dryRun)
        {
            Write("");
            Write(new string('=', 78));
            Write($"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {operation}");
            Write($"  Load: {loadServer}    Save: {saveServer}" + (dryRun ? "    [SZÁRAZ FUTTATÁS]" : ""));
            Write(new string('=', 78));
        }

        /// <summary>Státuszüzenet - ugyanaz, amit a felhasználó a státuszmezőben lát.</summary>
        public static void Status(string message)
        {
            // A többsoros üzenetek is olvashatóak maradjanak.
            foreach (var line in message.Replace("\r\n", "\n").Split('\n'))
                Write($"[{Stamp()}] {line}");
        }

        /// <summary>Kimenő HTTP-kérés a küldendő törzzsel.</summary>
        public static void Request(string method, string url, string? body)
        {
            Write($"[{Stamp()}] --> {method} {url}");
            if (!string.IsNullOrEmpty(body))
                WriteBody("kérés törzse", body!);
        }

        /// <summary>Beérkező HTTP-válasz.</summary>
        public static void Response(int statusCode, string? body)
        {
            Write($"[{Stamp()}] <-- {statusCode}");
            if (!string.IsNullOrEmpty(body))
                WriteBody("válasz törzse", body!);
        }

        /// <summary>Száraz futtatásnál: a kérés, ami NEM ment el.</summary>
        public static void SkippedRequest(string method, string url, string? body)
        {
            Write($"[{Stamp()}] --X {method} {url}   (száraz futtatás - nem lett elküldve)");
            if (!string.IsNullOrEmpty(body))
                WriteBody("elküldendő törzs", body!);
        }

        private static void WriteBody(string label, string body)
        {
            if (body.Length <= InlinePayloadLimit)
            {
                Write($"    {label} ({body.Length} karakter):");
                foreach (var line in body.Replace("\r\n", "\n").Split('\n'))
                    Write("    " + line);
                return;
            }

            string? file = DumpPayload(body);
            Write(file != null
                ? $"    {label} ({body.Length} karakter) -> {file}"
                : $"    {label} ({body.Length} karakter) - a kimentés nem sikerült");
        }

        /// <summary>A nagy törzseket külön fájlba menti, és a fájl nevét adja vissza.</summary>
        private static string? DumpPayload(string body)
        {
            if (!EnsureInitialized()) return null;
            try
            {
                string name = $"payload-{DateTime.Now:yyyyMMdd-HHmmss-fff}.json";
                File.WriteAllText(Path.Combine(_payloadDirectory!, name), body, Encoding.UTF8);
                return Path.Combine("payloads", name);
            }
            catch
            {
                return null;
            }
        }
    }
}
