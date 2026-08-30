using mRemoteNG.Themes;
using System.Drawing;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace mRemoteNG.UI.Controls
{
    [SupportedOSPlatform("windows")]
    // Repaint of a ProgressBar on a flat style
    internal class MrngProgressBar : ProgressBar
    {
        private ThemeManager _themeManager;


        public MrngProgressBar()
        {
            ThemeManager.getInstance().ThemeChanged += OnCreateControl;
        }

        protected override void OnCreateControl()
        {
            base.OnCreateControl();
            _themeManager = ThemeManager.getInstance();
            if (!_themeManager.ThemingActive) return;
            SetStyle(ControlStyles.UserPaint, true);
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            Invalidate();
        }

        private string _caption = string.Empty;

        /// <summary>
        /// Text drawn across the bar - how far along, and what happened when it stops.
        /// </summary>
        /// <remarks>
        /// A bar filling up says something is happening and nothing about how much is left, and
        /// when it empties it says nothing at all: the port scan finished by resetting the bar to
        /// zero, so the only sign of completion was the blue vanishing. Whether that meant done,
        /// cancelled or crashed was anyone's guess.
        /// </remarks>
        public string Caption
        {
            get => _caption;
            set
            {
                if (_caption == value) return;

                _caption = value ?? string.Empty;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (!_themeManager.ActiveAndExtended)
            {
                base.OnPaint(e);
                return;
            }

            Color progressFill = _themeManager.ActiveTheme.ExtendedPalette.getColor("ProgressBar_Fill");
            Color back = _themeManager.ActiveTheme.ExtendedPalette.getColor("ProgressBar_Background");

            // ClientRectangle, not ClipRectangle: the clip is only the part being repainted, so
            // anything that invalidates a strip of the bar - a caption changing, for one - had the
            // fill computed against that strip's width and drawn at the wrong length.
            Rectangle area = ClientRectangle;
            int doneProgress = Maximum > 0 ? (int)(area.Width * ((double)Value / Maximum)) : 0;

            using (SolidBrush fillBrush = new(progressFill))
            using (SolidBrush backBrush = new(back))
            {
                e.Graphics.FillRectangle(fillBrush, 0, 0, doneProgress, area.Height);
                e.Graphics.FillRectangle(backBrush, doneProgress, 0, area.Width - doneProgress, area.Height);
            }

            if (string.IsNullOrEmpty(_caption)) return;

            using SolidBrush textBrush = new(
                _themeManager.ActiveTheme.ExtendedPalette.getColor("Dialog_Foreground"));
            using StringFormat centred = new()
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };

            e.Graphics.DrawString(_caption, Font, textBrush, area, centred);
        }
    }
}