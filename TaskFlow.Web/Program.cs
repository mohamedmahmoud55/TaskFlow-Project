
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Data.Context;
using TaskFlow.Data.Entities;
using TaskFlow.Services.Boards;
using TaskFlow.Services.Projects;
using TaskFlow.Services.Security;
using TaskFlow.Services.Tasks;
using TaskFlow.Services.Teams;
using TaskFlow.Services.Workspaces;
using TaskFlow.Services.Tasks.Assignments;

namespace TaskFlow.Web
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            var dataProtectionBuilder = builder.Services
                .AddDataProtection()
                .SetApplicationName("TaskFlow");

            var keyRingPath = builder.Configuration["DataProtection:KeyRingPath"];
            if (!builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(keyRingPath))
            {
                throw new InvalidOperationException(
                    "DataProtection:KeyRingPath must be configured outside Development.");
            }

            if (!string.IsNullOrWhiteSpace(keyRingPath))
            {
                dataProtectionBuilder.PersistKeysToFileSystem(new DirectoryInfo(keyRingPath));
            }

            builder.Services.AddSingleton<IWebhookSecretProtector, DataProtectionWebhookSecretProtector>();
            builder.Services.AddScoped<IWorkspaceService, WorkspaceService>();
            builder.Services.AddScoped<ITeamService, TeamService>();
            builder.Services.AddScoped<IProjectService, ProjectService>();
           
            builder.Services.AddScoped<IBoardService, BoardService>();
            builder.Services.AddScoped<ITaskService, TaskService>();
           builder.Services.AddScoped<ITaskAssignmentService,TaskAssignmentService>();
            // Database Configuration
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(
                    builder.Configuration.GetConnectionString("DefaultConnection")
                ));

            // ASP.NET Core Identity Configuration
            builder.Services
                .AddIdentity<ApplicationUser, IdentityRole>(options =>
                {
                    options.Password.RequiredLength = 8;
                    options.Password.RequireDigit = true;
                    options.Password.RequireLowercase = true;
                    options.Password.RequireUppercase = true;
                    options.Password.RequireNonAlphanumeric = false;
                    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
                    options.Lockout.MaxFailedAccessAttempts = 5;
                    options.Lockout.AllowedForNewUsers = true;
                })
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();

            builder.Services.ConfigureApplicationCookie(options =>
            {
                options.LoginPath = "/Account/Login";
                options.SlidingExpiration = true;
            });
            // Add MVC services
            builder.Services.AddControllersWithViews();

            var app = builder.Build();

            // Configure HTTP request pipeline
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapStaticAssets();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}")
                .WithStaticAssets();

            app.Run();
        }
    }
}
