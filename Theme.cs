using System.Drawing;

namespace SmartPageDuplicate
{
    /// <summary>
    /// A HC Linear arculati színei, ahogy a Smartpage webes felület CSS-változói definiálják
    /// (smartpage2.hclinear.hu, /assets/index-*.css). Az elnevezések a forrás tokenjeit követik,
    /// hogy egy későbbi arculatváltás egyértelműen követhető legyen.
    ///
    /// A vezérszínpár: mély petróleumkék (--main-color) és élénk menta (--primary-color). A webes
    /// felületen a gombok alapból petróleumkékek, és hoverre váltanak mentára - ez a két szín
    /// jelöli itt is a beolvasást (forrás) és a mentést (cél).
    /// </summary>
    internal static class Theme
    {
        /// <summary>--main-color: a fő márkaszín, mély petróleumkék.</summary>
        internal static readonly Color Brand = FromHex(0x0D465F);

        /// <summary>--primary-color: élénk menta, a márka akcentusa.</summary>
        internal static readonly Color Accent = FromHex(0x1BE29A);

        /// <summary>--primary-color-hover</summary>
        internal static readonly Color AccentHover = FromHex(0x17C586);

        /// <summary>--text-color-dark: a törzsszöveg színe.</summary>
        internal static readonly Color Ink = FromHex(0x0D465F);

        /// <summary>--text-color-lighter: feliratok, másodlagos szöveg.</summary>
        internal static readonly Color InkSoft = FromHex(0x487386);

        /// <summary>--border-color</summary>
        internal static readonly Color Rule = FromHex(0xE3E9ED);

        /// <summary>--background-color-light: az ablak alapja.</summary>
        internal static readonly Color Ground = FromHex(0xF3F3F3);

        /// <summary>Kártyák, beviteli mezők háttere.</summary>
        internal static readonly Color Surface = Color.White;

        /// <summary>--button-background-color-grey: kitöltött, de nem szerkeszthető mező.</summary>
        internal static readonly Color SurfaceMuted = FromHex(0xDAE4E8);

        /// <summary>--disabled-color</summary>
        internal static readonly Color Disabled = FromHex(0xB7C8CF);

        // --- állapotszínek a naplóhoz -------------------------------------------------
        // Fehér háttéren kell olvashatónak lenniük, ezért a webes tokenek sötétebb párjai.

        /// <summary>--hc-linear-success-dark</summary>
        internal static readonly Color Success = FromHex(0x059669);

        /// <summary>--hc-linear-warning, sötétítve a fehér háttérhez.</summary>
        internal static readonly Color Warning = FromHex(0xB45309);

        /// <summary>--cancel-delete-button-color</summary>
        internal static readonly Color Danger = FromHex(0xE5342B);

        /// <summary>Semleges tájékoztatás (naplóútvonal, „nincs teendő" jellegű üzenet).</summary>
        internal static readonly Color Info = FromHex(0x487386);

        /// <summary>Száraz futtatás: a szokásostól eltérő üzemmód jelzése.</summary>
        internal static readonly Color DryRun = FromHex(0x0891B2);

        // --- a HC Linear jelkép három négyzete ----------------------------------------
        // A cég logójának felső eleme (lásd hclinear.hu favicon). A középső négyzet a
        // háttérhez igazodik: sötét alapon fehér, világos alapon a sötét márkaszín.

        internal static readonly Color MarkGreen = FromHex(0x66B331);
        internal static readonly Color MarkBlue = FromHex(0x1B9DD9);
        internal static readonly Color MarkDark = FromHex(0x232323);

        private static Color FromHex(int rgb)
            => Color.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
    }
}
