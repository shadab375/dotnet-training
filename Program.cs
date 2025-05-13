using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Claims;
using Microsoft.OpenApi.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();

// Configure Swagger with JWT authentication
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Todo API", Version = "v1" });
    
    // Define the JWT security scheme
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token in the text input below.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Add CORS services
builder.Services.AddCors();

// Add JWT Authentication
builder.Services.AddAuthentication().AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            builder.Configuration["JwtSettings:SecretKey"] ?? "YourTemporarySecretKeyNeedsToBeAtLeast32Chars")),
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});
builder.Services.AddAuthorization();

// Add EF Core with SQLite
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=todo.db";
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString));

// Register services
builder.Services.AddScoped<ITodoRepository, TodoRepositoryEF>();
builder.Services.AddScoped<IUserRepository, UserRepositoryEF>();

var app = builder.Build();

// Apply migrations at startup
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.Database.Migrate();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

// Configure CORS
app.UseCors(options => options
    .AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader());

// Define Todo endpoints
app.MapGet("/api/todos", GetAllTodos)
    .RequireAuthorization()
    .WithName("GetAllTodos");

app.MapGet("/api/todos/{id}", GetTodo)
    .RequireAuthorization()
    .WithName("GetTodo");

app.MapPost("/api/todos", CreateTodo)
    .RequireAuthorization()
    .WithName("CreateTodo");

app.MapPut("/api/todos/{id}", UpdateTodo)
    .RequireAuthorization()
    .WithName("UpdateTodo");

app.MapDelete("/api/todos/{id}", DeleteTodo)
    .RequireAuthorization()
    .WithName("DeleteTodo");

// Auth endpoints
app.MapPost("/api/auth/register", Register)
    .WithName("Register");

app.MapPost("/api/auth/login", Login)
    .WithName("Login");

// Keep the weatherforecast endpoint for now
var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

// CRUD Operation Handlers
IResult GetAllTodos(ITodoRepository repository, HttpContext context)
{
    var userId = GetUserIdFromToken(context);
    if (userId == null) return Results.Unauthorized();
    
    return Results.Ok(repository.GetAllByUserId(userId));
}

IResult GetTodo(string id, ITodoRepository repository, HttpContext context)
{
    var userId = GetUserIdFromToken(context);
    if (userId == null) return Results.Unauthorized();
    
    var todo = repository.GetById(id);
    if (todo == null) return Results.NotFound();
    if (todo.UserId != userId) return Results.Forbid();
    
    return Results.Ok(todo);
}

IResult CreateTodo(Todo todo, ITodoRepository repository, HttpContext context)
{
    var userId = GetUserIdFromToken(context);
    if (userId == null) return Results.Unauthorized();
    
    todo.UserId = userId;
    todo.Id = Guid.NewGuid().ToString();
    repository.Add(todo);
    
    return Results.Created($"/api/todos/{todo.Id}", todo);
}

IResult UpdateTodo(string id, Todo todo, ITodoRepository repository, HttpContext context)
{
    var userId = GetUserIdFromToken(context);
    if (userId == null) return Results.Unauthorized();
    
    var existingTodo = repository.GetById(id);
    if (existingTodo == null) return Results.NotFound();
    if (existingTodo.UserId != userId) return Results.Forbid();
    
    todo.Id = id;
    todo.UserId = userId;
    repository.Update(todo);
    
    return Results.Ok(todo);
}

IResult DeleteTodo(string id, ITodoRepository repository, HttpContext context)
{
    var userId = GetUserIdFromToken(context);
    if (userId == null) return Results.Unauthorized();
    
    var todo = repository.GetById(id);
    if (todo == null) return Results.NotFound();
    if (todo.UserId != userId) return Results.Forbid();
    
    repository.Delete(id);
    
    return Results.NoContent();
}

