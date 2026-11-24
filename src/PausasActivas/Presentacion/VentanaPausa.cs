using PausasActivas.Nucleo;
using PausasActivas.Recursos;

namespace PausasActivas.Presentacion;

// Comportamiento cerrado (RF-13 a RF-23). Antigravity puede cambiar la apariencia
// en AplicarApariencia, sin tocar eventos, el temporizador ni las firmas públicas.
internal sealed class VentanaPausa : Form, IPresentadorPausa
{
    private readonly IReproductorSonido _sonido;
    private readonly PictureBox _imagen = new();
    private readonly Label _contador = new();
    private readonly Button _posponer = new();
    private readonly Button _saltar = new();
    private readonly Button _termine = new();
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 1000 };
    private int _segundosRestantes;
    private bool _contando;
    private bool _cierreInterno;
    private bool _notificando;

    public VentanaPausa(IReproductorSonido sonido)
    {
        _sonido = sonido;
        AplicarApariencia();
        _posponer.Click += (_, _) => PosponerSolicitado?.Invoke(this, EventArgs.Empty);
        _saltar.Click += (_, _) => PedirSaltar();
        _termine.Click += (_, _) => TermineSolicitado?.Invoke(this, EventArgs.Empty);
        _timer.Tick += (_, _) => AvanzarContador();
    }

    public event EventHandler? PosponerSolicitado;
    public event EventHandler? SaltarSolicitado;
    public event EventHandler? TermineSolicitado;

    public void Mostrar(Image imagen, int posposicionesRestantes)
    {
        _imagen.Image = imagen;
        _segundosRestantes = ConstantesPausa.DuracionSegundos;
        _contando = true;
        _contador.Text = Formatear(_segundosRestantes);
        _posponer.Visible = true;
        _saltar.Visible = true;
        _termine.Visible = false;
        _posponer.Enabled = posposicionesRestantes > 0;
        _posponer.Text = $"Posponer ({posposicionesRestantes})";
        CentrarEnMonitorPrincipal();
        if (!Visible)
            Show();

        TopMost = true;
        Activate();
        ActiveControl = null;
        _timer.Start();
    }

    public void Cerrar()
    {
        _cierreInterno = true;
        _timer.Stop();
        _contando = false;
        Hide();
        _cierreInterno = false;
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Alt | Keys.F4))
        {
            PedirSaltar();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_cierreInterno && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            PedirSaltar();
            return;
        }

        base.OnFormClosing(e);
    }

    private void AvanzarContador()
    {
        if (!_contando)
            return;

        if (_segundosRestantes > 0)
            _segundosRestantes--;

        _contador.Text = Formatear(_segundosRestantes);
        if (_segundosRestantes > 0)
            return;

        _contando = false;
        _posponer.Visible = false;
        _saltar.Visible = false;
        _termine.Visible = true;
        ActiveControl = null;
        _sonido.Reproducir();
    }

    private void PedirSaltar()
    {
        if (_notificando)
            return;

        _notificando = true;
        try
        {
            SaltarSolicitado?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _notificando = false;
        }
    }

    private void CentrarEnMonitorPrincipal()
    {
        var area = Screen.PrimaryScreen?.WorkingArea ?? Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1280, 800);
        Location = new Point(area.Left + (area.Width - Width) / 2, area.Top + (area.Height - Height) / 2);
    }

    private void AplicarApariencia()
    {
        var verde = Color.FromArgb(28, 69, 50);
        var crema = Color.FromArgb(245, 247, 244);
        var borde = Color.FromArgb(223, 231, 225);

        Text = "Pausa activa";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        ControlBox = false;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.Manual;
        ClientSize = new Size(940, 700);
        BackColor = crema;
        Font = new Font("Segoe UI", 11F);

        var encabezado = new Panel { Dock = DockStyle.Top, Height = 66, BackColor = verde };
        var marca = new Label
        {
            Dock = DockStyle.Left,
            Width = 460,
            Text = "PAUSA ACTIVA",
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(28, 0, 0, 0),
            Font = new Font("Segoe UI", 15F, FontStyle.Bold)
        };
        var lema = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Respira  ·  Estira  ·  Suelta",
            ForeColor = Color.FromArgb(196, 219, 206),
            TextAlign = ContentAlignment.MiddleRight,
            Padding = new Padding(0, 0, 28, 0),
            Font = new Font("Segoe UI", 10F)
        };
        encabezado.Controls.Add(lema);
        encabezado.Controls.Add(marca);

        var marcoImagen = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24, 20, 24, 16),
            BackColor = crema
        };
        _imagen.Dock = DockStyle.Fill;
        _imagen.SizeMode = PictureBoxSizeMode.Zoom;
        _imagen.BackColor = Color.White;
        _imagen.TabStop = false;
        _imagen.Paint += (_, e) =>
        {
            using var lapiz = new Pen(borde);
            e.Graphics.DrawRectangle(lapiz, 0, 0, _imagen.Width - 1, _imagen.Height - 1);
        };
        marcoImagen.Controls.Add(_imagen);

        _contador.AutoSize = false;
        _contador.TextAlign = ContentAlignment.MiddleCenter;
        _contador.Dock = DockStyle.Top;
        _contador.Height = 92;
        _contador.Font = new Font("Segoe UI", 38F, FontStyle.Bold);
        _contador.ForeColor = verde;
        _contador.BackColor = crema;
        _contador.Text = "05:00";

        ConfigurarBoton(_posponer, "Posponer (2)", Color.White, verde, verde);
        ConfigurarBoton(_saltar, "Saltar", Color.White, Color.FromArgb(96, 108, 101), borde);
        ConfigurarBoton(_termine, "Terminé", verde, Color.White, verde);
        _termine.Visible = false;

        var botonera = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            BackColor = crema,
            Margin = Padding.Empty
        };
        botonera.Controls.Add(_posponer);
        botonera.Controls.Add(_saltar);
        botonera.Controls.Add(_termine);

        var zonaBotones = new Panel { Dock = DockStyle.Fill, BackColor = crema };
        zonaBotones.Controls.Add(botonera);
        void Centrar()
        {
            botonera.Left = Math.Max(0, (zonaBotones.ClientSize.Width - botonera.Width) / 2);
            botonera.Top = Math.Max(0, (zonaBotones.ClientSize.Height - botonera.Height) / 2);
        }
        zonaBotones.Resize += (_, _) => Centrar();
        botonera.SizeChanged += (_, _) => Centrar();
        Centrar();

        var pie = new Panel { Dock = DockStyle.Bottom, Height = 150, BackColor = crema };
        pie.Paint += (_, e) =>
        {
            using var lapiz = new Pen(borde);
            e.Graphics.DrawLine(lapiz, 24, 0, pie.Width - 24, 0);
        };
        pie.Controls.Add(zonaBotones);
        pie.Controls.Add(_contador);

        Controls.Add(marcoImagen);
        Controls.Add(pie);
        Controls.Add(encabezado);
    }

    private static void ConfigurarBoton(Button boton, string texto, Color fondo, Color frente, Color borde)
    {
        boton.Text = texto;
        boton.AutoSize = true;
        boton.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        boton.MinimumSize = new Size(170, 52);
        boton.Padding = new Padding(24, 0, 24, 0);
        boton.Margin = new Padding(10, 0, 10, 0);
        boton.FlatStyle = FlatStyle.Flat;
        boton.FlatAppearance.BorderSize = 1;
        boton.FlatAppearance.BorderColor = borde;
        boton.FlatAppearance.MouseOverBackColor = Color.FromArgb(235, 242, 237);
        boton.BackColor = fondo;
        boton.ForeColor = frente;
        boton.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        boton.Cursor = Cursors.Hand;
    }

    private static string Formatear(int segundos) =>
        $"{segundos / 60:00}:{segundos % 60:00}";
}
