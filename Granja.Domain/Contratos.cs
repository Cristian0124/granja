namespace Granja.Domain;

public interface IClock
{
    DateOnly Hoy { get; }
}

public interface IRepository<T> where T : class
{
    Task<List<T>> ListarAsync(CancellationToken ct);
    void Agregar(T entidad);
}

public interface IUnitOfWork
{
    Task<T> EjecutarAsync<T>(Func<Task<T>> operacion, CancellationToken ct);
    Task GuardarAsync(CancellationToken ct);
}
