using BookingManagement.Repositories.Extensions;
using BookingManagement.Services.Extensions;
using BookingManagement.Services.Interfaces;
using BookingManagement.Services.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.FileProviders;
using BookingManagement.User.Razor.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

// Đăng ký Repository service
builder.Services.AddRepositories(builder.Configuration);

// Đăng ký các services (sử dụng extension method)
builder.Services.AddApplicationServices();

// Đăng ký SignalR
builder.Services.AddSignalR();

// Đăng ký IHubContext cho SignalRService
builder.Services.AddTransient<BookingManagement.Services.Services.DummyHub>();
// Cấu hình Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.HttpOnly = true;
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.LoginPath = "/Login";
        options.LogoutPath = "/Logout";
        options.AccessDeniedPath = "/AccessDenied";
        options.SlidingExpiration = true;
    });

// Cấu hình Authorization
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("UserOnly", policy => policy.RequireRole("User"));
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// Cấu hình để phục vụ file từ thư mục chia sẻ
var sharedImagesFolderPath = builder.Configuration["SharedImagesFolderPath"] ?? "SharedAssets\\images";
var sharedImagesPath = Path.Combine(Directory.GetCurrentDirectory(), "..", sharedImagesFolderPath);
if (Directory.Exists(sharedImagesPath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(sharedImagesPath),
        RequestPath = "/shared-images"
    });
}
else
{
    // Tạo thư mục nếu chưa tồn tại
    Directory.CreateDirectory(sharedImagesPath);
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(sharedImagesPath),
        RequestPath = "/shared-images"
    });
}

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

// Map SignalR hub
app.MapHub<NotificationHub>("/notificationHub");
// Thêm tương thích với hub của admin
app.MapHub<NotificationHub>("/bookingHub");

Console.WriteLine("SignalR hubs mapped: /notificationHub and /bookingHub");

app.Run();
