using Microsoft.AspNetCore.Identity;

namespace Classes
{
    public class User : IdentityUser
    {
        public bool IsBanned { get; set; } = false;
    }
}
