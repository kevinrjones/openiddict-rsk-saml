using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenIddict.Abstractions;
using OpenIddict.IdP.Data;
using Rsk.Saml;
using Rsk.Saml.IdentityProvider.Storage.EntityFramework;
using Rsk.Saml.IdentityProvider.Storage.EntityFramework.Mappers;
using Rsk.Saml.Models;
using Rsk.Saml.OpenIddict.EntityFrameworkCore.DbContexts;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace OpenIddict.IdP;

public class Worker : IHostedService
{
    private readonly IServiceProvider _serviceProvider;

    public Worker(IServiceProvider serviceProvider)
        => _serviceProvider = serviceProvider;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();

        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.Database.EnsureCreatedAsync();

        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();

        if (await manager.FindByClientIdAsync("mvc") == null)
        {
            await manager.CreateAsync(new OpenIddictApplicationDescriptor
            {
                ClientId = "mvc",
                ClientSecret = "901564A5-E7FE-42CB-B10D-61EF6A8F3654",
                ConsentType = ConsentTypes.Explicit,
                DisplayName = "MVC client application",
                RedirectUris =
                {
                    new Uri("https://localhost:44338/callback/login/local")
                },
                PostLogoutRedirectUris =
                {
                    new Uri("https://localhost:44338/callback/logout/local")
                },
                Permissions =
                {
                    Permissions.Endpoints.Authorization,
                    Permissions.Endpoints.Logout,
                    Permissions.Endpoints.Token,
                    Permissions.GrantTypes.AuthorizationCode,
                    Permissions.ResponseTypes.Code,
                    Permissions.Scopes.Email,
                    Permissions.Scopes.Profile,
                    Permissions.Scopes.Roles
                },
                Requirements =
                {
                    Requirements.Features.ProofKeyForCodeExchange
                }
            });
        }

        if (await manager.FindByClientIdAsync("https://localhost:5001/saml") == null)
        {
            await manager.CreateAsync(new OpenIddictApplicationDescriptor
            {
                ClientId = "https://localhost:5001/saml",
                Permissions =
                {
                    Permissions.Scopes.Email,
                }
            });
        }
        
        var samlContext = scope.ServiceProvider.GetRequiredService<OpenIddictSamlMessageDbContext>();
        await samlContext.Database.EnsureCreatedAsync();

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        if (await userManager.FindByNameAsync("bob@test.fake")== null)
        {
            var user = new ApplicationUser();
            user.UserName = "bob@test.fake";
            user.Email = "bob@test.fake";
            await userManager.CreateAsync(user, "Password123!");

            // var createdUser = await userManager.FindByNameAsync(user.UserName);
            // userManager.AddClaimsAsync(createdUser, new List<Claim>()
            // {
            //     new Claim(JwtClaimTypes.Email, )
            // })
        }
        
        var scopeManager = scope.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();
        if (await scopeManager.FindByNameAsync("email") == null)
        {
            var openIddictScopeDescriptor = new OpenIddictScopeDescriptor
            {
                Name = "email",
                Resources =
                {
                    "https://localhost:5001/saml"
                }
            };
            
            var expectedClaims = new List<string>
            {
                "email",
            };
            
            var claims = JsonSerializer.Serialize(expectedClaims, new JsonSerializerOptions());

            using var jsonDocument = JsonDocument.Parse(claims);
            openIddictScopeDescriptor.Properties.Add("Claims", jsonDocument.RootElement);
            
            await scopeManager.CreateAsync(openIddictScopeDescriptor);
        }
        
        var samlConfigurationDbContext = scope.ServiceProvider.GetRequiredService<ISamlConfigurationDbContext>();
        
        
        if (samlConfigurationDbContext.ServiceProviders.SingleOrDefault(x => x.EntityId == "https://localhost:5001/saml") == null)
        {
            var artifact = new Rsk.Saml.Models.ServiceProvider()
            {
                EntityId = "https://localhost:5001/saml/artifact",
                EncryptAssertions = false,
                AssertionConsumerServices =
                {
                    new Service(SamlConstants.BindingTypes.HttpArtifact, "https://localhost:5001/signin-saml-openIddict-artifact")
                },
                SingleLogoutServices =
                    { new Service(SamlConstants.BindingTypes.HttpRedirect, "https://localhost:5001/signout-saml-artifasct") },
                ArtifactResolutionServices =
                    { new Service(SamlConstants.BindingTypes.Soap, "https://localhost:5001/ars-saml") },
                SigningCertificates = new List<X509Certificate2> { new X509Certificate2("Resources/testclient.cer") },
                EncryptionCertificate = new X509Certificate2("Resources/idsrv3test.cer"),
            };
            
            var serviceProvider = new Rsk.Saml.Models.ServiceProvider()
            {
                EntityId = "https://localhost:5001/saml",
                EncryptAssertions = false,
                AllowIdpInitiatedSso = true,
                AssertionConsumerServices =
                {
                    new Service(SamlConstants.BindingTypes.HttpPost, "https://localhost:5001/signin-saml-openIddict")
                },
                SingleLogoutServices =
                    { new Service(SamlConstants.BindingTypes.HttpRedirect, "https://localhost:5001/signout-saml") },

                SigningCertificates = new List<X509Certificate2> { new X509Certificate2("Resources/testclient.cer") },
                EncryptionCertificate = new X509Certificate2("Resources/idsrv3test.cer"),
            };
            samlConfigurationDbContext.ServiceProviders.Add(serviceProvider.ToEntity());
            samlConfigurationDbContext.ServiceProviders.Add(artifact.ToEntity());
            
            await samlConfigurationDbContext.SaveChangesAsync();
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
