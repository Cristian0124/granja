namespace Granja.Domain;

public enum TipoProducto { Queso, Yogurt }

public sealed class Granja
{
    public int Id { get; init; } = 1;
    public int NumeroVacas { get; set; }
    public decimal LitrosPorVaca { get; set; }
}

public sealed class ParametroProducto
{
    public TipoProducto Tipo { get; init; }
    public decimal LitrosPorUnidad { get; set; }
    public int VidaUtilDias { get; set; }
    public decimal Precio { get; set; }
}

public sealed record ProduccionLeche(int VacaId, DateOnly Fecha, decimal Litros);

public sealed class LoteProducto
{
    private LoteProducto() { }

    public LoteProducto(ParametroProducto parametro, DateOnly fecha, decimal litros)
    {
        Tipo = parametro.Tipo;
        FechaProduccion = fecha;
        FechaVencimiento = fecha.AddDays(parametro.VidaUtilDias);
        LitrosUsados = litros;
        CantidadInicial = litros / parametro.LitrosPorUnidad;
        if (CantidadInicial <= 0)
            throw new ValidacionException("La cantidad elaborada debe ser positiva.");
        CantidadDisponible = CantidadInicial;
    }

    public int Id { get; private set; }
    public TipoProducto Tipo { get; private set; }
    public DateOnly FechaProduccion { get; private set; }
    public DateOnly FechaVencimiento { get; private set; }
    public decimal LitrosUsados { get; private set; }
    public decimal CantidadInicial { get; private set; }
    public decimal CantidadDisponible { get; private set; }
    public decimal CantidadDanada { get; private set; }
    public bool EstaVencido(DateOnly fecha) => fecha > FechaVencimiento;
    public bool EsVendible(DateOnly fecha) =>
        fecha >= FechaProduccion && !EstaVencido(fecha) && CantidadDisponible > 0;

    public void Descontar(decimal cantidad, DateOnly fecha)
    {
        if (!EsVendible(fecha) || cantidad <= 0 || cantidad > CantidadDisponible)
            throw new InventarioInsuficienteException();
        CantidadDisponible -= cantidad;
    }

    public void ProcesarVencimiento(DateOnly fecha)
    {
        if (!EstaVencido(fecha))
            return;
        CantidadDanada += CantidadDisponible;
        CantidadDisponible = 0;
    }
}

public sealed class Venta
{
    private Venta() { }
    public Venta(DateOnly fecha, TipoProducto tipoProducto, decimal precioUnitario)
    {
        Fecha = fecha;
        TipoProducto = tipoProducto;
        PrecioUnitario = precioUnitario;
    }

    public int Id { get; private set; }
    public DateOnly Fecha { get; private set; }
    public TipoProducto TipoProducto { get; private set; }
    public decimal PrecioUnitario { get; private set; }
    public decimal Cantidad { get; private set; }
    public decimal Total { get; private set; }
    public List<VentaDetalle> Detalle { get; } = [];

    public void Despachar(LoteProducto lote, decimal cantidad)
    {
        lote.Descontar(cantidad, Fecha);
        Detalle.Add(new VentaDetalle(lote.Id, lote.FechaProduccion, cantidad));
        Cantidad += cantidad;
        Total = Cantidad * PrecioUnitario;
    }
}

public sealed record VentaDetalle(int LoteId, DateOnly FechaProduccion, decimal Cantidad)
{
    public int Id { get; private set; }
}

public class ValidacionException(string mensaje) : Exception(mensaje);
public class RecursoNoEncontradoException(string mensaje) : Exception(mensaje);
public class ConflictoException(string mensaje) : Exception(mensaje);
public sealed class InventarioInsuficienteException()
    : ConflictoException("No hay existencias vigentes suficientes.");
public sealed class LecheInsuficienteException()
    : ConflictoException("La cantidad supera la leche disponible de esa fecha.");
