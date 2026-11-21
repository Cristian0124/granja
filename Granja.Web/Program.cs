using System.Text.Json.Serialization;
using Granja.Domain;
using Granja.Infrastructure;
using Granja.Services;
using Granja.Web;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<GranjaDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Granja") ?? "Data Source=granja.db"));
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddScoped<GranjaService>();
builder.Services.AddScoped<ProduccionService>();
builder.Services.AddScoped<InventarioService>();
builder.Services.AddScoped<VentaService>();
builder.Services.AddScoped<ReporteService>();
builder.Services.AddAutoMapper(cfg =>
{
    var clave = builder.Configuration["AutoMapper:LicenseKey"];
    if (!string.IsNullOrWhiteSpace(clave))
        cfg.LicenseKey = clave;
}, typeof(GranjaProfile));
builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddSwaggerGen();

var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSwagger();
app.UseSwaggerUI();
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
app.MapControllers();

await using (var scope = app.Services.CreateAsyncScope())
    await scope.ServiceProvider.GetRequiredService<GranjaDbContext>().Database.MigrateAsync();

await app.RunAsync();
