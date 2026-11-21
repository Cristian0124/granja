using AutoMapper;
using Granja.Domain;
using Granja.Infrastructure;
using Granja.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Granja.Tests;

public sealed class ReglasTests : IAsyncLifetime
{
    private static readonly DateOnly Dia1 = new(2026, 12, 1);
    private static readonly DateOnly Dia2 = Dia1.AddDays(1);
    private static readonly DateOnly Dia3 = Dia1.AddDays(2);
    private static CancellationToken Ct => CancellationToken.None;
    private readonly SqliteConnection conexion = new("Data Source=:memory:");
    private GranjaDbContext db = null!;
    private ProduccionService produccion = null!;
    private VentaService ventas = null!;
    private InventarioService inventario = null!;
    private ReporteService reportes = null!;
    private GranjaService granja = null!;

    public async Task InitializeAsync()
    {
        await conexion.OpenAsync();
        db = new GranjaDbContext(new DbContextOptionsBuilder<GranjaDbContext>().UseSqlite(conexion).Options);
        await db.Database.MigrateAsync();
        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<GranjaProfile>(), NullLoggerFactory.Instance).CreateMapper();
        var unidad = new UnitOfWork(db);
        var reloj = new RelojFijo();
        var vacas = new Repository<Domain.Granja>(db);
        var leche = new Repository<ProduccionLeche>(db);
        var lotes = new Repository<LoteProducto>(db);
        var parametros = new Repository<ParametroProducto>(db);
        var historial = new Repository<Venta>(db);
        produccion = new ProduccionService(vacas, leche, lotes, parametros, unidad, reloj, mapper);
        ventas = new VentaService(lotes, historial, parametros, unidad, reloj, mapper);
        inventario = new InventarioService(lotes, unidad, reloj, mapper);
        reportes = new ReporteService(leche, lotes, historial, unidad, mapper);
        granja = new GranjaService(vacas, parametros, unidad, mapper);
    }

    [Fact]
    public async Task ProduccionUsaRelojYRechazaFechaDuplicada()
    {
        var registro = await produccion.RegistrarAsync(new(null, null), Ct);
        Assert.Equal(Dia1, registro.Fecha);
        Assert.Equal(150m, registro.LitrosProducidos);
        Assert.Equal(10, registro.Mediciones.Count);
        await Assert.ThrowsAsync<ConflictoException>(() => produccion.RegistrarAsync(new(Dia1, null), Ct));
        Assert.Equal(150m, Assert.Single(await reportes.LecheAsync(null, null, Ct)).LitrosProducidos);
    }

    [Fact]
    public async Task TransformacionNoSuperaLosLitrosDelDia()
    {
        await RegistrarLeche(Dia1);
        await Transformar(TipoProducto.Queso, 100, Dia1);
        await Assert.ThrowsAsync<LecheInsuficienteException>(() => Transformar(TipoProducto.Yogurt, 51, Dia1));
        Assert.Equal(new LecheDiaria(Dia1, 150, 100, 50), Assert.Single(await reportes.LecheAsync(null, null, Ct)));
    }

    [Fact]
    public async Task VentaUsaPrimeroElLoteAntiguo()
    {
        var (primero, segundo) = await PrepararQueso();
        var venta = await Vender(TipoProducto.Queso, 5, Dia3);
        Assert.Equal(primero.Id, Assert.Single(venta.Detalle).LoteId);
        var filas = await inventario.ConsultarAsync(Dia3, TipoProducto.Queso, Ct);
        Assert.Equal(5m, filas.Single(f => f.Lote.Id == primero.Id).Lote.CantidadDisponible);
        Assert.Equal(15m, filas.Single(f => f.Lote.Id == segundo.Id).Lote.CantidadDisponible);
    }

    [Fact]
    public async Task VentaReparteEntreLotesYGuardaElPrecio()
    {
        var (primero, segundo) = await PrepararQueso();
        var venta = await Vender(TipoProducto.Queso, 12, Dia3);
        Assert.Equal(336000m, venta.Total);
        Assert.Collection(venta.Detalle,
            d => Assert.Equal(new VentaDetalleDto(primero.Id, Dia1, 10), d),
            d => Assert.Equal(new VentaDetalleDto(segundo.Id, Dia2, 2), d));
        db.ChangeTracker.Clear();
        var reporte = await reportes.VentasAsync(Dia3, Dia3, TipoProducto.Queso, Ct);
        Assert.Equal(336000m, reporte.TotalGeneral);
        Assert.Equal(2, Assert.Single(reporte.Ventas).Detalle.Count);
    }

    [Fact]
    public async Task LoteVenceDespuesDeSuFechaDeVencimiento()
    {
        await RegistrarLeche(Dia1);
        var antiguo = await Transformar(TipoProducto.Yogurt, 50, Dia1);
        await RegistrarLeche(Dia2);
        var nuevo = await Transformar(TipoProducto.Yogurt, 50, Dia2);
        Assert.Equal(antiguo.Id, Assert.Single((await Vender(TipoProducto.Yogurt, 20, Dia1.AddDays(15))).Detalle).LoteId);
        Assert.Equal(nuevo.Id, Assert.Single((await Vender(TipoProducto.Yogurt, 5, Dia1.AddDays(16))).Detalle).LoteId);
    }

    [Fact]
    public async Task VentaSinExistenciasConservaInventario()
    {
        await PrepararQueso();
        await Assert.ThrowsAsync<InventarioInsuficienteException>(() => Vender(TipoProducto.Queso, 26, Dia3));
        db.ChangeTracker.Clear();
        var queso = (await reportes.TotalAsync(Ct)).Single(p => p.Tipo == TipoProducto.Queso);
        Assert.Equal(new ProductoTotal(TipoProducto.Queso, 25, 0, 25, 0), queso);
        Assert.Empty((await reportes.VentasAsync(null, null, null, Ct)).Ventas);
    }

    [Fact]
    public async Task EscenarioCompletoProcesaVencimientoYReportaPerdida()
    {
        await PrepararQueso();
        await Transformar(TipoProducto.Yogurt, 50, Dia1);
        await Vender(TipoProducto.Queso, 12, Dia3);
        await Vender(TipoProducto.Yogurt, 20, Dia3);
        var dia17 = Dia1.AddDays(16);
        Assert.Equal(30m, Assert.Single(await inventario.ProcesarAsync(dia17, Ct)).CantidadDanada);
        Assert.Empty(await inventario.ProcesarAsync(dia17, Ct));
        await Assert.ThrowsAsync<InventarioInsuficienteException>(() => Vender(TipoProducto.Yogurt, 5, dia17));
        Assert.Equal(30m, Assert.Single(await reportes.DanadoAsync(null, null, Ct)).CantidadDanada);
        Assert.Equal(new ProductoTotal(TipoProducto.Queso, 25, 12, 13, 0), (await reportes.TotalAsync(Ct))[0]);
        Assert.Equal(new ProductoTotal(TipoProducto.Yogurt, 50, 20, 0, 30), (await reportes.TotalAsync(Ct))[1]);
        Assert.Equal(new[] { 150m, 150m }, (await reportes.LecheAsync(null, null, Ct)).Select(r => r.LitrosProducidos));
    }

    [Fact]
    public async Task ReporteDeVacasCalculaPromedioExtremosYDebajoDelPromedio()
    {
        var configuracion = await granja.ConsultarAsync(Ct);
        await granja.ActualizarAsync(configuracion with { NumeroVacas = 3 }, Ct);
        await produccion.RegistrarAsync(new(Dia1, [new(1, 12), new(2, 15), new(3, 18)]), Ct);
        var reporte = await reportes.VacasAsync(Dia1, Dia1, Ct);
        Assert.Equal(15m, reporte.PromedioGeneral);
        Assert.Equal(3, Assert.Single(reporte.MayorProduccion).VacaId);
        Assert.Equal(1, Assert.Single(reporte.MenorProduccion).VacaId);
        Assert.Equal(1, Assert.Single(reporte.DebajoDelPromedio).VacaId);
    }

    private Task<ProduccionResponse> RegistrarLeche(DateOnly dia) => produccion.RegistrarAsync(new(dia, null), Ct);
    private Task<LoteDto> Transformar(TipoProducto tipo, decimal litros, DateOnly dia) =>
        produccion.TransformarAsync(new(tipo, litros, dia), Ct);
    private Task<VentaResponse> Vender(TipoProducto tipo, decimal cantidad, DateOnly dia) =>
        ventas.RegistrarVentaAsync(new(tipo, cantidad, dia), Ct);

    private async Task<(LoteDto, LoteDto)> PrepararQueso()
    {
        await RegistrarLeche(Dia1);
        await RegistrarLeche(Dia2);
        return (await Transformar(TipoProducto.Queso, 100, Dia1), await Transformar(TipoProducto.Queso, 150, Dia2));
    }

    public async Task DisposeAsync()
    {
        await db.DisposeAsync();
        await conexion.DisposeAsync();
    }

    private sealed class RelojFijo : IClock
    {
        public DateOnly Hoy => Dia1;
    }
}
