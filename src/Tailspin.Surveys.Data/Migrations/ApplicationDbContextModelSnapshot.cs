using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Tailspin.Surveys.Data.DataModels;

namespace Tailspin.Surveys.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    partial class ApplicationDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
            modelBuilder
                .HasAnnotation("ProductVersion", "1.1.2")
                .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

            modelBuilder.Entity("Tailspin.Surveys.Data.DataModels.ContributorRequest", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<DateTimeOffset>("Created");

                    b.Property<string>("EmailAddress")
                        .IsRequired()
                        .HasMaxLength(256);

                    b.Property<int>("SurveyId");

                    b.HasKey("Id");

                    b.HasIndex("SurveyId", "EmailAddress")
                        .IsUnique()
                        .HasName("SurveyIdEmailAddressIndex");

                    b.ToTable("ContributorRequest");
                });

            modelBuilder.Entity("Tailspin.Surveys.Data.DataModels.Question", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("PossibleAnswers");

                    b.Property<int>("SurveyId");

                    b.Property<string>("Text")
                        .IsRequired();

                    b.Property<int>("Type");

                    b.HasKey("Id");

                    b.HasIndex("SurveyId");

                    b.ToTable("Questions");
                });

            modelBuilder.Entity("Tailspin.Surveys.Data.DataModels.Survey", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<int>("OwnerId");

                    b.Property<bool>("Published");

                    b.Property<int>("TenantId");

                    b.Property<string>("Title")
                        .IsRequired();

                    b.HasKey("Id");

                    b.HasIndex("OwnerId");

                    b.ToTable("Survey");
                });

            modelBuilder.Entity("Tailspin.Surveys.Data.DataModels.SurveyContributor", b =>
                {
                    b.Property<int>("SurveyId");

                    b.Property<int>("UserId");

                    b.HasKey("SurveyId", "UserId");

                    b.HasIndex("UserId");

                    b.ToTable("SurveyContributor");
                });

            modelBuilder.Entity("Tailspin.Surveys.Data.DataModels.Tenant", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("ConcurrencyStamp")
                        .IsConcurrencyToken()
                        .IsRequired();

                    b.Property<DateTimeOffset>("Created");

                    b.Property<string>("IssuerValue")
                        .IsRequired()
                        .HasMaxLength(1000);

                    b.HasKey("Id");

                    b.HasIndex("IssuerValue")
                        .IsUnique()
                        .HasName("IssuerValueIndex");

                    b.ToTable("Tenant");
                });

            modelBuilder.Entity("Tailspin.Surveys.Data.DataModels.User", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("ConcurrencyStamp")
                        .IsConcurrencyToken()
                        .IsRequired();

                    b.Property<DateTimeOffset>("Created");

                    b.Property<string>("DisplayName")
                        .IsRequired()
                        .HasMaxLength(256);

                    b.Property<string>("Email")
                        .IsRequired()
                        .HasMaxLength(256);

                    b.Property<string>("ObjectId")
                        .IsRequired()
                        .HasMaxLength(38);

                    b.Property<int>("TenantId");

                    b.HasKey("Id");

                    b.HasIndex("ObjectId")
                        .HasName("UserObjectIdIndex");

                    b.HasIndex("TenantId");

                    b.ToTable("User");
                });

            modelBuilder.Entity("Tailspin.Surveys.Data.DataModels.ContributorRequest", b =>
                {
                    b.HasOne("Tailspin.Surveys.Data.DataModels.Survey")
                        .WithMany("Requests")
                        .HasForeignKey("SurveyId")
                        .OnDelete(DeleteBehavior.Cascade);
                });

            modelBuilder.Entity("Tailspin.Surveys.Data.DataModels.Question", b =>
                {
                    b.HasOne("Tailspin.Surveys.Data.DataModels.Survey", "Survey")
                        .WithMany("Questions")
                        .HasForeignKey("SurveyId")
                        .OnDelete(DeleteBehavior.Cascade);
                });

            modelBuilder.Entity("Tailspin.Surveys.Data.DataModels.Survey", b =>
                {
                    b.HasOne("Tailspin.Surveys.Data.DataModels.User", "Owner")
                        .WithMany()
                        .HasForeignKey("OwnerId");
                });

            modelBuilder.Entity("Tailspin.Surveys.Data.DataModels.SurveyContributor", b =>
                {
                    b.HasOne("Tailspin.Surveys.Data.DataModels.Survey", "Survey")
                        .WithMany("Contributors")
                        .HasForeignKey("SurveyId")
                        .OnDelete(DeleteBehavior.Cascade);

                    b.HasOne("Tailspin.Surveys.Data.DataModels.User", "User")
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade);
                });

            modelBuilder.Entity("Tailspin.Surveys.Data.DataModels.User", b =>
                {
                    b.HasOne("Tailspin.Surveys.Data.DataModels.Tenant")
                        .WithMany()
                        .HasForeignKey("TenantId")
                        .OnDelete(DeleteBehavior.Cascade);
                });
        }
    }
}
