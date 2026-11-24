namespace PausasActivas;

internal sealed class InstanciaUnica : IDisposable
{
    private readonly Mutex _mutex;
    private bool _poseeMutex;

    private InstanciaUnica(Mutex mutex, bool poseeMutex)
    {
        _mutex = mutex;
        _poseeMutex = poseeMutex;
    }

    public static InstanciaUnica? IntentarAdquirir()
    {
        var mutex = new Mutex(initiallyOwned: true, @"Local\PausasActivas", out var creada);
        if (creada)
            return new InstanciaUnica(mutex, poseeMutex: true);

        mutex.Dispose();
        return null;
    }

    public void Dispose()
    {
        if (_poseeMutex)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
            }
            _poseeMutex = false;
        }

        _mutex.Dispose();
    }
}
