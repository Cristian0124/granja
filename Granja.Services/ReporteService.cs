using AutoMapper;
using Granja.Domain;

namespace Granja.Services;

public sealed class ReporteService(IRepository<ProduccionLeche> producciones, IRepository<LoteProducto> lotes,
    IRepository<Venta> ventas, IUnitOfWork unidad, IMapper mapper)
{
    public async Task<ReporteVentas> VentasAsync(DateOnly? desde, DateOnly? hasta, TipoProducto? tipo, CancellationToken ct)
    {
        ValidarRango(desde, hasta);
        if (tipo.HasValue) Validacion.Tipo(tipo);
        var filtradas = (await ventas.ListarAsync(ct))
            .Where(v => EnRango(v.Fecha, desde, hasta) && (tipo is null || v.TipoProducto == tipo))
            .OrderBy(v => v.Fecha).ThenBy(v => v.Id).ToList();
        return new ReporteVentas(mapper.Map<List<VentaResponse>>(filtradas), filtradas.Sum(v => v.Total));
    }

    public Task<List<LecheDiaria>> LecheAsync(DateOnly? desde, DateOnly? hasta, CancellationToken ct)
    {
        ValidarRango(desde, hasta);
        return unidad.EjecutarAsync(async () =>
        {
            var leche = await producciones.ListarAsync(ct);
            var usados = (await lotes.ListarAsync(ct)).ToLookup(l => l.FechaProduccion);
            return leche.Where(p => EnRango(p.Fecha, desde, hasta)).GroupBy(p => p.Fecha)
                .OrderBy(g => g.Key).Select(g =>
                {
                    var producido = g.Sum(p => p.Litros);
                    var transformado = usados[g.Key].Sum(l => l.LitrosUsados);
                    return new LecheDiaria(g.Key, producido, transformado, producido - transformado);
                }).ToList();
        }, ct);
    }

    public async Task<List<ProductoDiario>> DiarioAsync(DateOnly? desde, DateOnly? hasta, CancellationToken ct)
    {
        ValidarRango(desde, hasta);
        return (await lotes.ListarAsync(ct)).Where(l => EnRango(l.FechaProduccion, desde, hasta))
            .GroupBy(l => new { l.FechaProduccion, l.Tipo })
            .OrderBy(g => g.Key.FechaProduccion).ThenBy(g => g.Key.Tipo)
            .Select(g => new ProductoDiario(g.Key.FechaProduccion, g.Key.Tipo, g.Sum(l => l.CantidadInicial))).ToList();
    }

    public Task<List<ProductoTotal>> TotalAsync(CancellationToken ct) =>
        unidad.EjecutarAsync(async () =>
        {
            var inventario = (await lotes.ListarAsync(ct)).ToLookup(l => l.Tipo);
            var historial = (await ventas.ListarAsync(ct)).ToLookup(v => v.TipoProducto);
            return Enum.GetValues<TipoProducto>().Select(tipo => new ProductoTotal(tipo,
                inventario[tipo].Sum(l => l.CantidadInicial), historial[tipo].Sum(v => v.Cantidad),
                inventario[tipo].Sum(l => l.CantidadDisponible), inventario[tipo].Sum(l => l.CantidadDanada))).ToList();
        }, ct);

    public async Task<List<LoteDto>> DanadoAsync(DateOnly? desde, DateOnly? hasta, CancellationToken ct)
    {
        ValidarRango(desde, hasta);
        var danados = (await lotes.ListarAsync(ct))
            .Where(l => l.CantidadDanada > 0 && EnRango(l.FechaVencimiento, desde, hasta))
            .OrderBy(l => l.FechaVencimiento).ThenBy(l => l.Id);
        return mapper.Map<List<LoteDto>>(danados);
    }

    public async Task<ReporteVacas> VacasAsync(DateOnly? desde, DateOnly? hasta, CancellationToken ct)
    {
        ValidarRango(desde, hasta);
        var vacas = (await producciones.ListarAsync(ct)).Where(p => EnRango(p.Fecha, desde, hasta))
            .GroupBy(p => p.VacaId).OrderBy(g => g.Key)
            .Select(g => new PromedioVaca(g.Key, g.Average(p => p.Litros), g.Sum(p => p.Litros), g.Count())).ToArray();
        if (vacas.Length == 0)
            return new ReporteVacas(null, [], [], [], []);
        var promedio = vacas.Average(v => v.Promedio);
        var maximo = vacas.Max(v => v.Promedio);
        var minimo = vacas.Min(v => v.Promedio);
        return new ReporteVacas(promedio, vacas, vacas.Where(v => v.Promedio == maximo).ToArray(),
            vacas.Where(v => v.Promedio == minimo).ToArray(), vacas.Where(v => v.Promedio < promedio).ToArray());
    }

    private static bool EnRango(DateOnly fecha, DateOnly? desde, DateOnly? hasta) =>
        (!desde.HasValue || fecha >= desde.Value) && (!hasta.HasValue || fecha <= hasta.Value);

    private static void ValidarRango(DateOnly? desde, DateOnly? hasta)
    {
        if (desde > hasta)
            throw new ValidacionException("La fecha desde debe ser anterior o igual a hasta.");
    }
}
