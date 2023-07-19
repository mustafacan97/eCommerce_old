﻿using eCommerce.Core.Entities.Catalog;
using eCommerce.Core.Entities.Customers;
using eCommerce.Core.Entities.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eCommerce.Infrastructure.Persistence.Builders.Media;

public class PictureBuilder : IEntityTypeConfiguration<Picture>
{
    #region Public Methods

    public void Configure(EntityTypeBuilder<Picture> builder)
    {
        builder.ToTable("Picture");

        builder.HasKey(p => p.Id);

        builder.Property(q => q.MimeType)
            .HasMaxLength(16);

        builder.Property(q => q.SeoFilename)
            .HasMaxLength(128);

        builder.Property(q => q.AltAttribute)
            .HasMaxLength(128);

        builder.Property(q => q.TitleAttribute)
            .HasMaxLength(128);

        builder.Property(q => q.VirtualPath)
            .HasMaxLength(512);

        builder.Property(q => q.CreatedOnUtc)
            .HasPrecision(6);

        builder.HasOne(q => q.Customer)
            .WithOne(q => q.Picture)
            .HasForeignKey<Customer>(q => q.PictureId)
            .IsRequired(false);

        builder.HasOne(q => q.Category)
            .WithOne(q => q.Picture)
            .HasForeignKey<Category>(q => q.PictureId)
            .IsRequired(false);

        builder.HasOne(q => q.Manufacturer)
            .WithOne(q => q.Picture)
            .HasForeignKey<Manufacturer>(q => q.PictureId)
            .IsRequired(false);
    }

    #endregion
}