using System;
using System.Linq;
using System.Reflection;
using System.ComponentModel.DataAnnotations;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Xunit;
using Library.Entities;

namespace Common.Tests.Entities;

public class CredentialEntityTest
{
    [Fact]
    public void CredentialEntity_HasBsonIdOnIdProperty()
    {
        // Given the CredentialEntity type
        var prop = typeof(CredentialEntity).GetProperty("Id");

        // Then Id is decorated with BsonId
        Assert.NotNull(prop);
        Assert.NotNull(prop.GetCustomAttribute<BsonIdAttribute>());
    }

    [Fact]
    public void CredentialEntity_EmailHasBsonElementSnakeCase()
    {
        var prop = typeof(CredentialEntity).GetProperty("Email");
        Assert.NotNull(prop);
        var bson = prop.GetCustomAttribute<BsonElementAttribute>();
        Assert.NotNull(bson);
        Assert.Equal("email", bson.ElementName);
    }

    [Fact]
    public void CredentialEntity_EmailHasRequiredAttribute()
    {
        var prop = typeof(CredentialEntity).GetProperty("Email");
        Assert.NotNull(prop);
        Assert.NotNull(prop.GetCustomAttribute<RequiredAttribute>());
    }

    [Fact]
    public void CredentialEntity_EmailHasEmailAddressAttribute()
    {
        var prop = typeof(CredentialEntity).GetProperty("Email");
        Assert.NotNull(prop);
        Assert.NotNull(prop.GetCustomAttribute<EmailAddressAttribute>());
    }

    [Fact]
    public void CredentialEntity_PasswordHashHasBsonElementSnakeCase()
    {
        var prop = typeof(CredentialEntity).GetProperty("PasswordHash");
        Assert.NotNull(prop);
        var bson = prop.GetCustomAttribute<BsonElementAttribute>();
        Assert.NotNull(bson);
        Assert.Equal("password_hash", bson.ElementName);
    }

    [Fact]
    public void CredentialEntity_RoleHasBsonElementSnakeCase()
    {
        var prop = typeof(CredentialEntity).GetProperty("Role");
        Assert.NotNull(prop);
        var bson = prop.GetCustomAttribute<BsonElementAttribute>();
        Assert.NotNull(bson);
        Assert.Equal("role", bson.ElementName);
    }

    [Fact]
    public void CredentialEntity_MustResetPasswordHasBsonElementSnakeCase()
    {
        var prop = typeof(CredentialEntity).GetProperty("MustResetPassword");
        Assert.NotNull(prop);
        var bson = prop.GetCustomAttribute<BsonElementAttribute>();
        Assert.NotNull(bson);
        Assert.Equal("must_reset_password", bson.ElementName);
    }

    [Fact]
    public void CredentialEntity_RefreshTokenHasBsonElementSnakeCase()
    {
        var prop = typeof(CredentialEntity).GetProperty("RefreshToken");
        Assert.NotNull(prop);
        var bson = prop.GetCustomAttribute<BsonElementAttribute>();
        Assert.NotNull(bson);
        Assert.Equal("refresh_token", bson.ElementName);
    }

    [Fact]
    public void CredentialEntity_RefreshTokenIsNullableByDefault()
    {
        // Given a new CredentialEntity
        var entity = new CredentialEntity();

        // Then RefreshToken is null (no active session)
        Assert.Null(entity.RefreshToken);
    }

    [Fact]
    public void CredentialEntity_MustResetPasswordDefaultsToFalse()
    {
        var entity = new CredentialEntity();
        Assert.False(entity.MustResetPassword);
    }

    [Fact]
    public void RefreshTokenDocument_HashHasBsonElementSnakeCase()
    {
        var prop = typeof(RefreshTokenDocument).GetProperty("Hash");
        Assert.NotNull(prop);
        var bson = prop.GetCustomAttribute<BsonElementAttribute>();
        Assert.NotNull(bson);
        Assert.Equal("hash", bson.ElementName);
    }

    [Fact]
    public void RefreshTokenDocument_ExpiryHasBsonElementSnakeCase()
    {
        var prop = typeof(RefreshTokenDocument).GetProperty("Expiry");
        Assert.NotNull(prop);
        var bson = prop.GetCustomAttribute<BsonElementAttribute>();
        Assert.NotNull(bson);
        Assert.Equal("expiry", bson.ElementName);
    }

    [Fact]
    public void CredentialEntity_CanBeConstructedWithAllFields()
    {
        // Given valid field values
        var refreshToken = new RefreshTokenDocument
        {
            Hash = "abc123",
            Expiry = DateTime.UtcNow.AddHours(24)
        };

        // When constructing a CredentialEntity
        var entity = new CredentialEntity
        {
            Email = "test@example.com",
            PasswordHash = "$2a$12$hash",
            Role = "Provider",
            MustResetPassword = true,
            RefreshToken = refreshToken
        };

        // Then all fields are set correctly
        Assert.Equal("test@example.com", entity.Email);
        Assert.Equal("$2a$12$hash", entity.PasswordHash);
        Assert.Equal("Provider", entity.Role);
        Assert.True(entity.MustResetPassword);
        Assert.NotNull(entity.RefreshToken);
        Assert.Equal("abc123", entity.RefreshToken.Hash);
    }
}
