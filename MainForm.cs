using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using System.Net.Security;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using SmartPageDuplicate.Models;
using SmartPageDuplicate.Copy;

namespace SmartPageDuplicate
{
    public partial class MainForm : Form
    {
        // Ismert host, aminek jelenleg lejárt/hibás a tanúsítványa - csak ennél kerüljük meg az
        // ellenőrzést; minden más szervernél a rendes TLS-validáció fut.
        private const string CertBypassHost = "smartpage-dev.hclinear.hu";

        private readonly HttpClientHandler _httpClientHandler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (request, cert, chain, sslPolicyErrors) =>
                sslPolicyErrors == SslPolicyErrors.None ||
                string.Equals(request.RequestUri?.Host, CertBypassHost, StringComparison.OrdinalIgnoreCase)
        };
        private HttpClient _httpClientLoad;
        private HttpClient _httpClientSave;
        private SmartpageApiClient? _loadApi;
        private SmartpageApiClient? _saveApi;
        private TimetableItem? _loadedTimetableItem;
        private LayoutItem? _loadedLayoutItem;
        private List<LayoutItems>? _loadedLayoutItems;
        private List<NamedEntity> _displaysLoad = new List<NamedEntity>();
        private List<NamedEntity> _displaysSave = new List<NamedEntity>();
        private List<RasterFontInfo> _rasterFontsLoad = new List<RasterFontInfo>();
        private List<RasterFontInfo> _rasterFontsSave = new List<RasterFontInfo>();
        private List<NamedEntity> _groupsLoad = new List<NamedEntity>();
        private List<NamedEntity> _groupsSave = new List<NamedEntity>();
        private Dictionary<int, string> _itemTypeLoad = new Dictionary<int, string>();
        private Dictionary<int, string> _itemTypeSave = new Dictionary<int, string>();
        private Dictionary<int, string> _anchorXLoad = new Dictionary<int, string>();
        private Dictionary<int, string> _anchorXSave = new Dictionary<int, string>();
        private Dictionary<int, string> _anchorYLoad = new Dictionary<int, string>();
        private Dictionary<int, string> _anchorYSave = new Dictionary<int, string>();
        private Dictionary<int, string> _textColorLoad = new Dictionary<int, string>();
        private Dictionary<int, string> _textColorSave = new Dictionary<int, string>();

        // A layout-elemek eddig lefordítatlanul átküldött hivatkozásai (H4). Csak eltérő
        // szervereknél töltjük be őket, mert egy szerveren belül az eredeti ID a helyes.
        private List<NamedEntity> _imagesLoad = new List<NamedEntity>();
        private List<NamedEntity> _imagesSave = new List<NamedEntity>();
        private List<NamedEntity> _gridsLoad = new List<NamedEntity>();
        private List<NamedEntity> _gridsSave = new List<NamedEntity>();
        private List<NamedEntity> _timetablesLoad = new List<NamedEntity>();
        private List<NamedEntity> _timetablesSave = new List<NamedEntity>();

        // A megálló-kötésekhez (slide): a megállók és az állapotok névtáblái.
        private List<NamedEntity> _stopsLoad = new List<NamedEntity>();
        private List<NamedEntity> _stopsSave = new List<NamedEntity>();
        private List<NamedEntity> _statesLoad = new List<NamedEntity>();
        private List<NamedEntity> _statesSave = new List<NamedEntity>();

        // A másolás közben kihagyott dolgok, hogy a művelet végén egyben látszódjanak.
        private readonly List<string> _skipped = new List<string>();

        // Olyan hiányok, amikkel a mentés biztosan elbukna - ilyenkor el sem indítjuk. Tipikusan
        // hiányzó raszterfont: azt az API-n keresztül nem lehet átvinni, kézzel kell pótolni.
        private readonly List<string> _blockingProblems = new List<string>();

        // A beolvasott elemben talált ismeretlen mezők. Azért marad meg a mentésig, mert a
        // beolvasás és a mentés között eltelhet idő, és a figyelmeztetés kicsúszhat a képből.
        private readonly List<string> _unknownFields = new List<string>();

        // Ténylegesen megváltozott hivatkozások típusonként, a mentés előtti előnézethez.
        private readonly Dictionary<string, int> _conversions = new Dictionary<string, int>();

        private void NoteConversion(string what)
        {
            _conversions.TryGetValue(what, out int count);
            _conversions[what] = count + 1;
        }

        /// <summary>
        /// Ha van olyan hiány, amivel a mentés biztosan elbukna, egyben jelenti, és igazzal tér
        /// vissza. Ilyenkor el sem indítjuk a műveletet - a szerver úgyis elutasítaná, csak
        /// érthetetlenebb üzenettel és a folyamat közepén.
        /// </summary>
        private bool ReportBlockingProblems()
        {
            if (_blockingProblems.Count == 0) return false;

            SetStatus("❌ A másolás nem indítható el, mert a cél szerverről hiányzik:", Color.Red);
            foreach (string problem in _blockingProblems.Distinct())
            {
                SetStatus($"      • {problem}", Color.Red);
            }
            SetStatus("   A raszterfontok az API-n keresztül nem vihetők át - előbb kézzel fel kell tölteni őket a cél szerverre.", Color.Red);

            MessageBox.Show(this,
                "A másolás nem folytatható, mert a cél szerverről hiányzik:\n\n"
                + FormatFieldList(_blockingProblems.Distinct().ToList())
                + "\n\nA raszterfontok az API-n keresztül nem vihetők át, ezért ezeket előbb kézzel "
                + "fel kell tölteni a cél szerverre. Utána a másolás megismételhető.",
                "Hiányzó raszterfont",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return true;
        }

        // In-memory auth/session values (UI fields removed)
        private string? _loadAuth;
        private string? _saveAuth;
        private string? _loadSession;
        private string? _saveSession;

        // In-memory login usernames
        private string? _loadUsername;
        private string? _saveUsername;

        private string? _baseLoadUrl;
        private string? _baseSaveUrl;

        // Flag to suppress login dialog during auto-copy
        private bool _isAutoCopyingCredentials = false;

        private readonly Dictionary<string, string> _baseUrls = new()
        {
            { "DEV", "https://smartpage-dev.hclinear.hu/backend/api/v1" },
            { "DEMO", "https://smartpage-demo.hclinear.hu/backend/api/v1" },
            { "PROD", "https://smartpage.hclinear.hu/backend/api/v1" },
            { "PROD2", "https://smartpage2.hclinear.hu/backend/api/v1" }
        };

        /// <summary>A szerelvény verziója „v2.0.0" alakban, a csprojban megadott érték alapján.</summary>
        private static string AppVersion
        {
            get
            {
                var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                return version == null ? "" : $"v{version.Major}.{version.Minor}.{version.Build}";
            }
        }

        public MainForm()
        {
            _httpClientLoad = new HttpClient(_httpClientHandler, disposeHandler: false);
            _httpClientSave = new HttpClient(_httpClientHandler, disposeHandler: false);
            InitializeComponent();

            // --- dropdown alapértékek ---
            cmbServerLoad.Items.AddRange(new[] { "DEV", "DEMO", "PROD", "PROD2" });
            cmbServerSave.Items.AddRange(new[] { "DEV", "DEMO", "PROD", "PROD2" });
            cmbServerLoad.SelectedIndex = -1;
            cmbServerSave.SelectedIndex = -1;

            // Hook combobox change events to trigger login dialog
            cmbServerLoad.SelectedIndexChanged += CmbServer_SelectedIndexChanged;
            cmbServerSave.SelectedIndexChanged += CmbServer_SelectedIndexChanged;
            cmbLoadEntityType.SelectedIndexChanged += CmbLoadEntityType_SelectedIndexChanged;

            // A verzió a csprojból jön, hogy egy hibajelentésből kiderüljön, melyik példány futott.
            this.Text = $"SmartPage Duplicate {AppVersion}";
            lblVersion.Text = AppVersion;

            // --- Set focus to Load server combo on startup ---
            cmbServerLoad.Focus();

            // Clear any placeholder or hard-coded auth/session values and rely on in-memory fields
            _loadAuth = null;
            _saveAuth = null;
            _loadSession = null;
            _saveSession = null;
            _loadUsername = null;
            _saveUsername = null;

            if (OperationLog.Directory != null)
                SetStatus($"📁 Napló: {OperationLog.Directory}", Color.DimGray);
            else
                SetStatus($"⚠️ A naplózás nem indult el: {OperationLog.FailureReason}", Color.Orange);
        }

        // Száraz futtatás: minden lefut a szerverre íráson kívül. Ilyenkor a POST-ok nem mennek el,
        // csak a naplóba kerül, mit küldenénk - így a fordítás eredménye ellenőrizhető, mielőtt
        // bármi megváltozna a szerveren.
        private bool DryRun => chkDryRun.Checked;

        private void SetStatus(string message, Color color)
        {
            txtStatus.SelectionStart = txtStatus.TextLength;
            txtStatus.SelectionLength = 0;
            txtStatus.SelectionColor = color;
            txtStatus.AppendText((string.IsNullOrEmpty(txtStatus.Text) ? "" : Environment.NewLine) + message);
            txtStatus.ScrollToCaret();

            // A státuszmező az ablak bezárásával elveszik; a napló marad.
            OperationLog.Status(message);
        }

        private record PostResult(bool Success, bool WasSkipped, System.Net.HttpStatusCode StatusCode, string Body, string Error)
        {
            public static PostResult Skipped() => new(true, true, System.Net.HttpStatusCode.OK, "", "");
        }

        // Minden szerverre írás ezen az egy ponton megy át: itt dől el, hogy a kérés ténylegesen
        // elmegy-e, itt kerül naplóba a teljes küldött törzs (a UI JSON-nézője 32 767 karakternél
        // levág, a napló nem), és itt bomlik ki a szerver strukturált hibaválasza.
        private async Task<PostResult> PostJsonAsync(string url, string json, string label,
            Func<int, string?>? elementNameResolver = null)
        {
            if (DryRun)
            {
                OperationLog.SkippedRequest("POST", url, json);
                SetStatus($"🔍 [száraz futtatás] {label}: a kérés NEM ment el, a küldendő JSON a naplóba került.", Color.RoyalBlue);
                return PostResult.Skipped();
            }

            OperationLog.Request("POST", url, json);
            using StringContent content = new StringContent(json, Encoding.UTF8, "application/json");
            HttpResponseMessage response = await _httpClientSave.PostAsync(url, content);
            string body = await response.Content.ReadAsStringAsync();
            OperationLog.Response((int)response.StatusCode, body);

            string error = response.IsSuccessStatusCode
                ? ""
                : ApiErrorFormatter.Format(response.StatusCode, body, elementNameResolver);

            return new PostResult(response.IsSuccessStatusCode, false, response.StatusCode, body, error);
        }

        private static void ApplyAuthHeaders(HttpClient client, string? auth, string? session)
        {
            client.DefaultRequestHeaders.Clear();
            if (!string.IsNullOrEmpty(auth))
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {auth}");
            if (!string.IsNullOrEmpty(session))
                client.DefaultRequestHeaders.Add("sessionid", session);
        }

        private async void CmbServer_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (sender == null)
                return;

            ComboBox combo = (ComboBox)sender;
            if (combo.SelectedIndex == -1)
                return;

            // If this is an auto-copy operation, skip the login dialog
            if (_isAutoCopyingCredentials)
                return;

            string? serverKey = combo.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(serverKey))
                return;

