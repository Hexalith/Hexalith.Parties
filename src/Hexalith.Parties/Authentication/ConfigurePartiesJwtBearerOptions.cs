using System.Text;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Hexalith.Parties.Authentication;

public sealed class ConfigurePartiesJwtBearerOptions(
    IOptions<PartiesAuthenticationOptions> authOptions,
    ILoggerFactory loggerFactory) : IConfigureNamedOptions<JwtBearerOptions>
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<ConfigurePartiesJwtBearerOptions>();

    public void Configure(string? name, JwtBearerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (name != JwtBearerDefaults.AuthenticationScheme)
        {
            return;
        }

        PartiesAuthenticationOptions authConfig = authOptions.Value;

        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            ValidIssuer = authConfig.Issuer,
            ValidAudience = authConfig.Audience,
        };

        if (!string.IsNullOrEmpty(authConfig.Authority))
        {
            options.Authority = authConfig.Authority;
            options.RequireHttpsMetadata = authConfig.RequireHttpsMetadata;
        }
        else if (!string.IsNullOrEmpty(authConfig.SigningKey))
        {
            options.TokenValidationParameters.IssuerSigningKey =
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(authConfig.SigningKey));
        }

        options.Events = new JwtBearerEvents
        {
            OnChallenge = context =>
            {
                context.HandleResponse();

                string detail = context.AuthenticateFailure switch
                {
                    SecurityTokenExpiredException => "The provided authentication token has expired.",
                    SecurityTokenInvalidIssuerException => "The provided authentication token has an invalid issuer.",
                    SecurityTokenInvalidSignatureException or SecurityTokenInvalidAudienceException =>
                        "The provided authentication token is invalid.",
                    not null => "The provided authentication token is invalid.",
                    _ when !string.IsNullOrEmpty(context.Error) => $"Authentication failed: {context.Error}.",
                    _ => "Authentication is required to access this resource.",
                };

                string correlationId = context.HttpContext.Items["CorrelationId"]?.ToString() ?? "unknown";

                _logger.LogWarning(
                    "Authentication challenge: CorrelationId={CorrelationId}, Path={RequestPath}, Reason={Reason}",
                    correlationId,
                    context.Request.Path.Value,
                    string.IsNullOrEmpty(context.Error) ? "MissingToken" : context.Error);

                var problemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "Unauthorized",
                    Type = "https://tools.ietf.org/html/rfc9457#section-3",
                    Detail = detail,
                    Instance = context.Request.Path,
                    Extensions = { ["correlationId"] = correlationId },
                };

                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return context.Response.WriteAsJsonAsync(
                    problemDetails,
                    options: null,
                    contentType: "application/problem+json");
            },
        };
    }

    public void Configure(JwtBearerOptions options) => Configure(JwtBearerDefaults.AuthenticationScheme, options);
}
