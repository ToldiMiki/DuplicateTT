using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows.Forms;
using SmartpageTimetableDuplicateV1.Models;

namespace SmartpageTimetableDuplicateV1
{
    public partial class MainForm : Form
    {
        private readonly HttpClient _httpClient = new HttpClient();
        private TimetableItem? _loadedItem;
        private List<RasterFontInfo> _rasterFontsLoad = new List<RasterFontInfo>();
        private List<RasterFontInfo> _rasterFontsSave = new List<RasterFontInfo>();
        private List<GroupInfo> _groupsLoad = new List<GroupInfo>();
        private List<GroupInfo> _groupsSave = new List<GroupInfo>();

        // In-memory auth/session values (UI fields removed)
        private string? _loadAuth;
        private string? _saveAuth;
        private string? _loadSession;
        private string? _saveSession;

        // In-memory login usernames
        private string? _loadUsername;
        private string? _saveUsername;

        // Flag to suppress login dialog during auto-copy
        private bool _isAutoCopyingCredentials = false;

        private readonly Dictionary<string, string> _serverUrls = new()
        {
            { "DEV", "https://smartpage-dev.hclinear.hu/backend/api/v1/dynamic-timetable" },
            { "DEMO", "https://smartpage-demo.hclinear.hu/backend/api/v1/dynamic-timetable" },
            { "PROD", "https://smartpage.hclinear.hu/backend/api/v1/dynamic-timetable" }
        };

