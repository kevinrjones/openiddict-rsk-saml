using System.Security.Cryptography.X509Certificates;
using Rsk.AspNetCore.Authentication.Saml2p;

var builder = WebApplication.CreateBuilder(args);

var licensee = builder.Configuration["Licensee"];
var licenseKey = builder.Configuration["LicenseKey"];

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = "cookie";
        options.DefaultChallengeScheme = "saml-openIddict";
    })
    .AddCookie("cookie")
    .AddSaml2p("saml-openIddict", options =>
    {
        options.TimeComparisonTolerance = 10;
        options.IdentityProviderMetadataAddress = "https://localhost:5003/saml/metadata";

        options.CallbackPath = "/signin-saml-openIddict";
        options.SignInScheme = "cookie";

        // options.NameIdClaimType = "sub";
        
        options.Licensee = licensee;
        options.LicenseKey = licenseKey;


        options.ServiceProviderOptions = new SpOptions
        {
            EntityId = "https://localhost:5001/saml",
            MetadataPath = "/saml-openIddict",
            SigningCertificate = new X509Certificate2("Resources/testclient.pfx", "test"),
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