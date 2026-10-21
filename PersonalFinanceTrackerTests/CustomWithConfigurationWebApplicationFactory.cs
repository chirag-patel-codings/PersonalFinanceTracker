using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace PersonalFinanceTrackerTests
{
    public class TestAuthHandlerOptions : AuthenticationSchemeOptions
    {
        public string UserId { get; set; } = string.Empty;
    }

    // server-side configuration running inside the in-memory test server host.
    // Simulates how production server would handle authentication, middleware, and dependency injection,
    // but it is isolated safely inside test project so don't have to touch production Program.cs.
    public class CustomWithConfigurationWebApplicationFactory : WebApplicationFactory<Program>
    {
        // Expose the dynamic test user ID publicly
        public string TestUserId { get; }

        public CustomWithConfigurationWebApplicationFactory(string testUserId)
        {
            TestUserId = testUserId;
        }

        // Called at _factory.CreateClient()
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");

            builder.ConfigureServices(services =>
            {
                // Register the factory instance so the handler can resolve it via DI
                services.AddSingleton(this);

                services.PostConfigure<AntiforgeryOptions>(options =>
                {
                    options.HeaderName = "Request-Verification-Token";
                    options.Cookie.Name = ".AspNetCore.Antiforgery.TestCookie";
                });

                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "TestScheme";
                    options.DefaultChallengeScheme = "TestScheme";
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("TestScheme", options => { });

                // Configure global JSON options for controllers here
                services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(options =>
                {
                    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
                });

            });
        }

        // Called when controller's action is called (To Authenticate the request)
        private class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
        {
            public TestAuthHandler(
                IOptionsMonitor<AuthenticationSchemeOptions> options,
                ILoggerFactory logger,
                UrlEncoder encoder,
                ISystemClock clock) : base(options, logger, encoder, clock)
            {
            }

            // Called when controller's action is called (To Authenticate the request)
            // Runs On server, before every single HTTP request (from test/client) ever reaches to controller's action.
            // It uses the same 'testUserId' to generate claims principle at server AND then validates it with client.
            protected override Task<AuthenticateResult> HandleAuthenticateAsync()
            {

                if (!Context.Request.Headers.ContainsKey("X-Test-Unauthorized"))
                {
                    // IF AN AUTHORIZED REQUEST
                    // Dynamically grab the factory from the request's service provider
                    var factory = Context.RequestServices.GetRequiredService<CustomWithConfigurationWebApplicationFactory>();
                    var userId = factory.TestUserId; // Your 19-digit user ID!

                    var claims = new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, userId),
                        new Claim(ClaimTypes.Name, "PFT_Test_User_123"),
                        new Claim("CurrencyCode", "USD"),
                        new Claim("CurrencySymbol", "$")
                    };

                    var identity = new ClaimsIdentity(claims, "TestScheme");
                    var principal = new ClaimsPrincipal(identity);
                    var ticket = new AuthenticationTicket(principal, "TestScheme");

                    return Task.FromResult(AuthenticateResult.Success(ticket));
                }
                else
                {
                    // IF UN-AUTHORIZED REQUEST
                    return Task.FromResult(AuthenticateResult.Fail("Simulated unauthenticated test request."));
                }
                
            }

        }
    
        

    }
}

