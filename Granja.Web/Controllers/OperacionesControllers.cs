using Granja.Domain;
using Granja.Services;
using Microsoft.AspNetCore.Mvc;

namespace Granja.Web.Controllers;

[ApiController, Route("api/granja")]
public sealed class GranjaController(GranjaService servicio) : ControllerBase
{
    [HttpGet]
    public Task<GranjaDto> Get(CancellationToken ct) => servicio.ConsultarAsync(ct);

    [HttpPut]
    public Task<GranjaDto> Put(GranjaDto dto, CancellationToken ct) => servicio.ActualizarAsync(dto, ct);
}

[ApiController, Route("api/produccion")]
public sealed class ProduccionController(ProduccionService servicio) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ProduccionResponse>> Registrar(ProduccionRequest dto, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await servicio.RegistrarAsync(dto, ct));

    [HttpPost("transformaciones")]
    public async Task<ActionResult<LoteDto>> Transformar(TransformacionRequest dto, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await servicio.TransformarAsync(dto, ct));
}

[ApiController, Route("api/inventario")]
public sealed class InventarioController(InventarioService servicio) : ControllerBase
{
    [HttpGet]
    public Task<List<InventarioFila>> Get(DateOnly? fecha, TipoProducto? tipo, CancellationToken ct) =>
        servicio.ConsultarAsync(fecha, tipo, ct);

    [HttpPost("vencimientos")]
    public Task<List<LoteDto>> Vencimientos(FechaRequest dto, CancellationToken ct) =>
        servicio.ProcesarAsync(dto.Fecha, ct);
}

[ApiController, Route("api/ventas")]
public sealed class VentasController(VentaService servicio) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<VentaResponse>> Registrar(VentaRequest dto, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await servicio.RegistrarVentaAsync(dto, ct));
}
