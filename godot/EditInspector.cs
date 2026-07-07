using System.Collections.Generic;
using Godot;

// The selection inspector shown top-right in edit mode: the focused object's type + editable fields
// (position / rotation / size), with the active field highlighted, plus its colour and the action
// keys. EditController drives it via Render() each frame while something is focused.
public partial class EditInspector : Control
{
    public readonly record struct Row(string Label, string Value, bool Active);

    private PanelContainer _panel = null!;
    private Label _title = null!;
    private VBoxContainer _rows = null!;
    private Label _footer = null!;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);   // fill the screen so the panel anchors to its corner

        // pin the panel's top-right corner (20px in) and let it grow left + down to fit its content
        _panel = new PanelContainer { Theme = UiTheme.Get() };
        _panel.SetAnchorsPreset(LayoutPreset.TopRight);
        _panel.GrowHorizontal = GrowDirection.Begin;
        _panel.GrowVertical = GrowDirection.End;
        _panel.OffsetLeft = -20; _panel.OffsetRight = -20;
        _panel.OffsetTop = 20; _panel.OffsetBottom = 20;
        AddChild(_panel);

        var pad = new MarginContainer();
        foreach (var m in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            pad.AddThemeConstantOverride(m, 16);
        _panel.AddChild(pad);

        var v = new VBoxContainer { CustomMinimumSize = new Vector2(280, 0) };
        v.AddThemeConstantOverride("separation", 6);
        pad.AddChild(v);

        _title = UiTheme.Heading("", 20);
        v.AddChild(_title);
        v.AddChild(new HSeparator());
        _rows = new VBoxContainer();
        _rows.AddThemeConstantOverride("separation", 4);
        v.AddChild(_rows);
        v.AddChild(new HSeparator());
        _footer = UiTheme.Body("", UiTheme.TextDim, 13);
        v.AddChild(_footer);

        Visible = false;
    }

    public void Render(string title, IReadOnlyList<Row> rows, string footer)
    {
        Visible = true;
        _title.Text = title;
        _footer.Text = footer;

        // rebuild the value rows (few of them; only while an object is focused)
        foreach (Node c in _rows.GetChildren()) c.QueueFree();
        foreach (Row r in rows)
        {
            var line = new HBoxContainer();
            var lbl = UiTheme.Body((r.Active ? "▸ " : "   ") + r.Label, r.Active ? UiTheme.Accent : UiTheme.TextDim, 16);
            lbl.CustomMinimumSize = new Vector2(150, 0);
            line.AddChild(lbl);
            line.AddChild(UiTheme.Body(r.Value, r.Active ? UiTheme.Text : UiTheme.TextDim, 16));
            _rows.AddChild(line);
        }
    }
}
