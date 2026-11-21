using Granja.Domain;
using Microsoft.AspNetCore.Diagnostics;

namespace Granja.Web;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, titulo) = exception switch
        {
            ValidacionException => (400, "Solicitud inválida"),
            RecursoNoEncontradoException => (404, "Recurso no encontrado"),
            ConflictoException => (409, "Conflicto"),
            _ => (500, "Error interno")
        };
        if (status == 500)
            logger.LogError(exception, "Error al procesar {Ruta}", context.Request.Path);
        await Results.Problem(statusCode: status, title: titulo,
            detail: status == 500 ? null : exception.Message).ExecuteAsync(context);
        return true;
    }
}
