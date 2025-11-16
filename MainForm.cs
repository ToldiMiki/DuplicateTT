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
    // Cached flattened raster fonts (id, ttFontName, size) for load and save servers
    private List<RasterFontInfo> _rasterFontsLoad = new List<RasterFontInfo>();
    private List<RasterFontInfo> _rasterFontsSave = new List<RasterFontInfo>();

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
            txtLoadAuth.Text = "eyJhbGciOiJSUzI1NiJ9.eyJsaWNlbnNlIjoiOTRjMWQ1NDgtZWZhMS00MTM0LTgzMTQtNTRhYzJmZjNjYjVmIiwic2FsdGVkIjoiYzI5ZWZkZjY0NDA1MDA4OTM1NzgzNWZmYzI4MDc0ZTgyNGEwODJhMGRhZWJmNjc1Yjg1NDM4YjVlNzJlZWI2ZCIsInRva2VuLXR5cGUiOiJBQ0NFU1MiLCJyb2xlcyI6WyJBVVRIX0FETUlOX0xEQVBfQ09ORklHX1JFQUQiLCJBVVRIX0FETUlOX0xEQVBfQ09ORklHX1dSSVRFIiwiQVVUSF9BRE1JTl9VU0VSX0JBTl9XUklURSIsIkFVVEhfQURNSU5fVVNFUl9MSUNFTlNFX1dSSVRFIiwiQVVUSF9BRE1JTl9VU0VSX0xJU1QiLCJBVVRIX0FETUlOX1VTRVJfTUZBX1dSSVRFIiwiQVVUSF9BRE1JTl9VU0VSX1BFUk1JU1NJT05fR1JPVVBfV1JJVEUiLCJBVVRIX0FETUlOX1VTRVJfUkVBRCIsIkFVVEhfQURNSU5fVVNFUl9TVEFUVVNfV1JJVEUiLCJBVVRIX0FETUlOX1VTRVJfU1lOQyIsIkFVVEhfQURNSU5fVVNFUl9XUklURSIsIkFVVEhfRlJPTlRFTkRfTUVOVUlURU1fTERBUCIsIkFVVEhfRlJPTlRFTkRfTUVOVUlURU1fTElDRU5TRSIsIkFVVEhfRlJPTlRFTkRfTUVOVUlURU1fTElDRU5TRV9VUExPQUQiLCJBVVRIX0ZST05URU5EX01FTlVJVEVNX1BBU1NXT1JEIiwiQVVUSF9GUk9OVEVORF9NRU5VSVRFTV9QRVJNSVNTSU9OX0dST1VQUyIsIkFVVEhfRlJPTlRFTkRfTUVOVUlURU1fUEVSTUlTU0lPTlMiLCJBVVRIX0ZST05URU5EX01FTlVJVEVNX1VTRVJTIiwiQVVUSF9MSUNFTlNFX0tFWV9MSVNUIiwiQVVUSF9MSUNFTlNFX0tFWV9MSVNUX0lOVEVSTkFMIiwiQVVUSF9QQVNTV09SRF9QUk9QRVJUWV9SRUFEIiwiQVVUSF9QQVNTV09SRF9QUk9QRVJUWV9XUklURSIsIkFVVEhfUEVSTUlTU0lPTl9HUk9VUF9JTlRFUk5BTF9MSVNUIiwiQVVUSF9QRVJNSVNTSU9OX0dST1VQX0xJU1QiLCJBVVRIX1BFUk1JU1NJT05fR1JPVVBfUkVBRCIsIkFVVEhfUEVSTUlTU0lPTl9MSVNUIiwiQVVUSF9QRVJNSVNTSU9OX1JFQUQiLCJBVVRIX1NFUklBTF9LRVlfVVBMT0FEIiwiUEVSTV9BTEVSVF9MSVNUIiwiUEVSTV9BTk5PVU5DRU1FTlRfR1JPVVBfV1JJVEUiLCJQRVJNX0FOTk9VTkNFTUVOVF9MSVNUIiwiUEVSTV9BTk5PVU5DRU1FTlRfUkVBRCIsIlBFUk1fQU5OT1VOQ0VNRU5UX1dSSVRFIiwiUEVSTV9DT01QQU5ZX0dST1VQX0xJU1QiLCJQRVJNX0NPTVBBTllfR1JPVVBfV1JJVEUiLCJQRVJNX0NPTVBBTllfTElTVCIsIlBFUk1fQ09NUEFOWV9SRUFEIiwiUEVSTV9DT01QQU5ZX1dSSVRFIiwiUEVSTV9EQVNIQk9BUkRfTE9BRCIsIlBFUk1fRElDVElPTkFSWV9XUklURSIsIlBFUk1fRElTUExBWV9MSVNUIiwiUEVSTV9ESVNQTEFZX1JFQUQiLCJQRVJNX0RJU1BMQVlfVEVYVF9DT0xPUl9MSVNUIiwiUEVSTV9ESVNQTEFZX1RFWFRfQ09MT1JfV1JJVEUiLCJQRVJNX0RJU1BMQVlfV1JJVEUiLCJQRVJNX0RZTkFNSUNfVElNRVRBQkxFX0xJU1QiLCJQRVJNX0RZTkFNSUNfVElNRVRBQkxFX1JFQUQiLCJQRVJNX0RZTkFNSUNfVElNRVRBQkxFX1dSSVRFIiwiUEVSTV9FTEVNRU5UX0xJU1QiLCJQRVJNX0VMRU1FTlRfUkVBRCIsIlBFUk1fRUxFTUVOVF9UWVBFX0xJU1QiLCJQRVJNX0VMRU1FTlRfVFlQRV9SRUFEIiwiUEVSTV9FTEVNRU5UX1RZUEVfV1JJVEUiLCJQRVJNX0VMRU1FTlRfV1JJVEUiLCJQRVJNX0VOVU1fTElTVCIsIlBFUk1fRU5VTV9SRUFEIiwiUEVSTV9FTlVNX1ZBTFVFX0xJU1QiLCJQRVJNX0VOVU1fV1JJVEUiLCJQRVJNX0ZJRUxEX0xJU1QiLCJQRVJNX0ZJRUxEX1JFQUQiLCJQRVJNX0ZJRUxEX1dSSVRFIiwiUEVSTV9GSVJNV0FSRV9MSVNUIiwiUEVSTV9GSVJNV0FSRV9XUklURSIsIlBFUk1fRkxBR19MSVNUIiwiUEVSTV9GTEFHX1JFQUQiLCJQRVJNX0ZMQUdfV1JJVEUiLCJQRVJNX0dSSURfTElTVCIsIlBFUk1fR1JJRF9SRUFEIiwiUEVSTV9HUklEX1dSSVRFIiwiUEVSTV9HUk9VUF9MSVNUIiwiUEVSTV9HUk9VUF9SRUFEIiwiUEVSTV9HUk9VUF9XUklURSIsIlBFUk1fSU1BR0VfTElTVCIsIlBFUk1fSU1BR0VfUkVBRCIsIlBFUk1fSU1BR0VfV1JJVEUiLCJQRVJNX0xBWU9VVF9MSVNUIiwiUEVSTV9MQVlPVVRfUkVBRCIsIlBFUk1fTEFZT1VUX1dSSVRFIiwiUEVSTV9NRU5VX0FCT1VUX1ZJRVciLCJQRVJNX01FTlVfQUxFUlRfVklFVyIsIlBFUk1fTUVOVV9BTk5PVU5DRU1FTlRfVklFVyIsIlBFUk1fTUVOVV9BVVRIX1NFUlZFUl9WSUVXIiwiUEVSTV9NRU5VX0NPTVBBTllfVklFVyIsIlBFUk1fTUVOVV9EQVNIQk9BUkRfVklFVyIsIlBFUk1fTUVOVV9ESUNUSU9OQVJZX1ZJRVciLCJQRVJNX01FTlVfRElTUExBWV9WSUVXIiwiUEVSTV9NRU5VX0RZTkFNSUNfVElNRVRBQkxFX1ZJRVciLCJQRVJNX01FTlVfRUxFTUVOVF9UWVBFX1ZJRVciLCJQRVJNX01FTlVfRU5VTV9WSUVXIiwiUEVSTV9NRU5VX0ZJRUxEX1ZJRVciLCJQRVJNX01FTlVfRklSTVdBUkVfVklFVyIsIlBFUk1fTUVOVV9GTEFHX1ZJRVciLCJQRVJNX01FTlVfR1JJRF9WSUVXIiwiUEVSTV9NRU5VX0dST1VQX1ZJRVciLCJQRVJNX01FTlVfSU1BR0VfVklFVyIsIlBFUk1fTUVOVV9MQVlPVVRfVklFVyIsIlBFUk1fTUVOVV9NQVBfVklFVyIsIlBFUk1fTUVOVV9NT05JVE9SSU5HX1ZJRVciLCJQRVJNX01FTlVfUFJPRFVDVF9DT05TT0xFX1ZJRVciLCJQRVJNX01FTlVfUFJPRFVDVF9MT0dTX1ZJRVciLCJQRVJNX01FTlVfUFJPRFVDVF9TTl9WSUVXIiwiUEVSTV9NRU5VX1JBU1RFUl9GT05UX1ZJRVciLCJQRVJNX01FTlVfUkVBTF9WSUVXIiwiUEVSTV9NRU5VX1JPV19WSUVXIiwiUEVSTV9NRU5VX1NDSEVEVUxFX0RJQ1RJT05BUllfVklFVyIsIlBFUk1fTUVOVV9TRVJWRVJfVklFVyIsIlBFUk1fTUVOVV9TRVRUSU5HU19WSUVXIiwiUEVSTV9NRU5VX1NMSURFX0VMRU1FTlRfVklFVyIsIlBFUk1fTUVOVV9TTElERV9URU1QTEFURV9WSUVXIiwiUEVSTV9NRU5VX1NMSURFX1ZJRVciLCJQRVJNX01FTlVfU1RBVEVfVklFVyIsIlBFUk1fTUVOVV9TVE9QX1RFTVBMQVRFX1ZJRVciLCJQRVJNX01FTlVfU1RPUF9WSUVXIiwiUEVSTV9NRU5VX1NZU1RFTV9QQVJBTUVURVJTX1ZJRVciLCJQRVJNX01FTlVfU1lTVEVNX1ZJRVciLCJQRVJNX01FTlVfVEVNUExBVEVfVklFVyIsIlBFUk1fTUVOVV9URVJNSU5BTF9TVEFUVVNFU19WSUVXIiwiUEVSTV9NRU5VX1RFWFRfVklFVyIsIlBFUk1fTUVOVV9USU1FVEFCTEVfU09VUkNFX0RBVEFfVklFVyIsIlBFUk1fTUVOVV9VU0VSX1ZJRVciLCJQRVJNX1BST0RVQ1RfQ09OU09MRV9MSVNUIiwiUEVSTV9QUk9EVUNUX0NPTlNPTEVfV1JJVEUiLCJQRVJNX1BST0RVQ1RfTE9HU19MSVNUIiwiUEVSTV9QUk9EVUNUX0xPR1NfV1JJVEUiLCJQRVJNX1BST0RVQ1RfU05fTElTVCIsIlBFUk1fUFJPRFVDVF9TTl9SRUFEIiwiUEVSTV9QUk9EVUNUX1NOX1dSSVRFIiwiUEVSTV9QUk9EVUNUX1NUT1BfTElTVCIsIlBFUk1fUFJPRFVDVF9TVE9QX1JFQUQiLCJQRVJNX1BST0RVQ1RfU1RPUF9XUklURSIsIlBFUk1fUkFTVEVSX0ZPTlRfTElTVCIsIlBFUk1fUkFTVEVSX0ZPTlRfUkVBRCIsIlBFUk1fUkFTVEVSX0ZPTlRfV1JJVEUiLCJQRVJNX1JFTEVBU0VfTk9URVNfTElTVCIsIlBFUk1fUk9XX0xJU1QiLCJQRVJNX1JPV19SRUFEIiwiUEVSTV9ST1dfV1JJVEUiLCJQRVJNX1NDSEVEVUxFX0RJQ1RJT05BUllfTElTVCIsIlBFUk1fU0NIRURVTEVfRElDVElPTkFSWV9SRUFEIiwiUEVSTV9TQ0hFRFVMRV9ESUNUSU9OQVJZX1dSSVRFIiwiUEVSTV9TRVJWRVJfTElTVCIsIlBFUk1fU0VSVkVSX1JFQUQiLCJQRVJNX1NFUlZFUl9XUklURSIsIlBFUk1fU0xJREVfRUxFTUVOVF9MSVNUIiwiUEVSTV9TTElERV9FTEVNRU5UX1JFQUQiLCJQRVJNX1NMSURFX0VMRU1FTlRfV1JJVEUiLCJQRVJNX1NMSURFX0xJU1QiLCJQRVJNX1NMSURFX1JFQUQiLCJQRVJNX1NMSURFX1RFTVBMQVRFX0xJU1QiLCJQRVJNX1NMSURFX1RFTVBMQVRFX1JFQUQiLCJQRVJNX1NMSURFX1RFTVBMQVRFX1dSSVRFIiwiUEVSTV9TTElERV9XUklURSIsIlBFUk1fU1RBVEVfTElTVCIsIlBFUk1fU1RBVEVfUkVBRCIsIlBFUk1fU1RBVEVfV1JJVEUiLCJQRVJNX1NUT1BfREFUQVNPVVJDRV9MSVNUIiwiUEVSTV9TVE9QX0RBVEFTT1VSQ0VfUkVBRCIsIlBFUk1fU1RPUF9EQVRBU09VUkNFX1dSSVRFIiwiUEVSTV9TVE9QX0xJU1QiLCJQRVJNX1NUT1BfUkVBRCIsIlBFUk1fU1RPUF9URU1QTEFURV9MSVNUIiwiUEVSTV9TVE9QX1RFTVBMQVRFX1JFQUQiLCJQRVJNX1NUT1BfVEVNUExBVEVfV1JJVEUiLCJQRVJNX1NUT1BfV1JJVEUiLCJQRVJNX1RFUk1JTkFMX1NUQVRVU0VTX0xJU1QiLCJQRVJNX1RFWFRfTElTVCIsIlBFUk1fVEVYVF9SRUFEIiwiUEVSTV9URVhUX1dSSVRFIiwiUEVSTV9USU1FVEFCTEVfREFUQV9MSVNUIiwiUEVSTV9UVF9GT05UX0xJU1QiLCJQRVJNX1RUX0ZPTlRfUkVBRCIsIlBFUk1fVFRfRk9OVF9XUklURSIsIlBFUk1fVVNFUl9DT01QQU5ZX0xJU1QiLCJQRVJNX1VTRVJfQ09NUEFOWV9XUklURSIsIlBFUk1fVVNFUl9HUk9VUF9MSVNUIiwiUEVSTV9VU0VSX0dST1VQX1dSSVRFIiwiUEVSTV9VU0VSX0xJU1QiLCJQRVJNX1VTRVJfUkVBRCIsIlBFUk1fVVNFUl9ST0xFX0xJU1QiLCJQRVJNX1VTRVJfUk9MRV9XUklURSIsIlBFUk1fVVNFUl9XUklURSJdLCJncm91cHMiOlsiU1lTX0FETUlOIl0sInNlY3VyZS1yYW5kb20taWRlbnRpZmllciI6IkhKOVlDUGVIM2FydHIvamQ0Ty9BczhZcERuRkhyOVpnaGJNRWE1WFlRQktuN2hGZ3N6VWNLSVpZdTdIS1VReU1FeUE1elBUL0dmYlNqQ3ZJM0xRN3ozZzdPNEZ4eUU0d0lGejduZ0ZrdGRnZExCemtLNUFtTzd5ek1kdi9BRzhaMExWU2ZkTFRlSlRMd1lCT0dhN2FOV2Q4THo3SXJpem4rT1FqK0E9PSIsInVzZXJJZCI6MTAxMDksInN1YiI6InBlcmN6ZWxzeXMiLCJpYXQiOjE3NjMzMTYxNTEsImV4cCI6MTc2MzMxOTc1MX0.etNf-ENLESxm50v2bLBNSoOpOaclm_GOXIanhz2yRSOAf2CRlEzqYrUUm-L2ix6dh8uDBtjU9Lozl5iEZbyF63m9_vK7q8veJwFgK76VMvXEals7L_XuJvsFr1bi20WcPtni7UfKllKQw5qojFQYcPBe5CgHFlIBCLgdyY0AnPzx0cTftOv88Mgm4XBfveobD8I0gSSQ3N2ZCt2bfZItgLL00U31HDVs7sU1EUlJ2EW8IrfAwQljL2Y2Px1BxtlHzk3pr1ZkiBOB0d38et4izX6HgHg1dOeParYHl3TM4dlufaz9Ks2wDPatFjtC59INZpdsBZ32mEmHAjnPh0Gayg";
            txtSaveAuth.Text = "eyJhbGciOiJSUzI1NiJ9.eyJsaWNlbnNlIjoiZTFiZjQ0NjAtYjhjNC00ZDgzLTg5OTItZGNiMDI4MDZkYWIwIiwic2FsdGVkIjoiYTJmYzE5OWQ2N2VjZWE3N2Y3NzBjZjFiZjgwNjJjOTRiZDRjNGZjZDYxY2JiMmQyNTU1MmI4MDU5MDBlOTg3MiIsInRva2VuLXR5cGUiOiJBQ0NFU1MiLCJyb2xlcyI6WyJBVVRIX0FETUlOX0xEQVBfQ09ORklHX1JFQUQiLCJBVVRIX0FETUlOX0xEQVBfQ09ORklHX1dSSVRFIiwiQVVUSF9BRE1JTl9VU0VSX0JBTl9XUklURSIsIkFVVEhfQURNSU5fVVNFUl9MSUNFTlNFX1dSSVRFIiwiQVVUSF9BRE1JTl9VU0VSX0xJU1QiLCJBVVRIX0FETUlOX1VTRVJfTUZBX1dSSVRFIiwiQVVUSF9BRE1JTl9VU0VSX1BFUk1JU1NJT05fR1JPVVBfV1JJVEUiLCJBVVRIX0FETUlOX1VTRVJfUkVBRCIsIkFVVEhfQURNSU5fVVNFUl9TVEFUVVNfV1JJVEUiLCJBVVRIX0FETUlOX1VTRVJfU1lOQyIsIkFVVEhfQURNSU5fVVNFUl9XUklURSIsIkFVVEhfRlJPTlRFTkRfTUVOVUlURU1fTERBUCIsIkFVVEhfRlJPTlRFTkRfTUVOVUlURU1fTElDRU5TRSIsIkFVVEhfRlJPTlRFTkRfTUVOVUlURU1fTElDRU5TRV9VUExPQUQiLCJBVVRIX0ZST05URU5EX01FTlVJVEVNX1BBU1NXT1JEIiwiQVVUSF9GUk9OVEVORF9NRU5VSVRFTV9QRVJNSVNTSU9OX0dST1VQUyIsIkFVVEhfRlJPTlRFTkRfTUVOVUlURU1fUEVSTUlTU0lPTlMiLCJBVVRIX0ZST05URU5EX01FTlVJVEVNX1VTRVJTIiwiQVVUSF9MSUNFTlNFX0tFWV9MSVNUIiwiQVVUSF9MSUNFTlNFX0tFWV9MSVNUX0lOVEVSTkFMIiwiQVVUSF9QQVNTV09SRF9QUk9QRVJUWV9SRUFEIiwiQVVUSF9QQVNTV09SRF9QUk9QRVJUWV9XUklURSIsIkFVVEhfUEVSTUlTU0lPTl9HUk9VUF9JTlRFUk5BTF9MSVNUIiwiQVVUSF9QRVJNSVNTSU9OX0dST1VQX0xJU1QiLCJBVVRIX1BFUk1JU1NJT05fR1JPVVBfUkVBRCIsIkFVVEhfUEVSTUlTU0lPTl9MSVNUIiwiQVVUSF9QRVJNSVNTSU9OX1JFQUQiLCJBVVRIX1NFUklBTF9LRVlfVVBMT0FEIiwiUEVSTV9BTEVSVF9MSVNUIiwiUEVSTV9BTk5PVU5DRU1FTlRfR1JPVVBfV1JJVEUiLCJQRVJNX0FOTk9VTkNFTUVOVF9MSVNUIiwiUEVSTV9BTk5PVU5DRU1FTlRfUkVBRCIsIlBFUk1fQU5OT1VOQ0VNRU5UX1dSSVRFIiwiUEVSTV9DT01QQU5ZX0dST1VQX0xJU1QiLCJQRVJNX0NPTVBBTllfR1JPVVBfV1JJVEUiLCJQRVJNX0NPTVBBTllfTElTVCIsIlBFUk1fQ09NUEFOWV9SRUFEIiwiUEVSTV9DT01QQU5ZX1dSSVRFIiwiUEVSTV9EQVNIQk9BUkRfTE9BRCIsIlBFUk1fRElDVElPTkFSWV9XUklURSIsIlBFUk1fRElTUExBWV9MSVNUIiwiUEVSTV9ESVNQTEFZX1JFQUQiLCJQRVJNX0RJU1BMQVlfVEVYVF9DT0xPUl9MSVNUIiwiUEVSTV9ESVNQTEFZX1RFWFRfQ09MT1JfV1JJVEUiLCJQRVJNX0RJU1BMQVlfV1JJVEUiLCJQRVJNX0RZTkFNSUNfVElNRVRBQkxFX0xJU1QiLCJQRVJNX0RZTkFNSUNfVElNRVRBQkxFX1JFQUQiLCJQRVJNX0RZTkFNSUNfVElNRVRBQkxFX1dSSVRFIiwiUEVSTV9FTEVNRU5UX0xJU1QiLCJQRVJNX0VMRU1FTlRfUkVBRCIsIlBFUk1fRUxFTUVOVF9UWVBFX0xJU1QiLCJQRVJNX0VMRU1FTlRfVFlQRV9SRUFEIiwiUEVSTV9FTEVNRU5UX1RZUEVfV1JJVEUiLCJQRVJNX0VMRU1FTlRfV1JJVEUiLCJQRVJNX0VOVU1fTElTVCIsIlBFUk1fRU5VTV9SRUFEIiwiUEVSTV9FTlVNX1ZBTFVFX0xJU1QiLCJQRVJNX0VOVU1fV1JJVEUiLCJQRVJNX0ZJRUxEX0xJU1QiLCJQRVJNX0ZJRUxEX1JFQUQiLCJQRVJNX0ZJRUxEX1dSSVRFIiwiUEVSTV9GSVJNV0FSRV9MSVNUIiwiUEVSTV9GSVJNV0FSRV9XUklURSIsIlBFUk1fRkxBR19MSVNUIiwiUEVSTV9GTEFHX1JFQUQiLCJQRVJNX0ZMQUdfV1JJVEUiLCJQRVJNX0dSSURfTElTVCIsIlBFUk1fR1JJRF9SRUFEIiwiUEVSTV9HUklEX1dSSVRFIiwiUEVSTV9HUk9VUF9MSVNUIiwiUEVSTV9HUk9VUF9SRUFEIiwiUEVSTV9HUk9VUF9XUklURSIsIlBFUk1fSU1BR0VfTElTVCIsIlBFUk1fSU1BR0VfUkVBRCIsIlBFUk1fSU1BR0VfV1JJVEUiLCJQRVJNX0xBWU9VVF9MSVNUIiwiUEVSTV9MQVlPVVRfUkVBRCIsIlBFUk1fTEFZT1VUX1dSSVRFIiwiUEVSTV9NRU5VX0FCT1VUX1ZJRVciLCJQRVJNX01FTlVfQUxFUlRfVklFVyIsIlBFUk1fTUVOVV9BTk5PVU5DRU1FTlRfVklFVyIsIlBFUk1fTUVOVV9BVVRIX1NFUlZFUl9WSUVXIiwiUEVSTV9NRU5VX0NPTVBBTllfVklFVyIsIlBFUk1fTUVOVV9EQVNIQk9BUkRfVklFVyIsIlBFUk1fTUVOVV9ESUNUSU9OQVJZX1ZJRVciLCJQRVJNX01FTlVfRElTUExBWV9WSUVXIiwiUEVSTV9NRU5VX0RZTkFNSUNfVElNRVRBQkxFX1ZJRVciLCJQRVJNX01FTlVfRUxFTUVOVF9UWVBFX1ZJRVciLCJQRVJNX01FTlVfRU5VTV9WSUVXIiwiUEVSTV9NRU5VX0ZJRUxEX1ZJRVciLCJQRVJNX01FTlVfRklSTVdBUkVfVklFVyIsIlBFUk1fTUVOVV9GTEFHX1ZJRVciLCJQRVJNX01FTlVfR1JJRF9WSUVXIiwiUEVSTV9NRU5VX0dST1VQX1ZJRVciLCJQRVJNX01FTlVfSU1BR0VfVklFVyIsIlBFUk1fTUVOVV9MQVlPVVRfVklFVyIsIlBFUk1fTUVOVV9NQVBfVklFVyIsIlBFUk1fTUVOVV9NT05JVE9SSU5HX1ZJRVciLCJQRVJNX01FTlVfUFJPRFVDVF9DT05TT0xFX1ZJRVciLCJQRVJNX01FTlVfUFJPRFVDVF9MT0dTX1ZJRVciLCJQRVJNX01FTlVfUFJPRFVDVF9TTl9WSUVXIiwiUEVSTV9NRU5VX1JBU1RFUl9GT05UX1ZJRVciLCJQRVJNX01FTlVfUkVBTF9WSUVXIiwiUEVSTV9NRU5VX1JPV19WSUVXIiwiUEVSTV9NRU5VX1NDSEVEVUxFX0RJQ1RJT05BUllfVklFVyIsIlBFUk1fTUVOVV9TRVJWRVJfVklFVyIsIlBFUk1fTUVOVV9TRVRUSU5HU19WSUVXIiwiUEVSTV9NRU5VX1NMSURFX0VMRU1FTlRfVklFVyIsIlBFUk1fTUVOVV9TTElERV9URU1QTEFURV9WSUVXIiwiUEVSTV9NRU5VX1NMSURFX1ZJRVciLCJQRVJNX01FTlVfU1RBVEVfVklFVyIsIlBFUk1fTUVOVV9TVE9QX1RFTVBMQVRFX1ZJRVciLCJQRVJNX01FTlVfU1RPUF9WSUVXIiwiUEVSTV9NRU5VX1NZU1RFTV9QQVJBTUVURVJTX1ZJRVciLCJQRVJNX01FTlVfU1lTVEVNX1ZJRVciLCJQRVJNX01FTlVfVEVNUExBVEVfVklFVyIsIlBFUk1fTUVOVV9URVJNSU5BTF9TVEFUVVNFU19WSUVXIiwiUEVSTV9NRU5VX1RFWFRfVklFVyIsIlBFUk1fTUVOVV9USU1FVEFCTEVfU09VUkNFX0RBVEFfVklFVyIsIlBFUk1fTUVOVV9VU0VSX1ZJRVciLCJQRVJNX1BST0RVQ1RfQ09OU09MRV9MSVNUIiwiUEVSTV9QUk9EVUNUX0NPTlNPTEVfV1JJVEUiLCJQRVJNX1BST0RVQ1RfTE9HU19MSVNUIiwiUEVSTV9QUk9EVUNUX0xPR1NfV1JJVEUiLCJQRVJNX1BST0RVQ1RfU05fTElTVCIsIlBFUk1fUFJPRFVDVF9TTl9SRUFEIiwiUEVSTV9QUk9EVUNUX1NOX1dSSVRFIiwiUEVSTV9QUk9EVUNUX1NUT1BfTElTVCIsIlBFUk1fUFJPRFVDVF9TVE9QX1JFQUQiLCJQRVJNX1BST0RVQ1RfU1RPUF9XUklURSIsIlBFUk1fUkFTVEVSX0ZPTlRfTElTVCIsIlBFUk1fUkFTVEVSX0ZPTlRfUkVBRCIsIlBFUk1fUkFTVEVSX0ZPTlRfV1JJVEUiLCJQRVJNX1JFTEVBU0VfTk9URVNfTElTVCIsIlBFUk1fUk9XX0xJU1QiLCJQRVJNX1JPV19SRUFEIiwiUEVSTV9ST1dfV1JJVEUiLCJQRVJNX1NDSEVEVUxFX0RJQ1RJT05BUllfTElTVCIsIlBFUk1fU0NIRURVTEVfRElDVElPTkFSWV9SRUFEIiwiUEVSTV9TQ0hFRFVMRV9ESUNUSU9OQVJZX1dSSVRFIiwiUEVSTV9TRVJWRVJfTElTVCIsIlBFUk1fU0VSVkVSX1JFQUQiLCJQRVJNX1NFUlZFUl9XUklURSIsIlBFUk1fU0xJREVfRUxFTUVOVF9MSVNUIiwiUEVSTV9TTElERV9FTEVNRU5UX1JFQUQiLCJQRVJNX1NMSURFX0VMRU1FTlRfV1JJVEUiLCJQRVJNX1NMSURFX0xJU1QiLCJQRVJNX1NMSURFX1JFQUQiLCJQRVJNX1NMSURFX1RFTVBMQVRFX0xJU1QiLCJQRVJNX1NMSURFX1RFTVBMQVRFX1JFQUQiLCJQRVJNX1NMSURFX1RFTVBMQVRFX1dSSVRFIiwiUEVSTV9TTElERV9XUklURSIsIlBFUk1fU1RBVEVfTElTVCIsIlBFUk1fU1RBVEVfUkVBRCIsIlBFUk1fU1RBVEVfV1JJVEUiLCJQRVJNX1NUT1BfREFUQVNPVVJDRV9MSVNUIiwiUEVSTV9TVE9QX0RBVEFTT1VSQ0VfUkVBRCIsIlBFUk1fU1RPUF9EQVRBU09VUkNFX1dSSVRFIiwiUEVSTV9TVE9QX0xJU1QiLCJQRVJNX1NUT1BfUkVBRCIsIlBFUk1fU1RPUF9URU1QTEFURV9MSVNUIiwiUEVSTV9TVE9QX1RFTVBMQVRFX1JFQUQiLCJQRVJNX1NUT1BfVEVNUExBVEVfV1JJVEUiLCJQRVJNX1NUT1BfV1JJVEUiLCJQRVJNX1RFUk1JTkFMX1NUQVRVU0VTX0xJU1QiLCJQRVJNX1RFWFRfTElTVCIsIlBFUk1fVEVYVF9SRUFEIiwiUEVSTV9URVhUX1dSSVRFIiwiUEVSTV9USU1FVEFCTEVfREFUQV9MSVNUIiwiUEVSTV9UVF9GT05UX0xJU1QiLCJQRVJNX1RUX0ZPTlRfUkVBRCIsIlBFUk1fVFRfRk9OVF9XUklURSIsIlBFUk1fVVNFUl9DT01QQU5ZX0xJU1QiLCJQRVJNX1VTRVJfQ09NUEFOWV9XUklURSIsIlBFUk1fVVNFUl9HUk9VUF9MSVNUIiwiUEVSTV9VU0VSX0dST1VQX1dSSVRFIiwiUEVSTV9VU0VSX0xJU1QiLCJQRVJNX1VTRVJfUkVBRCIsIlBFUk1fVVNFUl9ST0xFX0xJU1QiLCJQRVJNX1VTRVJfUk9MRV9XUklURSIsIlBFUk1fVVNFUl9XUklURSJdLCJncm91cHMiOlsiU1lTX0FETUlOIl0sInNlY3VyZS1yYW5kb20taWRlbnRpZmllciI6IjV1VTFXS0UzU295a0swN0pBdUdtVU1mMjZHT1M5VHN4Rm94dWJPd1FMcUIwaTJlWTRpVmNYSHZnM3BKN2dlSzhRNnFoSElpSWsrWFBxSWczNnh5cU9yOEF4QmFRQnBoU0xkVDM1VU44NmZ6dDZOVTJMWFpDcHNsUlRhdDR0ek1iRXd1dmNUclZjTWprTmNOM1E1a0EwcDVPemtTam1LakFMZzV6RFE9PSIsInVzZXJJZCI6MTAwMDAsInN1YiI6InN5cy5hZG1pbiIsImlhdCI6MTc2MzMxNjEyNCwiZXhwIjoxNzYzMzE5NzI0fQ.P4oicN7rU7vXLF1a4B_qcFqDO_-oy-yMWTBm7kgoc3ZZKB_vQ3bs1jXLX2803IdL3BkErNLR1s2qoArZs7Nmsx503Pd1qU6aPa1HY4FyYCyXbAORou64jvqas6qwnz8coR7INdGI2PMqVENTKJzrZSAAPcnnPzUJ-lFHI8UXyyvWe6_NBp-CN0X8R0a7z_0kXmqOGBOAmi4-n4M6vesJC0UXejO-Xka5bLINiKD9_m-xlN-2my-FrVZ0rr_hXA4UDLlpiRg_I5M1CpM4P29PqMAe2Oq7MdAfWICB5a94o_G3TvpE6pzwk7rFxPrLeH1RbWtojJs7qu-1BDzrPNbvpw";
            txtLoadSession.Text = "11844";
            txtSaveSession.Text = "13655";

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
            // Append the new message instead of overwriting previous messages.
            // Note: standard TextBox controls don't support per-line coloring. Setting ForeColor
            // changes the color for the whole control. If per-message colors are required,
            // consider switching to a RichTextBox.
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

        private string GetSelectedBaseUrl(ComboBox combo)
        {
            string key = combo.SelectedItem?.ToString() ?? "DEV";
            return _serverUrls[key];
        }

        private record RasterFontInfo(int Id, string TtFontName, int Size);

        /// <summary>
        /// Calls the backend raster-font listFonts endpoint and builds a flattened list of raster fonts for the given server key.
        /// The list contains objects with: id, ttFontName, size.
        /// The caller should pass the server key ("DEV" / "DEMO" / "PROD"); the method returns the flattened list.
        /// </summary>
        private async Task<List<RasterFontInfo>?> LoadFontsList(string key)
        {
            try
            {
                SetStatus("Betöltés: raster fonts lista...", Color.Black);

                // derive endpoint from the dynamic-timetable base URL by swapping the path
                string endpoint = _serverUrls[key].Replace("dynamic-timetable", "raster-font/listFonts");

                _httpClient.DefaultRequestHeaders.Clear();

                // Choose auth/session header source based on which combo currently selects this key.
                // If the key matches the Save combo we use the Save auth/session, otherwise we use the Load auth/session.
                string saveKey = cmbServerSave.SelectedItem?.ToString() ?? "DEV";
                string token = (key == saveKey) ? txtSaveAuth.Text.Trim() : txtLoadAuth.Text.Trim();
                string session = (key == saveKey) ? txtSaveSession.Text.Trim() : txtLoadSession.Text.Trim();

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
                        // rasterFonts may be an array under each top-level
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

        /// <summary>
        /// Lookup a raster font id by ttFontName and size. Returns null if not found.
        /// </summary>
        private int? LoadRasterFontId(List<RasterFontInfo> list, string ttFontName, int size)
        {
            if (list == null || list.Count == 0)
                return null;

            var match = list.FirstOrDefault(r => string.Equals(r.TtFontName, ttFontName, StringComparison.OrdinalIgnoreCase) && r.Size == size);
            return match == null ? null : match.Id;
        }

        /// <summary>
        /// Lookup a raster font size by font id. Returns null if not found.
        /// </summary>
        private int? LoadRasterFontSize(List<RasterFontInfo> list, int id)
        {
            if (list == null || list.Count == 0)
                return null;

            var match = list.FirstOrDefault(r => r.Id == id);
            return match == null ? null : match.Size;
        }

        /// <summary>
        /// Recursively remove JSON properties that are IDs.
        /// Rules: remove any property whose name equals "id" (case-insensitive) or ends with "Id" (case-insensitive).
        /// Operates on a JsonNode (JsonObject/JsonArray) so it stays resilient to model changes.
        /// </summary>
        private void RemoveIdProperties(JsonNode? node)
        {
            if (node is JsonObject obj)
            {
                var toRemove = new List<string>();
                foreach (var kv in obj)
                {
                    var propName = kv.Key;
                    if (propName.Equals("imageId", StringComparison.OrdinalIgnoreCase) )
                    {
                        // Don't remove imageId — they are meaningful and must be preserved
                        //continue;
                        //TODO: decide what to do with imageIds — for now, remove them
                        toRemove.Add(propName);
                    }
                    else if (propName.Equals("groupIds", StringComparison.OrdinalIgnoreCase) )
                    {
                        //TODO: decide what to do with groupIds — for now, remove them
                        toRemove.Add(propName);
                    }
                    else if ( propName.Equals("rasterFontId", StringComparison.OrdinalIgnoreCase))
                    {
                        // Special handling for rasterFontId: attempt to map Load-server rasterFontId -> Save-server rasterFontId
                        // hiba esetén álljon le a Save folyamat és jelezze a hibát
                        if (kv.Value != null && kv.Value.GetValue<int?>() is int rfId)
                        {
                            // Get the size for this raster font id from the Load-server list
                            int? size = LoadRasterFontSize(_rasterFontsLoad, rfId);
                            if (size.HasValue)
                            {
                                // found size on Load side - now lookup Save-side id
                                string ttFontName = ""; // default
                                // find ttFontName from Load-side list
                                var loadFont = _rasterFontsLoad.FirstOrDefault(r => r.Id == rfId);
                                if (loadFont != null)
                                {
                                    ttFontName = loadFont.TtFontName;
                                }
                                int? mappedId = LoadRasterFontId(_rasterFontsSave, ttFontName, size.Value);
                                if (mappedId.HasValue)
                                {
                                    // replace value with mapped id
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
                        propName.EndsWith("Id", StringComparison.OrdinalIgnoreCase))  //"dynamicTimeTableId", "dynamicTimeRowId"
                    {
                        toRemove.Add(propName);
                    }
                }

                foreach (var name in toRemove)
                {
                    obj.Remove(name);
                }

                // Recurse remaining properties
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
                string token = txtLoadAuth.Text.Trim();
                string session = txtLoadSession.Text.Trim();

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
                    // Load failed - abort save
                    SetStatus($"Hiba: raster font lista betöltése sikertelen a {serverLoadKey} (Load) szervernél.", Color.Red);
                    return;
                }
                _rasterFontsLoad = loadList;

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
                    // Save-side font list failed to load - abort save
                    SetStatus($"Hiba: raster font lista betöltése sikertelen a {serverSaveKey} (Save) szervernél.", Color.Red);
                    return;
                }
                _rasterFontsSave = saveList;
                string baseUrl = GetSelectedBaseUrl(cmbServerSave);

                // Serialize the loaded item to a JsonNode (camelCase) and remove ID-like properties recursively.
                var node = JsonSerializer.SerializeToNode(_loadedItem, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });

                RemoveIdProperties(node);

                // Ensure the JSON object uses the new name and preview the full (ID-stripped) item.
                if (node is JsonObject nodeObj)
                {
                    nodeObj["name"] = newName;
                }

                // Preview the exact JSON we will send (full item without IDs)
                txtJson.Text = node?.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) ?? "";

                // Use the full node as the outgoing JSON payload (IDs already removed)
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
