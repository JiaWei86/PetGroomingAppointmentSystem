using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;

namespace PetGroomingAppointmentSystem.Services
{
    public class SecurityHelper
    {
        private readonly HttpContext _httpContext;

        public SecurityHelper(IHttpContextAccessor httpContextAccessor)
        {
            _httpContext = httpContextAccessor.HttpContext ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        }

        /// <summary>
        /// Signs in a user with claims-based authentication
        /// </summary>
        /// <param name="userId">User ID (stored as NameIdentifier claim)</param>
        /// <param name="email">User email (used as Name and Email claim)</param>
        /// <param name="role">User role</param>
        /// <param name="rememberMe">Whether to create a persistent login session</param>
        /// <param name="additionalClaims">Optional additional claims to include</param>
        public async Task SignIn(string userId, string email, string role, bool rememberMe, Dictionary<string, string>? additionalClaims = null)
        {
            // (1) Create claims with identity and principal
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId),  // USER ID - CRITICAL for HomeController
                new Claim(ClaimTypes.Name, email),
                new Claim(ClaimTypes.Email, email),
                new Claim(ClaimTypes.Role, role)
            };

            // Add additional claims if provided
            if (additionalClaims != null)
            {
                foreach (var claim in additionalClaims)
                {
                    claims.Add(new Claim(claim.Key, claim.Value));
                }
            }

            // (2) Create claims identity with "Cookies" authentication scheme
            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            // (3) Create principal with the identity
            var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

            // (4) Create authentication properties for "Remember Me"
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = rememberMe,
                ExpiresUtc = rememberMe ? DateTimeOffset.UtcNow.AddDays(30) : DateTimeOffset.UtcNow.AddHours(1)
            };

            // (5) Sign in the user
            await _httpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                claimsPrincipal,
                authProperties);
        }

        /// <summary>
        /// Signs out the current user
        /// </summary>
        public async Task SignOut()
        {
            await _httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }
}