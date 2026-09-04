using System.Globalization;
using GestionCabanas.Data;
using GestionCabanas.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;

var culturaArgentina = new CultureInfo("es-AR");

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddScoped<IPasswordHasher<AdminUsuario>, PasswordHasher<AdminUsuario>>();
builder.Services.AddScoped<GestionCabanas.Services.DisponibilidadService>();
builder.Services.AddScoped<GestionCabanas.Services.INotificacionEmailService, GestionCabanas.Services.EmailNotificacionService>();
builder.Services.AddDataProtection();
builder.Services.AddHttpClient<GestionCabanas.Services.GraphOneDriveService>();
builder.Services.AddScoped<GestionCabanas.Services.ExcelReservasSyncService>();
builder.Services.AddHostedService<GestionCabanas.Services.SincronizacionAutomaticaService>();

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
