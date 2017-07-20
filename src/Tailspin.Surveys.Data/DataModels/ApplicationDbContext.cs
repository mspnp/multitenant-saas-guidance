// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using System;

namespace Tailspin.Surveys.Data.DataModels
{
    public class ApplicationDbContext : DbContext
    {
 
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<User>(b =>
            {
                b.ToTable("User");
                b.HasKey(u => u.Id);
                b.Property(u => u.DisplayName)
                    .IsRequired()
                    .HasMaxLength(256);
                b.Property(u => u.Email)
                    .IsRequired()
                    .HasMaxLength(256);
                b.Property(u => u.ConcurrencyStamp)
                    .IsRequired()
                    .IsConcurrencyToken();
                b.Property(u => u.ObjectId)
                    .IsRequired()
                    .HasMaxLength(38);
                b.Property(u => u.Created)
                    .IsRequired();
                b.HasIndex(u => u.ObjectId)
                    .HasName("UserObjectIdIndex");
            });

            modelBuilder.Entity<Tenant>(b =>
            {
                b.ToTable("Tenant");
                b.HasKey(t => t.Id);
                b.Property(t => t.ConcurrencyStamp)
                    .IsRequired()
                    .IsConcurrencyToken();
                b.Property(t => t.IssuerValue)
                    .IsRequired()
                    .HasMaxLength(1000);
                b.Property(t => t.Created)
                    .IsRequired();
                b.HasMany(typeof(User)).WithOne()
                    .HasForeignKey("TenantId");
                b.HasIndex(t => t.IssuerValue)
                    .HasName("IssuerValueIndex")
                    .IsUnique();
            });

            modelBuilder.Entity<SurveyContributor>(b =>
            {
                b.HasKey(r => new { r.SurveyId, r.UserId });
            });

            modelBuilder.Entity<Survey>(b =>
            {
                b.ToTable("Survey");
                b.HasKey(s => s.Id);
                b.HasOne(s => s.Owner)
                    .WithMany()
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<ContributorRequest>(b =>
            {
                b.ToTable("ContributorRequest");
                b.HasKey(cr => cr.Id);
                b.HasIndex(cr => new { cr.SurveyId, cr.EmailAddress })
                    .HasName("SurveyIdEmailAddressIndex")
                    .IsUnique();
                b.Property(cr => cr.EmailAddress)
                    .IsRequired()
                    .HasMaxLength(256);
                b.Property(cr => cr.SurveyId)
                    .IsRequired();
            });

            modelBuilder.Entity<SurveyContributor>(b =>
            {
                b.ToTable("SurveyContributor");
            });
        }

        public DbSet<Tenant> Tenants { get; set; }

        public DbSet<User> Users { get; set; }

        public DbSet<Survey> Surveys { get; set; }

        public DbSet<Question> Questions { get; set; }

        public DbSet<SurveyContributor> SurveyContributors { get; set; }

        public DbSet<ContributorRequest> ContributorRequests { get; set; }
    }
}
