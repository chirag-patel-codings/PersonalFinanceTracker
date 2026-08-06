
using Dapper;
using Microsoft.AspNetCore.Authentication.Cookies;
using MySql.Data.MySqlClient;
using PersonalFinanceTracker.Contracts;
using PersonalFinanceTracker.Models.Authentication;
using PersonalFinanceTracker.Services.Communications;
using PersonalFinanceTracker.Services.Contracts;
using PersonalFinanceTracker.Services.Repository;
using PersonalFinanceTracker.Services.Handler;
using Serilog;
using System.Data;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog to write Errors to a local file
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Error() // Only track Errors and Critical crashes
    .WriteTo.File(
        path: "Logs/app-errors-.txt",
        rollingInterval: RollingInterval.Day, // Creates a new file every day (app-errors-20260618.txt)
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
    )
    .CreateLogger();

// Tell ASP.NET Core to use Serilog for logging
builder.Host.UseSerilog();

// Custom code END

// Register Dapper type handler
SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());

// Add services to the container.
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.NumberHandling = JsonNumberHandling.WriteAsString;
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;    // Converts the Property Names of the members to "CamelCase"
        // options.JsonSerializerOptions.PropertyNamingPolicy = null;   // Does No Conversion to Property Names
    });
builder.Services.AddHttpContextAccessor();   // To Get httpContext in custom classes other than Controller

// Custom Code Starts -- Here...


// DATABASE CONNECTION AND 'INTERFACE' DEPENDANCY IS ADDED TO THE 'Services' HERE...
/*
builder.Services.AddScoped<IDbConnection>((s) =>
{
    IDbConnection conn = new MySqlConnection(builder.Configuration.GetConnectionString("pft_con_str"));
    conn.Open();
    return conn;
});
*/

// DATABASE CONNECTION AND 'INTERFACE' DEPENDANCY IS ADDED TO THE 'Services' HERE...
builder.Services.AddScoped<IDbConnectionFactory, DbConnectionFactory>();

builder.Services.AddTransient<IUserRegistrationRepository, UserRegistrationRepository>();
builder.Services.AddTransient<IUserRepository, UserRepository>();
builder.Services.AddScoped<ISessionUser, SessionUser>();
builder.Services.AddTransient<IEmailService, EmailService>();
builder.Services.AddTransient<ICategoryRepository, CategoryRepository>();
builder.Services.AddTransient<ITagRepository, TagRepository>();
builder.Services.AddTransient<IAccountRepository, AccountRepository>();
builder.Services.AddTransient<IBudgetDetailsRepository, BudgetDetailsRepository>();
builder.Services.AddTransient<IBudgetRepository, BudgetRepository>();
builder.Services.AddTransient<IGoalRepository, GoalRepository>();

// Custom Code ENDS -- Here...

// Adding Authenication/Authorization Cookies (for log-in functionality)!!!
builder.Services.AddAuthentication();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/auth/login";
        options.AccessDeniedPath = "/auth/accessdenied";
    });
builder.Services.AddAuthorization();    // required for ClaimsPrincipal authentication!

var app = builder.Build();

// Custom code to set Error Handler in Global Scope
// catch all crashes globally and send users to /Error
app.UseExceptionHandler("/Error");

// Catch 404 (Not Found) or 403 (Forbidden) errors and send them to the same controller
app.UseStatusCodePagesWithReExecute("/Error/{0}");

app.UseHsts();


// Custom code END


app.UseHttpsRedirection();

app.UseRouting();

// Required for Authentication and Authorization!!!
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();


app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

/*
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}")
    .WithStaticAssets(); 

 */


app.Run();
