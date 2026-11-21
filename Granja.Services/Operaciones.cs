using AutoMapper;
using Granja.Domain;

namespace Granja.Services;

public sealed class GranjaService(IRepository<Domain.Granja> granjas, IRepository<ParametroProducto> productos,
    IUnitOfWork unidad, IMapper mapper)
{
    public async Task<GranjaDto> ConsultarAsync(CancellationToken ct)
    {
        var granja = (await granjas.ListarAsync(ct)).Single();
        var parametros = (await productos.ListarAsync(ct)).OrderBy(p => p.Tipo);
        return new GranjaDto(granja.NumeroVacas, granja.LitrosPorVaca, mapper.Map<List<ParametroDto>>(parametros));
    }

    public Task<GranjaDto> ActualizarAsync(GranjaDto dto, CancellationToken ct)
    {
        if (dto.NumeroVacas <= 0 || dto.LitrosPorVaca <= 0 || dto.Productos is null ||
            !dto.Productos.Select(p => p?.Tipo).Order().SequenceEqual(
                Enum.GetValues<TipoProducto>().Select(t => (TipoProducto?)t)) ||
            dto.Productos.Any(p => p.LitrosPorUnidad <= 0 || p.VidaUtilDias <= 0 || p.Precio <= 0))
            throw new ValidacionException("Indique valores positivos para la granja y los parámetros de ambos productos.");

        return unidad.EjecutarAsync(async () =>
        {
            var granja = (await granjas.ListarAsync(ct)).Single();
            granja.NumeroVacas = dto.NumeroVacas;
            granja.LitrosPorVaca = dto.LitrosPorVaca;
            foreach (var parametro in await productos.ListarAsync(ct))
            {
                var cambio = dto.Productos.Single(p => p.Tipo == parametro.Tipo);
                parametro.LitrosPorUnidad = cambio.LitrosPorUnidad;
                parametro.VidaUtilDias = cambio.VidaUtilDias;
                parametro.Precio = cambio.Precio;
            }
            await unidad.GuardarAsync(ct);
            return await ConsultarAsync(ct);
        }, ct);
    }
}

public sealed class ProduccionService(IRepository<Domain.Granja> granjas, IRepository<ProduccionLeche> producciones,
    IRepository<LoteProducto> lotes, IRepository<ParametroProducto> productos, IUnitOfWork unidad, IClock reloj, IMapper mapper)
{
    public Task<ProduccionResponse> RegistrarAsync(ProduccionRequest dto, CancellationToken ct)
    {
        var fecha = dto.Fecha ?? reloj.Hoy;
        return unidad.EjecutarAsync(async () =>
        {
            if ((await producciones.ListarAsync(ct)).Any(p => p.Fecha == fecha))
                throw new ConflictoException("La producción de esa fecha ya está registrada.");

            var granja = (await granjas.ListarAsync(ct)).Single();
            var identificadores = Enumerable.Range(1, granja.NumeroVacas);
            var mediciones = dto.Mediciones ?? identificadores.Select(id => new MedicionVaca(id, granja.LitrosPorVaca)).ToList();
            if (mediciones.Any(m => m is null || m.Litros < 0) ||
                !mediciones.Select(m => m.VacaId).Order().SequenceEqual(identificadores))
                throw new ValidacionException("Indique una medición no negativa para cada vaca activa.");
            foreach (var medicion in mediciones)
                producciones.Agregar(new ProduccionLeche(medicion.VacaId, fecha, medicion.Litros));
            await unidad.GuardarAsync(ct);
            return new ProduccionResponse(fecha, mediciones.Sum(m => m.Litros), mediciones);
        }, ct);
    }

    public Task<LoteDto> TransformarAsync(TransformacionRequest dto, CancellationToken ct)
    {
        var fecha = dto.Fecha ?? reloj.Hoy;
        var tipo = Validacion.Tipo(dto.TipoProducto);
        Validacion.Positivo(dto.Litros);
        return unidad.EjecutarAsync(async () =>
        {
            var delDia = (await producciones.ListarAsync(ct)).Where(p => p.Fecha == fecha).ToList();
            if (delDia.Count == 0)
                throw new RecursoNoEncontradoException("No existe producción de leche para esa fecha.");
            var usados = (await lotes.ListarAsync(ct)).Where(l => l.FechaProduccion == fecha).Sum(l => l.LitrosUsados);
            if (dto.Litros > delDia.Sum(p => p.Litros) - usados)
                throw new LecheInsuficienteException();

            var parametro = (await productos.ListarAsync(ct)).Single(p => p.Tipo == tipo);
            if (parametro.VidaUtilDias > DateOnly.MaxValue.DayNumber - fecha.DayNumber)
                throw new ValidacionException("El vencimiento supera el rango de fechas permitido.");
            var lote = new LoteProducto(parametro, fecha, dto.Litros);
            lotes.Agregar(lote);
            await unidad.GuardarAsync(ct);
            return mapper.Map<LoteDto>(lote);
        }, ct);
    }
}

