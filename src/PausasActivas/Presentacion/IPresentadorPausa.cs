namespace PausasActivas.Presentacion;

internal interface IPresentadorPausa
{
    event EventHandler? PosponerSolicitado;
    event EventHandler? SaltarSolicitado;
    event EventHandler? TermineSolicitado;

    void Mostrar(Image imagen, int posposicionesRestantes);
    void Cerrar();
}
