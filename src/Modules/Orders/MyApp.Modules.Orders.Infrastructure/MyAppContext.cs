using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using MyApp.Modules.Orders.Infrastructure.Persistence.MicrosoftSqlServer.DbFirst.Entities;

namespace MyApp.Modules.Orders.Infrastructure;

public partial class MyAppContext : DbContext
{
    public MyAppContext()
    {
    }

    public MyAppContext(DbContextOptions<MyAppContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Customer> Customers { get; set; }

    public virtual DbSet<FailedHttpPayload> FailedHttpPayloads { get; set; }

    public virtual DbSet<Order> Orders { get; set; }

    public virtual DbSet<OrderItem> OrderItems { get; set; }

    public virtual DbSet<OrdersOutboxMessage> OrdersOutboxMessages { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Server=.\\SQLEXPRESS;Database=MyApp;Trusted_Connection=True;TrustServerCertificate=True;Connection Timeout=30;");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("Customer");

            entity.Property(e => e.Name).HasMaxLength(200);
        });

        modelBuilder.Entity<FailedHttpPayload>(entity =>
        {
            entity.ToTable("FailedHttpPayload");

            entity.Property(e => e.CorrelationId).HasMaxLength(64);
            entity.Property(e => e.Method).HasMaxLength(16);
            entity.Property(e => e.Path).HasMaxLength(512);
            entity.Property(e => e.RemoteIp).HasMaxLength(64);
            entity.Property(e => e.RequestContentType).HasMaxLength(128);
            entity.Property(e => e.RequestId).HasMaxLength(64);
            entity.Property(e => e.ResponseContentType).HasMaxLength(128);
            entity.Property(e => e.SpanId).HasMaxLength(64);
            entity.Property(e => e.TraceId).HasMaxLength(64);
            entity.Property(e => e.UserId).HasMaxLength(128);
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("Order");

            entity.Property(e => e.OrderDateUtc).HasPrecision(0);
            entity.Property(e => e.Status)
                .HasMaxLength(32)
                .HasDefaultValue("Draft");
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Customer).WithMany(p => p.Orders)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Order_Customer");
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.ToTable("OrderItem");

            entity.Property(e => e.UnitPrice).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Order).WithMany(p => p.OrderItems)
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("FK_OrderItem_Order");
        });

        modelBuilder.Entity<OrdersOutboxMessage>(entity =>
        {
            entity.ToTable("OrdersOutboxMessage");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.CorrelationId).HasMaxLength(128);
            entity.Property(e => e.IdempotencyKey).HasMaxLength(200);
            entity.Property(e => e.LastError).HasMaxLength(2000);
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.Type).HasMaxLength(200);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
