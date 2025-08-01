using Microsoft.AspNetCore.Authentication.Negotiate;
using SSRSProxyApi.Models;
using SSRSProxyApi.Services;
using SSRSProxyApi.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Configure SSRS settings
builder.Services.Configure<SSRSConfig>(
    builder.Configuration.GetSection("SSRS"));

// Get SSRS config for Swagger setup
var ssrsConfig = builder.Configuration.GetSection("SSRS").Get<SSRSConfig>();

// Configure Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo 
    { 
        Title = "SSRS Proxy API", 
        Version = "v1",
        Description = ssrsConfig?.IsDemo == true 
            ? "SSRS Proxy API - DEMO MODE ENABLED (No authentication required)"
            : "SSRS Proxy API - Authentication required for all endpoints"
    });
    
    // Only add security definition if not in demo mode
    if (ssrsConfig?.IsDemo != true)
    {
        c.AddSecurityDefinition("windows", new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "negotiate",
            Description = "Windows Authentication"
        });
        
        c.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "windows"
                    }
                },
                new string[] {}
            }
        });
    }
});

// Add HttpContextAccessor for pass-through authentication
builder.Services.AddHttpContextAccessor();

// Register SSRS service
builder.Services.AddScoped<ISSRSService, SSRSService>();
builder.Services.AddScoped<IUserInfoService, UserInfoService>();

// Register custom authorization handler
builder.Services.AddScoped<IAuthorizationHandler, ConditionalAuthorizationHandler>();

// Add CORS policy for React frontend (adjust origin as needed)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy => policy
            .WithOrigins(
                "http://localhost:5173"           // Local development
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
    );
});

builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme)
   .AddNegotiate();

builder.Services.AddAuthorization(options =>
{
    // Add the conditional authorization policy
    options.AddPolicy("ConditionalAuth", policy =>
        policy.Requirements.Add(new ConditionalAuthorizeAttribute()));
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "SSRS Proxy API v1");
        if (ssrsConfig?.IsDemo == true)
        {
            c.DocumentTitle = "SSRS Proxy API - DEMO MODE";
        }
    });
}

app.UseHttpsRedirection();

// Use CORS before authentication/authorization
app.UseCors("AllowFrontend");

// Serve static files and default files (index.html)
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapFallbackToFile("index.html");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
