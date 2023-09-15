﻿// <auto-generated />
using System;
using EzNutrition.Server.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace EzNutrition.Server.Migrations
{
    [DbContext(typeof(EzNutritionDbContext))]
    [Migration("20230410143020_mssql.onprem_migration_124")]
    partial class mssqlonprem_migration_124
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "6.0.5")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder, 1L, 1);

            modelBuilder.Entity("EzNutrition.Server.Data.Entities.Food", b =>
                {
                    b.Property<Guid>("FoodId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Cite")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Details")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("FriendlyCode")
                        .IsRequired()
                        .HasMaxLength(64)
                        .HasColumnType("varchar(64)");

                    b.Property<string>("FriendlyName")
                        .IsRequired()
                        .HasMaxLength(64)
                        .HasColumnType("varchar(64)");

                    b.HasKey("FoodId");

                    b.ToTable("Foods");
                });

            modelBuilder.Entity("EzNutrition.Server.Data.Entities.FoodNutrientValue", b =>
                {
                    b.Property<int>("FoodNutrientValueId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("FoodNutrientValueId"), 1L, 1);

                    b.Property<string>("Details")
                        .HasColumnType("nvarchar(max)");

                    b.Property<Guid>("FoodId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("MeasureUnit")
                        .HasMaxLength(64)
                        .HasColumnType("varchar(64)");

                    b.Property<int>("NutrientId")
                        .HasColumnType("int");

                    b.Property<decimal>("Value")
                        .HasColumnType("decimal(18,2)");

                    b.HasKey("FoodNutrientValueId");

                    b.HasIndex("FoodId");

                    b.HasIndex("NutrientId");

                    b.ToTable("FoodNutrientValues");
                });

            modelBuilder.Entity("EzNutrition.Server.Data.Entities.MultiDerivedPersonRelationship", b =>
                {
                    b.Property<Guid>("MultiDerivedPersonRelationshipId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid?>("ChildModelId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid?>("ParentModelId")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("MultiDerivedPersonRelationshipId");

                    b.HasIndex("ChildModelId");

                    b.HasIndex("ParentModelId");

                    b.ToTable("MultiDerivedPersonRelationships");
                });

            modelBuilder.Entity("EzNutrition.Server.Data.Entities.Nutrient", b =>
                {
                    b.Property<int>("NutrientId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("NutrientId"), 1L, 1);

                    b.Property<string>("DefaultMeasureUnit")
                        .IsRequired()
                        .HasMaxLength(64)
                        .HasColumnType("varchar(64)");

                    b.Property<string>("Details")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("FriendlyName")
                        .IsRequired()
                        .HasMaxLength(64)
                        .HasColumnType("varchar(64)");

                    b.HasKey("NutrientId");

                    b.ToTable("Nutrients");
                });

            modelBuilder.Entity("EzNutrition.Server.Data.Entities.Person", b =>
                {
                    b.Property<Guid>("PersonId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<decimal?>("AgeEnd")
                        .HasColumnType("decimal(18,2)");

                    b.Property<decimal?>("AgeStart")
                        .HasColumnType("decimal(18,2)");

                    b.Property<string>("BodySize")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Cite")
                        .HasColumnType("nvarchar(max)");

                    b.Property<Guid?>("DerivedFromPersonId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Details")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("FriendlyName")
                        .IsRequired()
                        .HasMaxLength(64)
                        .HasColumnType("varchar(64)");

                    b.Property<string>("Gender")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Illness")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("PhysicalActivityLevel")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("SpecialPhysiologicalPeriod")
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("PersonId");

                    b.HasIndex("DerivedFromPersonId");

                    b.ToTable("People");
                });

            modelBuilder.Entity("EzNutrition.Server.Data.Entities.PersonalDietaryReferenceIntakeValue", b =>
                {
                    b.Property<int>("PersonalDietaryReferenceIntakeValueId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("PersonalDietaryReferenceIntakeValueId"), 1L, 1);

                    b.Property<string>("MeasureUnit")
                        .HasMaxLength(64)
                        .HasColumnType("varchar(64)");

                    b.Property<int>("NutrientId")
                        .HasColumnType("int");

                    b.Property<Guid>("PersonId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<decimal>("Value")
                        .HasColumnType("decimal(18,2)");

                    b.HasKey("PersonalDietaryReferenceIntakeValueId");

                    b.HasIndex("NutrientId");

                    b.HasIndex("PersonId");

                    b.ToTable("PersonalDietaryReferenceIntakes");
                });

            modelBuilder.Entity("EzNutrition.Server.Data.Entities.FoodNutrientValue", b =>
                {
                    b.HasOne("EzNutrition.Server.Data.Entities.Food", "Food")
                        .WithMany("FoodNutrientValues")
                        .HasForeignKey("FoodId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("EzNutrition.Server.Data.Entities.Nutrient", "Nutrient")
                        .WithMany("FoodNutrientValues")
                        .HasForeignKey("NutrientId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Food");

                    b.Navigation("Nutrient");
                });

            modelBuilder.Entity("EzNutrition.Server.Data.Entities.MultiDerivedPersonRelationship", b =>
                {
                    b.HasOne("EzNutrition.Server.Data.Entities.Person", "ChildModel")
                        .WithMany("MultiDerivedFrom")
                        .HasForeignKey("ChildModelId");

                    b.HasOne("EzNutrition.Server.Data.Entities.Person", "ParentModel")
                        .WithMany("MultiDerivedTo")
                        .HasForeignKey("ParentModelId");

                    b.Navigation("ChildModel");

                    b.Navigation("ParentModel");
                });

            modelBuilder.Entity("EzNutrition.Server.Data.Entities.Person", b =>
                {
                    b.HasOne("EzNutrition.Server.Data.Entities.Person", "DerivedFromPerson")
                        .WithMany()
                        .HasForeignKey("DerivedFromPersonId");

                    b.Navigation("DerivedFromPerson");
                });

            modelBuilder.Entity("EzNutrition.Server.Data.Entities.PersonalDietaryReferenceIntakeValue", b =>
                {
                    b.HasOne("EzNutrition.Server.Data.Entities.Nutrient", "Nutrient")
                        .WithMany()
                        .HasForeignKey("NutrientId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("EzNutrition.Server.Data.Entities.Person", "Person")
                        .WithMany("DietaryReferenceIntakes")
                        .HasForeignKey("PersonId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Nutrient");

                    b.Navigation("Person");
                });

            modelBuilder.Entity("EzNutrition.Server.Data.Entities.Food", b =>
                {
                    b.Navigation("FoodNutrientValues");
                });

            modelBuilder.Entity("EzNutrition.Server.Data.Entities.Nutrient", b =>
                {
                    b.Navigation("FoodNutrientValues");
                });

            modelBuilder.Entity("EzNutrition.Server.Data.Entities.Person", b =>
                {
                    b.Navigation("DietaryReferenceIntakes");

                    b.Navigation("MultiDerivedFrom");

                    b.Navigation("MultiDerivedTo");
                });
#pragma warning restore 612, 618
        }
    }
}
