using Infrastructure.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data;

/// <summary>
/// Contexto del demo: sitios, tanques, inventarios y descargas (mas las tablas de ASP.NET Identity).
/// Proveedor de base de datos: PostgreSQL (Npgsql). Las fechas se guardan sin zona horaria.
/// </summary>
public partial class AppDbContext : IdentityDbContext<ApplicationUser>
{
    private const string Timestamp = "timestamp without time zone";

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public virtual DbSet<CatSitiosModel> CatSitios { get; set; }
    public virtual DbSet<CatTanqueModel> CatTanques { get; set; }
    public virtual DbSet<ProcDescargaModel> ProcDescargas { get; set; }
    public virtual DbSet<ProcInventarioModel> ProcInventarios { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ApplicationUser>().ToTable("AspNetUsers");

        modelBuilder.Entity<CatSitiosModel>(entity =>
        {
            entity.HasKey(e => e.IdSitio);
            entity.ToTable("Cat_Sitios");

            entity.Property(e => e.IdSitio).ValueGeneratedNever();
            entity.Property(e => e.Activa).HasMaxLength(1);
            entity.Property(e => e.Atiende).HasMaxLength(4).HasColumnName("atiende");
            entity.Property(e => e.CorreoElectronico).HasMaxLength(250);
            entity.Property(e => e.Direccion).HasMaxLength(250);
            entity.Property(e => e.DistUbicacion).HasMaxLength(30).HasColumnName("distUbicacion");
            entity.Property(e => e.EnvioCorreo).HasColumnType(Timestamp).HasColumnName("envioCorreo");
            entity.Property(e => e.IdSitioRelacionado).HasColumnName("idSitioRelacionado");
            entity.Property(e => e.IdZonaPrecio).HasColumnName("idZonaPrecio");
            entity.Property(e => e.Idcliente).HasColumnName("idcliente");
            entity.Property(e => e.NomContacto).HasMaxLength(250);
            entity.Property(e => e.Nombre).HasMaxLength(100);
            entity.Property(e => e.Telefono).HasMaxLength(10);
            entity.Property(e => e.TipoCliente).HasMaxLength(1).HasColumnName("tipoCliente");
            entity.Property(e => e.UltimaActualizacionAnalisis).HasColumnType(Timestamp).HasColumnName("ultimaActualizacionAnalisis");
            entity.Property(e => e.UltimoEnvio).HasColumnType(Timestamp).HasColumnName("ultimoEnvio");
        });

        modelBuilder.Entity<CatTanqueModel>(entity =>
        {
            entity.HasKey(e => new { e.IdSitio, e.NoTanque });
            entity.ToTable("Cat_Tanques");

            entity.Property(e => e.IdSitio).HasColumnName("idSitio");
            entity.Property(e => e.NoTanque).HasMaxLength(2).HasColumnName("noTanque");
            entity.Property(e => e.Capacidad).HasColumnType("decimal(18, 0)");
            entity.Property(e => e.Capacidad95).HasColumnType("decimal(18, 0)");
            entity.Property(e => e.Fondeja).HasColumnType("decimal(18, 0)").HasColumnName("fondeja");
            entity.Property(e => e.Producto).HasMaxLength(50).HasColumnName("producto");

            entity.HasOne(d => d.IdSitioNavigation).WithMany(p => p.CatTanques)
                .HasForeignKey(d => d.IdSitio)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Cat_Tanques_Cat_Sitios");
        });

        modelBuilder.Entity<ProcDescargaModel>(entity =>
        {
            entity.ToTable("Proc_Descargas");
            entity.Property(e => e.FechaFinal).HasColumnType(Timestamp);
            entity.Property(e => e.FechaInicio).HasColumnType(Timestamp);
            // Una descarga se identifica por sitio, tanque y fecha-hora de inicio.
            entity.HasIndex(e => new { e.IdSitio, e.NoTanque, e.FechaInicio });
        });

        modelBuilder.Entity<ProcInventarioModel>(entity =>
        {
            entity.HasKey(e => e.IdReg);
            entity.ToTable("Proc_Inventario");

            entity.Property(e => e.ClaveProducto).HasMaxLength(10).HasColumnName("claveProducto");
            entity.Property(e => e.Fecha).HasColumnType(Timestamp).HasColumnName("fecha");
            entity.Property(e => e.Idsitio).HasColumnName("idsitio");
            entity.Property(e => e.NoTanque).HasMaxLength(2).HasColumnName("noTanque");
            entity.Property(e => e.Temperatura).HasColumnName("temperatura");
            entity.Property(e => e.VolumenDisponible).HasColumnName("volumenDisponible");

            entity.HasIndex(e => new { e.Idsitio, e.NoTanque, e.Fecha });

            entity.HasOne(d => d.CatTanque).WithMany(p => p.ProcInventarios)
                .HasForeignKey(d => new { d.Idsitio, d.NoTanque })
                .HasConstraintName("FK_Proc_Inventario_Cat_Tanques");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
