using Asistente.Infrastructure.Services;

namespace Asistente.Tests;

public class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new();

    [Fact]
    public void HashPassword_ReturnsNonEmptyHash()
    {
        var hash = _hasher.HashPassword("MiContrasena123");
        Assert.False(string.IsNullOrEmpty(hash));
    }

    [Fact]
    public void HashPassword_ContainsSaltAndHash()
    {
        var hash = _hasher.HashPassword("Test123*");
        Assert.Contains(":", hash);
        var parts = hash.Split(':');
        Assert.Equal(2, parts.Length);
    }

    [Fact]
    public void HashPassword_DifferentHashForSamePassword()
    {
        var hash1 = _hasher.HashPassword("MismaContrasena");
        var hash2 = _hasher.HashPassword("MismaContrasena");
        // Should differ because salt is random each time
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void VerifyPassword_ReturnsTrueForCorrectPassword()
    {
        const string password = "Admin123*";
        var hash = _hasher.HashPassword(password);
        var valid = _hasher.VerifyPassword(password, hash);
        Assert.True(valid);
    }

    [Fact]
    public void VerifyPassword_ReturnsFalseForWrongPassword()
    {
        var hash = _hasher.HashPassword("CorrectPass");
        var valid = _hasher.VerifyPassword("WrongPass", hash);
        Assert.False(valid);
    }

    [Fact]
    public void VerifyPassword_ReturnsFalseForTamperedHash()
    {
        var hash = _hasher.HashPassword("SomePassword");
        var tampered = hash + "tampered";
        var valid = _hasher.VerifyPassword("SomePassword", tampered);
        Assert.False(valid);
    }

    [Fact]
    public void HashPassword_ThrowsOnNullInput()
    {
        Assert.Throws<ArgumentNullException>(() => _hasher.HashPassword(null!));
    }

    [Fact]
    public void VerifyPassword_ThrowsOnNullPassword()
    {
        Assert.Throws<ArgumentNullException>(() => _hasher.VerifyPassword(null!, "somehash"));
    }
}
