class OverlayForm : Form
{
    private Rectangle[] rects;

    public OverlayForm(Rectangle[] areas)
    {
        rects = areas;
        FormBorderStyle = FormBorderStyle.None;
        BackColor = Color.Lime; // прозорий ключ
        TransparencyKey = Color.Lime;
        TopMost = true;
        ShowInTaskbar = false;
        Bounds = Screen.PrimaryScreen.Bounds;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        using var pen = new Pen(Color.Red, 3);
        foreach (var r in rects)
        {
            // Малюємо у абсолютних координатах
            e.Graphics.DrawRectangle(pen, r);
        }
    }

    public async Task ShowTemporary(int ms)
    {
        this.Show();
        await Task.Delay(ms);
        this.Close();
    }
}
