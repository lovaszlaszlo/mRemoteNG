using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.Versioning;
using System.Windows.Forms;
using mRemoteNG.Themes;

namespace mRemoteNG.UI.Controls
{
    [SupportedOSPlatform("windows")]
    [ToolboxBitmap(typeof(Button))]
    //Extended button class, the button onPaint completely repaint the control
    public class MrngButton : Button
    {
        private ThemeManager _themeManager;

        /// <summary>
        /// Store the mouse state, required for coloring the component according to the mouse state
        /// </summary>
        public enum MouseState
        {
            HOVER,
            DOWN,
            OUT
        }

        public MrngButton()
        {
            ThemeManager.getInstance().ThemeChanged += OnCreateControl;
        }

        public MouseState _mice { get; set; }

        /// <summary>
        /// Rewrite the function to allow for coloring the component depending on the mouse state
        /// </summary>
        protected override void OnCreateControl()
        {
            base.OnCreateControl();
            _themeManager = ThemeManager.getInstance();
            if (_themeManager.ThemingActive)
            {
                _mice = MouseState.OUT;
                MouseEnter += (sender, args) =>
                {
                    _mice = MouseState.HOVER;
                    Invalidate();
                };
                MouseLeave += (sender, args) =>
                {
                    _mice = MouseState.OUT;
                    Invalidate();
                };
                MouseDown += (sender, args) =>
                {
                    if (args.Button == MouseButtons.Left)
                    {
                        _mice = MouseState.DOWN;
                        Invalidate();
                    }
                };
                MouseUp += (sender, args) =>
                {
                    _mice = MouseState.OUT;

                    Invalidate();
                };
                Invalidate();
            }
        }


        /// <summary>
        /// Repaint the componente, the elements considered are the clipping rectangle, text and an icon
        /// </summary>
        /// <param name="e"></param>
        protected override void OnPaint(PaintEventArgs e)
        {
            if (!_themeManager.ActiveAndExtended)
            {
                base.OnPaint(e);
                return;
            }

            Color back;
            Color fore;
            Color border;
            if (Enabled)
            {
                switch (_mice)
                {
                    case MouseState.HOVER:
                        back = _themeManager.ActiveTheme.ExtendedPalette.getColor("Button_Hover_Background");
                        fore = _themeManager.ActiveTheme.ExtendedPalette.getColor("Button_Hover_Foreground");
                        border = _themeManager.ActiveTheme.ExtendedPalette.getColor("Button_Hover_Border");
                        break;
                    case MouseState.DOWN:
                        back = _themeManager.ActiveTheme.ExtendedPalette.getColor("Button_Pressed_Background");
                        fore = _themeManager.ActiveTheme.ExtendedPalette.getColor("Button_Pressed_Foreground");
                        border = _themeManager.ActiveTheme.ExtendedPalette.getColor("Button_Pressed_Border");
                        break;
                    default:
                        back = _themeManager.ActiveTheme.ExtendedPalette.getColor("Button_Background");
                        fore = _themeManager.ActiveTheme.ExtendedPalette.getColor("Button_Foreground");
                        border = _themeManager.ActiveTheme.ExtendedPalette.getColor("Button_Border");
                        break;
                }
            }
            else
            {
                back = _themeManager.ActiveTheme.ExtendedPalette.getColor("Button_Disabled_Background");
                fore = _themeManager.ActiveTheme.ExtendedPalette.getColor("Button_Disabled_Foreground");
                border = _themeManager.ActiveTheme.ExtendedPalette.getColor("Button_Disabled_Border");
            }


            e.Graphics.FillRectangle(new SolidBrush(back), e.ClipRectangle);
            e.Graphics.DrawRectangle(new Pen(border, 1), 0, 0, Width - 1, Height - 1);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = TextRenderingHint.AntiAlias;
            // The image and the caption are placed together, as one block centred in the button.
            //
            // They used to be placed independently: the caption was centred in the whole button
            // as if there were no image, and the image was then dropped immediately to the left
            // of where the caption starts - no gap at all, and on a button whose caption is wide
            // relative to its width, off the left edge and under the border.
            //
            // Setting TextImageRelation or ImageAlign on such a button changes nothing, because
            // this method paints both by hand whenever the extended theme is on.
            if (Image == null)
            {
                TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, fore,
                                      TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            else
            {
                const int gap = 6;
                const int edge = 3;

                // MeasureText, not Graphics.MeasureString: DrawText below is what actually renders
                // the caption, and the two measure differently.
                Size textSize = TextRenderer.MeasureText(e.Graphics, Text, Font);
                int blockWidth = Image.Width + gap + textSize.Width;
                int left = Math.Max(edge, (Width - blockWidth) / 2);

                e.Graphics.DrawImageUnscaled(Image, left, (Height - Image.Height) / 2);

                int textLeft = left + Image.Width + gap;
                Rectangle textRect = new(textLeft, 0, Math.Max(0, Width - textLeft - edge), Height);

                TextRenderer.DrawText(e.Graphics, Text, Font, textRect, fore,
                                      TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                                      TextFormatFlags.EndEllipsis);
            }

            // Draw focus rectangle if button has focus
            if (Focused && Enabled)
            {
                Rectangle focusRect = new(2, 2, Width - 4, Height - 4);
                ControlPaint.DrawFocusRectangle(e.Graphics, focusRect);
            }
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            // 
            // NGButton
            // 
            this.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular,
                                                System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ResumeLayout(false);
        }
    }
}