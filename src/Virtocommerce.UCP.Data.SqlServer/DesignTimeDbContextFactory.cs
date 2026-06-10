using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Virtocommerce.UCP.Data.Repositories;

namespace Virtocommerce.UCP.Data.SqlServer;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<UCPDbContext>
{
    public UCPDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<UCPDbContext>();
        var connectionString = args.Length != 0 ? args[0] : "Server=(local);User=virto;Password=virto;Database=VirtoCommerce3;";

        builder.UseSqlServer(
            connectionString,
            options => options.MigrationsAssembly(typeof(SqlServerDataAssemblyMarker).Assembly.GetName().Name));

        return new UCPDbContext(builder.Options);
    }
}
