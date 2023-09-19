using System.Security.Cryptography.X509Certificates;
using Rsk.AspNetCore.Authentication.Saml2p;
using Rsk.Saml;
using Rsk.Saml.Configuration;

var builder = WebApplication.CreateBuilder(args);

var licensee = builder.Configuration["Licensee"];
var licenseKey = builder.Configuration["LicenseKey"];

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services
    .AddAuthentication(options => { options.DefaultScheme = "cookie"; })
    .AddCookie("cookie")
    .AddSaml2p("saml-openIddict", options =>
    {
        options.TimeComparisonTolerance = 10;

        options.IdentityProviderMetadataAddress = "https://localhost:5003/saml/metadata";
        options.ProtocolBinding = SamlConstants.BindingTypes.HttpPost;
        options.IdentityProviderMetadataRequireHttps = false;
        options.SignInScheme = "cookie";
        options.CallbackPath = "/signin-saml-openIddict";
        options.ArtifactResolutionService = "/ars-saml-openIddict";

        options.Licensee = licensee;
        options.LicenseKey = licenseKey;

        options.ServiceProviderOptions = new SpOptions
        {
            EntityId = "https://localhost:5001/saml",
            SigningCertificate = new X509Certificate2("Resources/testclient.pfx", "test"),
            EncryptionCertificate = new X509Certificate2("Resources/idsrv3test.pfx", "idsrv3test"),
            MetadataPath = "/saml-openIddict",
            MetadataOptions = new ServiceProviderMetadataOptions {ValidUntilInterval = null, CacheDuration = "PT1H"},
            RequireEncryptedAssertions = false
        };
    });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();