using System.Security.Cryptography;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.EntityFrameworkCore;
using PhotoGallery.Models;

namespace PhotoGallery.Data;

public class PhotoGalleryDbContext : DbContext
{
    public PhotoGalleryDbContext(DbContextOptions<PhotoGalleryDbContext> options) : base(options)
    {
    }

    public DbSet<Photo> Photos { get; set; } = default!;
    public DbSet<User> Users { get; set; } = default!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Photo>()
            .HasOne(x => x.User)
            .WithMany(x => x.Photos)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        var password = "ga11ery!";
        var salt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(128 / 8));
        var hashedPassword = Convert.ToBase64String(KeyDerivation.Pbkdf2(
            password: password!,
            salt: Convert.FromBase64String(salt),
            prf: KeyDerivationPrf.HMACSHA256,
            iterationCount: 100000,
            numBytesRequested: 256 / 8));

        modelBuilder.Entity<User>().HasData(
            new User { Id = 1, Username = "galleryAdmin", Password = hashedPassword, Salt = salt }
        );
    }
}