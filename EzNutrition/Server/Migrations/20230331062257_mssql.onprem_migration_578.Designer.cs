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
    [Migration("20230331062257_mssql.onprem_migration_578")]
    partial class mssqlonprem_migration_578
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

            modelBuilder.Entity("EzNutrition.Server.Data.Entities.Food", b =>
                {
                    b.Navigation("FoodNutrientValues");
                });

            modelBuilder.Entity("EzNutrition.Server.Data.Entities.Nutrient", b =>
                {
                    b.Navigation("FoodNutrientValues");
                });
#pragma warning restore 612, 618
        }
    }
}
