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
using SmartpageTimetableDuplicateV1.Models;

namespace SmartpageTimetableDuplicateV1
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
        private List<DisplayInfo> _displaysLoad = new List<DisplayInfo>();
        private List<DisplayInfo> _displaysSave = new List<DisplayInfo>();
        private List<RasterFontInfo> _rasterFontsLoad = new List<RasterFontInfo>();
        private List<RasterFontInfo> _rasterFontsSave = new List<RasterFontInfo>();
        private List<GroupInfo> _groupsLoad = new List<GroupInfo>();
        private List<GroupInfo> _groupsSave = new List<GroupInfo>();
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

        // A másolás közben kihagyott dolgok, hogy a művelet végén egyben látszódjanak.
        private readonly List<string> _skipped = new List<string>();

        // A beolvasott elemben talált ismeretlen mezők. Azért marad meg a mentésig, mert a
        // beolvasás és a mentés között eltelhet idő, és a figyelmeztetés kicsúszhat a képből.
        private readonly List<string> _unknownFields = new List<string>();

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

            // --- státuszmező formázás ---
            txtStatus.Font = new Font("Segoe UI", 10, FontStyle.Regular);
            txtStatus.ForeColor = Color.Black;

            // --- JSON mező formázás ---
            txtJson.Font = new Font("Consolas", 9);

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

        // A szerverek között az ID-k nem hordozhatók, a nevek igen - de nem mindig karakterre
        // pontosan: a PROD-on például a "SourceSans3-Bold " fontcsalád neve záró szóközzel
        // szerepel, a DEMO-n anélkül. Minden név szerinti párosítás ezen a normalizáláson megy át.
        private static bool NameEquals(string? a, string? b)
            => string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);

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

        private record DisplayInfo(int Id, string Name);
        private record RasterFontInfo(int Id, string TtFontName, int Size);
        private record GroupInfo(int Id, string Name);
        private record AnchorXItem(int Id, string Label);
        private record AnchorYItem(int Id, string Label);
        private record TextColorItem(int Id, string Label);

        /// <summary>Bármi, amit név szerint kell újrakeresni a másik szerveren (kép, rács, menetrend).</summary>
        private record NamedEntity(int Id, string Name);

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

        private async Task<List<DisplayInfo>?> LoadDisplaysList(bool fromLoadServer)
        {
            return await LoadListAsync("display/list", fromLoadServer, DeserializeDisplaysList);
        }
        private List<DisplayInfo>? DeserializeDisplaysList(string body)
        {
            var root = JsonNode.Parse(body) as JsonArray;
            if (root == null)
            {
                return null;
            }
            var list = new List<DisplayInfo>();
            foreach (var item in root)
            {
                if (item is JsonObject groupObj)
                {
                    int? id = groupObj["id"]?.GetValue<int?>();
                    string? name = groupObj["name"]?.GetValue<string?>();
                    if (id.HasValue && !string.IsNullOrEmpty(name))
                    {
                        list.Add(new DisplayInfo(id.Value, name!));
                    }
                }
            }
            return list;
        }

        private async Task<List<GroupInfo>?> LoadGroupsList(bool fromLoadServer)
        {
            return await LoadListAsync("group/list", fromLoadServer, DeserializeGroupsList);
        }
        private List<GroupInfo>? DeserializeGroupsList(string body)
        {
            var root = JsonNode.Parse(body) as JsonArray;
            if (root == null)
            {
                return null;
            }
            var list = new List<GroupInfo>();
            foreach (var item in root)
            {
                if (item is JsonObject groupObj)
                {
                    int? id = groupObj["id"]?.GetValue<int?>();
                    string? name = groupObj["name"]?.GetValue<string?>();
                    if (id.HasValue && !string.IsNullOrEmpty(name))
                    {
                        list.Add(new GroupInfo(id.Value, name!));
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


        private void RemoveIdProperties(JsonNode? node)
        {
            if (node is JsonObject obj)
            {
                // Előbb csak besorolunk, módosítani csak a ciklus után szabad: az értékcsere
                // (ReplaceWith) ugyanazt az objektumot írja, amin a foreach fut.
                var toRemove = new List<string>();
                var displayIdKeys = new List<string>();
                var groupIdsKeys = new List<string>();
                var rasterFontKeys = new List<string>();

                foreach (var kv in obj)
                {
                    var propName = kv.Key;
                    if (propName.Equals("imageId", StringComparison.OrdinalIgnoreCase))
                    {
                        // Ha Load szerver != Save szerver, törölni kell a háttérképet
                        if (!IsSameServer())
                        {
                            toRemove.Add(propName);
                            NoteSkipped($"az ID={kv.Value} háttérkép kimarad, mert a Load és Save szerver különböző - kézzel pótolandó.");
                        }
                    }
                    else if (propName.Equals("imageContent", StringComparison.OrdinalIgnoreCase))
                    {
                        toRemove.Add(propName);
                    }
                    else if (propName.Equals("displayId", StringComparison.OrdinalIgnoreCase))
                    {
                        displayIdKeys.Add(propName);
                    }
                    else if (propName.Equals("groupIds", StringComparison.OrdinalIgnoreCase))
                    {
                        groupIdsKeys.Add(propName);
                    }
                    else if (propName.Equals("rasterFontId", StringComparison.OrdinalIgnoreCase))
                    {
                        rasterFontKeys.Add(propName);
                    }
                    else if (propName.Equals("id", StringComparison.OrdinalIgnoreCase) ||
                        propName.EndsWith("Id", StringComparison.OrdinalIgnoreCase))
                    {
                        toRemove.Add(propName);
                    }
                }

                foreach (var name in toRemove)
                {
                    obj.Remove(name);
                }
                foreach (var key in displayIdKeys)
                {
                    ConvertDisplayId(obj, key);
                }
                foreach (var key in groupIdsKeys)
                {
                    ConvertGroupIds(obj[key]);
                }
                foreach (var key in rasterFontKeys)
                {
                    ConvertRasterFontId(obj, key);
                }

                foreach (var kv in obj)
                {
                    RemoveIdProperties(kv.Value);
                }
            }
            else if (node is JsonArray arr)
            {
                foreach (var item in arr)
                {
                    RemoveIdProperties(item);
                }
            }
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
        /// Load-oldali ID -> címke -> Save-oldali ID fordítás (elemtípus, anchor, szín). Igazzal tér
        /// vissza, ha a mező nincs kitöltve (nincs mit fordítani) vagy a fordítás sikerült.
        /// </summary>
        private bool TryConvertByLabel(JsonObject itemObj, string field,
            Dictionary<int, string> loadMap, Dictionary<int, string> saveMap,
            string what, string itemName)
        {
            if (itemObj[field] is not JsonValue value || !value.TryGetValue(out int loadId))
                return true;

            if (!loadMap.TryGetValue(loadId, out string? label) || string.IsNullOrEmpty(label))
            {
                NoteSkipped($"a Load ({cmbServerLoad.SelectedItem}) szerveren nincs {field}={loadId} ({itemName}).");
                return false;
            }

            // A találat hiányát a null érték jelzi, nem a 0-s kulcs: a 0 elvileg érvényes ID is lehet.
            var match = saveMap.FirstOrDefault(kvp => NameEquals(kvp.Value, label));
            if (match.Value == null)
            {
                NoteSkipped($"a Save ({cmbServerSave.SelectedItem}) szerveren nincs '{label}' {what} ({itemName}).");
                return false;
            }

            itemObj[field] = match.Key;
            return true;
        }

        /// <summary>
        /// Load-oldali ID -> név -> Save-oldali ID fordítás (kép, rács, menetrend). Egy szerveren
        /// belül nem fut le: ott az eredeti ID a helyes.
        /// </summary>
        private bool TryConvertByName(JsonObject itemObj, string field,
            List<NamedEntity> loadList, List<NamedEntity> saveList,
            string what, string itemName)
        {
            if (IsSameServer()) return true;
            if (itemObj[field] is not JsonValue value || !value.TryGetValue(out int loadId))
                return true;

            var loadEntity = loadList.FirstOrDefault(e => e.Id == loadId);
            if (loadEntity == null)
            {
                NoteSkipped($"a Load ({cmbServerLoad.SelectedItem}) szerveren nincs {what} ezzel az azonosítóval: {loadId} ({itemName}).");
                return false;
            }

            var saveEntity = saveList.FirstOrDefault(e => NameEquals(e.Name, loadEntity.Name));
            if (saveEntity == null)
            {
                NoteSkipped($"a Save ({cmbServerSave.SelectedItem}) szerveren nincs '{loadEntity.Name}' nevű {what} ({itemName}).");
                return false;
            }

            itemObj[field] = saveEntity.Id;
            return true;
        }

        /// <summary>
        /// A raszterfontot név + méret pár azonosítja. Nem dob kivételt, mint a menetrend-ági
        /// párja: itt egyetlen elem hibája miatt nem kell az egész műveletet eldobni.
        /// </summary>
        private bool TryConvertRasterFontField(JsonObject itemObj, string field, string itemName)
        {
            if (IsSameServer()) return true;
            if (itemObj[field] is not JsonValue value || !value.TryGetValue(out int loadId))
                return true;

            var loadFont = _rasterFontsLoad.FirstOrDefault(rf => rf.Id == loadId);
            if (loadFont == null)
            {
                NoteSkipped($"a Load ({cmbServerLoad.SelectedItem}) szerveren nincs raszterfont ezzel az azonosítóval: {loadId} ({itemName}).");
                return false;
            }

            var saveFont = _rasterFontsSave.FirstOrDefault(rf => NameEquals(rf.TtFontName, loadFont.TtFontName) && rf.Size == loadFont.Size);
            if (saveFont == null)
            {
                NoteSkipped($"a Save ({cmbServerSave.SelectedItem}) szerveren nincs '{loadFont.TtFontName}' {loadFont.Size}px raszterfont ({itemName}) - a fontok API-ból nem vihetők át, ezt kézzel kell pótolni.");
                return false;
            }

            itemObj[field] = saveFont.Id;
            return true;
        }

        private void ConvertRasterFontId(JsonObject parent, string key)
        {
            if (parent[key]?.GetValue<int?>() is not int rasterFontId)
            {
                SetStatus($"❌ Hiba: a Load szerveren nincs rasterFontId: {parent[key]}", Color.Red);
                throw new Exception("Invalid rasterFontId value");
            }

            var rasterFontLoad = _rasterFontsLoad.FirstOrDefault(rf => rf.Id == rasterFontId);
            if (rasterFontLoad == null)
            {
                SetStatus($"❌ Hiba: a Load szerveren nem található a rasterFontId: {rasterFontId}", Color.Red);
                throw new Exception("No matching raster font on Load server");
            }

            var rasterFontSave = _rasterFontsSave.FirstOrDefault(rf => NameEquals(rf.TtFontName, rasterFontLoad.TtFontName)
                                                                        && rf.Size == rasterFontLoad.Size);
            if (rasterFontSave == null)
            {
                SetStatus($"❌ Hiba: a Save szerveren nem található a {rasterFontLoad.TtFontName} (Size: {rasterFontLoad.Size}) raster font", Color.Red);
                throw new Exception("No matching raster font on Save server");
            }

            // Az értékadás korábban egy "kv.Value is JsonObject" feltétel mögött állt - a mező
            // viszont szám, sosem objektum, így a fordítás eredménye soha nem íródott vissza.
            parent[key] = rasterFontSave.Id;
        }

        private void ConvertDisplayId(JsonObject parent, string key)
        {
            if (parent[key] == null) return;
            if (parent[key]?.GetValue<int?>() is not int displayId)
            {
                SetStatus($"❌ Hiba: a Load szerveren nincs displayId: {parent[key]}", Color.Red);
                throw new Exception("Invalid displayId value");
            }

            var displayLoad = _displaysLoad.FirstOrDefault(d => d.Id == displayId);
            if (displayLoad == null)
            {
                SetStatus($"❌ Hiba: a Load szerveren nem található a displayId: {displayId}", Color.Red);
                throw new Exception("No matching display on Load server");
            }

            var displaySave = _displaysSave.FirstOrDefault(d => NameEquals(d.Name, displayLoad.Name));
            if (displaySave == null)
            {
                SetStatus($"❌ Hiba: a Save szerveren nem található a {displayLoad.Name} kijelző", Color.Red);
                throw new Exception("No matching display on Save server");
            }

            parent[key] = displaySave.Id;
        }

        private void ConvertGroupIds(JsonNode? node)
        {
            if (IsSameServer())
            {
                return; // Ugyanaz a szerver, nincs szükség konverzióra
            }

            // Handle case where node is the groupIds array itself
            if (node is JsonArray arr && arr.Count > 0)
            {
                // Check if this is an array of integers (groupIds)
                var firstElem = arr.FirstOrDefault();
                if (firstElem?.GetValue<int?>().HasValue ?? false)
                {
                    // This is the groupIds array, map each ID
                    var newIds = new List<int>();
                    foreach (var v in arr)
                    {
                        int? loadId = v?.GetValue<int?>();
                        if (!loadId.HasValue)
                        {
                            NoteSkipped($"érvénytelen jogosultsági csoport (groupId): {v}");
                            continue; // Kihagyja ezt az elemet, folytatja a többivel
                        }

                        var gLoad = _groupsLoad.FirstOrDefault(g => g.Id == loadId.Value);
                        if (gLoad == null)
                        {
                            NoteSkipped($"a Load szerveren nincs groupId={loadId} jogosultsági csoport.");
                            continue; // Kihagyja ezt az elemet, folytatja a többivel
                        }

                        var gSave = _groupsSave.FirstOrDefault(g => NameEquals(g.Name, gLoad.Name));
                        if (gSave == null)
                        {
                            NoteSkipped($"a Save szerveren nincs '{gLoad.Name}' jogosultsági csoport.");
                            continue; // Kihagyja ezt az elemet, folytatja a többivel
                        }

                        newIds.Add(gSave.Id);
                    }

                    // Replace the array content in-place
                    arr.Clear();
                    foreach (var id in newIds)
                    {
                        arr.Add(id);
                    }
                    return;
                }
            }
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

            // Utolsó lehetőség a visszalépésre, mielőtt bármit a szerverre írnánk.
            if (!ConfirmSaveWithUnknownFields())
            {
                SetStatus("⛔ A mentés megszakadt a felhasználó kérésére - a szerveren semmi nem változott.", Color.Red);
                return;
            }

            _skipped.Clear();
            OperationLog.BeginOperation(
                $"MENTÉS - {cmbLoadEntityType.SelectedItem} \"{txtSaveName.Text.Trim()}\"",
                serverLoadKey, serverSaveKey, DryRun);
            if (DryRun)
            {
                SetStatus("🔍 Száraz futtatás: a fordítás lefut, de a szerverre semmi nem íródik.", Color.RoyalBlue);
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
                if (!IsSameServer() && !await LoadCrossServerNameTablesAsync())
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

                RemoveIdProperties(node);

                if (node is JsonObject nodeObj)
                {
                    nodeObj["name"] = newName;
                }

                DisplayTxtJson(node);

                string jsonOut = node?.ToJsonString(new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                }) ?? "{}";

                // A fordítás lefutott, de még semmit nem írtunk: ha valami kimaradt (tipikusan a
                // háttérkép eltérő szerverek között), most lehet nemet mondani.
                if (_skipped.Count > 0 && !ConfirmIncompleteCopy($"{_skipped.Count} hivatkozás"))
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
        private async Task<bool> LoadCrossServerNameTablesAsync()
        {
            var tables = new (string Endpoint, string Label, Action<List<NamedEntity>> SetLoad, Action<List<NamedEntity>> SetSave)[]
            {
                ("image/list", "kép", l => _imagesLoad = l, l => _imagesSave = l),
                ("grid/list", "rács", l => _gridsLoad = l, l => _gridsSave = l),
                ("dynamic-timetable/list", "dinamikus menetrend", l => _timetablesLoad = l, l => _timetablesSave = l),
            };

            foreach (var (endpoint, label, setLoad, setSave) in tables)
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
            }
            return true;
        }

        /// <summary>
        /// A félbemaradt layout eltakarítása. A layout/remove kaszkádol az elemekre, tehát egyetlen
        /// hívás elég; külön elem-takarítás nem kell.
        /// </summary>
        private async Task RollbackLayoutAsync(int layoutId)
        {
            if (DryRun || layoutId <= 0) return;

            string url = $"{_baseSaveUrl}/layout/remove?id={layoutId}";
            SetStatus($"↩️ Visszavonás: a hiányos Layout (ID={layoutId}) törlése...", Color.RoyalBlue);
            try
            {
                OperationLog.Request("DELETE", url, null);
                HttpResponseMessage response = await _httpClientSave.DeleteAsync(url);
                string body = await response.Content.ReadAsStringAsync();
                OperationLog.Response((int)response.StatusCode, body);

                if (response.IsSuccessStatusCode)
                {
                    SetStatus($"↩️ A hiányos Layout (ID={layoutId}) törölve - a szerveren nem maradt félkész elem.", Color.RoyalBlue);
                    return;
                }
                SetStatus($"❌ A visszavonás nem sikerült - {ApiErrorFormatter.Format(response.StatusCode, body)}", Color.Red);
                SetStatus($"❗ A(z) {layoutId} azonosítójú Layoutot kézzel kell törölni a {cmbServerSave.SelectedItem} szerveren!", Color.Red);
            }
            catch (Exception ex)
            {
                SetStatus($"❌ A visszavonás nem sikerült: {ex.Message}", Color.Red);
                SetStatus($"❗ A(z) {layoutId} azonosítójú Layoutot kézzel kell törölni a {cmbServerSave.SelectedItem} szerveren!", Color.Red);
            }
        }

        /// <summary>
        /// Ha elem maradt ki a fordításból, a felhasználó döntsön: vállalja a hiányos másolatot,
        /// vagy inkább ne jöjjön létre semmi.
        /// </summary>
        private bool ConfirmIncompleteCopy(string what)
        {
            var answer = MessageBox.Show(this,
                $"{what} nem másolható át, mert a hivatkozás nem található meg a cél szerveren:\n\n"
                + FormatFieldList(_skipped)
                + "\n\nHa folytatja, a másolat ezek nélkül jön létre, tehát eltér az eredetitől.\n\n"
                + "Folytatja a mentést?\n\n"
                + "Igen  - mentés a hiányos tartalommal\n"
                + "Nem  - megszakítás; a szerveren semmi nem jön létre",
                "Hiányos másolat",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);

            bool proceed = answer == DialogResult.Yes;
            OperationLog.Status($"[hiányos másolat] a felhasználó döntése: {(proceed ? "folytatás" : "megszakítás")}");
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

                if (node is JsonObject nodeObj)
                {
                    nodeObj.Remove("id");
                    nodeObj["name"] = newName;
                    ConvertDisplayId(nodeObj, "displayId");
                    ConvertGroupIds(nodeObj["groupIds"]);
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

                        // Címke szerinti fordítás: a Load-oldali ID-hez tartozó szöveges címkét
                        // keressük meg a Save szerveren, és annak az ID-jét írjuk vissza.
                        itemIsValid &= TryConvertByLabel(itemObj, "elementTypeId", _itemTypeLoad, _itemTypeSave, "elemtípus", itemName);
                        itemObj.Remove("elementTypeLabel");

                        itemIsValid &= TryConvertByLabel(itemObj, "anchorX", _anchorXLoad, _anchorXSave, "AnchorX érték", itemName);
                        itemObj.Remove("anchorXLabel");

                        itemIsValid &= TryConvertByLabel(itemObj, "anchorY", _anchorYLoad, _anchorYSave, "AnchorY érték", itemName);
                        itemObj.Remove("anchorYLabel");

                        itemIsValid &= TryConvertByLabel(itemObj, "fontColor", _textColorLoad, _textColorSave, "TextColor érték", itemName);
                        itemIsValid &= TryConvertByLabel(itemObj, "backgroundColor", _textColorLoad, _textColorSave, "TextColor érték", itemName);

                        // Név szerinti fordítás. Ezek a mezők korábban érintetlenül mentek át, így
                        // eltérő szerverre másolva vagy 422-t okoztak, vagy - ha az ID ott véletlenül
                        // létezett - némán egy másik képre/rácsra/menetrendre mutattak.
                        itemIsValid &= TryConvertByName(itemObj, "imageId", _imagesLoad, _imagesSave, "kép", itemName);
                        itemIsValid &= TryConvertByName(itemObj, "gridId", _gridsLoad, _gridsSave, "rács", itemName);
                        itemIsValid &= TryConvertByName(itemObj, "dynamicTimetableId", _timetablesLoad, _timetablesSave, "dinamikus menetrend", itemName);

                        // A raszterfont név + méret párral azonosítható, ezért külön úton megy.
                        itemIsValid &= TryConvertRasterFontField(itemObj, "rasterFontId", itemName);
                        itemObj.Remove("ttFontName");

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

                // Itt még semmit nem írtunk a szerverre: ha elem maradt ki, most lehet nemet mondani.
                int skippedItems = (_loadedLayoutItems?.Count ?? 0) - itemsArray.Count;
                if (skippedItems > 0 && !ConfirmIncompleteCopy($"{skippedItems} elem"))
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

        /// <summary>
        /// A mentés előtti utolsó emlékeztető, ha a beolvasáskor ismeretlen mezők voltak. A
        /// beolvasás óta eltelhetett idő, és a figyelmeztetés kicsúszhatott a státuszmezőből.
        /// </summary>
        private bool ConfirmSaveWithUnknownFields()
        {
            if (_unknownFields.Count == 0) return true;

            var answer = MessageBox.Show(this,
                $"A beolvasott elemben {_unknownFields.Count} olyan mező volt, amit ez a program nem ismer:\n\n"
                + FormatFieldList(_unknownFields)
                + "\n\nHa most ment, ezek az adatok nem kerülnek át a másolatba.\n\n"
                + "Folytatja a mentést?",
                "Ismeretlen mezők - a másolat hiányos lesz",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);

            bool proceed = answer == DialogResult.Yes;
            OperationLog.Status($"[mezőőr] mentés előtti döntés: {(proceed ? "folytatás" : "megszakítás")}");
            return proceed;
        }

        private void DisplayTxtJson(object? obj, bool append = false)
        {
            if (obj == null) return;
            string json = JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
            string shortJson = Regex.Replace(json, "\"(content|imageContent)\":\\s*\"[^\"]*\"", "\"$1\": \"...xxx...\"");
            if (append)
            {
                txtJson.AppendText((string.IsNullOrEmpty(txtJson.Text) ? "" : Environment.NewLine) + shortJson);
            }
            else
            {
                txtJson.Text = shortJson;
            }
        }
    }
}
