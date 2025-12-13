using PetGroomingAppointmentSystem.Services;
using PetGroomingAppointmentSystem.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// -----------------------------
// Register DB (LocalDB .mdf file-based) 
// -----------------------------
builder.Services.AddSqlServer<DB>($@"
    Data Source=(LocalDB)\MSSQLLocalDB;
    AttachDbFilename={builder.Environment.ContentRootPath}\DB.mdf;
    Database=PetGroomingSystem;
    Trusted_Connection=True;
    MultipleActiveResultSets=true;
");

// Add Email Service
builder.Services.AddScoped<IEmailService, EmailService>();

// Add EmailHelper Service ← ADD THIS LINE
builder.Services.AddScoped<EmailHelper>();

// Add Chatbot Service
builder.Services.AddScoped<IChatbotService, ChatbotService>();
builder.Services.AddHttpClient<ChatbotService>();

// Add session support
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
});

// Add MVC (controllers + views) and Razor Pages
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

var app = builder.Build();

// -----------------------------
// HTTP pipeline
// -----------------------------
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();

// Admin protection middleware (checks session UserRole set at login)
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value?.ToLower() ?? "";
    if (path.StartsWith("/admin") && !path.StartsWith("/admin/auth/login") && !path.StartsWith("/admin/auth/logout"))
    {
        var role = context.Session.GetString("UserRole");
        if (string.IsNullOrEmpty(role) || (role.ToLower() != "admin" && role.ToLower() != "staff"))
        {
            context.Response.Redirect("/Admin/Auth/Login");
            return;
        }
    }
    await next();
});

app.UseAuthorization();

// Area routes (Customer / Admin / Staff)
app.MapControllerRoute(
    name: "customer",
    pattern: "{area=Customer}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "admin",
    pattern: "{area=Admin}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "staff",
    pattern: "{area=Staff}/{controller=Home}/{action=Index}/{id?}");

// Map API controllers
app.MapControllers();

// Default fallback route
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();
app.Run();