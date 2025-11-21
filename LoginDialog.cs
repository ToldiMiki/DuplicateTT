using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SmartpageTimetableDuplicateV1
{
    /// <summary>
    /// Modal dialog for entering username and password, then authenticating against the Smartpage auth-server backend.
    /// </summary>
    public partial class LoginDialog : Form
    {
        private string _serverKey; // "DEV", "DEMO", or "PROD"
        private HttpClientHandler _httpClientHandler;
        private HttpClient _httpClient;

        // Results after successful authentication
        public string? AuthToken { get; private set; }
        public string? SessionId { get; private set; }
    public string? Username { get; private set; }

        private readonly Dictionary<string, string> _authUrls = new()
        {
            { "DEV", "https://smartpage-dev.hclinear.hu/auth-server-backend/api/v1/auth" },
            { "DEMO", "https://smartpage-demo.hclinear.hu/auth-server-backend/api/v1/auth" },
            { "PROD", "https://smartpage.hclinear.hu/auth-server-backend/api/v1/auth" }
        };

        public LoginDialog(string serverKey, HttpClient httpClient)
        {
            _serverKey = serverKey;
            _httpClient = httpClient;
            // We'll create a new handler that manages cookies automatically
            _httpClientHandler = new HttpClientHandler
            {
                UseCookies = true,
                CookieContainer = new System.Net.CookieContainer()
            };
            InitializeComponent();
            SetupUI();
        }

        private void SetupUI()
        {
            this.Text = $"Bejelentkezés - {_serverKey}";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Size = new Size(310, 210);

            // Label for username
            Label lblUsername = new Label
            {
                Text = "Felhasználónév:",
                Location = new Point(10, 15),
                Size = new Size(270, 20)
            };
            this.Controls.Add(lblUsername);

            // TextBox for username
            TextBox txtUsername = new TextBox
            {
                Name = "txtUsername",
                Location = new Point(10, 35),
                Size = new Size(270, 24)
            };
            this.Controls.Add(txtUsername);

            // Label for password
            Label lblPassword = new Label
            {
                Text = "Jelszó:",
                Location = new Point(10, 65),
                Size = new Size(270, 20)
            };
            this.Controls.Add(lblPassword);

            // TextBox for password (masked)
            TextBox txtPassword = new TextBox
            {
                Name = "txtPassword",
                Location = new Point(10, 85),
                Size = new Size(270, 24),
                UseSystemPasswordChar = true
            };
            this.Controls.Add(txtPassword);

            // Login button
            Button btnLogin = new Button
            {
                Text = "Belépés",
                Location = new Point(100, 125),
                Size = new Size(80, 30),
                Name = "btnLogin"
            };
            btnLogin.Click += BtnLogin_Click;
            this.Controls.Add(btnLogin);

            // Cancel button
            Button btnCancel = new Button
            {
                Text = "Mégse",
                Location = new Point(190, 125),
                Size = new Size(80, 30),
                DialogResult = DialogResult.Cancel
            };
            this.Controls.Add(btnCancel);

            this.CancelButton = btnCancel;
        }

        private async void BtnLogin_Click(object? sender, EventArgs e)
        {
            TextBox? txtUsername = this.Controls["txtUsername"] as TextBox;
            TextBox? txtPassword = this.Controls["txtPassword"] as TextBox;

            // Store the username for later retrieval
            Username = txtUsername?.Text.Trim();

            if (txtUsername == null || txtPassword == null)
            {
                MessageBox.Show("Belső hiba: nem található a beviteli mezők.", "Hiba", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string username = txtUsername.Text.Trim();
            string password = txtPassword.Text.Trim();

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Felhasználónév és jelszó kitöltése kötelező!", "Hiba", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                this.Enabled = false;

                // Step 1: Call /sign-in to get cookies (authentication-code and session-id)
                using (HttpClient cookieClient = new HttpClient(_httpClientHandler, false))
                {
                    var signInPayload = new { username, password };
                    string signInJson = JsonSerializer.Serialize(signInPayload);
                    StringContent signInContent = new StringContent(signInJson, Encoding.UTF8, "application/json");

                    string signInUrl = $"{_authUrls[_serverKey]}/sign-in";
                    HttpResponseMessage signInResponse = await cookieClient.PostAsync(signInUrl, signInContent);

                    if (!signInResponse.IsSuccessStatusCode)
                    {
                        string err = await signInResponse.Content.ReadAsStringAsync();
                        MessageBox.Show($"Bejelentkezés sikertelen (sign-in): {signInResponse.StatusCode}\n{err}", "Hiba", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        this.Enabled = true;
                        return;
                    }

                    // Step 2: Call /token to get the accessToken
                    // The cookies are automatically included by HttpClientHandler with UseCookies=true
                    string tokenUrl = $"{_authUrls[_serverKey]}/token";
                    HttpResponseMessage tokenResponse = await cookieClient.PostAsync(tokenUrl, null);

                    if (tokenResponse.IsSuccessStatusCode)
                    {
                        string tokenBody = await tokenResponse.Content.ReadAsStringAsync();
                        var tokenObj = JsonSerializer.Deserialize<JsonElement>(tokenBody);

                        // Extract accessToken from the response
                        if (tokenObj.TryGetProperty("accessToken", out var tokenProp))
                        {
                            AuthToken = tokenProp.GetString();
                        }

                        // Extract sessionId from the set-cookie headers (if needed)
                        // The CookieContainer already holds the session-id cookie, but we can also extract it
                        if (signInResponse.Headers.TryGetValues("Set-Cookie", out var cookies))
                        {
                            foreach (var cookie in cookies)
                            {
                                if (cookie.Contains("session-id="))
                                {
                                    // Parse out the session-id value
                                    var parts = cookie.Split(';')[0].Split('=');
                                    if (parts.Length == 2)
                                    {
                                        SessionId = parts[1];
                                    }
                                    break;
                                }
                            }
                        }

                        if (!string.IsNullOrEmpty(AuthToken))
                        {
                            this.DialogResult = DialogResult.OK;
                            this.Close();
                        }
                        else
                        {
                            MessageBox.Show("Token lekérése sikeres, de accessToken hiányzik a válaszból.", "Hiba", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            this.Enabled = true;
                        }
                    }
                    else
                    {
                        string err = await tokenResponse.Content.ReadAsStringAsync();
                        MessageBox.Show($"Token lekérése sikertelen: {tokenResponse.StatusCode}\n{err}", "Hiba", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        this.Enabled = true;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hiba a bejelentkezés során: {ex.Message}", "Hiba", MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.Enabled = true;
            }
        }

        private void InitializeComponent()
        {
            // This method is called by the designer or can be left empty if controls are set up programmatically
            this.SuspendLayout();
            this.ResumeLayout(false);
        }
    }
}
