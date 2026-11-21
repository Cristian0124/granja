using AutoMapper;
using Granja.Domain;

namespace Granja.Services;

public sealed record ParametroDto(TipoProducto Tipo, decimal LitrosPorUnidad, int VidaUtilDias, decimal Precio);
public sealed record GranjaDto(int NumeroVacas, decimal LitrosPorVaca, List<ParametroDto> Productos);
public sealed record MedicionVaca(int VacaId, decimal Litros);
public sealed record ProduccionRequest(DateOnly? Fecha, List<MedicionVaca>? Mediciones);
public sealed record ProduccionResponse(DateOnly Fecha, decimal LitrosProducidos, List<MedicionVaca> Mediciones);
public sealed record TransformacionRequest(TipoProducto? TipoProducto, decimal Litros, DateOnly? Fecha);
public sealed record VentaRequest(TipoProducto? TipoProducto, decimal Cantidad, DateOnly? Fecha);
public sealed record FechaRequest(DateOnly? Fecha);
public sealed record LoteDto(int Id, TipoProducto Tipo, DateOnly FechaProduccion,
    DateOnly FechaVencimiento, decimal LitrosUsados, decimal CantidadInicial,
    decimal CantidadDisponible, decimal CantidadDanada);
public sealed record InventarioFila(LoteDto Lote, string Estado);
public sealed record VentaDetalleDto(int LoteId, DateOnly FechaProduccion, decimal Cantidad);
public sealed record VentaResponse(int Id, DateOnly Fecha, TipoProducto TipoProducto,
    decimal Cantidad, decimal PrecioUnitario, decimal Total, List<VentaDetalleDto> Detalle);
public sealed record ReporteVentas(List<VentaResponse> Ventas, decimal TotalGeneral);
public sealed record LecheDiaria(DateOnly Fecha, decimal LitrosProducidos, decimal LitrosTransformados, decimal LitrosNoAprovechados);
public sealed record ProductoDiario(DateOnly Fecha, TipoProducto Tipo, decimal Producido);
public sealed record ProductoTotal(TipoProducto Tipo, decimal Producido, decimal Vendido, decimal Disponible, decimal Danado);
public sealed record PromedioVaca(int VacaId, decimal Promedio, decimal TotalLitros, int DiasRegistrados);
public sealed record ReporteVacas(decimal? PromedioGeneral, PromedioVaca[] Vacas,
    PromedioVaca[] MayorProduccion, PromedioVaca[] MenorProduccion, PromedioVaca[] DebajoDelPromedio);

public sealed class GranjaProfile : Profile
{
    public GranjaProfile()
    {
        CreateMap<ParametroProducto, ParametroDto>();
        CreateMap<LoteProducto, LoteDto>();
        CreateMap<VentaDetalle, VentaDetalleDto>();
        CreateMap<Venta, VentaResponse>().ForCtorParam(nameof(VentaResponse.Detalle),
            opt => opt.MapFrom(v => v.Detalle.OrderBy(d => d.FechaProduccion).ThenBy(d => d.LoteId)));
    }
}
