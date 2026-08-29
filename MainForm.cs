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
        }

        private void SetStatus(string message, Color color)
        {
            txtStatus.SelectionStart = txtStatus.TextLength;
            txtStatus.SelectionLength = 0;
            txtStatus.SelectionColor = color;
            txtStatus.AppendText((string.IsNullOrEmpty(txtStatus.Text) ? "" : Environment.NewLine) + message);
            txtStatus.ScrollToCaret();
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
                var toRemove = new List<string>();
                foreach (var kv in obj)
                {
                    var propName = kv.Key;
                    if (propName.Equals("imageId", StringComparison.OrdinalIgnoreCase))
                    {
                        // Ha Load szerver != Save szerver, törölni kell a háttérképet
                        string? loadServerKey = cmbServerLoad.SelectedItem?.ToString();
                        string? saveServerKey = cmbServerSave.SelectedItem?.ToString();
                        if (loadServerKey != saveServerKey)
                        {
                            toRemove.Add(propName);
                            SetStatus($"⚠️ Figyelem: az ID={kv.Value} kép a másolásból kimarad, mert a Load és Save szerverek különbözőek, majd kézzel pótolja.", Color.Orange);
                        }
                    }
                    else if (propName.Equals("imageContent", StringComparison.OrdinalIgnoreCase))
                    {
                        toRemove.Add(propName);
                    }
                    else if (propName.Equals("displayId", StringComparison.OrdinalIgnoreCase))
                    {
                        ConvertDisplayId1(kv.Value);
                    }
                    else if (propName.Equals("groupIds", StringComparison.OrdinalIgnoreCase))
                    {
                        ConvertGroupIds(kv.Value);
                    }
                    else if (propName.Equals("rasterFontId", StringComparison.OrdinalIgnoreCase))
                    {
                        ConvertRasterFontId(kv);
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
private void ConvertRasterFontId(KeyValuePair<string, JsonNode?> kv)
        {
            if (kv.Value != null && kv.Value.GetValue<int?>() is int rasterFontId)
            {
                var rasterFontLoad = _rasterFontsLoad.FirstOrDefault(rf => rf.Id == rasterFontId);
                if (rasterFontLoad == null)
                {
                    SetStatus($"❌ Hiba: a Load szerveren nem található a rasterFontId: {rasterFontId}", Color.Red);
                    throw new Exception("No matching raster font on Load server");
                }

                var rasterFontSave = _rasterFontsSave.FirstOrDefault(rf => string.Equals(rf.TtFontName, rasterFontLoad.TtFontName, StringComparison.OrdinalIgnoreCase)
                                                                            && rf.Size == rasterFontLoad.Size);
                if (rasterFontSave == null)
                {
                    SetStatus($"❌ Hiba: a Save szerveren nem található a {rasterFontLoad.TtFontName} (Size: {rasterFontLoad.Size}) raster font", Color.Red);
                    throw new Exception("No matching raster font on Save server");
                }

                // Frissítse az értéket a Save szerver rasterFontId-jére
                if (kv.Value is JsonObject parentObj)
                {
                    parentObj[kv.Key] = rasterFontSave.Id;
                }
            }
            else
            {
                SetStatus($"❌ Hiba: a Load szerveren nincs rasterFontId: {kv.Value}", Color.Red);
                throw new Exception("Invalid rasterFontId value");
            }
        }

        private void ConvertDisplayId1(JsonNode? jsonNode)
        {
            if (jsonNode == null) return;
            if (jsonNode.GetValue<int?>() is int displayId)
            {
                var displayLoad = _displaysLoad.FirstOrDefault(d => d.Id == displayId);
                if (displayLoad == null)
                {
                    SetStatus($"❌ Hiba: a Load szerveren nem található a displayId: {displayId}", Color.Red);
                    throw new Exception("No matching display on Load server");
                }

                var displaySave = _displaysSave.FirstOrDefault(d => string.Equals(d.Name, displayLoad.Name, StringComparison.OrdinalIgnoreCase));
                if (displaySave == null)
                {
                    SetStatus($"❌ Hiba: a Save szerveren nem található a {displayLoad.Name} kijelző", Color.Red);
                    throw new Exception("No matching display on Save server");
                }

                // Frissítse az értéket a Save szerver displayId-jére
                if (jsonNode is JsonValue jsonValue && jsonValue.TryGetValue(out int _))
                {
                    jsonNode.ReplaceWith(displaySave.Id);
                }
            }
            else
            {
                SetStatus($"❌ Hiba: a Load szerveren nincs displayId: {jsonNode}", Color.Red);
                throw new Exception("Invalid displayId value");
            }
        }

        private void ConvertGroupIds(JsonNode? node)
        {
            // Ha Load szerver == Save szerver, akkor nem kell konvertálni
            string? loadServerKey = cmbServerLoad.SelectedItem?.ToString();
            string? saveServerKey = cmbServerSave.SelectedItem?.ToString();
            if (loadServerKey == saveServerKey)
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
                            SetStatus($"⚠️ Figyelem: érvénytelen Jogosultság (groupId) a betöltött Elem-nél: {v} <- kihagyva", Color.Orange);
                            continue; // Kihagyja ezt az elemet, folytatja a többivel
                        }

                        var gLoad = _groupsLoad.FirstOrDefault(g => g.Id == loadId.Value);
                        if (gLoad == null)
                        {
                            SetStatus($"⚠️ Figyelem: a 'Load' szerveren nem található a groupId: {loadId} <- kihagyva", Color.Orange);
                            continue; // Kihagyja ezt az elemet, folytatja a többivel
                        }

                        var gSave = _groupsSave.FirstOrDefault(g => string.Equals(g.Name, gLoad.Name, StringComparison.OrdinalIgnoreCase));
                        if (gSave == null)
                        {
                            SetStatus($"⚠️ Figyelem: a 'Save' szerveren nem található a {gLoad.Name} jogosultsági csoport <- kihagyva", Color.Orange);
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
                HttpResponseMessage fullResponse = await _httpClientLoad.GetAsync(fullUrl);
                if (!fullResponse.IsSuccessStatusCode)
                {
                    string err = await fullResponse.Content.ReadAsStringAsync();
                    SetStatus($"❌ Hiba load: {fullResponse.StatusCode} - {err}", Color.Red);
                    return;
                }

                string fullJson = await fullResponse.Content.ReadAsStringAsync();
                var fullItem = JsonSerializer.Deserialize<TimetableItem>(
                    fullJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (fullItem == null)
                {
                    SetStatus($"❌ Hiba: a teljes elem nem értelmezhető.", Color.Red);
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
                HttpResponseMessage briefResponse = await _httpClientLoad.GetAsync(briefUrl);
                if (!briefResponse.IsSuccessStatusCode)
                {
                    string err = await briefResponse.Content.ReadAsStringAsync();
                    SetStatus($"❌ Hiba load-brief: {briefResponse.StatusCode} - {err}", Color.Red);
                    return;
                }

                string briefJson = await briefResponse.Content.ReadAsStringAsync();
                var briefItem = JsonSerializer.Deserialize<LayoutItem>(
                    briefJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (briefItem == null)
                {
                    SetStatus($"❌ Hiba: a load-brief nem értelmezhető.", Color.Red);
                    return;
                }

                _loadedLayoutItem = briefItem;
                txtSaveName.Text = briefItem.Name ?? "";
                DisplayTxtJson(briefItem);
                SetStatus($"✅ Layout brief sikeresen beolvasva a {cmbServerLoad.SelectedItem} szerverről.", Color.ForestGreen);

                string fullUrl = $"{_baseLoadUrl}/element/list/layoutId?layoutId={id}";
                HttpResponseMessage fullResponse = await _httpClientLoad.GetAsync(fullUrl);
                if (!fullResponse.IsSuccessStatusCode)
                {
                    string err = await fullResponse.Content.ReadAsStringAsync();
                    SetStatus($"❌ Hiba full-load: {fullResponse.StatusCode} - {err}", Color.Red);
                    return;
                }

                string fullJson = await fullResponse.Content.ReadAsStringAsync();
                var fullItem = JsonSerializer.Deserialize<List<LayoutItems>>(
                    fullJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (fullItem == null)
                {
                    SetStatus($"❌ Hiba: a teljes full-elem nem értelmezhető.", Color.Red);
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
                await SaveLayoutEntityAsync(newName);
            }
            else
            {
                SetStatus($"❌ Hiba: a kiválasztott elemtípus nem értelmezhető.", Color.Red);
                return;
            }
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

                StringContent content = new StringContent(jsonOut, Encoding.UTF8, "application/json");
                string url = $"{_baseSaveUrl}/dynamic-timetable/save";
                HttpResponseMessage response = await _httpClientSave.PostAsync(url, content);
                if (response.IsSuccessStatusCode)
                {
                    SetStatus($"✅ Sikeres mentés a {cmbServerSave.SelectedItem} szerverre.", Color.ForestGreen);
                }
                else
                {
                    string err = await response.Content.ReadAsStringAsync();
                    SetStatus($"❌ Hiba: {response.StatusCode} - {err}", Color.Red);
                }
            }
            catch (Exception ex)
            {
                SetStatus($"❌ Hiba: {ex.Message}", Color.Red);
            }
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
                    ConvertDisplayId1(nodeObj["displayId"]);
                    ConvertGroupIds(nodeObj["groupIds"]);
                }

                DisplayTxtJson(node);

                string jsonOut = node?.ToJsonString(new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                }) ?? "{}";

                StringContent content = new StringContent(jsonOut, Encoding.UTF8, "application/json");
                string url = $"{_baseSaveUrl}/layout/save";
                HttpResponseMessage response = await _httpClientSave.PostAsync(url, content);
                if (response.IsSuccessStatusCode)
                {
                    SetStatus($"✅ Sikeres brief mentés a {cmbServerSave.SelectedItem} szerverre.", Color.ForestGreen);
                }
                else
                {
                    string err = await response.Content.ReadAsStringAsync();
                    SetStatus($"❌ Hiba: {response.StatusCode} - {err}", Color.Red);
                    return;
                }
                //response body contains the saved layout's ID
                string respBody = await response.Content.ReadAsStringAsync();
                if (!int.TryParse(respBody.Trim(), out int layoutId))
                {
                    SetStatus($"❌ Hiba: a brief mentés nem egy szám ID-t adott vissza: '{respBody}'", Color.Red);
                    return;
                }
                SetStatus($"✅ Az új Layout ID-ja: {layoutId}", Color.ForestGreen);
                if (layoutId < 10000)
                {
                    SetStatus($"❌ Hiba: a brief mentés nem adott vissza megfelelő ID-t! (ID={layoutId})", Color.Red);
                    return;
                }
                // Now save the layout items
                if (_loadedLayoutItems == null || _loadedLayoutItems.Count == 0)
                {
                    SetStatus("Nincs mentendő Layout elem (csak brief fejléc), ezért a mentési folyamat befejezve.", Color.ForestGreen);
                    return;
                }
                var itemsArray = new JsonArray();
                foreach (var layoutItem in _loadedLayoutItems)
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

                        // Convert elementTypeId from Load to Save server
                        if (itemObj["elementTypeId"] is JsonValue etIdValue && etIdValue.TryGetValue(out int etId))
                        {
                            if (_itemTypeLoad.TryGetValue(etId, out string? typeLabel) && !string.IsNullOrEmpty(typeLabel))
                            {
                                var saveId = _itemTypeSave.FirstOrDefault(kvp => kvp.Value == typeLabel).Key;
                                if (saveId != 0)
                                {
                                    itemObj["elementTypeId"] = saveId;
                                }
                                else
                                {
                                    itemIsValid = false;
                                    SetStatus($"⚠️ Figyelem: a Save ({cmbServerSave.SelectedItem}) szerveren nincs '{typeLabel}' elemtípus ({layoutItem.Name}).", Color.Orange);
                                }
                            }
                            else
                            {
                                itemIsValid = false;
                                SetStatus($"⚠️ Figyelem: a Load ({cmbServerLoad.SelectedItem}) szerveren nincs elementTypeId {etId} elem ({layoutItem.Name}).", Color.Orange);
                            }
                        }
                        itemObj.Remove("elementTypeLabel");

                        // Convert anchorX from Load to Save server
                        if (itemObj["anchorX"] is JsonValue axIdValue && axIdValue.TryGetValue(out int axId))
                        {
                            if (_anchorXLoad.TryGetValue(axId, out string? axLabel) && !string.IsNullOrEmpty(axLabel))
                            {
                                var saveAxId = _anchorXSave.FirstOrDefault(kvp => kvp.Value == axLabel).Key;
                                if (saveAxId != 0)
                                {
                                    itemObj["anchorX"] = saveAxId;
                                }
                                else
                                {
                                    itemIsValid = false;
                                    SetStatus($"⚠️ Figyelem: a Save ({cmbServerSave.SelectedItem}) szerveren nincs '{axLabel}' AnchorX érték ({layoutItem.Name}).", Color.Orange);
                                }
                            }
                            else
                            {
                                itemIsValid = false;
                                SetStatus($"⚠️ Figyelem: a Load ({cmbServerLoad.SelectedItem}) szerveren nincs anchorX {axId} elem ({layoutItem.Name}).", Color.Orange);
                            }
                        }
                        itemObj.Remove("anchorXLabel");

                        // Convert anchorY from Load to Save server
                        if (itemObj["anchorY"] is JsonValue ayIdValue && ayIdValue.TryGetValue(out int ayId))
                        {
                            if (_anchorYLoad.TryGetValue(ayId, out string? ayLabel) && !string.IsNullOrEmpty(ayLabel))
                            {
                                var saveAyId = _anchorYSave.FirstOrDefault(kvp => kvp.Value == ayLabel).Key;
                                if (saveAyId != 0)
                                {
                                    itemObj["anchorY"] = saveAyId;
                                }
                                else
                                {
                                    itemIsValid = false;
                                    SetStatus($"⚠️ Figyelem: a Save ({cmbServerSave.SelectedItem}) szerveren nincs '{ayLabel}' AnchorY érték ({layoutItem.Name}).", Color.Orange);
                                }
                            }
                            else
                            {
                                itemIsValid = false;
                                SetStatus($"⚠️ Figyelem: a Load ({cmbServerLoad.SelectedItem}) szerveren nincs anchorY {ayId} elem ({layoutItem.Name}).", Color.Orange);
                            }
                        }
                        itemObj.Remove("anchorYLabel");

                        // Convert fontColor from Load to Save server
                        if (itemObj["fontColor"] is JsonValue fcIdValue && fcIdValue.TryGetValue(out int fcId))
                        {
                            if (_textColorLoad.TryGetValue(fcId, out string? fcLabel) && !string.IsNullOrEmpty(fcLabel))
                            {
                                var saveFcId = _textColorSave.FirstOrDefault(kvp => kvp.Value == fcLabel).Key;
                                if (saveFcId != 0)
                                {
                                    itemObj["fontColor"] = saveFcId;
                                }
                                else
                                {
                                    itemIsValid = false;
                                    SetStatus($"⚠️ Figyelem: a Save ({cmbServerSave.SelectedItem}) szerveren nincs '{fcLabel}' TextColor érték ({layoutItem.Name}).", Color.Orange);
                                }
                            }
                            else
                            {
                                itemIsValid = false;
                                SetStatus($"⚠️ Figyelem: a Load ({cmbServerLoad.SelectedItem}) szerveren nincs fontColor {fcId} elem ({layoutItem.Name}).", Color.Orange);
                            }
                        }

                        // Convert backgroundColor from Load to Save server
                        if (itemObj["backgroundColor"] is JsonValue bcIdValue && bcIdValue.TryGetValue(out int bcId))
                        {
                            if (_textColorLoad.TryGetValue(bcId, out string? bcLabel) && !string.IsNullOrEmpty(bcLabel))
                            {
                                var saveBcId = _textColorSave.FirstOrDefault(kvp => kvp.Value == bcLabel).Key;
                                if (saveBcId != 0)
                                {
                                    itemObj["backgroundColor"] = saveBcId;
                                }
                                else
                                {
                                    itemIsValid = false;
                                    SetStatus($"⚠️ Figyelem: a Save ({cmbServerSave.SelectedItem}) szerveren nincs '{bcLabel}' TextColor érték ({layoutItem.Name}).", Color.Orange);
                                }
                            }
                            else
                            {
                                itemIsValid = false;
                                SetStatus($"⚠️ Figyelem: a Load ({cmbServerLoad.SelectedItem}) szerveren nincs backgroundColor {bcId} elem ({layoutItem.Name}).", Color.Orange);
                            }
                        }
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
                    }
                    else
                    {
                        SetStatus($"❌ Hiba: elem kihagyva a másolásból: {originLayoutItem.Name} - {originLayoutItem.ElementTypeLabel}", Color.Red);
                    }
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
                StringContent contentFull = new StringContent(jsonFull, Encoding.UTF8, "application/json");
                string urlFull = $"{_baseSaveUrl}/element/save/all";
                HttpResponseMessage responseFull = await _httpClientSave.PostAsync(urlFull, contentFull);
                if (responseFull.IsSuccessStatusCode)
                {
                    SetStatus($"✅ Layout elemek ({itemsArray.Count}db) sikeresen mentve a {cmbServerSave.SelectedItem} szerverre.", Color.ForestGreen);
                }
                else
                {
                    string err = await responseFull.Content.ReadAsStringAsync();
                    SetStatus($"❌ Hiba Layout elemek mentésekor: {responseFull.StatusCode} - {err}", Color.Red);
                }
            }
            catch (Exception ex)
            {
                SetStatus($"❌ Hiba: {ex.Message}", Color.Red);
            }
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
