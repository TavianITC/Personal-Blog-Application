using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Personal_Blog_Application.Tests.Helpers
{
    public static class IdentityFakes
    {
        public static UserManager<TUser> CreateUserManager<TUser>() where TUser : class
        {
            var store = A.Fake<IUserStore<TUser>>();
            var options = A.Fake<IOptions<IdentityOptions>>();
            A.CallTo(() => options.Value).Returns(new IdentityOptions());

            return A.Fake<UserManager<TUser>>(o => o.WithArgumentsForConstructor(new object?[]
            {
                store,
                options,
                A.Fake<IPasswordHasher<TUser>>(),
                Array.Empty<IUserValidator<TUser>>(),
                Array.Empty<IPasswordValidator<TUser>>(),
                A.Fake<ILookupNormalizer>(),
                new IdentityErrorDescriber(),
                A.Fake<IServiceProvider>(),
                A.Fake<ILogger<UserManager<TUser>>>()
            }));
        }

        public static SignInManager<TUser> CreateSignInManager<TUser>(UserManager<TUser> userManager)
            where TUser : class
        {
            var contextAccessor = A.Fake<IHttpContextAccessor>();
            A.CallTo(() => contextAccessor.HttpContext).Returns(new DefaultHttpContext());

            var options = A.Fake<IOptions<IdentityOptions>>();
            A.CallTo(() => options.Value).Returns(new IdentityOptions());

            return A.Fake<SignInManager<TUser>>(o => o.WithArgumentsForConstructor(new object?[]
            {
                userManager,
                contextAccessor,
                A.Fake<IUserClaimsPrincipalFactory<TUser>>(),
                options,
                A.Fake<ILogger<SignInManager<TUser>>>(),
                A.Fake<IAuthenticationSchemeProvider>(),
                A.Fake<IUserConfirmation<TUser>>()
            }));
        }
    }
}
