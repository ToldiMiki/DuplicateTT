using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Forms;
using SmartpageTimetableDuplicateV1.Models;

namespace SmartpageTimetableDuplicateV1
{
    public partial class MainForm : Form
    {
        private readonly HttpClient _httpClient = new HttpClient();
        private TimetableItem? _loadedItem;

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
            cmbServerLoad.SelectedIndex = 0;
            cmbServerSave.SelectedIndex = 0;

            // --- mezők alapértékei ---
            txtLoadAuth.Text = "123456789";
            txtSaveAuth.Text = "123456789";
            txtLoadSession.Text = "13650";
            txtSaveSession.Text = "13650";

            // --- státuszmező formázás ---
            txtStatus.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            txtStatus.ForeColor = Color.Black;

            // --- JSON mező formázás ---
            txtJson.Font = new Font("Consolas", 9);

            // --- induláskor a kurzor az ID mezőre áll ---
            ActiveControl = txtLoadId;
        }

        private void SetStatus(string message, Color color)
        {
            txtStatus.ForeColor = color;
            txtStatus.Text = message;
            txtStatus.Refresh();
        }

        private string GetSelectedBaseUrl(ComboBox combo)
        {
            string key = combo.SelectedItem?.ToString() ?? "DEV";
            return _serverUrls[key];
        }

        private async void btnLoad_Click(object sender, EventArgs e)
        {
            SetStatus("Beolvasás folyamatban...", Color.Black);

            try
            {
                string id = txtLoadId.Text.Trim();
                string token = txtLoadAuth.Text.Trim();
                string session = txtLoadSession.Text.Trim();
                string baseUrl = GetSelectedBaseUrl(cmbServerLoad);

                if (string.IsNullOrEmpty(id))
                {
                    SetStatus("Hiba: az ID mező üres!", Color.Red);
                    return;
                }

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
                _httpClient.DefaultRequestHeaders.Add("sessionid", session);

                // --- load-brief ---
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

                // --- teljes load ---
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
            SetStatus("Mentés folyamatban...", Color.Black);

            try
            {
                string token = txtSaveAuth.Text.Trim();
                string session = txtSaveSession.Text.Trim();
                string newName = txtSaveName.Text.Trim();
                string baseUrl = GetSelectedBaseUrl(cmbServerSave);

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

                // Csak a minimális adatok
                var minimal = new
                {
                    name = newName,
                    width = _loadedItem.Width,
                    height = _loadedItem.Height,
                    groupIds = _loadedItem.GroupIds ?? new List<int>()
                };

                string jsonOut = JsonSerializer.Serialize(minimal, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    WriteIndented = true
                });

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
                _httpClient.DefaultRequestHeaders.Add("sessionid", session);

                StringContent content = new StringContent(jsonOut, Encoding.UTF8, "application/json");
                string url = $"{baseUrl}/save-brief";

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
