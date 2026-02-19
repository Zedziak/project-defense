using Azure.Core;
using Classes;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using ProjectDefense.Data;
using ProjectDefense.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<User>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireLecturerRole",
         policy => policy.RequireRole("Lecturer"));
    options.AddPolicy("RequireStudentRole",
         policy => policy.RequireRole("Student"));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "ProjectDefense API", Version = "v1" });
});

var supportedCultures = new[] { "en-US", "pl-PL" };
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.SetDefaultCulture(supportedCultures[0]);
    options.AddSupportedCultures(supportedCultures);
    options.AddSupportedUICultures(supportedCultures);
});

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Rooms", "RequireLecturerRole");
    options.Conventions.AuthorizeFolder("/Lecturer", "RequireLecturerRole");
    options.Conventions.AuthorizeFolder("/Student", "RequireStudentRole");
}).AddViewLocalization()
.AddDataAnnotationsLocalization();

builder.Services.AddTransient<IEmailSender, EmailSender>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<User>>();

        string[] roleNames = { "Lecturer", "Student" };
        foreach (var roleName in roleNames)
        {
            var roleExist = await roleManager.RoleExistsAsync(roleName);
            if (!roleExist)
            {
                await roleManager.CreateAsync(new IdentityRole(roleName));
            }
        }

        async Task CreateUserAsync(string email, string password, string role)
        {
            var user = await userManager.FindByNameAsync(email);
            if (user == null)
            {
                user = new User
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true
                };
                var result = await userManager.CreateAsync(user, password);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(user, role);
                }
            }
        }

        await CreateUserAsync("lecturer1@test.com", "Password123!", "Lecturer");
        await CreateUserAsync("lecturer2@test.com", "Password123!", "Lecturer");

        await CreateUserAsync("student1@test.com", "Password123!", "Student");
        await CreateUserAsync("student2@test.com", "Password123!", "Student");
        await CreateUserAsync("student3@test.com", "Password123!", "Student");
        await CreateUserAsync("student4@test.com", "Password123!", "Student");

    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the DB.");
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();

    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "ProjectDefense API v1"));
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture(supportedCultures[0])
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);
app.UseRequestLocalization(localizationOptions);

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/rooms", async ([FromServices] ApplicationDbContext db) =>
{
    var rooms = await db.Rooms
        .Select(r => new RoomDto 
        {
            Id = r.Id,
            Name = r.Name,
            RoomNumber = r.RoomNumber
        })
        .ToListAsync();

    return Results.Ok(rooms); 
})
.WithName("GetRooms")
.WithTags("API");

app.MapGet("/api/slots/available", async ([FromServices] ApplicationDbContext db) =>
{
    var slots = await db.Reservations
        .Include(r => r.LecturerAvailability.Room)
        .Include(r => r.LecturerAvailability.Lecturer)
        .Where(r => r.StudentId == null && r.StartTime > DateTime.Now)
        .OrderBy(r => r.StartTime)
        .Select(r => new AvailableSlotDto
        {
            ReservationId = r.Id,
            StartTime = r.StartTime,
            EndTime = r.EndTime,
            LecturerName = r.LecturerAvailability.Lecturer.UserName,
            RoomName = r.LecturerAvailability.Room.Name
        })
        .ToListAsync();

    return Results.Ok(slots); 
})
.WithName("GetAvailableSlots")
.WithTags("API");


app.MapPost("/api/slots/{id}/book", async (
    int id,
    [FromBody] BookSlotRequestDto request,
    [FromServices] ApplicationDbContext db,
    [FromServices] IConfiguration config,
    HttpContext http
) => {
    var apyKeyFromConfig = config["ApiKey"];

    if (!http.Request.Headers.TryGetValue("X-Api-Key", out StringValues keyFromRequest) || keyFromRequest != apyKeyFromConfig)
    {
        return Results.Unauthorized();
    }

    var studentId = request.StudentId;

    var student = await db.Users.FindAsync(studentId);
    if (student == null)
    {
        return Results.NotFound(new { message = "Student with this ID not found." });
    }

    var hasBooking = await db.Reservations
        .AnyAsync(r => r.StudentId == studentId && r.StartTime > DateTime.Now);

    if (hasBooking)
    {
        return Results.Conflict(new { message = "Student already has an active reservation." });
    }

    var reservation = await db.Reservations.FindAsync(id);

    if (reservation == null || reservation.StudentId != null || reservation.StartTime <= DateTime.Now)
    {
        return Results.NotFound(new { message = "Slot is no longer available." });
    }

    reservation.StudentId = studentId;
    await db.SaveChangesAsync();

    return Results.Ok(new { message = "Slot booked successfully." });
})
.WithName("BookSlot")
.WithTags("API");

app.MapGet("/api/students", async ([FromServices] UserManager<User> userManager, [FromServices] IConfiguration config, HttpContext http) =>
{
    var apyKeyFromConfig = config["ApiKey"];
    if (!http.Request.Headers.TryGetValue("X-Api-Key", out StringValues keyFromRequest) || keyFromRequest != apyKeyFromConfig)
    {
        return Results.Unauthorized();
    }

    var students = await userManager.GetUsersInRoleAsync("Student");

    var studentDtos = students.Select(s => new StudentDto
    {
        Id = s.Id,
        UserName = s.UserName
    }).ToList();

    return Results.Ok(studentDtos);
})
.WithName("GetStudents")
.WithTags("API");

app.MapRazorPages();
app.Run();