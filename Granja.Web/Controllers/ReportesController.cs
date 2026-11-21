using Granja.Domain;
using Granja.Services;
using Microsoft.AspNetCore.Mvc;

namespace Granja.Web.Controllers;

[ApiController, Route("api/reportes")]
public sealed class ReportesController(ReporteService servicio) : ControllerBase
{
    [HttpGet("ventas")]
    public Task<ReporteVentas> Ventas(DateOnly? desde, DateOnly? hasta, TipoProducto? tipo, CancellationToken ct) =>
        servicio.VentasAsync(desde, hasta, tipo, ct);

    [HttpGet("leche-diaria")]
    public Task<List<LecheDiaria>> Leche(DateOnly? desde, DateOnly? hasta, CancellationToken ct) =>
        servicio.LecheAsync(desde, hasta, ct);

    [HttpGet("producto-diario")]
    public Task<List<ProductoDiario>> Diario(DateOnly? desde, DateOnly? hasta, CancellationToken ct) =>
        servicio.DiarioAsync(desde, hasta, ct);

    [HttpGet("producto-total")]
    public Task<List<ProductoTotal>> Total(CancellationToken ct) => servicio.TotalAsync(ct);

    [HttpGet("producto-danado")]
    public Task<List<LoteDto>> Danado(DateOnly? desde, DateOnly? hasta, CancellationToken ct) =>
        servicio.DanadoAsync(desde, hasta, ct);

    [HttpGet("vacas")]
    public Task<ReporteVacas> Vacas(DateOnly? desde, DateOnly? hasta, CancellationToken ct) =>
        servicio.VacasAsync(desde, hasta, ct);
}
