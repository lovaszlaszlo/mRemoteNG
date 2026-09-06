using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace mRemoteNG.UI
{
    public class FullscreenHandler(Form handledForm)
    {
        private readonly Form _handledForm = handledForm;
        private FormWindowState _savedWindowState;
        private FormBorderStyle _savedBorderStyle;
        private Rectangle _savedBounds;
        private bool _value;

        /// <summary>
        /// Everything around the session that should go away in fullscreen: the menu bar, the
        /// toolbars, the tab strips.
        /// </summary>
        /// <remarks>
        /// Removing the window border and maximising is all this used to do, so the menu, the
        /// toolbars and the tabs stayed on screen and the session got what was left. RDP has a
        /// fullscreen of its own - the ActiveX control's, which does take the whole screen - so
        /// the shortfall only showed on every other protocol, where this is the only fullscreen
        /// there is.
        ///
        /// Filled in by the form that owns this, because what counts as chrome is its business.
        /// </remarks>
        public IList<Control> Chrome { get; } = [];

        /// <summary>
        /// Run on the way in and on the way out, for chrome that is hidden by some means other
        /// than <see cref="Control.Visible"/>.
        /// </summary>
        public Action<bool> ChromeToggled { get; set; }

        public bool Value
        {
            get => _value;
            set
            {
                if (_value == value) return;
                if (!_value)
                    EnterFullscreen();
                else
                    ExitFullscreen();
                _value = value;
            }
        }

        private void EnterFullscreen()
        {
            _savedBorderStyle = _handledForm.FormBorderStyle;
            _savedWindowState = _handledForm.WindowState;
            _savedBounds = _handledForm.Bounds;

            _savedChromeVisibility.Clear();
            foreach (Control control in Chrome)
            {
                _savedChromeVisibility[control] = control.Visible;
                control.Visible = false;
            }

            ChromeToggled?.Invoke(true);

            _handledForm.FormBorderStyle = FormBorderStyle.None;
            if (_handledForm.WindowState == FormWindowState.Maximized)
            {
                _handledForm.WindowState = FormWindowState.Normal;
            }

            _handledForm.WindowState = FormWindowState.Maximized;
        }

        private void ExitFullscreen()
        {
            _handledForm.FormBorderStyle = _savedBorderStyle;
            _handledForm.WindowState = _savedWindowState;
            _handledForm.Bounds = _savedBounds;

            // Put back what each control was, not just "visible": a toolbar the user had turned
            // off in the View menu has to stay off.
            foreach (KeyValuePair<Control, bool> saved in _savedChromeVisibility)
            {
                if (!saved.Key.IsDisposed) saved.Key.Visible = saved.Value;
            }

            _savedChromeVisibility.Clear();
            ChromeToggled?.Invoke(false);
        }

        private readonly Dictionary<Control, bool> _savedChromeVisibility = [];
    }
}