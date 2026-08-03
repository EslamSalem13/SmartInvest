using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using SmartInvest.API.Common;
using SmartInvest.Application.Common.Mappings;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Layer registrations (Onion composition root)
// ---------------------------------------------------------------------------
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

    //Auto MApper
    builder.Services.AddAutoMapper(options =>
    options.AddProfile<PlansAndPrograms>());

    #region Repositories
    builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
    builder.Services.AddScoped<IPlanRepo, PlanRepo>();
    builder.Services.AddScoped<IProgramRepo, ProgramRepo>();
    #endregion

    #region Services
    builder.Services.AddScoped<IPlanService, PlanService>();
    builder.Services.AddScoped<IProgramService, ProgramService>();
    builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
#endregion


// ---------------------------------------------------------------------------
// JWT Authentication
// ---------------------------------------------------------------------------
var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()!;
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddSingleton<IAuthorizationHandler, SuperAdminAuthorizationHandler>();
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();

// ---------------------------------------------------------------------------
// CORS (Angular client)
// ---------------------------------------------------------------------------
const string CorsPolicy = "SmartInvestCors";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                     ?? ["http://localhost:4200"];

builder.Services.AddCors(options =>
    options.AddPolicy(CorsPolicy, policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()));

// ---------------------------------------------------------------------------
// MVC + Swagger
// ---------------------------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "SmartInvest API", Version = "v1" });

    var jwtScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token (without the 'Bearer' prefix)."
    };

    options.AddSecurityDefinition("Bearer", jwtScheme);
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

var app = builder.Build();


app.UseMiddleware<SmartInvest.API.Middleware.ExceptionHandlingMiddleware>();

using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

    // بذور الأدوار: تُنشأ مرة واحدة، مع تعبئة الأدوار القديمة (الموجودة قبل نظام الصلاحيات)
    // التي ليس لها اسم معروض أو صلاحيات. لا نكتب فوق دور عدّله السوبر أدمن.
    async Task SeedRoleAsync(string name, string displayName, bool isSystem, string[] permissions)
    {
        var role = await roleManager.FindByNameAsync(name);

        if (role == null)
        {
            role = new ApplicationRole(name) { DisplayName = displayName, IsSystem = isSystem };
            var created = await roleManager.CreateAsync(role);
            if (!created.Succeeded)
            {
                return;
            }
        }
        else if (string.IsNullOrWhiteSpace(role.DisplayName))
        {
            // دور قديم من قبل الترقية — نعطيه الاسم المعروض ونوعه.
            role.DisplayName = displayName;
            role.IsSystem = isSystem;
            await roleManager.UpdateAsync(role);
        }

        // الصلاحيات تُضاف فقط لو الدور لا يملك أي صلاحية بعد.
        var existing = await roleManager.GetClaimsAsync(role);
        if (existing.Any(c => c.Type == Permissions.ClaimType))
        {
            return;
        }

        foreach (var permission in permissions)
        {
            await roleManager.AddClaimAsync(role, new Claim(Permissions.ClaimType, permission));
        }
    }

    // السوبر أدمن: دور نظام، يملك كل الصلاحيات ضمنيًا (لا يحتاج claims).
    await SeedRoleAsync(Roles.SuperAdmin, "سوبر أدمن", isSystem: true, permissions: []);

    // إدارة التخطيط — بدون أي صلاحيات على الإدارة المالية.
    await SeedRoleAsync(Roles.PlanningManager, "مدير التخطيط", isSystem: false,
    [
        Permissions.DashboardView,
        Permissions.ProjectsView, Permissions.ProjectsCreate, Permissions.ProjectsEdit,
        Permissions.ProjectsDelete, Permissions.ProjectsApprove,
        Permissions.PlansView, Permissions.PlansManage,
        Permissions.FinancialYearsManage,
        Permissions.ContractorsView, Permissions.ContractorsManage,
        Permissions.AgenciesView, Permissions.AgenciesManage,
        Permissions.UsersView, Permissions.UsersManage,
    ]);

    await SeedRoleAsync(Roles.PlanningEmployee, "موظف تخطيط", isSystem: false,
    [
        Permissions.ProjectsView, Permissions.ProjectsCreate, Permissions.ProjectsEdit,
        Permissions.PlansView,
        Permissions.ContractorsView,
        Permissions.AgenciesView,
    ]);

    // الإدارة المالية — قسم مستقل، بدون صلاحيات التخطيط.
    await SeedRoleAsync(Roles.FinancialManager, "مدير الإدارة المالية", isSystem: false,
    [
        Permissions.FinancialView, Permissions.FinancialUpload, Permissions.FinancialComplete,
        Permissions.MemosView, Permissions.MemosManage,
    ]);

    await SeedRoleAsync(Roles.FinancialEmployee, "موظف الإدارة المالية", isSystem: false,
    [
        Permissions.FinancialView, Permissions.FinancialUpload,
        Permissions.MemosView,
    ]);

    const string adminEmail = "admin@gmail.com";
    var adminUser = await userManager.FindByEmailAsync(adminEmail);

    if (adminUser == null)
    {
        adminUser = new ApplicationUser
        {
            UserName = "admin",
            Email = adminEmail,
            FullName = "مدير النظام",
            EmailConfirmed = true,
            IsActive = true
        };

        var createResult = await userManager.CreateAsync(adminUser, "Admin@123");
        if (createResult.Succeeded)
        {
            await userManager.AddToRoleAsync(adminUser, Roles.PlanningManager);
        }
    }

    const string superAdminEmail = "superadmin@gmail.com";
    var superAdminUser = await userManager.FindByEmailAsync(superAdminEmail);

    if (superAdminUser == null)
    {
        superAdminUser = new ApplicationUser
        {
            UserName = "superadmin",
            Email = superAdminEmail,
            FullName = "السوبر أدمن",
            EmailConfirmed = true,
            IsActive = true
        };

        var createResult = await userManager.CreateAsync(superAdminUser, "SuperAdmin@123");
        if (createResult.Succeeded)
        {
            await userManager.AddToRoleAsync(superAdminUser, Roles.SuperAdmin);
        }
    }

    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await LookupSeeder.SeedAsync(dbContext);
}


// ---------------------------------------------------------------------------
// HTTP pipeline
// ---------------------------------------------------------------------------
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors(CorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
