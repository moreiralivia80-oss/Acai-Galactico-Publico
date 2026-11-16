using acaigalatico.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using System.Security.Cryptography;
using System.Text;
using acaigalatico.Domain.Interfaces;
using acaigalatico.Infrastructure.Repositories;
using acaigalatico.Application.Interfaces;
using acaigalatico.Application.Services;
using System.Globalization;
using Microsoft.AspNetCore.Localization;
using System.Text.Json.Serialization;

AppContext.SetSwitch("System.Net.Quic.Enabled", false);
AppContext.SetSwitch("System.Net.SocketsHttpHandler.Http3Support", false);

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.WriteIndented = true;
    });

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApiDocument(config =>
{
    config.DocumentName = "v1";
    config.Title = "Acai Galatico API";
    config.Version = "v1";
});
builder.Services.AddOpenApi();

// Força Kestrel a usar apenas HTTP/1.1 e HTTP/2 (desabilita HTTP/3/MsQuic)
builder.WebHost.ConfigureKestrel(options =>
{
    options.ConfigureEndpointDefaults(o =>
        o.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2);
});

// --- INICIO DA CONFIGURAÇÃO DO BANCO ---

// 1. Pega a string de conex�o do appsettings.json
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// 2. Configura o AppDbContext para usar SQLite (Igual ao Web)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString, b => b.MigrationsAssembly("acaigalatico.Infrastructure")));

// 3. Configura o Identity (Sem UI para API)
builder.Services.AddIdentityCore<IdentityUser>(options => {
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequiredLength = 8;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<AppDbContext>();
// ^ Essa parte b.MigrationsAssembly é essencial porque o Context está em outro projeto!


// --- FIM DA CONFIGURA��O DO BANCO ---

// Registrar o servio de Seed
builder.Services.AddScoped<acaigalatico.Infrastructure.SeedingService>();

// Registrar Repositorios e Services
builder.Services.AddScoped<IProdutoRepository, ProdutoRepository>();
builder.Services.AddScoped<IProdutoService, ProdutoService>();
builder.Services.AddScoped<ICategoriaRepository, CategoriaRepository>();
builder.Services.AddScoped<ICategoriaService, CategoriaService>();
builder.Services.AddScoped<IClienteRepository, ClienteRepository>();
builder.Services.AddScoped<IClienteService, ClienteService>();
builder.Services.AddScoped<IVendaRepository, VendaRepository>();
builder.Services.AddScoped<IVendaService, VendaService>();

var app = builder.Build();

// --- CONFIGURAÇÃO DE CULTURA (PT-BR) ---
var defaultCulture = new CultureInfo("pt-BR");
var localizationOptions = new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(defaultCulture),
    SupportedCultures = new List<CultureInfo> { defaultCulture },
    SupportedUICultures = new List<CultureInfo> { defaultCulture }
};
app.UseRequestLocalization(localizationOptions);
// ---------------------------------------

// --- APLICA MIGRAÇÕES AUTOMATICAMENTE ---
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<acaigalatico.Infrastructure.Context.AppDbContext>();
    db.Database.EnsureCreated();
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "Falha ao sincronizar o banco de dados.");
}
// ----------------------------------------

// --- BLOCO DE INICIALIZAÇÃO DE DADOS ---
if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        try
        {
            var seedingService = scope.ServiceProvider.GetRequiredService<acaigalatico.Infrastructure.SeedingService>();
            // A senha do administrador inicial vem da configuração (user-secrets ou variável de ambiente)
            await seedingService.SeedAsync(app.Configuration["AdminSeed:Password"]);
        }
        catch (Exception ex)
        {
            // Log do erro mas deixa a aplicação subir
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "Erro ao popular o banco de dados na inicialização.");
        }
    }
}
// ---------------------------------------

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseOpenApi(options => 
    {
        options.Path = "/swagger/{documentName}/swagger.json";
    });
    app.UseSwaggerUi(config =>
    {
        config.Path = "/swagger";
        config.DocumentPath = "/swagger/v1/swagger.json";
    });

    // Redireciona a raiz (/) para o Swagger (/swagger) automaticamente
    app.MapGet("/", () => Results.Redirect("/swagger"));
}
// Redirecionamento para HTTPS desabilitado até configurar certificado/endpoint HTTPS
// app.UseHttpsRedirection();

// --- PROTEÇÃO DA API POR CHAVE ---
// Todas as rotas /api exigem o cabeçalho X-Api-Key com o valor configurado em Api:Key
// (user-secrets ou variável de ambiente). O cliente desktop envia essa chave em cada requisição.
var chaveApi = app.Configuration["Api:Key"];
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        if (string.IsNullOrWhiteSpace(chaveApi))
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsync("A chave da API (Api:Key) não está configurada no servidor.");
            return;
        }

        var chaveRecebida = context.Request.Headers["X-Api-Key"].ToString();
        var chaveValida = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(chaveRecebida), Encoding.UTF8.GetBytes(chaveApi));

        if (!chaveValida)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Chave da API ausente ou inválida.");
            return;
        }
    }

    await next();
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
