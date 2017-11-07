using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SidekaApi.Models
{
    public class SidekaDbContext : DbContext
    {
        public SidekaDbContext(DbContextOptions<SidekaDbContext> options) : base(options)
        {
        }

        public DbSet<SidekaContent> SidekaContent { get; set; }
        public DbSet<SidekaDesa> SidekaDesa { get; set; }
        public DbSet<SidekaLog> SidekaLog { get; set; }
        public DbSet<SidekaToken> SidekaToken { get; set; }
        public DbSet<WordpressUser> WordpressUser { get; set; }
        public DbSet<WordpressUserMeta> WordpressUserMeta { get; set; }
        public DbSet<WordpressOption> WordpressOption { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            foreach (var entity in builder.Model.GetEntityTypes())
            {
                entity.Relational().TableName = entity.Relational().TableName.ToSnakeCase();

                foreach (var property in entity.GetProperties())
                {
                    property.Relational().ColumnName = property.Name.ToSnakeCase();
                }

                foreach (var key in entity.GetKeys())
                {
                    key.Relational().Name = key.Relational().Name.ToSnakeCase();
                }

                foreach (var key in entity.GetForeignKeys())
                {
                    key.Relational().Name = key.Relational().Name.ToSnakeCase();
                }

                foreach (var index in entity.GetIndexes())
                {
                    index.Relational().Name = index.Relational().Name.ToSnakeCase();
                }
            }


        }

        public override int SaveChanges()
        {
            AddTimestamps();
            return base.SaveChanges();
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            AddTimestamps();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            //AddTimestamps();
            return base.SaveChangesAsync(cancellationToken);
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default(CancellationToken))
        {
            //AddTimestamps();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        private void AddTimestamps()
        {
            //var entities = ChangeTracker.Entries().Where(x => x.Entity is IAuditableEntity && (x.State == EntityState.Added || x.State == EntityState.Modified));

            ////var currentUsername = !string.IsNullOrEmpty(System.Web.HttpContext.Current?.User?.Identity?.Name)
            ////    ? HttpContext.Current.User.Identity.Name
            ////    : "Anonymous";

            //foreach (var entity in entities)
            //{
            //    if (entity.State == EntityState.Added)
            //    {
            //        ((IAuditableEntity)entity.Entity).DateCreated = DateTime.UtcNow;
            //        //((IAuditableEntity)entity.Entity).UserCreated = currentUsername;
            //    }

            //    ((IAuditableEntity)entity.Entity).DateModified = DateTime.UtcNow;
            //    //((IAuditableEntity)entity.Entity).UserModified = currentUsername;
            //}
        }
    }

    public static class StringExtensions
    {
        public static string ToSnakeCase(this string input)
        {
            if (string.IsNullOrEmpty(input)) { return input; }

            var startUnderscores = Regex.Match(input, @"^_+");
            return startUnderscores + Regex.Replace(input, @"([a-z0-9])([A-Z])", "$1_$2").ToLower();
        }
    }
}