            using (LoginDialog loginDialog = new LoginDialog(serverKey))
            {
                DialogResult result = loginDialog.ShowDialog(this);
                if (result == DialogResult.OK)
                {
                    if (combo == cmbServerLoad)
                    {
                        _loadAuth = loginDialog.AuthToken ?? "";
                        _loadSession = loginDialog.SessionId ?? "";
                        _loadUsername = loginDialog.Username ?? "";
                        txtLoadUsername.Text = _loadUsername;
                        SetStatus($"✅ Bejelentkezés sikeres a {serverKey} (Load) szerverre.", Color.ForestGreen);

                        // Set headers and base URL for Load client
                        ApplyAuthHeaders(_httpClientLoad, _loadAuth, _loadSession);
                        _baseLoadUrl = _baseUrls[serverKey];
                        _loadApi = new SmartpageApiClient(_httpClientLoad, _baseLoadUrl);

                        // A Load bejelentkezés csak akkor másolja át magát a Save oldalra, ha ott
                        // még nincs kiválasztott szerver. Korábban felülírta a már beállított
                        // célszervert is, így a másolat a forrásra került vissza.
                        if (cmbServerSave.SelectedIndex != -1)
                        {
                            SetStatus($"ℹ️ A Save szerver ({cmbServerSave.SelectedItem}) beállítása megmarad.", Color.DimGray);
                            return;
                        }

                        // Automatically copy Load credentials to Save server (without showing login dialog)
                        _isAutoCopyingCredentials = true;
                        _saveAuth = _loadAuth;
                        _saveSession = _loadSession;
                        _saveUsername = _loadUsername;
                        cmbServerSave.SelectedItem = serverKey;
                        txtSaveUsername.Text = _saveUsername;

                        // Fontos: a Save kliens mindig saját, önálló HttpClient-példány (csak a
                        // handlert - és így a TLS-beállítást - osztja meg a Load kliensével).
                        // Ha ugyanaz a példány lenne, egy későbbi, eltérő szerverre való Save
                        // bejelentkezés törölné/felülírná a Load kliens fejléceit is.
                        _httpClientSave = new HttpClient(_httpClientHandler, disposeHandler: false);
                        ApplyAuthHeaders(_httpClientSave, _saveAuth, _saveSession);
                        _baseSaveUrl = _baseLoadUrl;
                        _saveApi = new SmartpageApiClient(_httpClientSave, _baseSaveUrl);

                        _isAutoCopyingCredentials = false;
                        SetStatus($"✅ Bejelentkezési adatok automatikusan másolva a Save szerverre ({serverKey}).", Color.ForestGreen);
                    }
                    else if (combo == cmbServerSave)
                    {
                        _saveAuth = loginDialog.AuthToken ?? "";
                        _saveSession = loginDialog.SessionId ?? "";
                        _saveUsername = loginDialog.Username ?? "";
                        txtSaveUsername.Text = _saveUsername;
                        SetStatus($"✅ Bejelentkezés sikeres a {serverKey} (Save) szerverre.", Color.ForestGreen);

                        // Set headers for Save client - biztonságos, mert _httpClientSave mindig
                        // önálló példány, sosem ugyanaz az objektum, mint _httpClientLoad.
                        ApplyAuthHeaders(_httpClientSave, _saveAuth, _saveSession);
                        _baseSaveUrl = _baseUrls[serverKey];
                        _saveApi = new SmartpageApiClient(_httpClientSave, _baseSaveUrl);
                    }
                }
                else
                {
                    combo.SelectedIndex = -1;
                    SetStatus($"⚠️ Bejelentkezés visszavonva a {serverKey} szervernél.", Color.Orange);
                }
            }
        }

        private void CmbLoadEntityType_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_loadedTimetableItem != null || _loadedLayoutItem != null || _loadedLayoutItems != null ||
                 !string.IsNullOrEmpty(txtJson.Text) || !string.IsNullOrEmpty(txtSaveName.Text) || !string.IsNullOrEmpty(txtLoadEntityId.Text))
            {
                _loadedTimetableItem = null;
                _loadedLayoutItem = null;
                _loadedLayoutItems = null;
                _unknownFields.Clear();
                txtLoadEntityId.Text = "";
                txtSaveName.Text = "";
                txtJson.Text = "";
                txtStatus.Clear();
                SetStatus("⚠️ Entity típus megváltozott --> előző elem törölve.", Color.Orange);
            }
        }
        /// <summary>
        /// A betöltött névtáblákból összeállítja a fordító bemenetét. A fordítás maga a
        /// felülettől és a hálózattól független CopyTranslator dolga - ez a metódus a híd.
        /// </summary>
        private ServerCatalog BuildCatalog(bool fromLoadServer) => new()
        {
            ServerKey = (fromLoadServer ? cmbServerLoad : cmbServerSave).SelectedItem?.ToString() ?? "?",
            Displays = fromLoadServer ? _displaysLoad : _displaysSave,
            Groups = fromLoadServer ? _groupsLoad : _groupsSave,
            Images = fromLoadServer ? _imagesLoad : _imagesSave,
            Grids = fromLoadServer ? _gridsLoad : _gridsSave,
            Timetables = fromLoadServer ? _timetablesLoad : _timetablesSave,
            Stops = fromLoadServer ? _stopsLoad : _stopsSave,
            States = fromLoadServer ? _statesLoad : _statesSave,
            RasterFonts = fromLoadServer ? _rasterFontsLoad : _rasterFontsSave,
            ElementTypes = fromLoadServer ? _itemTypeLoad : _itemTypeSave,
            AnchorX = fromLoadServer ? _anchorXLoad : _anchorXSave,
            AnchorY = fromLoadServer ? _anchorYLoad : _anchorYSave,
            TextColors = fromLoadServer ? _textColorLoad : _textColorSave,
        };

        /// <summary>A fordító a mentés elején jön létre, a friss névtáblákkal.</summary>
        private CopyTranslator NewTranslator()
            => new(BuildCatalog(true), BuildCatalog(false), IsSameServer());

        /// <summary>A fordító jelentését átveszi a felület saját listáiba (és így a naplóba is).</summary>
        private void ReportTranslation(CopyTranslator translator)
        {
            foreach (string message in translator.Report.Skipped)
            {
                if (_skipped.Contains(message)) continue;
                _skipped.Add(message);
                SetStatus("⚠️ Figyelem: " + message, Color.Orange);
            }
            foreach (var kv in translator.Report.Conversions)
            {
                _conversions[kv.Key] = kv.Value;
            }
            foreach (string problem in translator.Report.Blocking)
            {
                if (!_blockingProblems.Contains(problem)) _blockingProblems.Add(problem);
            }
        }

        /// <summary>
        /// A képhivatkozás rendezése. A döntést a fordító hozza (megvan-e a célon, kell-e
        /// feltölteni); ez a metódus csak a hálózati részt teszi hozzá, amit a fordító
        /// szándékosan nem ismer.
        /// </summary>
        private async Task<bool> ResolveImageAsync(CopyTranslator translator, JsonObject itemObj, string field, string itemName)
        {
            var lookup = translator.LookupImage(itemObj, field, itemName);
            switch (lookup.Kind)
            {
                case CopyTranslator.ImageLookupKind.NothingToDo:
                case CopyTranslator.ImageLookupKind.FoundOnTarget:
                    return true;

                case CopyTranslator.ImageLookupKind.MissingOnSource:
                    return false;

                case CopyTranslator.ImageLookupKind.NeedsUpload:
                    if (DryRun)
                    {
                        SetStatus($"🔍 [száraz futtatás] a(z) \"{lookup.Name}\" kép feltöltésre kerülne a {cmbServerSave.SelectedItem} szerverre ({itemName}).", Color.RoyalBlue);
                        NoteConversion("feltöltendő kép");
                        return true;
                    }

                    int? uploadedId = await UploadImageAsync(translator, lookup.SourceId, lookup.Name);
                    if (uploadedId == null)
                    {
                        NoteSkipped($"a(z) \"{lookup.Name}\" kép feltöltése nem sikerült ({itemName}).");
                        return false;
                    }
                    translator.RegisterUploadedImage(itemObj, field, uploadedId.Value, lookup.Name);
                    return true;

                default:
                    return false;
            }
        }

        private record AnchorXItem(int Id, string Label);
        private record AnchorYItem(int Id, string Label);
        private record TextColorItem(int Id, string Label);

        // Az id + name szerkezet közös a képeknél, rácsoknál és menetrendeknél.
        private async Task<List<NamedEntity>?> LoadNamedListAsync(string endpoint, bool fromLoadServer)
        {
            return await LoadListAsync(endpoint, fromLoadServer, body =>
            {
                if (JsonNode.Parse(body) is not JsonArray root) return null;
                var list = new List<NamedEntity>();
                foreach (var item in root)
                {
                    if (item is JsonObject o
                        && o["id"]?.GetValue<int?>() is int id
                        && o["name"]?.GetValue<string?>() is string name
                        && !string.IsNullOrEmpty(name))
                    {
                        list.Add(new NamedEntity(id, name));
                    }
                }
                return list;
            });
        }

        // A tényleges HTTP GET + JSON-értelmezés a UI-mentes SmartpageApiClient-ben történik;
        // ez a metódus csak a Load/Save oldal kiválasztását és a hibaüzenet státuszsorba
        // írását végzi.
        private async Task<List<T>?> LoadListAsync<T>(string endpoint, bool fromLoadServer, Func<string, List<T>?> customDeserializer)
        {
            var api = fromLoadServer ? _loadApi : _saveApi;
            if (api == null)
            {
                SetStatus($"❌ Hiba {endpoint}: nincs bejelentkezve a {(fromLoadServer ? "Load" : "Save")} szerverre.", Color.Red);
                return null;
            }

            var result = await api.LoadListAsync(endpoint, customDeserializer);
            if (!result.Success)
            {
                SetStatus($"❌ Hiba {endpoint}: {result.Error}", Color.Red);
                return null;
            }
            return result.Value;
        }

        private async Task LoadDictionaryAsync<T>(string itemName, bool fromLoadServer, string endpoint, Func<string, List<T>?> deserializer, Action<Dictionary<int, string>> setDict, Func<T, int> idSelector, Func<T, string?> labelSelector)
        {
            var list = await LoadListAsync<T>(endpoint, fromLoadServer, deserializer);
            if (list != null)
            {
                var dict = list.ToDictionary(idSelector, item => labelSelector(item) ?? "");
                setDict(dict);
                string serverKey = fromLoadServer ? cmbServerLoad.SelectedItem?.ToString() ?? "DEV" : cmbServerSave.SelectedItem?.ToString() ?? "DEV";
                SetStatus($"✅ Betöltve {list.Count}db {itemName} a {(fromLoadServer ? "Load" : "Save")} ({serverKey}) szerverről.", Color.ForestGreen);
            }
        }

        private async Task<List<RasterFontInfo>?> LoadFontsList(bool fromLoadServer)
        {
            return await LoadListAsync("raster-font/listFonts", fromLoadServer, DeserializeFontsList);
        }
        private List<RasterFontInfo>? DeserializeFontsList(string body)
        {
            var root = JsonNode.Parse(body) as JsonArray;
            if (root == null)
            {
                return null;
            }
            var list = new List<RasterFontInfo>();
            foreach (var item in root)
            {
                if (item is JsonObject top)
                {
                    if (top["rasterFonts"] is JsonArray rfs)
                    {
                        foreach (var rf in rfs)
                        {
                            if (rf is JsonObject rfObj)
                            {
                                int? id = rfObj["id"]?.GetValue<int?>();
                                string? ttName = rfObj["ttFontName"]?.GetValue<string?>();
                                int? size = rfObj["size"]?.GetValue<int?>();
                                if (id.HasValue && !string.IsNullOrEmpty(ttName) && size.HasValue)
                                {
                                    list.Add(new RasterFontInfo(id.Value, ttName!, size.Value));
                                }
                            }
                        }
                    }
                }
            }
            return list;
        }

        private async Task<List<NamedEntity>?> LoadDisplaysList(bool fromLoadServer)
        {
            return await LoadListAsync("display/list", fromLoadServer, DeserializeDisplaysList);
        }
        private List<NamedEntity>? DeserializeDisplaysList(string body)
        {
            var root = JsonNode.Parse(body) as JsonArray;
            if (root == null)
            {
                return null;
            }
            var list = new List<NamedEntity>();
            foreach (var item in root)
            {
                if (item is JsonObject groupObj)
                {
                    int? id = groupObj["id"]?.GetValue<int?>();
                    string? name = groupObj["name"]?.GetValue<string?>();
                    if (id.HasValue && !string.IsNullOrEmpty(name))
                    {
                        list.Add(new NamedEntity(id.Value, name!));
                    }
                }
            }
            return list;
        }

        private async Task<List<NamedEntity>?> LoadGroupsList(bool fromLoadServer)
        {
            return await LoadListAsync("group/list", fromLoadServer, DeserializeGroupsList);
        }
        private List<NamedEntity>? DeserializeGroupsList(string body)
        {
            var root = JsonNode.Parse(body) as JsonArray;
            if (root == null)
            {
                return null;
            }
            var list = new List<NamedEntity>();
            foreach (var item in root)
            {
                if (item is JsonObject groupObj)
                {
                    int? id = groupObj["id"]?.GetValue<int?>();
                    string? name = groupObj["name"]?.GetValue<string?>();
                    if (id.HasValue && !string.IsNullOrEmpty(name))
                    {
                        list.Add(new NamedEntity(id.Value, name!));
                    }
                }
            }
            return list;
        }

        private async Task LoadElementTypesList(bool fromLoadServer)
        {
            await LoadDictionaryAsync("elem típus", fromLoadServer, "element-type/list", DeserializeElementTypesList, d => { if (fromLoadServer) _itemTypeLoad = d; else _itemTypeSave = d; }, et => et.Id, et => et.TypeLabel);
        }
        private List<ElementType>? DeserializeElementTypesList(string body)
        {
            return JsonSerializer.Deserialize<List<ElementType>>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        private async Task LoadAnchorXList(bool fromLoadServer)
        {
            await LoadDictionaryAsync("AnchorX érték", fromLoadServer, "enum/list/enum/values/AnchorX", DeserializeAnchorXList, d => { if (fromLoadServer) _anchorXLoad = d; else _anchorXSave = d; }, ax => ax.Id, ax => ax.Label);
        }
        private List<AnchorXItem>? DeserializeAnchorXList(string body)
        {
            return JsonSerializer.Deserialize<List<AnchorXItem>>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        private async Task LoadAnchorYList(bool fromLoadServer)
        {
            await LoadDictionaryAsync("AnchorY érték", fromLoadServer, "enum/list/enum/values/AnchorY", DeserializeAnchorYList, d => { if (fromLoadServer) _anchorYLoad = d; else _anchorYSave = d; }, ay => ay.Id, ay => ay.Label);
        }
        private List<AnchorYItem>? DeserializeAnchorYList(string body)
        {
            return JsonSerializer.Deserialize<List<AnchorYItem>>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        private async Task LoadTextColorList(bool fromLoadServer)
        {
            await LoadDictionaryAsync("TextColor érték", fromLoadServer, "enum/list/enum/values/TextColor", DeserializeTextColorList, d => { if (fromLoadServer) _textColorLoad = d; else _textColorSave = d; }, tc => tc.Id, tc => tc.Label);
        }
        private List<TextColorItem>? DeserializeTextColorList(string body)
        {
            return JsonSerializer.Deserialize<List<TextColorItem>>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }


        /// <summary>Azonos szerveren belül az eredeti ID-k a helyesek - ilyenkor nem fordítunk.</summary>
        private bool IsSameServer()
            => cmbServerLoad.SelectedItem?.ToString() == cmbServerSave.SelectedItem?.ToString();

        private void NoteSkipped(string message)
        {
            _skipped.Add(message);
            SetStatus("⚠️ Figyelem: " + message, Color.Orange);
        }

        /// <summary>
        /// A művelet végén egyben megmutatja, mi maradt ki. A figyelmeztetések menet közben is
        /// megjelennek, de a hosszú státusznaplóban elvesznek - itt egy helyen látszanak.
        /// </summary>
        private void ReportSkipped()
        {
            if (_skipped.Count == 0)
            {
                SetStatus("✅ Összegzés: minden hivatkozás lefordítható volt, semmi nem maradt ki.", Color.ForestGreen);
                return;
            }

            SetStatus($"⚠️ Összegzés: {_skipped.Count} dolog maradt ki a másolatból:", Color.DarkOrange);
            foreach (var item in _skipped)
            {
                SetStatus("      • " + item, Color.DarkOrange);
            }
        }

        /// <summary>
        /// Kép átvitele a Load szerverről a Save szerverre. A tartalom base64-ként utazik.
        ///
        /// A szerver a feltöltött képet újrakódolhatja: egy 4 bites palettás PNG 8 bitesként jön
        /// vissza. A felbontás és a színtípus megmarad, tehát a kép tartalma nem sérül, de
        /// byte-azonosságra nem szabad építeni.
        /// </summary>
        private async Task<int?> UploadImageAsync(CopyTranslator translator, int loadImageId, string name)
        {
            SetStatus($"⬆️ A(z) \"{name}\" kép átvitele a {cmbServerSave.SelectedItem} szerverre...", Color.RoyalBlue);

            // 1. Letöltés a forrásról. Az image/load POST-ot vár, nem GET-et.
            string loadUrl = $"{_baseLoadUrl}/image/load";
            string loadBody = new JsonObject { ["id"] = loadImageId }.ToJsonString();
            JsonObject? image;
            try
            {
                OperationLog.Request("POST", loadUrl, loadBody);
                using var content = new StringContent(loadBody, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await _httpClientLoad.PostAsync(loadUrl, content);
                string body = await response.Content.ReadAsStringAsync();
                OperationLog.Response((int)response.StatusCode, body);

                if (!response.IsSuccessStatusCode)
                {
                    SetStatus($"❌ A kép letöltése sikertelen - {ApiErrorFormatter.Format(response.StatusCode, body)}", Color.Red);
                    return null;
                }
                image = JsonNode.Parse(body) as JsonObject;
            }
            catch (Exception ex)
            {
                SetStatus($"❌ A kép letöltése sikertelen: {ex.Message}", Color.Red);
                return null;
            }

            if (image == null || image["file"] == null)
            {
                SetStatus($"❌ A(z) \"{name}\" kép tartalma üres, a feltöltés kimarad.", Color.Red);
                return null;
            }

            // 2. Az azonosítót el kell dobni, a jogosultsági csoportokat pedig lefordítani.
            image.Remove("id");
            image.Remove("imageUrl");
            image.Remove("version");
            translator.TranslateGroupIdsOf(image);

            // 3. Feltöltés a célra.
            var result = await PostJsonAsync($"{_baseSaveUrl}/image/save", image.ToJsonString(), $"Kép feltöltése: {name}");
            if (!result.Success)
            {
                SetStatus($"❌ A(z) \"{name}\" kép feltöltése sikertelen - {result.Error}", Color.Red);
                return null;
            }
            if (!int.TryParse(result.Body.Trim(), out int newId) || newId <= 0)
            {
                SetStatus($"❌ A kép feltöltése nem érvényes azonosítót adott vissza: '{result.Body}'", Color.Red);
                return null;
            }

            SetStatus($"✅ A(z) \"{name}\" kép átvitte (új ID={newId}).", Color.ForestGreen);
            return newId;
        }

        /// <summary>
        /// Elem választása listából. A szerver adja a listát (357 layout, 27 menetrend a PROD2-n),
        /// így az azonosítót nem kell kézzel begépelni - egy elgépelt ID-ből rossz elem másolása
        /// lenne, amit csak a JSON-ból lehetne észrevenni.
        /// </summary>
        private async void BtnPickEntity_Click(object? sender, EventArgs e)
        {
            if (_baseLoadUrl == null)
            {
                SetStatus("❌ Hiba: előbb jelentkezz be a Load szerverre!", Color.Red);
                return;
            }

            string entityType = cmbLoadEntityType.SelectedItem?.ToString() ?? "Timetable";
            bool isLayout = entityType == "Layout";

            btnPickEntity.Enabled = false;
            try
            {
                SetStatus($"{entityType} lista betöltése a {cmbServerLoad.SelectedItem} szerverről...", Color.Black);
                var rows = await LoadPickerRowsAsync(isLayout);
                if (rows == null || rows.Count == 0)
                {
                    SetStatus($"❌ Hiba: a(z) {entityType} lista üres vagy nem érhető el.", Color.Red);
                    return;
                }

                using var dialog = new EntityPickerDialog(
                    $"{entityType} választása - {cmbServerLoad.SelectedItem}",
                    isLayout ? "Kijelző" : "Méret",
                    rows);

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    txtLoadEntityId.Text = dialog.SelectedId.ToString();
                    SetStatus($"✅ Kiválasztva: ID={dialog.SelectedId} \"{dialog.SelectedName}\"", Color.ForestGreen);
                }
            }
            finally
            {
                btnPickEntity.Enabled = true;
            }
        }

        /// <summary>A választólista sorai: a kiegészítő oszlop layoutnál a kijelző, menetrendnél a méret.</summary>
        private async Task<List<PickerRow>?> LoadPickerRowsAsync(bool isLayout)
        {
            string endpoint = isLayout ? "layout/list" : "dynamic-timetable/list";
            return await LoadListAsync(endpoint, fromLoadServer: true, body =>
            {
                if (JsonNode.Parse(body) is not JsonArray root) return null;
                var rows = new List<PickerRow>();
                foreach (var item in root)
                {
                    if (item is not JsonObject o) continue;
                    if (o["id"]?.GetValue<int?>() is not int id) continue;
                    string name = o["name"]?.GetValue<string?>() ?? "";

                    string extra;
                    if (isLayout)
                    {
                        extra = o["displayName"]?.GetValue<string?>() ?? "";
                    }
                    else
                    {
                        int? width = o["width"]?.GetValue<int?>();
                        int? height = o["height"]?.GetValue<int?>();
                        extra = width.HasValue && height.HasValue ? $"{width}×{height}" : "";
                    }
                    rows.Add(new PickerRow(id, name, extra));
                }
                return rows;
            });
        }

        private async void BtnLoad_Click(object sender, EventArgs e)
        {
            txtStatus.Clear();

            var serverLoadSelected = cmbServerLoad.SelectedItem;
            if (serverLoadSelected == null)
            {
                SetStatus($"❌ Hiba: nincs kiválasztva Load szerver!", Color.Red);
                return;
            }
            if (_baseLoadUrl == null)
            {
                SetStatus($"❌ Hiba: nincs bejelentkezve a Load szerverre!", Color.Red);
                return;
            }

            string id = txtLoadEntityId.Text.Trim();
            if (string.IsNullOrEmpty(id))
            {
                SetStatus($"❌ Hiba: az ID mező üres!", Color.Red);
                return;
            }

            string entityType = cmbLoadEntityType.SelectedItem?.ToString() ?? "Timetable";
            _unknownFields.Clear();
            OperationLog.BeginOperation($"BEOLVASÁS - {entityType} ID={id}",
                serverLoadSelected.ToString() ?? "?", "-", dryRun: false);

            if (entityType == "Layout")
            {
                await LoadLayoutEntityAsync(id);
            }
            else if (entityType == "Timetable")
            {
                await LoadTimetableEntityAsync(id);
            }
            else
            {
                SetStatus($"❌ Hiba: az elemtípus nem értelmezhető.", Color.Red);
                return;
            }
        }

        private async Task LoadTimetableEntityAsync(string id)
        {
            SetStatus("Timetable beolvasása elkezdődött...", Color.Black);
            try
            {
                /*                
                                string briefUrl = $"{baseUrl}/dynamic-timetable/load-brief?id={id}";
                                HttpResponseMessage briefResponse = await _httpClient.GetAsync(briefUrl);
                                if (!briefResponse.IsSuccessStatusCode)
                                {
                                    string err = await briefResponse.Content.ReadAsStringAsync();
                                    SetStatus($"❌ Hiba load-brief: {briefResponse.StatusCode} - {err}", Color.Red);
                                    return;
                                }

                                string briefJson = await briefResponse.Content.ReadAsStringAsync();
                                var briefItem = JsonSerializer.Deserialize<TimetableItem>(
                                    briefJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                                if (briefItem == null)
                                {
                                    SetStatus($"❌ Hiba: a Timetable load-brief nem értelmezhető.", Color.Red);
                                    return;
                                }
                                SetStatus($"✅ Timetable brief sikeresen beolvasva a {cmbServerLoad.SelectedItem} szerverről.", Color.ForestGreen);
                */
                string fullUrl = $"{_baseLoadUrl}/dynamic-timetable/load?id={id}"; //included the brief information, fields
                OperationLog.Request("GET", fullUrl, null);
                HttpResponseMessage fullResponse = await _httpClientLoad.GetAsync(fullUrl);
                if (!fullResponse.IsSuccessStatusCode)
                {
                    string err = await fullResponse.Content.ReadAsStringAsync();
                    OperationLog.Response((int)fullResponse.StatusCode, err);
                    SetStatus($"❌ A Timetable beolvasása sikertelen - {ApiErrorFormatter.Format(fullResponse.StatusCode, err)}", Color.Red);
                    return;
                }

                string fullJson = await fullResponse.Content.ReadAsStringAsync();
                // A beolvasott nyers JSON naplóba kerül: ez lesz az összehasonlítási alap, ha
                // később kiderül, hogy a másolat eltér az eredetitől.
                OperationLog.Response((int)fullResponse.StatusCode, fullJson);
                var fullItem = JsonSerializer.Deserialize<TimetableItem>(
                    fullJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (fullItem == null)
                {
                    SetStatus($"❌ Hiba: a teljes elem nem értelmezhető.", Color.Red);
                    return;
                }

                if (!CheckModelFields(fullJson, typeof(TimetableItem), "Timetable"))
                {
                    DiscardLoadedEntity("A beolvasás megszakadt: ismeretlen mezők miatt az elem nem került betöltésre.");
                    return;
                }

                _loadedTimetableItem = fullItem;
                txtSaveName.Text = fullItem.Name ?? "";
                DisplayTxtJson(fullItem);
                SetStatus($"✅ Timetable sikeresen beolvasva a {cmbServerLoad.SelectedItem} szerverről.", Color.ForestGreen);
            }
            catch (Exception ex)
            {
                SetStatus($"❌ Hiba: {ex.Message}", Color.Red);
            }
        }

        private async Task LoadLayoutEntityAsync(string id)
        {
            SetStatus("Layout beolvasása elkezdődött...", Color.Black);
            try
            {
                string briefUrl = $"{_baseLoadUrl}/layout/load/{id}";
                OperationLog.Request("GET", briefUrl, null);
                HttpResponseMessage briefResponse = await _httpClientLoad.GetAsync(briefUrl);
                if (!briefResponse.IsSuccessStatusCode)
                {
                    string err = await briefResponse.Content.ReadAsStringAsync();
                    OperationLog.Response((int)briefResponse.StatusCode, err);
                    SetStatus($"❌ A Layout fejléc beolvasása sikertelen - {ApiErrorFormatter.Format(briefResponse.StatusCode, err)}", Color.Red);
                    return;
                }

                string briefJson = await briefResponse.Content.ReadAsStringAsync();
                OperationLog.Response((int)briefResponse.StatusCode, briefJson);
                var briefItem = JsonSerializer.Deserialize<LayoutItem>(
                    briefJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (briefItem == null)
                {
                    SetStatus($"❌ Hiba: a load-brief nem értelmezhető.", Color.Red);
                    return;
                }

                if (!CheckModelFields(briefJson, typeof(LayoutItem), "Layout fejléc"))
                {
                    DiscardLoadedEntity("A beolvasás megszakadt: ismeretlen mezők miatt az elem nem került betöltésre.");
                    return;
                }

                _loadedLayoutItem = briefItem;
                txtSaveName.Text = briefItem.Name ?? "";
                DisplayTxtJson(briefItem);
                SetStatus($"✅ Layout brief sikeresen beolvasva a {cmbServerLoad.SelectedItem} szerverről.", Color.ForestGreen);

                string fullUrl = $"{_baseLoadUrl}/element/list/layoutId?layoutId={id}";
                OperationLog.Request("GET", fullUrl, null);
                HttpResponseMessage fullResponse = await _httpClientLoad.GetAsync(fullUrl);
                if (!fullResponse.IsSuccessStatusCode)
                {
                    string err = await fullResponse.Content.ReadAsStringAsync();
                    OperationLog.Response((int)fullResponse.StatusCode, err);
                    SetStatus($"❌ A Layout elemeinek beolvasása sikertelen - {ApiErrorFormatter.Format(fullResponse.StatusCode, err)}", Color.Red);
                    return;
                }

                string fullJson = await fullResponse.Content.ReadAsStringAsync();
                OperationLog.Response((int)fullResponse.StatusCode, fullJson);
                var fullItem = JsonSerializer.Deserialize<List<LayoutItems>>(
                    fullJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (fullItem == null)
                {
                    SetStatus($"❌ Hiba: a teljes full-elem nem értelmezhető.", Color.Red);
                    return;
                }

                if (!CheckModelFields(fullJson, typeof(LayoutItems), "Layout elemek"))
                {
                    // A fejléc ilyenkor már be van töltve - azt is el kell dobni, különben
                    // egy elemek nélküli, félig betöltött állapot maradna a memóriában.
                    DiscardLoadedEntity("A beolvasás megszakadt: ismeretlen mezők miatt az elem nem került betöltésre.");
                    return;
                }

                _loadedLayoutItems = fullItem;
                DisplayTxtJson(fullItem, true);
                SetStatus($"✅ Layout összes eleme ({fullItem.Count}db) sikeresen beolvasva a {cmbServerLoad.SelectedItem} szerverről.", Color.ForestGreen);
            }
            catch (Exception ex)
            {
                SetStatus($"❌ Hiba: {ex.Message}", Color.Red);
            }
        }

        private async void BtnSave_Click(object sender, EventArgs e)
        {
            txtStatus.Clear();

            var serverLoadSelected = cmbServerLoad.SelectedItem;
            if (serverLoadSelected == null)
            {
                SetStatus($"❌ Hiba: nincs kiválasztva Load szerver!", Color.Red);
                return;
            }
            string serverLoadKey = serverLoadSelected.ToString() ?? "DEV";
            if (_baseLoadUrl == null)
            {
                SetStatus($"❌ Hiba: nincs bejelentkezve a Load szerverre!", Color.Red);
                return;
            }

            var serverSaveSelected = cmbServerSave.SelectedItem;
            if (serverSaveSelected == null)
            {
                SetStatus($"❌ Hiba: nincs kiválasztva Save szerver!", Color.Red);
                return;
            }
            string serverSaveKey = serverSaveSelected.ToString() ?? "DEV";
            if (_baseSaveUrl == null)
            {
                SetStatus($"❌ Hiba: nincs bejelentkezve a Save szerverre!", Color.Red);
                return;
            }

            _skipped.Clear();
            _conversions.Clear();
            _blockingProblems.Clear();
            OperationLog.BeginOperation(
                $"MENTÉS - {cmbLoadEntityType.SelectedItem} \"{txtSaveName.Text.Trim()}\"",
                serverLoadKey, serverSaveKey, DryRun);
            if (DryRun)
            {
                SetStatus("🔍 Száraz futtatás: a fordítás lefut, de a szerverre semmi nem íródik.", Color.RoyalBlue);
            }

            // A gyors, olcsó ellenőrzések előre: ne a listák betöltése után derüljön ki, hogy
            // üres a név, vagy hogy a név már foglalt.
            if (string.IsNullOrEmpty(txtSaveName.Text.Trim()))
            {
                SetStatus($"❌ Hiba: az új név üres!", Color.Red);
                return;
            }
            if (!await EnsureFreeNameAsync(cmbLoadEntityType.SelectedItem?.ToString() ?? "Timetable"))
            {
                return;
            }

            var displaysLoad = await LoadDisplaysList(true);  // load displays list from Load server
            if (displaysLoad == null)
            {
                SetStatus($"❌ Hiba: displays lista betöltése sikertelen a {serverLoadKey} (Load) szervernél.", Color.Red);
                return;
            }
            _displaysLoad = displaysLoad;

            var displaysSave = await LoadDisplaysList(false);  // load displays list from Save server
            if (displaysSave == null)
            {
                SetStatus($"❌ Hiba: displays lista betöltése sikertelen a {serverSaveKey} (Save) szervernél.", Color.Red);
                return;
            }
            _displaysSave = displaysSave;

            var rasterFontsLoad = await LoadFontsList(true);  // load fonts list from Load server
            if (rasterFontsLoad == null)
            {
                SetStatus($"❌ Hiba: raster font lista betöltése sikertelen a {serverLoadKey} (Load) szervernél.", Color.Red);
                return;
            }
            SetStatus($"✅ Betöltve {rasterFontsLoad.Count}db raster font a Load ({serverLoadKey}) szerverről.", Color.ForestGreen);
            _rasterFontsLoad = rasterFontsLoad;

            var rasterFontsSave = await LoadFontsList(false);  // load fonts list from Save server
            if (rasterFontsSave == null)
            {
                SetStatus($"❌ Hiba: raster font lista betöltése sikertelen a {serverSaveKey} (Save) szervernél.", Color.Red);
                return;
            }
            SetStatus($"✅ Betöltve {rasterFontsSave.Count}db raster font a Save ({serverSaveKey}) szerverről.", Color.ForestGreen);
            _rasterFontsSave = rasterFontsSave;

            var groupsLoad = await LoadGroupsList(true);  // load groups list from Load server
            if (groupsLoad != null)
            {
                _groupsLoad = groupsLoad;
                SetStatus($"✅ Betöltve {groupsLoad.Count}db csoport a Load ({serverLoadKey}) szerverről: {string.Join(", ", groupsLoad.Select(g => g.Name))}", Color.ForestGreen);
            }

            var groupsSave = await LoadGroupsList(false);  // load groups list from Save server
            if (groupsSave != null)
            {
                _groupsSave = groupsSave;
                SetStatus($"✅ Betöltve {groupsSave.Count}db csoport a Save ({serverSaveKey}) szerverről: {string.Join(", ", groupsSave.Select(g => g.Name))}", Color.ForestGreen);
            }

            string newName = txtSaveName.Text.Trim();
            if (string.IsNullOrEmpty(newName))
            {
                SetStatus($"❌ Hiba: az új név üres!", Color.Red);
                return;
            }

            string entityType = cmbLoadEntityType.SelectedItem?.ToString() ?? "Timetable";
            if (entityType == "Timetable")
            {
                // A háttérkép átviteléhez a képlista kell mindkét oldalról.
                if (!IsSameServer() && !await LoadCrossServerNameTablesAsync(includeLayoutTables: false))
                {
                    return;
                }
                await SaveTimetableEntityAsync(newName);
            }
            else if (entityType == "Layout")
            {
                await LoadElementTypesList(true);
                await LoadElementTypesList(false);
                await LoadAnchorXList(true);
                await LoadAnchorXList(false);
                await LoadAnchorYList(true);
                await LoadAnchorYList(false);
                await LoadTextColorList(true);
                await LoadTextColorList(false);

                // A képek, rácsok és menetrendek névtáblái csak akkor kellenek, ha eltérő
                // szerverre másolunk - egy szerveren belül az eredeti ID a helyes. Az image/list
                // több megabájt, ezért nem érdemes fölöslegesen letölteni.
                if (!IsSameServer() && !await LoadCrossServerNameTablesAsync(includeLayoutTables: true))
                {
                    return;
                }

                await SaveLayoutEntityAsync(newName);
            }
            else
            {
                SetStatus($"❌ Hiba: a kiválasztott elemtípus nem értelmezhető.", Color.Red);
                return;
            }

            ReportSkipped();
        }

        private async Task SaveTimetableEntityAsync(string newName)
        {
            SetStatus("Timetable mentése elkezdődött...", Color.Black);

            if (_loadedTimetableItem == null)
            {
                SetStatus($"❌ Hiba: nincs beolvasott Timetable elem!", Color.Red);
                return;
            }

            try
            {
                var node = JsonSerializer.SerializeToNode(_loadedTimetableItem, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });

                var translator = NewTranslator();
                translator.TranslateTimetable(node);

                if (node is JsonObject nodeObj)
                {
                    nodeObj["name"] = newName;

                    // A háttérkép átvitele: név szerint keressük a célon, és ha nincs meg,
                    // feltöltjük. Ha ez sem megy, a mező kimarad - így legalább a menetrend
                    // létrejön, csak háttérkép nélkül.
                    if (!await ResolveImageAsync(translator, nodeObj, "imageId", "háttérkép"))
                    {
                        nodeObj.Remove("imageId");
                        NoteSkipped("a háttérkép kimarad a menetrendből - kézzel pótolandó.");
                    }
                }

                ReportTranslation(translator);

                DisplayTxtJson(node);

                string jsonOut = node?.ToJsonString(new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                }) ?? "{}";

                // Van olyan hiány, amivel a mentés biztosan elbukna? Akkor ne is kezdjük el.
                if (ReportBlockingProblems()) return;

                // A fordítás lefutott, de még semmit nem írtunk: itt lehet megállítani.
                if (!ConfirmCopy("Timetable", totalItems: 0, copiedItems: 0))
                {
                    SetStatus("⛔ A mentés megszakadt a felhasználó kérésére - a szerveren semmi nem változott.", Color.Red);
                    return;
                }

                var result = await PostJsonAsync($"{_baseSaveUrl}/dynamic-timetable/save", jsonOut, "Timetable mentése");
                if (result.WasSkipped)
                {
                    SetStatus("🔍 Száraz futtatás vége - a szerveren semmi nem változott.", Color.RoyalBlue);
                }
                else if (result.Success)
                {
                    SetStatus($"✅ Sikeres mentés a {cmbServerSave.SelectedItem} szerverre.", Color.ForestGreen);
                }
                else
                {
                    SetStatus($"❌ Hiba - {result.Error}", Color.Red);
                }
            }
            catch (Exception ex)
            {
                SetStatus($"❌ Hiba: {ex.Message}", Color.Red);
            }
        }

        /// <summary>
        /// A szerverek közötti másoláshoz szükséges név -> ID táblák betöltése mindkét oldalról.
        /// Hamissal tér vissza, ha bármelyik lista nem érhető el: hiányos táblákkal fordítani
        /// rosszabb, mint el sem kezdeni.
        /// </summary>
        private async Task<bool> LoadCrossServerNameTablesAsync(bool includeLayoutTables)
        {
            // A képlista mindkét entitástípushoz kell: a layout-elemek képeihez és a menetrend
            // háttérképéhez egyaránt.
            if (!await LoadNameTableAsync("image/list", "kép", l => _imagesLoad = l, l => _imagesSave = l))
                return false;

            if (!includeLayoutTables) return true;

            return await LoadNameTableAsync("grid/list", "rács", l => _gridsLoad = l, l => _gridsSave = l)
                && await LoadNameTableAsync("dynamic-timetable/list", "dinamikus menetrend",
                       l => _timetablesLoad = l, l => _timetablesSave = l)
                // A megálló-kötések átviteléhez:
                && await LoadNameTableAsync("stop/list", "megálló", l => _stopsLoad = l, l => _stopsSave = l)
                && await LoadNameTableAsync("state/list", "állapot", l => _statesLoad = l, l => _statesSave = l);
        }

        private async Task<bool> LoadNameTableAsync(string endpoint, string label,
            Action<List<NamedEntity>> setLoad, Action<List<NamedEntity>> setSave)
        {
            var load = await LoadNamedListAsync(endpoint, true);
            if (load == null)
            {
                SetStatus($"❌ Hiba: a(z) {label} lista betöltése sikertelen a Load szerverről - a másolás nem folytatható.", Color.Red);
                return false;
            }
            setLoad(load);

            var save = await LoadNamedListAsync(endpoint, false);
            if (save == null)
            {
                SetStatus($"❌ Hiba: a(z) {label} lista betöltése sikertelen a Save szerverről - a másolás nem folytatható.", Color.Red);
                return false;
            }
            setSave(save);

            SetStatus($"✅ Betöltve {load.Count} db {label} a Load, {save.Count} db a Save szerverről.", Color.ForestGreen);
            return true;
        }

        /// <summary>
        /// A szerver a nevek egyediségét kikényszeríti (422 - "A megadott érték már szerepel a
        /// nyilvántartásban!"). Ezt a mentés elején ellenőrizzük, hogy ne a művelet végén derüljön
        /// ki - főleg a szerveren belüli duplikálásnál, ahol az alapértelmezett név mindig
        /// ütközik a forráséval.
        /// </summary>
        private async Task<bool> EnsureFreeNameAsync(string entityType)
        {
            string endpoint = entityType == "Layout" ? "layout/list" : "dynamic-timetable/list";
            var existing = await LoadNamedListAsync(endpoint, fromLoadServer: false);
            if (existing == null)
            {
                SetStatus("⚠️ A névütközés nem ellenőrizhető (a lista nem érhető el) - a mentés folytatódik.", Color.Orange);
                return true;
            }

            string name = txtSaveName.Text.Trim();
            var clash = existing.FirstOrDefault(e => CopyTranslator.NameEquals(e.Name, name));
            if (clash == null) return true;

            string suggestion = SuggestFreeName(name, existing);
            var answer = MessageBox.Show(this,
                $"A(z) {cmbServerSave.SelectedItem} szerveren már létezik ilyen nevű elem:\n\n"
                + $"    ID={clash.Id}   \"{clash.Name}\"\n\n"
                + "A szerver megköveteli a nevek egyediségét, ezért ezt a mentést elutasítaná.\n\n"
                + $"Javasolt szabad név:\n\n    \"{suggestion}\"\n\n"
                + "Igen  - folytatás a javasolt névvel\n"
                + "Nem  - maradjon az eredeti név (a mentés várhatóan elbukik)\n"
                + "Mégse  - a mentés megszakítása",
                "Névütközés",
                MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1);

            if (answer == DialogResult.Cancel)
            {
                SetStatus("⛔ A mentés megszakadt: a név már foglalt a cél szerveren.", Color.Red);
                return false;
            }
            if (answer == DialogResult.Yes)
            {
                txtSaveName.Text = suggestion;
                SetStatus($"✏️ Az új név: \"{suggestion}\"", Color.RoyalBlue);
            }
            else
            {
                SetStatus($"⚠️ A név marad \"{name}\" - a szerver ezt várhatóan elutasítja.", Color.Orange);
            }
            return true;
        }

        /// <summary>Szabad nevet keres "név (2)", "név (3)" ... alakban.</summary>
        private static string SuggestFreeName(string name, List<NamedEntity> existing)
        {
            // Ha a név már "... (N)" alakú, a számot növeljük, nem fűzünk hozzá újabb zárójelet.
            var match = Regex.Match(name, @"^(.*?)\s*\((\d+)\)$");
            string baseName = match.Success ? match.Groups[1].Value : name;
            int start = match.Success ? int.Parse(match.Groups[2].Value) + 1 : 2;

            for (int i = start; i < start + 1000; i++)
            {
                string candidate = $"{baseName} ({i})";
                if (!existing.Any(e => CopyTranslator.NameEquals(e.Name, candidate)))
                {
                    return candidate;
                }
            }
            return $"{baseName} ({DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()})";
        }

        /// <summary>
        /// A megálló-kötések (slide-ok) átvitele. A slide köti a layoutot a megállóhoz - enélkül a
        /// másolat sehol nem jelenik meg. Csak eltérő szerverek között fut: egy szerveren belüli
        /// duplikálásnál nem szabad, mert két layout nem versenghet ugyanazon a megállón.
        /// </summary>
        private async Task CopySlidesAsync(int sourceLayoutId, int newLayoutId)
        {
            var sourceSlides = await LoadSlidesForLayoutAsync(sourceLayoutId);
            if (sourceSlides == null)
            {
                NoteSkipped("a megálló-kötések nem olvashatók (slide/list) - a másolatot kézzel kell megállóhoz rendelni.");
                return;
            }
            if (sourceSlides.Count == 0)
            {
                SetStatus("ℹ️ A forrás Layout nincs megállóhoz kötve, így nincs mit átvinni.", Color.DimGray);
                return;
            }

            SetStatus($"📍 {sourceSlides.Count} megálló-kötés átvitele...", Color.Black);
            int created = 0;

            var translator = NewTranslator();
            foreach (var slide in sourceSlides)
            {
                string label = translator.DescribeSlide(slide);
                var payload = translator.TranslateSlide(slide, newLayoutId);
                if (payload == null)
                {
                    ReportTranslation(translator);
                    continue;
                }

                var result = await PostJsonAsync($"{_baseSaveUrl}/slide/save", payload.ToJsonString(),
                    $"Megálló-kötés: {label}");
                if (result.Success)
                {
                    created++;
                }
                else
                {
                    NoteSkipped($"a megálló-kötés mentése sikertelen ({label}) - {result.Error}");
                }
            }

            if (created > 0)
            {
                SetStatus(DryRun
                    ? $"🔍 [száraz futtatás] {created} megálló-kötés jönne létre."
                    : $"✅ {created} megálló-kötés létrehozva - a másolat megjelenik ezeken a megállókon.", Color.ForestGreen);
            }
        }

        /// <summary>
        /// Egy layout megálló-kötései. A slide/list-nek nincs szűrt változata, ezért a teljes
        /// listát kérjük le, és memóriában szűrünk - a mérés szerint ez néhány másodperc.
        /// </summary>
        private async Task<List<JsonObject>?> LoadSlidesForLayoutAsync(int layoutId)
        {
            return await LoadListAsync("slide/list", fromLoadServer: true, body =>
            {
                if (JsonNode.Parse(body) is not JsonArray root) return null;
                return root.OfType<JsonObject>()
                           .Where(o => o["layoutId"]?.GetValue<int?>() == layoutId)
                           .Select(o => (JsonObject)o.DeepClone())
                           .ToList();
            });
        }

        /// <summary>
        /// A félbemaradt layout eltakarítása. A layout/remove kaszkádol az elemekre, tehát egyetlen
        /// hívás elég; külön elem-takarítás nem kell.
        /// </summary>
        private async Task RollbackLayoutAsync(int layoutId)
        {
            if (DryRun || layoutId <= 0) return;

            SetStatus($"↩️ Visszavonás: a hiányos Layout (ID={layoutId}) törlése...", Color.RoyalBlue);
            try
            {
                var (success, body, status) = await DeleteLayoutAsync(layoutId);

                // A layout/remove kaszkádol az elemekre, de a megálló-kötésekre NEM: ha a
                // layouthoz slide tartozik, 422-vel elutasítja a törlést. Ilyenkor előbb a
                // kötéseket kell elbontani.
                if (!success && status == System.Net.HttpStatusCode.UnprocessableEntity)
                {
                    if (await RemoveSlidesOfLayoutAsync(layoutId))
                    {
                        (success, body, status) = await DeleteLayoutAsync(layoutId);
                    }
                }

                if (success)
                {
                    SetStatus($"↩️ A hiányos Layout (ID={layoutId}) törölve - a szerveren nem maradt félkész elem.", Color.RoyalBlue);
                    return;
                }
                SetStatus($"❌ A visszavonás nem sikerült - {ApiErrorFormatter.Format(status, body)}", Color.Red);
                SetStatus($"❗ A(z) {layoutId} azonosítójú Layoutot kézzel kell törölni a {cmbServerSave.SelectedItem} szerveren!", Color.Red);
            }
            catch (Exception ex)
            {
                SetStatus($"❌ A visszavonás nem sikerült: {ex.Message}", Color.Red);
                SetStatus($"❗ A(z) {layoutId} azonosítójú Layoutot kézzel kell törölni a {cmbServerSave.SelectedItem} szerveren!", Color.Red);
            }
        }

        private async Task<(bool Success, string Body, System.Net.HttpStatusCode Status)> DeleteLayoutAsync(int layoutId)
        {
            string url = $"{_baseSaveUrl}/layout/remove?id={layoutId}";
            OperationLog.Request("DELETE", url, null);
            HttpResponseMessage response = await _httpClientSave.DeleteAsync(url);
            string body = await response.Content.ReadAsStringAsync();
            OperationLog.Response((int)response.StatusCode, body);
            return (response.IsSuccessStatusCode, body, response.StatusCode);
        }

        /// <summary>A layouthoz tartozó megálló-kötések elbontása, hogy a layout törölhető legyen.</summary>
        private async Task<bool> RemoveSlidesOfLayoutAsync(int layoutId)
        {
            var slides = await LoadListAsync("slide/list", fromLoadServer: false, body =>
            {
                if (JsonNode.Parse(body) is not JsonArray root) return null;
                return root.OfType<JsonObject>()
                           .Where(o => o["layoutId"]?.GetValue<int?>() == layoutId)
                           .Select(o => o["id"]?.GetValue<int?>() ?? 0)
                           .Where(id => id > 0)
                           .ToList();
            });
            if (slides == null || slides.Count == 0) return false;

            SetStatus($"↩️ {slides.Count} megálló-kötés elbontása a visszavonáshoz...", Color.RoyalBlue);
            foreach (int slideId in slides)
            {
                string url = $"{_baseSaveUrl}/slide/remove?id={slideId}";
                OperationLog.Request("DELETE", url, null);
                HttpResponseMessage response = await _httpClientSave.DeleteAsync(url);
                OperationLog.Response((int)response.StatusCode, await response.Content.ReadAsStringAsync());
                if (!response.IsSuccessStatusCode) return false;
            }
            return true;
        }

        /// <summary>
        /// Ha elem maradt ki a fordításból, a felhasználó döntsön: vállalja a hiányos másolatot,
        /// vagy inkább ne jöjjön létre semmi.
        /// </summary>
        /// <summary>
        /// A mentés előtti előnézet. Ez az egyetlen pont, ahol a művelet még megállítható, ezért
        /// mindent egy helyen mutat: honnan hova, mi fordult le, és mi marad ki.
        /// </summary>
        private bool ConfirmCopy(string entityType, int totalItems, int copiedItems)
        {
            var text = new StringBuilder();

            text.AppendLine("MIT MÁSOLUNK");
            text.AppendLine($"   Típus:     {entityType}");
            text.AppendLine($"   Forrás:    {cmbServerLoad.SelectedItem}  (ID={txtLoadEntityId.Text.Trim()})");
            text.AppendLine($"   Cél:       {cmbServerSave.SelectedItem}");
            text.AppendLine($"   Új név:    \"{txtSaveName.Text.Trim()}\"");
            if (IsSameServer())
            {
                text.AppendLine("   Megjegyzés: azonos szerver - az azonosítók változatlanul maradnak.");
            }
            text.AppendLine();

            if (totalItems > 0)
            {
                text.AppendLine("ELEMEK");
                text.AppendLine(copiedItems == totalItems
                    ? $"   mind a(z) {totalItems} elem átkerül"
                    : $"   {totalItems} elemből {copiedItems} kerül át, {totalItems - copiedItems} kimarad");
                text.AppendLine();
            }

            if (_conversions.Count > 0)
            {
                text.AppendLine("ÁTFORDÍTOTT HIVATKOZÁSOK (a cél szerver azonosítóira)");
                foreach (var pair in _conversions.OrderByDescending(p => p.Value))
                {
                    text.AppendLine($"   {pair.Key,-24} {pair.Value,3} db");
                }
                text.AppendLine();
            }

            if (_skipped.Count > 0)
            {
                text.AppendLine($"KIMARAD ({_skipped.Count} tétel)");
                foreach (var item in _skipped)
                {
                    text.AppendLine($"   • {item}");
                }
                text.AppendLine();
            }

            if (_unknownFields.Count > 0)
            {
                text.AppendLine($"ISMERETLEN MEZŐK ({_unknownFields.Count}) - ezek sem kerülnek át");
                foreach (var field in _unknownFields)
                {
                    text.AppendLine($"   • {field}");
                }
                text.AppendLine();
            }

            text.AppendLine(new string('-', 60));
            text.AppendLine(DryRun
                ? "SZÁRAZ FUTTATÁS: a szerverre semmi nem íródik, csak a napló készül el."
                : $"A fentiek a(z) {cmbServerSave.SelectedItem} szerverre íródnak.");

            using var dialog = new CopyPreviewDialog(text.ToString(), DryRun);
            bool proceed = dialog.ShowDialog(this) == DialogResult.OK;

            OperationLog.Status($"[előnézet] a felhasználó döntése: {(proceed ? "folytatás" : "megszakítás")}");
            return proceed;
        }

        /// <summary>
        /// A szerver megköveteli, hogy az első elem kép vagy közleményhely legyen. Ez a hiba
        /// egyébként csak az elemek mentésekor derülne ki - akkor, amikor a fejléc már létrejött.
        /// </summary>
        private void WarnIfFirstElementInvalid(JsonArray itemsArray)
        {
            if (itemsArray.Count == 0) return;

            // A cél szerver ID-jével dolgozunk, mert a fordítás már lefutott.
            var first = itemsArray.OrderBy(e => e?["prioritySn"]?.GetValue<int?>() ?? int.MaxValue).FirstOrDefault();
            if (first?["elementTypeId"]?.GetValue<int?>() is not int typeId) return;
            if (!_itemTypeSave.TryGetValue(typeId, out string? label)) return;

            bool allowed = label != null
                && (label.Contains("Image", StringComparison.OrdinalIgnoreCase)
                    || label.Contains("Announcement", StringComparison.OrdinalIgnoreCase));
            if (allowed) return;

            SetStatus($"⚠️ Figyelem: az első elem típusa '{label}'. A szerver megköveteli, hogy az első elem "
                    + "kép vagy közleményhely legyen - a mentés emiatt elutasításra kerülhet.", Color.DarkOrange);
        }

        private async Task SaveLayoutEntityAsync(string newName)
        {
            SetStatus("Layout mentése elkezdődött...", Color.Black);

            if (_loadedLayoutItem == null)
            {
                SetStatus($"❌ Hiba: nincs beolvasott Layout elem!", Color.Red);
                return;
            }

            try
            {
                var node = JsonSerializer.SerializeToNode(_loadedLayoutItem, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });

                var translator = NewTranslator();

                if (node is JsonObject nodeObj)
                {
                    nodeObj.Remove("id");
                    nodeObj["name"] = newName;
                    translator.TranslateLayoutHeader(nodeObj);
                }

                DisplayTxtJson(node);

                string jsonOut = node?.ToJsonString(new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                }) ?? "{}";

                // FONTOS: az elemek fordítása MEGELŐZI a fejléc mentését. Így ha valamelyik elem
                // nem fordítható, a felhasználó még azelőtt dönthet a megszakításról, hogy bármi
                // létrejönne a szerveren - nem kell utólag takarítani.
                var itemsArray = new JsonArray();
                var savedNames = new List<string>();
                foreach (var layoutItem in _loadedLayoutItems ?? new List<LayoutItems>())
                {
                    bool itemIsValid = true;
                    LayoutItems originLayoutItem = layoutItem;
                    var itemNode = JsonSerializer.SerializeToNode(layoutItem, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                    });

                    if (itemNode is JsonObject itemObj)
                    {
                        itemObj.Remove("id");  //itemObj["id"] = null; //remove ID
                        itemObj.Remove("layoutId"); //itemObj["layoutId"] = layoutId;
                        itemObj.Remove("content");

                        string itemName = layoutItem.Name;

                        // Az összes hivatkozás fordítása a tesztelt, felülettől független
                        // fordítóban történik; a kép külön úton megy, mert feltöltéssel járhat.
                        itemIsValid &= translator.TranslateLayoutElement(itemObj, itemName);
                        itemIsValid &= await ResolveImageAsync(translator, itemObj, "imageId", itemName);

                        if (itemIsValid)
                        {
                            if (itemObj["announcement"] is JsonObject announcement)
                            {
                                announcement.Remove("id");
                                announcement["name"] = $"{newName} - {DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
                            }
                            //if "name" property is like GUID, then set a new UUID value
                            string? currentName = itemObj["name"]?.GetValue<string>();
                            if (!string.IsNullOrWhiteSpace(currentName) && Guid.TryParse(currentName, out _))
                            {
                                itemObj["name"] = Guid.NewGuid().ToString();
                            }

                        }
                    }
                    if (itemIsValid)
                    {
                        itemsArray.Add(itemNode);
                        savedNames.Add(originLayoutItem.Name);
                    }
                    else
                    {
                        SetStatus($"❌ Elem kihagyva a másolásból: {originLayoutItem.Name} - {originLayoutItem.ElementTypeLabel}", Color.Red);
                    }
                }

                ReportTranslation(translator);

                // Van olyan hiány, amivel a mentés biztosan elbukna? Akkor ne is kezdjük el.
                if (ReportBlockingProblems()) return;

                // Itt még semmit nem írtunk a szerverre: ez az utolsó pont, ahol meg lehet állni.
                if (!ConfirmCopy("Layout", _loadedLayoutItems?.Count ?? 0, itemsArray.Count))
                {
                    SetStatus("⛔ A mentés megszakadt a felhasználó kérésére - a szerveren semmi nem változott.", Color.Red);
                    return;
                }

                // A szerver az első elemre külön szabályt érvényesít; érdemes előre szólni, mert
                // ez a hiba csak a mentés végén derülne ki.
                WarnIfFirstElementInvalid(itemsArray);

                var briefResult = await PostJsonAsync($"{_baseSaveUrl}/layout/save", jsonOut, "Layout fejléc mentése");
                if (!briefResult.Success)
                {
                    SetStatus($"❌ A Layout fejléc mentése sikertelen - {briefResult.Error}", Color.Red);
                    return;
                }

                int layoutId;
                if (briefResult.WasSkipped)
                {
                    // Száraz futtatásnál nincs valódi ID. A helykitöltő csak azért kell, hogy az
                    // elemek JSON-ja is összeálljon, és a fordítás eredménye ellenőrizhető legyen.
                    layoutId = 0;
                    SetStatus("🔍 [száraz futtatás] a Layout fejléc nem jött létre; az elemek layoutId mezője helykitöltő 0.", Color.RoyalBlue);
                }
                else
                {
                    SetStatus($"✅ Sikeres brief mentés a {cmbServerSave.SelectedItem} szerverre.", Color.ForestGreen);

                    //response body contains the saved layout's ID
                    if (!int.TryParse(briefResult.Body.Trim(), out layoutId) || layoutId <= 0)
                    {
                        // A korábbi 10000-es küszöb a PROD2 ID-tartományának véletlene volt, nem
                        // az API szerződése; egy friss telepítésen hamis hibát jelzett volna.
                        SetStatus($"❌ Hiba: a fejléc mentése nem érvényes ID-t adott vissza: '{briefResult.Body}'", Color.Red);
                        return;
                    }
                    SetStatus($"✅ Az új Layout ID-ja: {layoutId}", Color.ForestGreen);
                }

                if (itemsArray.Count == 0)
                {
                    SetStatus("Nincs mentendő Layout elem (csak brief fejléc), ezért a mentési folyamat befejezve.", Color.ForestGreen);
                    return;
                }

                var fullJson = new JsonObject();
                fullJson["layoutId"] = layoutId;
                fullJson["elements"] = itemsArray;
                //fullJson["groupIds"] = node["groupIds"];
                DisplayTxtJson(fullJson, true);
                string jsonFull = fullJson.ToJsonString(new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });

                // Az elemnév-feloldó teszi érthetővé a szerver "elements[3].imageId" alakú hibáit.
                var itemsResult = await PostJsonAsync($"{_baseSaveUrl}/element/save/all", jsonFull,
                    $"Layout elemek mentése ({itemsArray.Count}db)",
                    index => index >= 0 && index < savedNames.Count ? savedNames[index] : null);

                if (itemsResult.WasSkipped)
                {
                    SetStatus("🔍 Száraz futtatás vége - a szerveren semmi nem változott.", Color.RoyalBlue);
                }
                else if (itemsResult.Success)
                {
                    SetStatus($"✅ Layout elemek ({itemsArray.Count}db) sikeresen mentve a {cmbServerSave.SelectedItem} szerverre.", Color.ForestGreen);

                    // A megálló-kötés csak szerverek között követi a másolatot; azonos szerveren
                    // belüli duplikálásnál nem, mert két layout nem versenghet ugyanazon a megállón.
                    if (!IsSameServer() && int.TryParse(txtLoadEntityId.Text.Trim(), out int sourceLayoutId))
                    {
                        await CopySlidesAsync(sourceLayoutId, layoutId);
                    }
                }
                else
                {
                    SetStatus($"❌ A Layout elemek mentése sikertelen - {itemsResult.Error}", Color.Red);
                    // A fejléc már létrejött, de elemek nélkül használhatatlan: takarítsuk el,
                    // különben árva layout marad a szerveren.
                    await RollbackLayoutAsync(layoutId);
                }
            }
            catch (Exception ex)
            {
                SetStatus($"❌ Hiba: {ex.Message}", Color.Red);
            }
        }

        /// <summary>
        /// Jelzi, ha a szerver olyan mezőt küldött, amit a modell nem ismer. Az ilyen mező a
        /// deszerializáláskor csendben eldobódik, és a másolatból is kimarad - ez az egyetlen
        /// jelzés róla, mert az API-nak nincs dokumentációja.
        ///
        /// Hamissal tér vissza, ha a felhasználó a talált mezők láttán megszakítja a műveletet.
        /// </summary>
        private bool CheckModelFields(string rawJson, Type modelType, string what)
        {
            List<string> unknown;
            try
            {
                unknown = ModelFieldGuard.FindUnknownFields(JsonNode.Parse(rawJson), modelType);
            }
            catch (Exception ex)
            {
                // A mezőellenőrzés nem akadályozhatja a munkát.
                OperationLog.Status($"[mezőőr] az ellenőrzés nem futott le: {ex.Message}");
                return true;
            }

            if (unknown.Count == 0) return true;

            SetStatus($"⚠️ Figyelem: a szerver {unknown.Count} olyan mezőt küldött a(z) {what} elemben, amit ez a program nem ismer - ezek a másolatból KIMARADNAK:", Color.DarkOrange);
            foreach (var field in unknown)
            {
                SetStatus($"      • {field}", Color.DarkOrange);
                _unknownFields.Add($"{what}: {field}");
            }
            SetStatus("      (a program frissítésre szorul - a mezőket fel kell venni a modellbe)", Color.DarkOrange);

            var answer = MessageBox.Show(this,
                $"A szerver {unknown.Count} olyan mezőt küldött a(z) {what} elemben, amit ez a program nem ismer:\n\n"
                + FormatFieldList(unknown)
                + "\n\nEzek a mezők a másolatból KIMARADNAK, vagyis a másolat eltérne az eredetitől.\n\n"
                + "Folytatja a beolvasást?\n\n"
                + "Igen  - folytatás, tudomásul véve a hiányt\n"
                + "Nem  - a művelet megszakítása, a beolvasott elem eldobása",
                $"Ismeretlen mezők - {what}",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);

            bool proceed = answer == DialogResult.Yes;
            OperationLog.Status($"[mezőőr] a felhasználó döntése: {(proceed ? "folytatás" : "megszakítás")}");
            return proceed;
        }

        /// <summary>
        /// A betöltött elem eldobása. Így nem marad félig betöltött állapot a memóriában, amit a
        /// felhasználó később véletlenül elmenthetne.
        /// </summary>
        private void DiscardLoadedEntity(string reason)
        {
            _loadedTimetableItem = null;
            _loadedLayoutItem = null;
            _loadedLayoutItems = null;
            _unknownFields.Clear();
            txtSaveName.Text = "";
            txtJson.Text = "";
            SetStatus("⛔ " + reason, Color.Red);
        }

        /// <summary>Hosszú listát nem érdemes egy üzenetablakba zsúfolni.</summary>
        private static string FormatFieldList(List<string> fields)
        {
            const int maxShown = 20;
            var shown = fields.Take(maxShown).Select(f => "    • " + f);
            string text = string.Join(Environment.NewLine, shown);
            if (fields.Count > maxShown)
                text += Environment.NewLine + $"    ... és még {fields.Count - maxShown} db (a teljes lista a naplóban)";
            return text;
        }

        // Base64 tartalmak, amiket nincs értelme a nézőben megmutatni.
        private static readonly string[] LargeContentFields =
            { "content", "imageContent", "rasterContent", "file" };

        private void DisplayTxtJson(object? obj, bool append = false)
        {
            if (obj == null) return;

            // A nagy tartalmakat már a fa szintjén cseréljük ki, nem utólag reguláris
            // kifejezéssel: így nem kell több megabájtos szöveget legenerálni, hogy aztán
            // kivágjunk belőle. A klón azért kell, hogy a MENTENDŐ fát ne csonkítsuk meg.
            JsonNode? node = obj is JsonNode existing
                ? existing.DeepClone()
                : JsonSerializer.SerializeToNode(obj);
            if (node == null) return;

            ReplaceLargeContent(node);

            string json = node.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            if (append)
            {
                txtJson.AppendText((string.IsNullOrEmpty(txtJson.Text) ? "" : Environment.NewLine) + json);
            }
            else
            {
                txtJson.Text = json;
            }
        }

        private static void ReplaceLargeContent(JsonNode? node)
        {
            if (node is JsonObject obj)
            {
                // A kulcsokról másolat kell: az értékadás iteráció közben módosítaná a gyűjteményt.
                foreach (string key in obj.Select(kv => kv.Key).ToList())
                {
                    bool isLarge = LargeContentFields.Contains(key, StringComparer.OrdinalIgnoreCase);
                    if (isLarge && obj[key] is JsonValue value && value.TryGetValue(out string? text) && text != null)
                    {
                        obj[key] = $"...({text.Length} karakter kihagyva)...";
                    }
                    else
                    {
                        ReplaceLargeContent(obj[key]);
                    }
                }
            }
            else if (node is JsonArray array)
            {
                foreach (var item in array)
                {
                    ReplaceLargeContent(item);
                }
            }
        }
    }
}