public sealed class VentaService(IRepository<LoteProducto> lotes, IRepository<Venta> ventas,
    IRepository<ParametroProducto> productos, IUnitOfWork unidad, IClock reloj, IMapper mapper)
{
    public Task<VentaResponse> RegistrarVentaAsync(VentaRequest dto, CancellationToken ct)
    {
        var fecha = dto.Fecha ?? reloj.Hoy;
        var tipo = Validacion.Tipo(dto.TipoProducto);
        Validacion.Positivo(dto.Cantidad);
        return unidad.EjecutarAsync(async () =>
        {
            var vendibles = (await lotes.ListarAsync(ct)).Where(l => l.Tipo == tipo && l.EsVendible(fecha))
                .OrderBy(l => l.FechaProduccion).ThenBy(l => l.Id).ToList();
            if (vendibles.Sum(l => l.CantidadDisponible) < dto.Cantidad)
                throw new InventarioInsuficienteException();

            var precio = (await productos.ListarAsync(ct)).Single(p => p.Tipo == tipo).Precio;
            if (precio > 1 && dto.Cantidad > decimal.MaxValue / precio)
                throw new ValidacionException("El total supera la capacidad numérica del sistema.");
            var venta = new Venta(fecha, tipo, precio);
            var restante = dto.Cantidad;
            foreach (var lote in vendibles)
            {
                if (restante == 0)
                    break;
                var cantidad = Math.Min(restante, lote.CantidadDisponible);
                venta.Despachar(lote, cantidad);
                restante -= cantidad;
            }
            ventas.Agregar(venta);
            await unidad.GuardarAsync(ct);
            return mapper.Map<VentaResponse>(venta);
        }, ct);
    }
}

public sealed class InventarioService(IRepository<LoteProducto> lotes, IUnitOfWork unidad, IClock reloj, IMapper mapper)
{
    public async Task<List<InventarioFila>> ConsultarAsync(DateOnly? fecha, TipoProducto? tipo, CancellationToken ct)
    {
        var dia = fecha ?? reloj.Hoy;
        if (tipo.HasValue) Validacion.Tipo(tipo);
        return (await lotes.ListarAsync(ct))
            .Where(l => l.FechaProduccion <= dia && (tipo is null || l.Tipo == tipo))
            .OrderBy(l => l.FechaProduccion).ThenBy(l => l.Id)
            .Select(l => new InventarioFila(mapper.Map<LoteDto>(l), Estado(l, dia))).ToList();
    }

    private static string Estado(LoteProducto lote, DateOnly fecha)
    {
        if (lote.CantidadDanada > 0) return "Danado";
        if (lote.CantidadDisponible == 0) return "Agotado";
        return lote.EstaVencido(fecha) ? "Vencido" : "Disponible";
    }

    public Task<List<LoteDto>> ProcesarAsync(DateOnly? fecha, CancellationToken ct)
    {
        var dia = fecha ?? reloj.Hoy;
        return unidad.EjecutarAsync(async () =>
        {
            var vencidos = (await lotes.ListarAsync(ct)).Where(l => l.EstaVencido(dia) && l.CantidadDisponible > 0).ToList();
            foreach (var lote in vencidos)
                lote.ProcesarVencimiento(dia);
            await unidad.GuardarAsync(ct);
            return mapper.Map<List<LoteDto>>(vencidos);
        }, ct);
    }
}

internal static class Validacion
{
    public static TipoProducto Tipo(TipoProducto? tipo) => tipo is { } valor && Enum.IsDefined(valor)
        ? valor : throw new ValidacionException("Indique Queso o Yogurt.");

    public static void Positivo(decimal cantidad)
    {
        if (cantidad <= 0)
            throw new ValidacionException("La cantidad debe ser mayor que cero.");
    }
}
