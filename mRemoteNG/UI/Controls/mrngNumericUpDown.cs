using System;
using System.Drawing;
using System.Runtime.Versioning;
using System.Windows.Forms;
using mRemoteNG.Themes;

// ReSharper disable LocalizableElement

namespace mRemoteNG.UI.Controls
{
    [SupportedOSPlatform("windows")]
    //Repaint of the NumericUpDown, the composite control buttons are replaced because the
    //original ones cannot be themed due to protected inheritance
    internal class MrngNumericUpDown : NumericUpDown
    {
        private readonly ThemeManager _themeManager;
        private MrngButton Up;
        private MrngButton Down;

        public MrngNumericUpDown()
        {
            _themeManager = ThemeManager.getInstance();
            ThemeManager.getInstance().ThemeChanged += OnCreateControl;
        }

        protected override void OnCreateControl()
        {
            base.OnCreateControl();
            if (!_themeManager.ActiveAndExtended) return;
            ForeColor = _themeManager.ActiveTheme.ExtendedPalette.getColor("TextBox_Foreground");
            BackColor = _themeManager.ActiveTheme.ExtendedPalette.getColor("TextBox_Background");
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);

            if (Controls.Count > 0)
            {
                for (int i = 0; i < Controls.Count; i++)
                {
                    //Remove those non-themable buttons
                    if (Controls[i].GetType().ToString().Equals("System.Windows.Forms.UpDownBase+UpDownButtons"))
                        Controls.Remove(Controls[i]);

                    /* This is a bit of a hack.
                     * But if we have the buttons that we created already, redraw/return and don't add any more...
                     *
                     * OptionsPages are an example where the control is potentially created twice:
                     * AddOptionsPagesToListView and then LstOptionPages_SelectedIndexChanged
                     */
                    if (!(Controls[i] is MrngButton)) continue;
                    if (!Controls[i].Text.Equals("\u25B2") && !Controls[i].Text.Equals("\u25BC")) continue;
                    Invalidate();
                    return;
                }
            }

            //Add new themable buttons
            Up = new MrngButton
            {
                Text = "\u25B2",
                Font = new Font(Font.FontFamily, 5f * ScaleFactor)
            };
            Up.Click += Up_Click;
            Down = new MrngButton
            {
                Text = "\u25BC",
                Font = new Font(Font.FontFamily, 5f * ScaleFactor)
            };
            Down.Click += Down_Click;
            Controls.Add(Up);
            Controls.Add(Down);
            LayOutButtons();
            Invalidate();
        }

        /// <summary>How much larger everything is than at the 96 DPI these sizes were written for.</summary>
        private float ScaleFactor => DeviceDpi / 96f;

        /// <summary>
        /// Puts the replacement spin buttons against the right edge and stretches the number field
        /// up to them.
        /// </summary>
        /// <remarks>
        /// Removing the native spin buttons does not give their space back: the base class still
        /// lays the text box out as if they were there. The replacement buttons went to a hard
        /// coded offset instead, which left a strip of bare control between the number and the
        /// arrows where the borders met - visible as an unreadable sliver. Sizing from the actual
        /// client area closes it, and gives the number the space back as well.
        /// </remarks>
        private void LayOutButtons()
        {
            if (Up == null || Down == null || ClientSize.Width <= 0) return;

            // Start where the text box ends rather than at a fixed offset. Widening the text box
            // instead does not work: the base class lays it out again on every layout pass and
            // puts it straight back.
            int left = ClientSize.Width - (int)Math.Round(17 * ScaleFactor);
            foreach (Control child in Controls)
            {
                if (child is MrngButton) continue;
                left = child.Right;
                break;
            }

            int width = ClientSize.Width - left - 1;
            if (width <= 0) return;

            int half = (ClientSize.Height - 2) / 2;
            Up.SetBounds(left, 1, width, half);
            Down.SetBounds(left, 1 + half, width, half);
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);

            // The base class moves its text box during layout, so the buttons have to follow it
            // afterwards or the gap reappears.
            LayOutButtons();
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            LayOutButtons();
        }

        protected override void OnDpiChangedAfterParent(EventArgs e)
        {
            base.OnDpiChangedAfterParent(e);
            LayOutButtons();
        }

        private void Down_Click(object sender, EventArgs e)
        {
            DownButton();
        }

        private void Up_Click(object sender, EventArgs e)
        {
            UpButton();
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            if (_themeManager.ActiveAndExtended)
            {
                if (Enabled)
                {
                    ForeColor = _themeManager.ActiveTheme.ExtendedPalette.getColor("TextBox_Foreground");
                    BackColor = _themeManager.ActiveTheme.ExtendedPalette.getColor("TextBox_Background");
                }
                else
                {
                    BackColor = _themeManager.ActiveTheme.ExtendedPalette.getColor("TextBox_Disabled_Background");
                }
            }

            base.OnEnabledChanged(e);
            Invalidate();
        }


        //Redrawing border
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (!_themeManager.ActiveAndExtended) return;
            //Fix Border
            if (BorderStyle != BorderStyle.None)
                e.Graphics.DrawRectangle(
                                         new Pen(_themeManager.ActiveTheme.ExtendedPalette.getColor("TextBox_Border"),
                                                 1), 0, 0, Width - 1,
                                         Height - 1);
        }

        private void InitializeComponent()
        {
            ((System.ComponentModel.ISupportInitialize)(this)).BeginInit();
            this.SuspendLayout();
            // 
            // NGNumericUpDown
            // 
            this.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular,
                                                System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            ((System.ComponentModel.ISupportInitialize)(this)).EndInit();
            this.ResumeLayout(false);
        }
    }
}