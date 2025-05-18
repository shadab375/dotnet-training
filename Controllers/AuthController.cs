using Microsoft.AspNetCore.Mvc;
using Models;
using Repositories;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Controllers {
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IUserRepository _userRepository;
        private readonly IConfiguration _config;

        public AuthController(IUserRepository userRepository, IConfiguration config)
        {
            _userRepository = userRepository;
            _config = config;
        }

        [HttpPost("register")]
        public IActionResult Register([FromBody] RegisterRequest request)
        {
            if (_userRepository.GetByEmail(request.Email) != null)
            {
                return BadRequest(new { message = "User already exists" });
            }

            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);
            var user = new User
            {
                Id = Guid.NewGuid().ToString(),
                Name = request.Name,
                Email = request.Email,
                Password = hashedPassword
            };

            _userRepository.Add(user);
            var token = GenerateJwtToken(user);
            return Ok(new
            {
                user = new { id = user.Id, name = user.Name, email = user.Email },
                token
            });
        }

        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            var user = _userRepository.GetByEmail(request.Email);
            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
            {
                return BadRequest(new { message = "Invalid credentials" });
            }
            var token = GenerateJwtToken(user);
            return Ok(new
            {
                user = new { id = user.Id, name = user.Name, email = user.Email },
                token
            });
        }

        private string GenerateJwtToken(User user)
        {
            var secretKey = _config["JwtSettings:SecretKey"] ?? "YourTemporarySecretKeyNeedsToBeAtLeast32Chars";
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
    }
} 