using System;
using Godot;

// A focusable "Mode" row that shows the choices inline as a pill toggle:  [icon] Mode  ( Race | Free Fly )
// Left/Right (or Enter, or clicking a pill) change the selection and call OnChange. Up/Down fall
// through so it navigates like any other row. The whole row lights up on focus like MenuRow.
public partial class Segmented : Control
{
    private readonly UiIcon _icon;
    private readonly Label _label;
    private readonly string[] _options;
    private readonly Action<int> _onChange;   // fired with the new index
    private readonly StyleBoxFlat _fill = new();
    private readonly StyleBoxFlat _pill = new();
    private int _index;
    private float _t;

    private const float PillW = 108f, PillH = 34f, Gap = 4f;

    public Segmented(string glyph, string label, string[] options, int index, Action<int> onChange, float minWidth)
    {
        _options = options; _index = index; _onChange = onChange;
        CustomMinimumSize = new Vector2(minWidth, 56);
        FocusMode = FocusModeEnum.All;
        _fill.SetCornerRadiusAll(UiTheme.RadMd);
        _pill.SetCornerRadiusAll(UiTheme.RadSm);

        _icon = new UiIcon(glyph, UiTheme.IconDim)
        {
            CustomMinimumSize = new Vector2(24, 24),
            Position = new Vector2(UiTheme.S5, 16),
            Size = new Vector2(24, 24),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        AddChild(_icon);

        _label = new Label { Text = label };
        _label.AddThemeFontSizeOverride("font_size", 20);
        _label.AddThemeColorOverride("font_color", new Color(UiTheme.Text, 0.80f));
        _label.Position = new Vector2(UiTheme.S5 + 24 + UiTheme.S4, 15);
        _label.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_label);
    }

    public int Index => _index;

    public void SetIndex(int i) { _index = Mathf.PosMod(i, _options.Length); QueueRedraw(); }

    private void Cycle(int dir)
    {
        SetIndex(_index + dir);
        _onChange(_index);
    }

    public override void _GuiInput(InputEvent ev)
    {
        if (ev is InputEventKey { Pressed: true } k && (k.Keycode is Key.Left or Key.Right or Key.Enter or Key.KpEnter))
        {
            Cycle(k.Keycode == Key.Left ? -1 : 1);
            AcceptEvent();
        }
        else if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mb)
        {
            float x0 = TrackLeft();
            for (int i = 0; i < _options.Length; i++)
            {
                float px = x0 + i * (PillW + Gap);
                if (mb.Position.X >= px && mb.Position.X <= px + PillW) { SetIndex(i); _onChange(_index); break; }
            }
            GrabFocus();
            AcceptEvent();
        }
    }

    private float TrackLeft() => Size.X - UiTheme.S5 - _options.Length * PillW - (_options.Length - 1) * Gap;

    public override void _Process(double delta)
    {
        float target = HasFocus() ? 1f : (GetGlobalRect().HasPoint(GetGlobalMousePosition()) ? 0.4f : 0f);
        float nt = Mathf.MoveToward(_t, target, (float)delta * 8f);
        if (Mathf.IsEqualApprox(nt, _t)) return;
        _t = nt;
        _icon.Color = UiTheme.IconDim.Lerp(UiTheme.Text, _t);
        _icon.QueueRedraw();
        _label.AddThemeColorOverride("font_color", new Color(UiTheme.Text, Mathf.Lerp(0.72f, 1f, _t)));
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_t > 0.01f)
        {
            _fill.BgColor = new Color(UiTheme.Accent, 0.13f * _t);
            DrawStyleBox(_fill, new Rect2(Vector2.Zero, Size));
            float barH = Size.Y * 0.52f;
            DrawRect(new Rect2(new Vector2(2, (Size.Y - barH) * 0.5f), new Vector2(3, barH)),
                new Color(UiTheme.Accent, _t), true);
        }

        Font font = UiTheme.BodyFont;
        float x0 = TrackLeft();
        float y = (Size.Y - PillH) * 0.5f;
        for (int i = 0; i < _options.Length; i++)
        {
            var r = new Rect2(new Vector2(x0 + i * (PillW + Gap), y), new Vector2(PillW, PillH));
            bool sel = i == _index;
            _pill.BgColor = sel ? new Color(UiTheme.Accent, 0.22f + 0.10f * _t) : new Color(1, 1, 1, 0.05f);
            _pill.BorderColor = sel ? new Color(UiTheme.Accent, 0.9f) : new Color(1, 1, 1, 0.08f);
            _pill.SetBorderWidthAll(sel ? 1 : 1);
            DrawStyleBox(_pill, r);

            Color tc = sel ? UiTheme.Accent : new Color(UiTheme.Text, 0.6f);
            Vector2 ts = font.GetStringSize(_options[i], HorizontalAlignment.Center, -1, 16);
            DrawString(font, new Vector2(r.Position.X, r.Position.Y + (PillH + ts.Y) * 0.5f - 4),
                _options[i], HorizontalAlignment.Center, PillW, 16, tc);
        }
    }
}
