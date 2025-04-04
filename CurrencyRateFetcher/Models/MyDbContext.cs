using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using static CurrencyRateFetcher.SettingsHelper;

namespace CurrencyRateFetcher.Models;

public partial class MyDbContext : DbContext
{
    private readonly DatabaseConfig _databaseConfig;

    public MyDbContext(DatabaseConfig databaseConfig)
    {
        _databaseConfig = databaseConfig;
    }

    public virtual DbSet<Currency> Currencies { get; set; }
    public virtual DbSet<CurrencyRates> CurrencyRates { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
            var connectionString = $"" +
            $"Server={_databaseConfig.dbAddress};" +
            $"Database={_databaseConfig.dbName};" +
            $"Port={_databaseConfig.dbPort};" +
            $"User={_databaseConfig.dbUser};" +
            $"Password={_databaseConfig.dbPassword};";

            optionsBuilder.UseMySQL(connectionString);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Currency>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.HasIndex(e => e.CurrencyCode, "currency_code").IsUnique();

            entity.HasIndex(e => e.CurrencyCode, "idx_currency_code");

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.CurrencyCode)
                .HasMaxLength(3)
                .IsFixedLength()
                .HasColumnName("currency_code");
        });

        modelBuilder.Entity<CurrencyRates>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.HasIndex(e => e.CurrencyId, "idx_currency_id");

            entity.HasIndex(e => e.Date, "idx_date");

            entity.HasIndex(e => new { e.Date, e.CurrencyId }, "unique_date_currency").IsUnique();

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.CurrencyId)
                .HasColumnType("int(11)")
                .HasColumnName("currency_id");
            entity.Property(e => e.Date)
                .HasColumnType("date")
                .HasColumnName("date");
            entity.Property(e => e.ExchangeRate)
                .HasPrecision(15, 6)
                .HasColumnName("exchange_rate");

            entity.HasOne(d => d.Currency).WithMany(p => p.CurrencyRates)
                .HasForeignKey(d => d.CurrencyId)
                .HasConstraintName("fk_currency_id");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
