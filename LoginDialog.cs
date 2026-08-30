using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net.Http;
using System.Net.Security;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SmartPageDuplicate
{
    /// <summary>
    /// Modal dialog for entering username and password, then authenticating against the Smartpage auth-server backend.
    /// </summary>
    public partial class LoginDialog : Form
    {
        private string _serverKey; // "DEV", "DEMO", "PROD", "PROD2"
        private HttpClientHandler _httpClientHandler;

        // Results after successful authentication
        public string? AuthToken { get; private set; }
        public string? SessionId { get; private set; }
    public string? Username { get; private set; }

        private readonly Dictionary<string, string> _authUrls = new()
        {
            { "DEV", "https://smartpage-dev.hclinear.hu/auth-server-backend/api/v1/auth" },
            { "DEMO", "https://smartpage-demo.hclinear.hu/auth-server-backend/api/v1/auth" },
            { "PROD", "https://smartpage.hclinear.hu/auth-server-backend/api/v1/auth" },
            { "PROD2", "https://smartpage2.hclinear.hu/auth-server-backend/api/v1/auth" }
        };

        public LoginDialog(string serverKey)
        {
            _serverKey = serverKey;
            // We'll create a new handler that manages cookies automatically
            _httpClientHandler = new HttpClientHandler
            {
                UseCookies = true,
                CookieContainer = new System.Net.CookieContainer(),
                // Csak az ismerten hibás tanúsítványú DEV szervernél hagyjuk figyelmen kívül a
                // TLS-hibát; DEMO/PROD ellen a bejelentkezés a rendes tanúsítvány-ellenőrzéssel megy.
                ServerCertificateCustomValidationCallback = (request, cert, chain, sslPolicyErrors) =>
                    sslPolicyErrors == SslPolicyErrors.None ||
                    string.Equals(_serverKey, "DEV", StringComparison.OrdinalIgnoreCase)
            };
            InitializeComponent();
            SetupUI();
        }

        private static Font UiFont(float size = 9F, FontStyle style = FontStyle.Regular)
            => new Font("Segoe UI", size, style);

        private void SetupUI()
        {
            this.Text = "Bejelentkezés";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.ClientSize = new Size(330, 232);
            this.BackColor = Theme.Surface;
            this.Font = UiFont();

            // Fejléc a márka színével, benne a szerver megnevezésével: bejelentkezéskor ez a
            // legfontosabb információ - melyik környezetbe lépünk be.
            Panel header = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(this.ClientSize.Width, 52),
                BackColor = Theme.Brand
            };
            header.Paint += (s, e) =>
            {
                using var brush = new SolidBrush(Theme.Accent);
                e.Graphics.FillRectangle(brush, 0, header.Height - 3, header.Width, 3);
            };
            header.Controls.Add(new Label
            {
                Text = "Bejelentkezés",
                Font = UiFont(11F),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Location = new Point(14, 7),
                Size = new Size(200, 22)
            });
            header.Controls.Add(new Label
            {
                Text = _serverKey + " szerver",
                Font = UiFont(8.5F, FontStyle.Bold),
                ForeColor = Theme.Accent,
                BackColor = Color.Transparent,
                Location = new Point(16, 29),
                Size = new Size(200, 16)
            });
            this.Controls.Add(header);

            int y = 68;
            this.Controls.Add(new Label
            {
                Text = "Felhasználónév",
                Location = new Point(16, y),
                Size = new Size(280, 18),
                ForeColor = Theme.InkSoft
            });
            TextBox txtUsername = new TextBox
            {
                Name = "txtUsername",
                Location = new Point(16, y + 19),
                Size = new Size(298, 25),
                BorderStyle = BorderStyle.FixedSingle,
                Font = UiFont()
            };
            this.Controls.Add(txtUsername);

            y += 54;
            this.Controls.Add(new Label
            {
                Text = "Jelszó",
                Location = new Point(16, y),
                Size = new Size(280, 18),
                ForeColor = Theme.InkSoft
            });
            TextBox txtPassword = new TextBox
            {
                Name = "txtPassword",
                Location = new Point(16, y + 19),
                Size = new Size(298, 25),
                UseSystemPasswordChar = true,
                BorderStyle = BorderStyle.FixedSingle,
                Font = UiFont()
            };
            this.Controls.Add(txtPassword);

            y += 58;
            Button btnCancel = new Button
            {
                Text = "Mégse",
                Location = new Point(16, y),
                Size = new Size(100, 32),
                DialogResult = DialogResult.Cancel,
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.Surface,
                ForeColor = Theme.InkSoft,
                Font = UiFont(),
                Cursor = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderColor = Theme.Rule;
            this.Controls.Add(btnCancel);

            Button btnLogin = new Button
            {
                Text = "Belépés",
                Name = "btnLogin",
                Location = new Point(194, y),
                Size = new Size(120, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.Brand,
                ForeColor = Color.White,
                Font = UiFont(9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnLogin.FlatAppearance.BorderSize = 0;
            btnLogin.FlatAppearance.MouseOverBackColor = Theme.AccentHover;
            btnLogin.Click += BtnLogin_Click;
            this.Controls.Add(btnLogin);

            // Enter = belépés, Esc = mégse.
            this.AcceptButton = btnLogin;
            this.CancelButton = btnCancel;
            txtUsername.Select();
        }

        // A hibaválasz gyakran nyers HTML (pl. nginx 404-es alapoldala, ha a szerver nem
        // elérhető - tipikusan VPN nélkül); ilyenkor a HTML-t nem dobjuk a felhasználóra,
        // hanem egy rövid, érthető üzenetet adunk.
        private static string FormatErrorMessage(HttpResponseMessage response, string body)
        {
            string? contentType = response.Content.Headers.ContentType?.MediaType;
            bool looksLikeHtml = (contentType != null && contentType.Contains("html", StringComparison.OrdinalIgnoreCase))
                || body.TrimStart().StartsWith("<html", StringComparison.OrdinalIgnoreCase);

            if (looksLikeHtml)
            {
                return response.StatusCode == System.Net.HttpStatusCode.NotFound
                    ? $"{(int)response.StatusCode} - a szerver nem található (ellenőrizd a VPN-kapcsolatot)."
                    : $"{(int)response.StatusCode} {response.ReasonPhrase} - a szerver váratlan választ adott.";
            }

            return $"{(int)response.StatusCode} {response.ReasonPhrase}\n{body}";
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
            // A jelszóban a szóköz értékes karakter - itt nem szabad levágni.
            string password = txtPassword.Text;

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
                        MessageBox.Show($"Bejelentkezés sikertelen (sign-in): {FormatErrorMessage(signInResponse, err)}", "Hiba", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                        MessageBox.Show($"Token lekérése sikertelen: {FormatErrorMessage(tokenResponse, err)}", "Hiba", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

        /// <summary>A dialógus saját HttpClientHandlerét a Form.Dispose nem takarítja el.</summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _httpClientHandler?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
