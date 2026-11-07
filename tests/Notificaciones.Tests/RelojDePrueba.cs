namespace Notificaciones.Tests;

/// <summary>Reloj que solo avanza cuando la prueba lo pide.</summary>
public sealed class RelojDePrueba : TimeProvider
{
    public DateTimeOffset Ahora { get; private set; } = DateTimeOffset.FromUnixTimeMilliseconds(1760720400000);

    public override DateTimeOffset GetUtcNow() => Ahora;

    public void Avanzar(TimeSpan tiempo) => Ahora += tiempo;
}
