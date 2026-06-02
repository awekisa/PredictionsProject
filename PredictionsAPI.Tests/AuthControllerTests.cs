using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using PredictionsAPI.Controllers;
using PredictionsAPI.DTOs.Auth;
using PredictionsAPI.Entities;
using PredictionsAPI.Services.Interfaces;

namespace PredictionsAPI.Tests;

public class AuthControllerTests
{
    [Fact]
    public async Task UpdateUsername_AuthenticatedUser_UpdatesDisplayNameAndReturnsAuthResponse()
    {
        var user = new ApplicationUser
        {
            Id = "user-1",
            Email = "alice@example.com",
            DisplayName = "Alice"
        };
        var userManager = CreateUserManagerMock();
        userManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);
        userManager.Setup(m => m.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);
        var tokenService = new Mock<ITokenService>();
        tokenService.Setup(s => s.GenerateTokenAsync(user)).ReturnsAsync("fresh-token");
        var controller = CreateController(userManager, tokenService);

        var result = await controller.UpdateUsername(new UpdateUsernameRequest { DisplayName = "  Alice V  " });

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<AuthResponse>().Subject;
        response.Token.Should().Be("fresh-token");
        response.Email.Should().Be("alice@example.com");
        response.DisplayName.Should().Be("Alice V");
        user.DisplayName.Should().Be("Alice V");
        userManager.Verify(m => m.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task UpdateUsername_NameTooShort_ReturnsBadRequest()
    {
        var user = new ApplicationUser { Id = "user-1", Email = "alice@example.com", DisplayName = "Alice" };
        var userManager = CreateUserManagerMock();
        userManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);
        var controller = CreateController(userManager, new Mock<ITokenService>());

        var result = await controller.UpdateUsername(new UpdateUsernameRequest { DisplayName = " A " });

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value.Should().Be("Username must be at least 2 characters.");
        userManager.Verify(m => m.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task UpdateUsername_MissingUser_ReturnsUnauthorized()
    {
        var userManager = CreateUserManagerMock();
        userManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync((ApplicationUser?)null);
        var controller = CreateController(userManager, new Mock<ITokenService>());

        var result = await controller.UpdateUsername(new UpdateUsernameRequest { DisplayName = "Alice" });

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task ChangePassword_ValidRequest_ReturnsNoContent()
    {
        var user = new ApplicationUser { Id = "user-1", Email = "alice@example.com", DisplayName = "Alice" };
        var userManager = CreateUserManagerMock();
        userManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);
        userManager
            .Setup(m => m.ChangePasswordAsync(user, "OldPass1", "NewPass1"))
            .ReturnsAsync(IdentityResult.Success);
        var controller = CreateController(userManager, new Mock<ITokenService>());

        var result = await controller.ChangePassword(new ChangePasswordRequest
        {
            CurrentPassword = "OldPass1",
            NewPassword = "NewPass1"
        });

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task ChangePassword_CurrentPasswordMismatch_ReturnsBadRequestMessage()
    {
        var user = new ApplicationUser { Id = "user-1", Email = "alice@example.com", DisplayName = "Alice" };
        var userManager = CreateUserManagerMock();
        userManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);
        userManager
            .Setup(m => m.ChangePasswordAsync(user, "wrong", "NewPass1"))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError
            {
                Code = "PasswordMismatch",
                Description = "Incorrect password."
            }));
        var controller = CreateController(userManager, new Mock<ITokenService>());

        var result = await controller.ChangePassword(new ChangePasswordRequest
        {
            CurrentPassword = "wrong",
            NewPassword = "NewPass1"
        });

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value.Should().Be("Current password is incorrect.");
    }

    [Fact]
    public async Task ChangePassword_IdentityPasswordRuleFailure_ReturnsPasswordRuleMessage()
    {
        var user = new ApplicationUser { Id = "user-1", Email = "alice@example.com", DisplayName = "Alice" };
        var userManager = CreateUserManagerMock();
        userManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);
        userManager
            .Setup(m => m.ChangePasswordAsync(user, "OldPass1", "weak"))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError
            {
                Code = "PasswordRequiresDigit",
                Description = "Passwords must have at least one digit."
            }));
        var controller = CreateController(userManager, new Mock<ITokenService>());

        var result = await controller.ChangePassword(new ChangePasswordRequest
        {
            CurrentPassword = "OldPass1",
            NewPassword = "weak"
        });

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value.Should().Be("Password must be at least 6 characters and contain an uppercase letter, a lowercase letter, and a digit.");
    }

    [Fact]
    public async Task ChangePassword_MissingUser_ReturnsUnauthorized()
    {
        var userManager = CreateUserManagerMock();
        userManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync((ApplicationUser?)null);
        var controller = CreateController(userManager, new Mock<ITokenService>());

        var result = await controller.ChangePassword(new ChangePasswordRequest
        {
            CurrentPassword = "OldPass1",
            NewPassword = "NewPass1"
        });

        result.Should().BeOfType<UnauthorizedResult>();
    }

    private static AuthController CreateController(
        Mock<UserManager<ApplicationUser>> userManager,
        Mock<ITokenService> tokenService)
    {
        var controller = new AuthController(userManager.Object, tokenService.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([
                    new Claim(ClaimTypes.NameIdentifier, "user-1")
                ], "TestAuth"))
            }
        };
        return controller;
    }

    private static Mock<UserManager<ApplicationUser>> CreateUserManagerMock()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        return new Mock<UserManager<ApplicationUser>>(
            store.Object,
            Mock.Of<IOptions<IdentityOptions>>(),
            Mock.Of<IPasswordHasher<ApplicationUser>>(),
            Array.Empty<IUserValidator<ApplicationUser>>(),
            Array.Empty<IPasswordValidator<ApplicationUser>>(),
            Mock.Of<ILookupNormalizer>(),
            new IdentityErrorDescriber(),
            Mock.Of<IServiceProvider>(),
            Mock.Of<ILogger<UserManager<ApplicationUser>>>());
    }
}
