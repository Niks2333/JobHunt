using JobHunt.Api.Entities;
using JobHunt.Api.Models.Requests.Auth;
using JobHunt.Api.Models.Responses.Auth;
using JobHunt.Api.Services.Results;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace JobHunt.Api.Services;

public class AuthService(UserManager<ApplicationUser> userManager)
{
    public async Task<RegisterUserResult> RegisterAsync(RegisterUserRequest request)
    {
        var email = request.Email.Trim();
        var user = new ApplicationUser { Email = email, UserName = email };

        IdentityResult result;
        try
        {
            // Identity validates the account, hashes the password, and saves via its EF store.
            result = await userManager.CreateAsync(user, request.Password);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // The unique username index also protects simultaneous registrations of this email.
            return new(null, new() { [nameof(request.Email)] = ["An account with this email already exists."] });
        }

        if (!result.Succeeded)
        {
            var errors = result.Errors.GroupBy(error => error.Code.StartsWith("Password", StringComparison.Ordinal)
                    ? nameof(request.Password) : nameof(request.Email))
                .ToDictionary(group => group.Key, group => group.Select(error =>
                        error.Code is "DuplicateEmail" or "DuplicateUserName"
                            ? "An account with this email already exists." : error.Description)
                    .Distinct().ToArray());
            return new(null, errors);
        }

        return new(new AuthUserResponse(user.Id, user.Email!, user.EmailConfirmed), []);
    }
}
