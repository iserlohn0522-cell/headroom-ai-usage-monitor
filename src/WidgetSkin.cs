using System.Drawing;
namespace Headroom
{
    sealed class WidgetSkin
    {
        public string Id, Font;
        public Color Background, Surface, Text, Muted, Border, Accent, Track;
        public int Radius;
        public bool Segmented;
        public static string Normalize(string id) { return id == "paper" || id == "terminal" ? id : "midnight"; }
        public static WidgetSkin Get(string id)
        {
            if (id == "paper") return new WidgetSkin { Id=id, Font="Segoe UI", Background=Color.FromArgb(248,245,237), Surface=Color.FromArgb(235,229,215), Text=Color.FromArgb(39,50,48), Muted=Color.FromArgb(89,101,94), Border=Color.FromArgb(170,179,161), Accent=Color.FromArgb(34,113,86), Track=Color.FromArgb(217,222,206), Radius=16 };
            if (id == "terminal") return new WidgetSkin { Id=id, Font="Consolas", Background=Color.FromArgb(9,22,18), Surface=Color.FromArgb(17,39,29), Text=Color.FromArgb(166,248,181), Muted=Color.FromArgb(119,172,136), Border=Color.FromArgb(63,118,78), Accent=Color.FromArgb(126,239,155), Track=Color.FromArgb(28,62,40), Radius=2, Segmented=true };
            return new WidgetSkin { Id="midnight", Font="Segoe UI", Background=Color.FromArgb(19,25,42), Surface=Color.FromArgb(34,43,66), Text=Color.FromArgb(237,243,255), Muted=Color.FromArgb(162,178,202), Border=Color.FromArgb(65,82,114), Accent=Color.FromArgb(103,187,255), Track=Color.FromArgb(43,57,80), Radius=12 };
        }
    }
}
