using Microsoft.EntityFrameworkCore;
using Coco_Beach.Models;
using QuestPDF.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Configurar licencia de QuestPDF
QuestPDF.Settings.License = LicenseType.Community;

// Add services to the container.
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<Coco_Beach.Servicios.LayoutInjectorAttribute>();
});

// Configuración de sesión para el login
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8); // 8 horas de sesión
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = ".CocoBeach.Session";
});

// ?? NUEVO: Registrar IHttpContextAccessor para la auditoría
builder.Services.AddHttpContextAccessor();

// Configura DbContext
builder.Services.AddDbContext<Coco_BeachDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Coco_BeachConnection")));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

// Importante: UseSession debe ir después de UseRouting y antes de UseAuthorization
app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Login}/{action=Autenticar}/{id?}");

app.Run();