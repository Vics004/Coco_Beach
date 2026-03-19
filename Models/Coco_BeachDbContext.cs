using Microsoft.EntityFrameworkCore;

namespace Coco_Beach.Models
{
    public class Coco_BeachDbContext : DbContext
    {
        public Coco_BeachDbContext(DbContextOptions options) : base(options)
        {

        }

        public DbSet<rol> rol { get; set; }
        public DbSet<persona> persona { get; set; }
        public DbSet<usuario> usuario { get; set; }
        public DbSet<estado> estado { get; set; }
        public DbSet<recurso> recurso { get; set; }
        public DbSet<reserva> reserva { get; set; }
        public DbSet<check_in> check_in { get; set; }
    }
}
