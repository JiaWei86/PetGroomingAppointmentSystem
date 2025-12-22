using Microsoft.EntityFrameworkCore;
using PetGroomingAppointmentSystem.Hubs;
using PetGroomingAppointmentSystem.Models;
using PetGroomingAppointmentSystem.Services;

var builder = WebApplication.CreateBuilder(args);

// =================== Services ===================

// Database setup
builder.Services.AddSqlServer<DB>($@"
    Data Source=(LocalDB)\MSSQLLocalDB;
    AttachDbFilename={builder.Environment.ContentRootPath}\DB.mdf;
");

// AWS S3 Storage Service
builder.Services.Configure<AWSSettings>(builder.Configuration.GetSection("AWS"));
builder.Services.AddSingleton<IS3StorageService, S3StorageService>();

// ========== Admin Area Services ==========
builder.Services.AddScoped<PetGroomingAppointmentSystem.Areas.Admin.Services.IEmailService,
                           PetGroomingAppointmentSystem.Areas.Admin.Services.EmailService>();
builder.Services.AddScoped<PetGroomingAppointmentSystem.Areas.Admin.Services.IPhoneService,
                           PetGroomingAppointmentSystem.Areas.Admin.Services.PhoneService>();
builder.Services.AddScoped<PetGroomingAppointmentSystem.Areas.Admin.Services.IValidationService,
                           PetGroomingAppointmentSystem.Areas.Admin.Services.ValidationService>();

// ========== Customer Area Services ==========
builder.Services.AddScoped<PetGroomingAppointmentSystem.Services.IEmailService,
                           PetGroomingAppointmentSystem.Services.EmailService>();

// ========== Shared Services (Admin & Customer) ==========
builder.Services.AddScoped<PetGroomingAppointmentSystem.Services.IPhoneService, PetGroomingAppointmentSystem.Services.PhoneService>();
builder.Services.AddScoped<PetGroomingAppointmentSystem.Services.IValidationService, PetGroomingAppointmentSystem.Services.ValidationService>();
builder.Services.AddScoped<IPasswordService, PasswordService>();

// Chatbot Service
builder.Services.AddScoped<IChatbotService, ChatbotService>();
builder.Services.AddHttpClient<ChatbotService>();

// Recaptcha Service
builder.Services.AddHttpClient<IRecaptchaService, RecaptchaService>();

// SignalR
builder.Services.AddSignalR();

// MVC + Razor Pages
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// Session support
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
});

var app = builder.Build();

// =================== Middleware ===================

// Error & HSTS
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// HTTPS & static files
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseSession();

// Admin protection middleware
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

// =================== Routing ===================

// Area routes
app.MapControllerRoute(
    name: "customer",
    pattern: "{area=Customer}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "admin",
    pattern: "{area=Admin}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "staff",
    pattern: "{area=Staff}/{controller=Home}/{action=Index}/{id?}");

// Default route & API controllers
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllers();
app.MapRazorPages();

// SignalR hub
app.MapHub<ChatHub>("/chatHub");

app.Run();
