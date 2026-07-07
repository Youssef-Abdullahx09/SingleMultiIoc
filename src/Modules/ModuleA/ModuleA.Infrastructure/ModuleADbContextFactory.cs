using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ModuleA.Infrastructure;

public sealed class ModuleADbContextFactory : IDesignTimeDbContextFactory<ModuleADbContext>
{
    public ModuleADbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ModuleADbContext>();
        optionsBuilder.UseSqlServer("Server=localhost;Database=ModularShop_ModuleA;Trusted_Connection=True;TrustServerCertificate=True;");
        return new ModuleADbContext(optionsBuilder.Options);
    }
}
