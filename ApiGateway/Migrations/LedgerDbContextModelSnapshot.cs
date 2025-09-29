using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using ApiGateway.Data;

#nullable disable

namespace ApiGateway.Migrations
{
    [DbContext(typeof(LedgerDbContext))]
    partial class LedgerDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "9.0.0");

            modelBuilder.Entity("ApiGateway.Data.LedgerAccount", b =>
                {
                    b.Property<Guid>("Id").HasColumnType("uuid");
                    b.Property<string>("AccountNumber").HasMaxLength(20).HasColumnType("character varying(20)");
                    b.Property<string>("AccountType").HasMaxLength(50).HasColumnType("character varying(50)");
                    b.Property<decimal>("Balance").HasPrecision(18, 2).HasColumnType("numeric(18,2)");
                    b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
                    b.Property<string>("Currency").HasMaxLength(3).HasColumnType("character varying(3)");
                    b.Property<Guid>("UserId").HasColumnType("uuid");
                    b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
                    b.HasKey("Id");
                    b.HasIndex("AccountNumber").IsUnique();
                    b.ToTable("LedgerAccounts");
                });

            modelBuilder.Entity("ApiGateway.Data.LedgerPayee", b =>
                {
                    b.Property<Guid>("Id").HasColumnType("uuid");
                    b.Property<string>("AccountNumber").HasMaxLength(50).HasColumnType("character varying(50)");
                    b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
                    b.Property<string>("Name").HasMaxLength(200).HasColumnType("character varying(200)");
                    b.Property<Guid>("UserId").HasColumnType("uuid");
                    b.HasKey("Id");
                    b.HasIndex("UserId", "AccountNumber").IsUnique();
                    b.ToTable("LedgerPayees");
                });

            modelBuilder.Entity("ApiGateway.Data.LedgerTransaction", b =>
                {
                    b.Property<Guid>("Id").HasColumnType("uuid");
                    b.Property<Guid>("AccountId").HasColumnType("uuid");
                    b.Property<decimal>("Amount").HasPrecision(18, 2).HasColumnType("numeric(18,2)");
                    b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
                    b.Property<string>("Currency").HasMaxLength(3).HasColumnType("character varying(3)");
                    b.Property<string>("Description").HasMaxLength(500).HasColumnType("character varying(500)");
                    b.Property<string>("Type").HasMaxLength(10).HasColumnType("character varying(10)");
                    b.Property<Guid>("UserId").HasColumnType("uuid");
                    b.HasKey("Id");
                    b.HasIndex("AccountId");
                    b.HasIndex("UserId");
                    b.ToTable("LedgerTransactions");
                });
#pragma warning restore 612, 618
        }
    }
}
