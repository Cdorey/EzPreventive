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
    [Migration("20230806141846_mssql.onprem_migration_313")]
    partial class mssqlonprem_migration_313
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "6.0.5")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder, 1L, 1);

            modelBuilder.Entity("AdviceDisease", b =>
                {
                    b.Property<int>("AdvicesAdviceId")
                        .HasColumnType("int");

                    b.Property<int>("DiseasesDiseaseId")
                        .HasColumnType("int");

                    b.HasKey("AdvicesAdviceId", "DiseasesDiseaseId");

                    b.HasIndex("DiseasesDiseaseId");

                    b.ToTable("AdviceDisease");
                });

            modelBuilder.Entity("EzNutrition.Shared.Advice", b =>
                {
                    b.Property<int>("AdviceId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("AdviceId"), 1L, 1);

                    b.Property<string>("Content")
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("Priority")
                        .HasColumnType("int");

                    b.HasKey("AdviceId");

                    b.ToTable("Advices");
                });

            modelBuilder.Entity("EzNutrition.Shared.Disease", b =>
                {
                    b.Property<int>("DiseaseId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("DiseaseId"), 1L, 1);

                    b.Property<string>("FriendlyName")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("ICD10")
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("DiseaseId");

                    b.ToTable("Diseases");
                });

            modelBuilder.Entity("EzNutrition.Shared.EER", b =>
                {
                    b.Property<int>("EERId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("EERId"), 1L, 1);

                    b.Property<decimal?>("AgeStart")
                        .HasColumnType("decimal(18,2)");

                    b.Property<int?>("AvgBwEER")
                        .HasColumnType("int");

                    b.Property<decimal?>("BEE")
                        .HasColumnType("decimal(18,2)");

                    b.Property<string>("Gender")
                        .HasColumnType("nvarchar(max)");

                    b.Property<decimal?>("PAL")
                        .HasColumnType("decimal(18,2)");

                    b.HasKey("EERId");

                    b.ToTable("EERs");
                });

            modelBuilder.Entity("EzNutrition.Shared.Food", b =>
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

            modelBuilder.Entity("EzNutrition.Shared.FoodNutrientValue", b =>
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

            modelBuilder.Entity("EzNutrition.Shared.MultiDerivedPersonRelationship", b =>
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

            modelBuilder.Entity("EzNutrition.Shared.Nutrient", b =>
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

            modelBuilder.Entity("EzNutrition.Shared.Person", b =>
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

            modelBuilder.Entity("EzNutrition.Shared.PersonalDietaryReferenceIntakeValue", b =>
                {
                    b.Property<int>("PersonalDietaryReferenceIntakeValueId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("PersonalDietaryReferenceIntakeValueId"), 1L, 1);

                    b.Property<string>("Details")
                        .HasColumnType("nvarchar(max)");

                    b.Property<bool>("IsOffsetValue")
                        .HasColumnType("bit");

                    b.Property<string>("MeasureUnit")
                        .HasMaxLength(64)
                        .HasColumnType("varchar(64)");

                    b.Property<int>("NutrientId")
                        .HasColumnType("int");

                    b.Property<Guid>("PersonId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("ReferenceType")
                        .IsRequired()
                        .HasMaxLength(64)
                        .HasColumnType("nvarchar(64)");

                    b.Property<decimal>("Value")
                        .HasColumnType("decimal(18,2)");

                    b.HasKey("PersonalDietaryReferenceIntakeValueId");

                    b.HasIndex("NutrientId");

                    b.HasIndex("PersonId");

                    b.ToTable("PersonalDietaryReferenceIntakes");
                });

            modelBuilder.Entity("AdviceDisease", b =>
                {
                    b.HasOne("EzNutrition.Shared.Advice", null)
                        .WithMany()
                        .HasForeignKey("AdvicesAdviceId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("EzNutrition.Shared.Disease", null)
                        .WithMany()
                        .HasForeignKey("DiseasesDiseaseId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("EzNutrition.Shared.FoodNutrientValue", b =>
                {
                    b.HasOne("EzNutrition.Shared.Food", "Food")
                        .WithMany("FoodNutrientValues")
                        .HasForeignKey("FoodId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("EzNutrition.Shared.Nutrient", "Nutrient")
                        .WithMany("FoodNutrientValues")
                        .HasForeignKey("NutrientId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Food");

                    b.Navigation("Nutrient");
                });

            modelBuilder.Entity("EzNutrition.Shared.MultiDerivedPersonRelationship", b =>
                {
                    b.HasOne("EzNutrition.Shared.Person", "ChildModel")
                        .WithMany("MultiDerivedFrom")
                        .HasForeignKey("ChildModelId");

                    b.HasOne("EzNutrition.Shared.Person", "ParentModel")
                        .WithMany("MultiDerivedTo")
                        .HasForeignKey("ParentModelId");

                    b.Navigation("ChildModel");

                    b.Navigation("ParentModel");
                });

            modelBuilder.Entity("EzNutrition.Shared.Person", b =>
                {
                    b.HasOne("EzNutrition.Shared.Person", "DerivedFromPerson")
                        .WithMany()
                        .HasForeignKey("DerivedFromPersonId");

                    b.Navigation("DerivedFromPerson");
                });

            modelBuilder.Entity("EzNutrition.Shared.PersonalDietaryReferenceIntakeValue", b =>
                {
                    b.HasOne("EzNutrition.Shared.Nutrient", "Nutrient")
                        .WithMany()
                        .HasForeignKey("NutrientId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("EzNutrition.Shared.Person", "Person")
                        .WithMany("DietaryReferenceIntakes")
                        .HasForeignKey("PersonId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Nutrient");

                    b.Navigation("Person");
                });

            modelBuilder.Entity("EzNutrition.Shared.Food", b =>
                {
                    b.Navigation("FoodNutrientValues");
                });

            modelBuilder.Entity("EzNutrition.Shared.Nutrient", b =>
                {
                    b.Navigation("FoodNutrientValues");
                });

            modelBuilder.Entity("EzNutrition.Shared.Person", b =>
                {
                    b.Navigation("DietaryReferenceIntakes");

                    b.Navigation("MultiDerivedFrom");

                    b.Navigation("MultiDerivedTo");
                });
#pragma warning restore 612, 618
        }
    }
}