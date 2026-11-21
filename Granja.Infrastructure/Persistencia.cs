using Granja.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granja.Infrastructure;

public sealed class GranjaDbContext(DbContextOptions<GranjaDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelo)
    {
        modelo.Entity<Domain.Granja>().HasData(
            new Domain.Granja { NumeroVacas = 10, LitrosPorVaca = 15 });
        modelo.Entity<ParametroProducto>().HasKey(p => p.Tipo);
        modelo.Entity<ParametroProducto>().HasData(
            new ParametroProducto { Tipo = TipoProducto.Queso, LitrosPorUnidad = 10, VidaUtilDias = 30, Precio = 28000 },
            new ParametroProducto { Tipo = TipoProducto.Yogurt, LitrosPorUnidad = 1, VidaUtilDias = 15, Precio = 5000 });
        modelo.Entity<ProduccionLeche>().HasKey(p => new { p.Fecha, p.VacaId });
        modelo.Entity<LoteProducto>().HasIndex(l => new { l.Tipo, l.FechaProduccion });
        modelo.Entity<Venta>().HasMany(v => v.Detalle).WithOne().HasForeignKey("VentaId").IsRequired();
        modelo.Entity<Venta>().Navigation(v => v.Detalle).AutoInclude();
        modelo.Entity<VentaDetalle>().HasOne<LoteProducto>().WithMany()
            .HasForeignKey(d => d.LoteId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class Repository<T>(GranjaDbContext db) : IRepository<T> where T : class
{
    public Task<List<T>> ListarAsync(CancellationToken ct) => db.Set<T>().ToListAsync(ct);
    public void Agregar(T entidad) => db.Set<T>().Add(entidad);
}

public sealed class UnitOfWork(GranjaDbContext db) : IUnitOfWork
{
    public async Task<T> EjecutarAsync<T>(Func<Task<T>> operacion, CancellationToken ct)
    {
        // SQLite adquiere el bloqueo de escritura al iniciar esta transacción.
        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var resultado = await operacion();
            await transaccion.CommitAsync(ct);
            return resultado;
        }
        catch
        {
            db.ChangeTracker.Clear();
            throw;
        }
    }

    public Task GuardarAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

public sealed class SystemClock : IClock
{
    private static readonly TimeZoneInfo Zona = TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");
    public DateOnly Hoy => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Zona).DateTime);
}
