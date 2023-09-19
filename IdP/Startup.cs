using IdentityModel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenIddict.IdP.Data;
using Quartz;
using Rsk.Saml.Configuration;
using Rsk.Saml.OpenIddict.AspNetCore.Identity.Configuration.DependencyInjection;
using Rsk.Saml.OpenIddict.Configuration.DependencyInjection;
using Rsk.Saml.OpenIddict.EntityFrameworkCore.Configuration.DependacyInjection;
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


        services.AddDbContext<ApplicationDbContext>(options =>
        {
            // Configure the context to use sqlite.
            options.UseMySql(connectionString, serverVersion,
                sqlOptions => sqlOptions.MigrationsAssembly(migrationsAssembly));
            //options.UseSqlite($"Filename={Path.Combine(Path.GetTempPath(), "openiddict-velusia-server.sqlite3")}");

            // Register the entity sets needed by OpenIddict.
            // Note: use the generic overload if you need
            // to replace the default OpenIddict entities.
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

        // OpenIddict offers native integration with Quartz.NET to perform scheduled tasks
        // (like pruning orphaned authorizations/tokens from the database) at regular intervals.
        services.AddQuartz(options =>
        {
            options.UseMicrosoftDependencyInjectionJobFactory();
            options.UseSimpleTypeLoader();
            options.UseInMemoryStore();
        });

        // Register the Quartz.NET service and configure it to block shutdown until jobs are complete.
        services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);

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
                //SAML requires this for metadata generation, looks like you can run this without setting it
                //There does seem to be some validation on the validation options class but it doesn't throw an exception
                //An additional check may need to be done to ensure this is set
                //options.SetIssuer("https://localhost:5003");

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
                        .AddSamlMessageDbContext(optionsBuilder =>
                            optionsBuilder.UseMySql(connectionString, serverVersion,
                                sqlOptions => sqlOptions.MigrationsAssembly(migrationsAssembly)))
                        .AddSamlConfigurationDbContext(optionsBuilder =>
                            optionsBuilder.UseMySql(connectionString, serverVersion,
                                sqlOptions => sqlOptions.MigrationsAssembly(migrationsAssembly)))
                        ;

                    builder.ConfigureSamlOpenIddictServerOptions(serverOptions =>
                    {
                        serverOptions.HostOptions = new SamlHostUserInteractionOptions()
                        {
                            LoginUrl = "/Identity/Account/Login",
                            LogoutUrl = "/Connect/Logout"
                        };
                        serverOptions.IdpOptions = new SamlIdpOptions()
                        {
                            Licensee = "DEMO",
                            LicenseKey =
                                "eyJTb2xkRm9yIjowLjAsIktleVByZXNldCI6NiwiU2F2ZUtleSI6ZmFsc2UsIkxlZ2FjeUtleSI6ZmFsc2UsIlJlbmV3YWxTZW50VGltZSI6IjAwMDEtMDEtMDFUMDA6MDA6MDAiLCJhdXRoIjoiREVNTyIsImV4cCI6IjIwMjMtMTEtMjVUMDA6MDA6MDAiLCJpYXQiOiIyMDIyLTEwLTI1VDA5OjAwOjE3Iiwib3JnIjoiREVNTyIsImF1ZCI6Mn0=.fcLiikHn5WUYPaecYH3OtW64QAG2WHlJqcER6hKO0PF3eHul8lZYXDS7EImvqPRbnPGqBHDrTbYfqtbr4tJmFfZvwPHSuGLkDqRuAtbFbD9cblTsjkBUp+Yh1pZwXOSlMYJ1uzeMQsBs81mAYJxRrsD0JaNo3wKtPYEiOplusLPu/rh03k2hNFajyIrj7zPsgs2i6doqlhG0wI0nvwrkKJjerGM0Dup7XioTH//ZehiQT9w3iVF1nUaK3iVxaEUc/Q546hPlRBtfqy/rdD1BH97oFVes2V7EVR2nxA9vi9NOYs6YZo1K1elXuTovGodQrCedsvQvKb6/gTpoxam8qDhgmy9MH4mmfHUDFb4lgKI+LYXhi5Udpb86kbmn4KyaNEdtmVrdCUugc8TH7jIzXph06ZguEZ0YOqV/MChkoc8h4F4CG7y8XVvUAGrZhQ2NGGdOUKxg8A0lO9RLf/2Cahhkn99PeBVd+Mk6oI1kzBLIGN9rjc0+4lVfb1BBmRcczp3hUsm+sja5gqOxSR38DrBgGZrBQtSD1ug2EP4Q8thMQp6o7mCQJoOQsyjoslyWs3YFT9/4w3419iwWYwcUnKvAfk3fvkjfzB9cA0KmvlQruHwOdIjCSb8ZB3gr+h8iIfI6V2oNkycqvZBMRdVAX9GMIbTv/26qfITwuCVCTH0="
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