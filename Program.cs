using System.Globalization;
using Azure.Storage.Blobs;
using GestionCabanas.Data;
using GestionCabanas.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

var culturaArgentina = new CultureInfo("es-AR");

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

var cadenaConexion = builder.Configuration.GetConnectionString("Default");
var carpetaBaseDatos = Path.GetDirectoryName(new SqliteConnectionStringBuilder(cadenaConexion).DataSource);
if (!string.IsNullOrEmpty(carpetaBaseDatos))
{
    Directory.CreateDirectory(carpetaBaseDatos);
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(cadenaConexion));

builder.Services.AddScoped<IPasswordHasher<AdminUsuario>, PasswordHasher<AdminUsuario>>();
builder.Services.AddScoped<GestionCabanas.Services.DisponibilidadService>();
builder.Services.AddSingleton<GestionCabanas.Services.AlmacenamientoFotosService>();
builder.Services.AddScoped<GestionCabanas.Services.INotificacionEmailService, GestionCabanas.Services.EmailNotificacionService>();

// El disco del contenedor es efímero: sin esto, cada reinicio (incluido el scale-to-zero
// de Container Apps) invalida las cookies de sesión y los tokens de OneDrive guardados
// cifrados en la base. Se persisten las claves en el mismo Storage Account de las fotos.
var cadenaConexionBlobs = builder.Configuration["BlobFotos:ConnStr"];
if (!string.IsNullOrWhiteSpace(cadenaConexionBlobs))
{
    var contenedorClaves = new BlobContainerClient(cadenaConexionBlobs, "dataprotection-keys");
    contenedorClaves.CreateIfNotExists();
    builder.Services.AddDataProtection()
        .PersistKeysToAzureBlobStorage(contenedorClaves.GetBlobClient("keys.xml"));
}
else
{
    builder.Services.AddDataProtection();
}

builder.Services.AddHttpClient<GestionCabanas.Services.GraphOneDriveService>();
builder.Services.AddScoped<GestionCabanas.Services.ExcelReservasSyncService>();
builder.Services.AddScoped<GestionCabanas.Services.ExcelEscrituraService>();
builder.Services.AddHostedService<GestionCabanas.Services.SincronizacionAutomaticaService>();

// El límite por defecto (~28,6 MB) no alcanza para subir varias fotos de celular de una sola vez.
builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = 209_715_200; // 200 MB
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Admin/Account/Login";
        options.AccessDeniedPath = "/Admin/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
    });

var app = builder.Build();

app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(culturaArgentina),
    SupportedCultures = new[] { culturaArgentina },
    SupportedUICultures = new[] { culturaArgentina }
});

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("SeedData");
    SeedData.Inicializar(db, scope.ServiceProvider.GetRequiredService<IPasswordHasher<AdminUsuario>>(), builder.Configuration, logger);
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
