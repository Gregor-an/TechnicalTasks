using Microsoft.EntityFrameworkCore;
using Task2.LoginApi.Models;

namespace Task2.LoginApi.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
}
