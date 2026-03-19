using Microsoft.EntityFrameworkCore;
using Coco_Beach.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Configuración de sesión para el login
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8); // 8 horas de sesión
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = ".CocoBeach.Session";
});

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
