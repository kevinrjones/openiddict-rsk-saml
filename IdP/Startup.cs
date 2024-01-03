using IdentityModel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenIddict.IdP.Data;
using Rsk.Saml.Configuration;
using Rsk.Saml.OpenIddict.AspNetCore.Identity.Configuration.DependencyInjection;
using Rsk.Saml.OpenIddict.Configuration.DependencyInjection;
using Rsk.Saml.OpenIddict.EntityFrameworkCore.Configuration.DependencyInjection;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace OpenIddict.IdP;

public class Startup
{
    public Startup(IConfiguration configuration)
        => Configuration = configuration;

    public IConfiguration Configuration { get; }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddControllersWithViews();
        services.AddRazorPages();
        var connectionString = Configuration.GetConnectionString("identity");
        var serverVersion = ServerVersion.AutoDetect(connectionString);
        var migrationsAssembly = typeof(Startup).Assembly.GetName().Name;

        var licensee = Configuration["Licensee"];
        var licenseKey = Configuration["LicenseKey"];


        services.AddDbContext<ApplicationDbContext>(options =>
        {
            // Configure the context to use sqlite.
            options.UseMySql(connectionString, serverVersion,
                sqlOptions => sqlOptions.MigrationsAssembly(migrationsAssembly));
            options.UseOpenIddict();
        });

        services.AddDatabaseDeveloperPageExceptionFilter();

        // Register the Identity services.
        services.AddIdentity<ApplicationUser, IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders()
            .AddDefaultUI();

        services.Configure<IdentityOptions>(options =>
        {
            options.ClaimsIdentity.UserIdClaimType = JwtClaimTypes.Subject;
            options.ClaimsIdentity.UserNameClaimType = JwtClaimTypes.Name;
            options.ClaimsIdentity.RoleClaimType = JwtClaimTypes.Role;
            options.ClaimsIdentity.EmailClaimType = JwtClaimTypes.Email;
        });

        services.AddOpenIddict()

            // Register the OpenIddict core components.
            .AddCore(options =>
            {
                // Configure OpenIddict to use the Entity Framework Core stores and models.
                // Note: call ReplaceDefaultEntities() to replace the default OpenIddict entities.
                options.UseEntityFrameworkCore()
                    .UseDbContext<ApplicationDbContext>();

                // Enable Quartz.NET integration.
                options.UseQuartz();
            })

            // Register the OpenIddict server components.
            .AddServer(options =>
            {
                // Enable the authorization, logout, token and userinfo endpoints.
                options.SetAuthorizationEndpointUris("connect/authorize")
                    .SetLogoutEndpointUris("connect/logout")
                    .SetTokenEndpointUris("connect/token")
                    .SetUserinfoEndpointUris("connect/userinfo");

                // Mark the "email", "profile" and "roles" scopes as supported scopes.
                options.RegisterScopes(Scopes.Email, Scopes.Profile, Scopes.Roles);

                // Note: this sample only uses the authorization code flow but you can enable
                // the other flows if you need to support implicit, password or client credentials.
                options.AllowAuthorizationCodeFlow();

                // Register the signing and encryption credentials.
                options.AddDevelopmentEncryptionCertificate()
                    .AddDevelopmentSigningCertificate();

                // Register the ASP.NET Core host and configure the ASP.NET Core-specific options.
                options.UseAspNetCore()
                    .EnableAuthorizationEndpointPassthrough()
                    .EnableLogoutEndpointPassthrough()
                    .EnableTokenEndpointPassthrough()
                    .EnableUserinfoEndpointPassthrough()
                    .EnableStatusCodePagesIntegration();

                options.AddSamlPlugin(builder =>
                {
                    builder.UseSamlEntityFrameworkCore()
                        .AddSamlMessageDbContext(options => options.UseMySql(connectionString, serverVersion,
                            sqlOptions => sqlOptions.MigrationsAssembly(migrationsAssembly)))
                        .AddSamlConfigurationDbContext(options => options.UseMySql(connectionString, serverVersion,
                            sqlOptions => sqlOptions.MigrationsAssembly(migrationsAssembly)))
                        .AddSamlArtifactDbContext(options => options.UseMySql(connectionString, serverVersion,
                            sqlOptions => sqlOptions.MigrationsAssembly(migrationsAssembly)));

                    builder.ConfigureSamlOpenIddictServerOptions(serverOptions =>
                    {
                        serverOptions.IdpOptions = new SamlIdpOptions
                        {
                            Licensee = licensee,
                            LicenseKey = licenseKey
                        };

                        serverOptions.HostOptions = new SamlHostUserInteractionOptions()
                        {
                            LoginUrl = "/Identity/Account/Login",
                            LogoutUrl = "/Connect/Logout"
                        };
                    });

                    builder.AddSamlAspIdentity<ApplicationUser>();
                });

            })

            // Register the OpenIddict validation components.
            .AddValidation(options =>
            {
                // Import the configuration from the local OpenIddict server instance.
                options.UseLocalServer();

                // Register the ASP.NET Core host.
                options.UseAspNetCore();
            });

        services.AddHostedService<Worker>();

    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
            app.UseMigrationsEndPoint();
        }
        else
        {
            app.UseStatusCodePagesWithReExecute("~/error");
            //app.UseExceptionHandler("~/error");

            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            //app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseStaticFiles();

        app.UseRouting();
        app.UseOpenIddictSamlPlugin();

        app.UseAuthentication();
        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
            endpoints.MapDefaultControllerRoute();
            endpoints.MapRazorPages();
        });
    }
}