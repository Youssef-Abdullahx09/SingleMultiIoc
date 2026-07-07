using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ModuleB.Infrastructure;

public sealed class ModuleBDbContextFactory : IDesignTimeDbContextFactory<ModuleBDbContext>
{
    public ModuleBDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ModuleBDbContext>();
        optionsBuilder.UseSqlServer("Server=localhost;Database=ModularShop_ModuleB;Trusted_Connection=True;TrustServerCertificate=True;");
        return new ModuleBDbContext(optionsBuilder.Options);
    }
}
