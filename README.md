# Granja lechera

API REST en C# y .NET 10 para gestionar producción por vaca, lotes, ventas FIFO y reportes.

## Ejecutar en Visual Studio

1. Instala Visual Studio 2026 con la carga de trabajo **Desarrollo de ASP.NET y web** y el SDK de .NET 10.
2. Abre `Granja.sln` y selecciona `Granja.Web` como proyecto de inicio.
3. Selecciona el perfil **http** y pulsa **F5**. Se abre Swagger en `http://localhost:5056/swagger`.

Desde una terminal en el directorio de la solución:

```sh
dotnet restore
dotnet run --project Granja.Web --launch-profile http
```

Al iniciar, EF Core aplica la migración incluida y crea `granja.db` en el directorio de trabajo de la API. La configuración inicial se puede editar con `PUT /api/granja`. La conexión se configura con `ConnectionStrings__Granja`, por ejemplo `Data Source=prueba.db`. Si cuentas con una clave de AutoMapper, configúrala en `AutoMapper__LicenseKey`.

## Verificar el taller

- Abre **Explorador de pruebas > Ejecutar todas las pruebas** en Visual Studio, o ejecuta `dotnet test`. Las pruebas usan SQLite en memoria y aplican la migración real.
- Abre `Escenario.http` en Visual Studio y ejecuta las solicitudes en orden sobre una base nueva. También puedes reproducirlas en Swagger.
- El reporte del escenario debe mostrar queso (kg) con 25 producidos, 12 vendidos y 13 disponibles. Para yogurt (L), debe mostrar 50 producidos, 20 vendidos y 30 dañados. La última venta de yogurt responde con HTTP 409.
- Para probar el reporte de vacas con mediciones distintas, cambia el número configurado a 3 y registra otra fecha con `{"mediciones":[{"vacaId":1,"litros":12},{"vacaId":2,"litros":15},{"vacaId":3,"litros":18}]}`. Filtra el reporte por esa fecha. El promedio es 15 L y la vaca 1 queda por debajo.

## Decisiones del modelo

- La granja utiliza identificadores de vacas del 1 al número configurado. El registro diario estándar aplica el valor configurado a cada vaca. Si se envían mediciones, se exige una por vaca activa. El historial conserva los identificadores y las mediciones registrados.
- La fecha explícita permite simular la operación. Si se omite, se toma el día de Colombia. El inventario muestra los saldos actuales de los lotes y usa la fecha consultada para clasificar su vigencia.
- El día del vencimiento todavía se puede vender. El procesamiento posterior mueve a dañados solo el saldo que quede. El reporte de producto total usa el saldo registrado tras procesar los vencimientos. Para simular una secuencia, ejecuta sus operaciones en orden cronológico.
- Services depende de las interfaces de Domain. Infrastructure implementa los repositorios y Web registra las dependencias. Los controladores exponen DTO y los errores de negocio se devuelven con ProblemDetails.
- Una venta usa una transacción de SQLite desde la consulta de existencias hasta que se registra el precio, el total, los lotes despachados y sus saldos. Para lotes con la misma fecha, se despacha primero el de menor identificador.