        public MainForm()
        {
            InitializeComponent();

            // --- dropdown alapértékek ---
            cmbServerLoad.Items.AddRange(new[] { "DEV", "DEMO", "PROD" });
            cmbServerSave.Items.AddRange(new[] { "DEV", "DEMO", "PROD" });
            cmbServerLoad.SelectedIndex = -1;
            cmbServerSave.SelectedIndex = -1;

            // Hook combobox change events to trigger login dialog
            cmbServerLoad.SelectedIndexChanged += CmbServer_SelectedIndexChanged;
            cmbServerSave.SelectedIndexChanged += CmbServer_SelectedIndexChanged;

            // --- státuszmező formázás ---
            txtStatus.Font = new Font("Segoe UI", 10, FontStyle.Bold);
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
            txtStatus.ForeColor = color;
            if (string.IsNullOrEmpty(txtStatus.Text))
            {
                txtStatus.Text = message;
            }
            else
            {
                txtStatus.AppendText(Environment.NewLine + message);
            }
            txtStatus.Refresh();
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

            using (LoginDialog loginDialog = new LoginDialog(serverKey, _httpClient))
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

                        // Automatically copy Load credentials to Save server (without showing login dialog)
                        _isAutoCopyingCredentials = true;
                        _saveAuth = _loadAuth;
                        _saveSession = _loadSession;
                        _saveUsername = _loadUsername;
                        cmbServerSave.SelectedItem = serverKey;
                        txtSaveUsername.Text = _saveUsername;
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
                    }
                }
                else
                {
                    combo.SelectedIndex = -1;
                    SetStatus($"⚠️ Bejelentkezés visszavonva a {serverKey} szervernél.", Color.Orange);
                }
            }
        }

        private string GetSelectedBaseUrl(ComboBox combo)
        {
            string key = combo.SelectedItem?.ToString() ?? "DEV";
            return _serverUrls[key];
        }

        private record RasterFontInfo(int Id, string TtFontName, int Size);
        private record GroupInfo(int Id, string Name);

        private async Task<List<RasterFontInfo>?> LoadFontsList(string key)
        {
            try
            {
                string endpoint = _serverUrls[key].Replace("dynamic-timetable", "raster-font/listFonts");

                _httpClient.DefaultRequestHeaders.Clear();

                string saveKey = cmbServerSave.SelectedItem?.ToString() ?? "DEV";
                string token;
                string session;
                if (key == saveKey)
                {
                    token = _saveAuth?.Trim() ?? "";
                    session = _saveSession?.Trim() ?? "";
                }
                else
                {
                    token = _loadAuth?.Trim() ?? "";
                    session = _loadSession?.Trim() ?? "";
                }

                if (!string.IsNullOrEmpty(token))
                    _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
                if (!string.IsNullOrEmpty(session))
                    _httpClient.DefaultRequestHeaders.Add("sessionid", session);

                HttpResponseMessage resp = await _httpClient.GetAsync(endpoint);
                if (!resp.IsSuccessStatusCode)
                {
                    string err = await resp.Content.ReadAsStringAsync();
                    SetStatus($"Hiba fonts list: {resp.StatusCode} - {err}", Color.Red);
                    return null;
                }

                string body = await resp.Content.ReadAsStringAsync();
                var root = JsonNode.Parse(body) as JsonArray;
                if (root == null)
                {
                    SetStatus("Hiba: raster-font lista nem értelmezhető.", Color.Red);
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
                SetStatus($"✅ Betöltve {list.Count}db raster font (key={key}).", Color.ForestGreen);
                return list;
            }
            catch (Exception ex)
            {
                SetStatus($"Hiba betöltéskor: {ex.Message}", Color.Red);
                return null;
            }
        }

        private async Task<List<GroupInfo>?> LoadGroupsList(string key, bool isLoadServer = true)
        {
            try
            {
                string endpoint = _serverUrls[key].Replace("dynamic-timetable", "group/list");

                _httpClient.DefaultRequestHeaders.Clear();

                string token = isLoadServer ? (_loadAuth?.Trim() ?? "") : (_saveAuth?.Trim() ?? "");
                string session = isLoadServer ? (_loadSession?.Trim() ?? "") : (_saveSession?.Trim() ?? "");

                if (!string.IsNullOrEmpty(token))
                    _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
                if (!string.IsNullOrEmpty(session))
                    _httpClient.DefaultRequestHeaders.Add("sessionid", session);

                HttpResponseMessage resp = await _httpClient.GetAsync(endpoint);
                if (!resp.IsSuccessStatusCode)
                {
                    string err = await resp.Content.ReadAsStringAsync();
                    SetStatus($"Hiba group list: {resp.StatusCode} - {err}", Color.Red);
                    return null;
                }

                string body = await resp.Content.ReadAsStringAsync();
                var root = JsonNode.Parse(body) as JsonArray;
                if (root == null)
                {
                    SetStatus("Hiba: group lista nem értelmezhető.", Color.Red);
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
                SetStatus($"✅ Betöltve {list.Count}db csoport a {(isLoadServer ? "Load" : "Save")} szerverről.", Color.ForestGreen);
                return list;
            }
            catch (Exception ex)
            {
                SetStatus($"Hiba csoportok betöltésekor: {ex.Message}", Color.Red);
                return null;
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
                            SetStatus($"Figyelmeztetés: érvénytelen Jogosultság (groupId) a betöltött Elem-nél: {v} <- kihagyva", Color.Orange);
                            continue; // Kihagyja ezt az elemet, folytatja a többivel
                        }

                        var gLoad = _groupsLoad.FirstOrDefault(g => g.Id == loadId.Value);
                        if (gLoad == null)
                        {
                            SetStatus($"Figyelmeztetés: a 'Load' szerveren nem található a groupId: {loadId} <- kihagyva", Color.Orange);
                            continue; // Kihagyja ezt az elemet, folytatja a többivel
                        }

                        var gSave = _groupsSave.FirstOrDefault(g => string.Equals(g.Name, gLoad.Name, StringComparison.OrdinalIgnoreCase));
                        if (gSave == null)
                        {
                            SetStatus($"Figyelmeztetés: a 'Save' szerveren nem található a {gLoad.Name} jogosultsági csoport <- kihagyva", Color.Orange);
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

        private int? LoadRasterFontId(List<RasterFontInfo> list, string ttFontName, int size)
        {
            if (list == null || list.Count == 0)
                return null;

            var match = list.FirstOrDefault(r => string.Equals(r.TtFontName, ttFontName, StringComparison.OrdinalIgnoreCase) && r.Size == size);
            return match == null ? null : match.Id;
        }

        private int? LoadRasterFontSize(List<RasterFontInfo> list, int id)
        {
            if (list == null || list.Count == 0)
                return null;

            var match = list.FirstOrDefault(r => r.Id == id);
            return match == null ? null : match.Size;
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
                        // Ha Load szerver == Save szerver, nem kell törölni a háttérképet
                        string? loadServerKey = cmbServerLoad.SelectedItem?.ToString();
                        string? saveServerKey = cmbServerSave.SelectedItem?.ToString();
                        if (loadServerKey != saveServerKey)
                        {
                            toRemove.Add(propName);
                        }
                    }
                    else if (propName.Equals("groupIds", StringComparison.OrdinalIgnoreCase))
                    {
                        ConvertGroupIds(kv.Value);
                    }
                    else if (propName.Equals("rasterFontId", StringComparison.OrdinalIgnoreCase))
                    {
                        if (kv.Value != null && kv.Value.GetValue<int?>() is int rfId)
                        {
                            int? size = LoadRasterFontSize(_rasterFontsLoad, rfId);
                            if (size.HasValue)
                            {
                                string ttFontName = "";
                                var loadFont = _rasterFontsLoad.FirstOrDefault(r => r.Id == rfId);
                                if (loadFont != null)
                                {
                                    ttFontName = loadFont.TtFontName;
                                }
                                int? mappedId = LoadRasterFontId(_rasterFontsSave, ttFontName, size.Value);
                                if (mappedId.HasValue)
                                {
                                    obj[propName] = mappedId.Value;
                                }
                                else
                                {
                                    SetStatus($"Hiba: nem található megfelelő raster font a Save szerveren: ttFontName={ttFontName}, size={size.Value}", Color.Red);
                                    throw new Exception("No matching raster font on Save server");
                                }
                            }
                            else
                            {
                                SetStatus($"Hiba: nem található raster font méret a Load szerveren az id alapján: rasterFontId={rfId}", Color.Red);
                                throw new Exception("No matching raster font size on Load server");
                            }
                        }
                        else
                        {
                            SetStatus($"Hiba: érvénytelen rasterFontId érték: {kv.Value}", Color.Red);
                            throw new Exception("Invalid rasterFontId value");
                        }
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

        private async void btnLoad_Click(object sender, EventArgs e)
        {
            txtStatus.Clear();
            SetStatus("Beolvasás folyamatban...", Color.Black);

            try
            {
                var serverLoadSelected = cmbServerLoad.SelectedItem;
                if (serverLoadSelected == null)
                {
                    SetStatus("Hiba: nincs kiválasztva Load szerver!", Color.Red);
                    return;
                }
                string baseUrl = GetSelectedBaseUrl(cmbServerLoad);

                string id = txtLoadId.Text.Trim();
                string token = _loadAuth?.Trim() ?? "";
                string session = _loadSession?.Trim() ?? "";

                if (string.IsNullOrEmpty(id))
                {
                    SetStatus("Hiba: az ID mező üres!", Color.Red);
                    return;
                }

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
                _httpClient.DefaultRequestHeaders.Add("sessionid", session);

                string briefUrl = $"{baseUrl}/load-brief?id={id}";
                HttpResponseMessage briefResponse = await _httpClient.GetAsync(briefUrl);
                if (!briefResponse.IsSuccessStatusCode)
                {
                    string err = await briefResponse.Content.ReadAsStringAsync();
                    SetStatus($"Hiba load-brief: {briefResponse.StatusCode} - {err}", Color.Red);
                    return;
                }

                string briefJson = await briefResponse.Content.ReadAsStringAsync();
                var briefItem = JsonSerializer.Deserialize<TimetableItem>(
                    briefJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (briefItem == null)
                {
                    SetStatus("Hiba: a load-brief nem értelmezhető.", Color.Red);
                    return;
                }

                string fullUrl = $"{baseUrl}/load?id={id}";
                HttpResponseMessage fullResponse = await _httpClient.GetAsync(fullUrl);
                if (!fullResponse.IsSuccessStatusCode)
                {
                    string err = await fullResponse.Content.ReadAsStringAsync();
                    SetStatus($"Hiba load: {fullResponse.StatusCode} - {err}", Color.Red);
                    return;
                }

                string fullJson = await fullResponse.Content.ReadAsStringAsync();
                var fullItem = JsonSerializer.Deserialize<TimetableItem>(
                    fullJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (fullItem == null)
                {
                    SetStatus("Hiba: a teljes elem nem értelmezhető.", Color.Red);
                    return;
                }

                _loadedItem = fullItem;
                txtSaveName.Text = fullItem.Name ?? "";
                txtJson.Text = JsonSerializer.Serialize(fullItem, new JsonSerializerOptions { WriteIndented = true });
                SetStatus($"✅ Sikeres beolvasás a {cmbServerLoad.SelectedItem} szerverről.", Color.ForestGreen);
            }
            catch (Exception ex)
            {
                SetStatus($"Hiba: {ex.Message}", Color.Red);
            }
        }

        private async void btnSave_Click(object sender, EventArgs e)
        {
            txtStatus.Clear();
            SetStatus("Mentés folyamatban...", Color.Black);

            try
            {
                string token = _saveAuth?.Trim() ?? "";
                string session = _saveSession?.Trim() ?? "";
                string newName = txtSaveName.Text.Trim();

                if (_loadedItem == null)
                {
                    SetStatus("Hiba: nincs beolvasott elem!", Color.Red);
                    return;
                }

                if (string.IsNullOrEmpty(newName))
                {
                    SetStatus("Hiba: az új név üres!", Color.Red);
                    return;
                }

                var serverLoadSelected = cmbServerLoad.SelectedItem;
                if (serverLoadSelected == null)
                {
                    SetStatus("Hiba: nincs kiválasztva Load szerver!", Color.Red);
                    return;
                }
                string serverLoadKey = serverLoadSelected.ToString() ?? "";
                var loadList = await LoadFontsList(serverLoadKey);
                if (loadList == null)
                {
                    SetStatus($"Hiba: raster font lista betöltése sikertelen a {serverLoadKey} (Load) szervernél.", Color.Red);
                    return;
                }
                _rasterFontsLoad = loadList;
                var groupsListLoad = await LoadGroupsList(serverLoadKey, isLoadServer: true);  // Load groups list from Load server
                if (groupsListLoad != null)
                {
                    _groupsLoad = groupsListLoad;
                    SetStatus($"✅ Load csoportok: {string.Join(", ", groupsListLoad.Select(g => g.Name))}", Color.ForestGreen);
                }

                var serverSaveSelected = cmbServerSave.SelectedItem;
                if (serverSaveSelected == null)
                {
                    SetStatus("Hiba: nincs kiválasztva Save szerver!", Color.Red);
                    return;
                }
                string serverSaveKey = serverSaveSelected.ToString() ?? "";
                var saveList = await LoadFontsList(serverSaveKey);
                if (saveList == null)
                {
                    SetStatus($"Hiba: raster font lista betöltése sikertelen a {serverSaveKey} (Save) szervernél.", Color.Red);
                    return;
                }
                _rasterFontsSave = saveList;
                var groupsListSave = await LoadGroupsList(serverSaveKey, isLoadServer: false);  // Load groups list from Save server
                if (groupsListSave != null)
                {
                    _groupsSave = groupsListSave;
                    SetStatus($"✅ Save csoportok: {string.Join(", ", groupsListSave.Select(g => g.Name))}", Color.ForestGreen);
                }
                string baseUrl = GetSelectedBaseUrl(cmbServerSave);

                var node = JsonSerializer.SerializeToNode(_loadedItem, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });

                RemoveIdProperties(node);

                if (node is JsonObject nodeObj)
                {
                    nodeObj["name"] = newName;
                }

                txtJson.Text = node?.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) ?? "";

                string jsonOut = node?.ToJsonString(new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                }) ?? "{}";

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
                _httpClient.DefaultRequestHeaders.Add("sessionid", session);

                StringContent content = new StringContent(jsonOut, Encoding.UTF8, "application/json");
                string url = $"{baseUrl}/save";

                HttpResponseMessage response = await _httpClient.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    SetStatus($"✅ Sikeres mentés a {cmbServerSave.SelectedItem} szerverre.", Color.ForestGreen);
                }
                else
                {
                    string err = await response.Content.ReadAsStringAsync();
                    SetStatus($"Hiba: {response.StatusCode} - {err}", Color.Red);
                }
            }
            catch (Exception ex)
            {
                SetStatus($"Hiba: {ex.Message}", Color.Red);
            }
        }
    }
}