// Auth Handlers
IResult Register(RegisterRequest request, IUserRepository userRepository, IConfiguration config)
{
    if (userRepository.GetByEmail(request.Email) != null)
    {
        return Results.BadRequest(new { message = "User already exists" });
    }

    var hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);
    var user = new User
    {
        Id = Guid.NewGuid().ToString(),
        Name = request.Name,
        Email = request.Email,
        Password = hashedPassword
    };

    userRepository.Add(user);

    var token = GenerateJwtToken(user, config);

    return Results.Ok(new
    {
        user = new { id = user.Id, name = user.Name, email = user.Email },
        token
    });
}

IResult Login(LoginRequest request, IUserRepository userRepository, IConfiguration config)
{
    var user = userRepository.GetByEmail(request.Email);
    if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
    {
        return Results.BadRequest(new { message = "Invalid credentials" });
    }

    var token = GenerateJwtToken(user, config);

    return Results.Ok(new
    {
        user = new { id = user.Id, name = user.Name, email = user.Email },
        token
    });
}

// Helper methods
string? GetUserIdFromToken(HttpContext context)
{
    return context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
}

string GenerateJwtToken(User user, IConfiguration config)
{
    var secretKey = config["JwtSettings:SecretKey"] ?? "YourTemporarySecretKeyNeedsToBeAtLeast32Chars";
    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
    var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

    var claims = new List<Claim>
    {
        new Claim(ClaimTypes.NameIdentifier, user.Id),
        new Claim(ClaimTypes.Email, user.Email)
    };

    var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
        claims: claims,
        expires: DateTime.Now.AddDays(1),
        signingCredentials: creds
    );

    return new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);
}

app.Run();

// Model classes
public class Todo
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public bool Completed { get; set; } = false;
    public string UserId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Deadline { get; set; }
    public string Priority { get; set; } = "Medium";
}

public class User
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class RegisterRequest
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

// DbContext for Entity Framework
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Todo> Todos { get; set; }
    public DbSet<User> Users { get; set; }
}

// Repository interfaces
public interface ITodoRepository
{
    IEnumerable<Todo> GetAllByUserId(string userId);
    Todo? GetById(string id);
    void Add(Todo todo);
    void Update(Todo todo);
    void Delete(string id);
}

public interface IUserRepository
{
    User? GetById(string id);
    User? GetByEmail(string email);
    void Add(User user);
}

// EntityFramework implementations
public class TodoRepositoryEF : ITodoRepository
{
    private readonly AppDbContext _context;

    public TodoRepositoryEF(AppDbContext context)
    {
        _context = context;
    }

    public IEnumerable<Todo> GetAllByUserId(string userId)
    {
        return _context.Todos.Where(t => t.UserId == userId).ToList();
    }

    public Todo? GetById(string id)
    {
        return _context.Todos.FirstOrDefault(t => t.Id == id);
    }

    public void Add(Todo todo)
    {
        _context.Todos.Add(todo);
        _context.SaveChanges();
    }

    public void Update(Todo todo)
    {
        var existingTodo = _context.Todos.Find(todo.Id);
        if (existingTodo != null)
        {
            existingTodo.Title = todo.Title;
            existingTodo.Description = todo.Description;
            existingTodo.Completed = todo.Completed;
            existingTodo.Deadline = todo.Deadline;
            existingTodo.Priority = todo.Priority;
            existingTodo.UserId = todo.UserId;
            
            _context.SaveChanges();
        }
    }

    public void Delete(string id)
    {
        var todo = _context.Todos.Find(id);
        if (todo != null)
        {
            _context.Todos.Remove(todo);
            _context.SaveChanges();
        }
    }
}

public class UserRepositoryEF : IUserRepository
{
    private readonly AppDbContext _context;

    public UserRepositoryEF(AppDbContext context)
    {
        _context = context;
    }

    public User? GetById(string id)
    {
        return _context.Users.FirstOrDefault(u => u.Id == id);
    }

    public User? GetByEmail(string email)
    {
        return _context.Users.FirstOrDefault(u => u.Email == email);
    }

    public void Add(User user)
    {
        _context.Users.Add(user);
        _context.SaveChanges();
    }
}

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
