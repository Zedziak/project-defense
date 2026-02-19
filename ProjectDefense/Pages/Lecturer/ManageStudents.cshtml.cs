using Classes;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ProjectDefense.Pages.Lecturer
{
    public class ManageStudentsModel : PageModel
    {
        private readonly UserManager<User> _userManager;

        public ManageStudentsModel(UserManager<User> userManager)
        {
            _userManager = userManager;
        }

        public List<User> Students { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        public async Task OnGetAsync()
        {
            var lecturerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var allStudents = await _userManager.GetUsersInRoleAsync("Student");
            Students = allStudents.OrderBy(s => s.UserName).ToList();
        }

        public async Task<IActionResult> OnPostBanAsync(string userId)
        {
            return await SetBanStatus(userId, true);
        }

        public async Task<IActionResult> OnPostUnbanAsync(string userId)
        {
            return await SetBanStatus(userId, false);
        }

        private async Task<IActionResult> SetBanStatus(string userId, bool isBanned)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound();
            }

            user.IsBanned = isBanned;
            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                StatusMessage = $"User {user.UserName} has been {(isBanned ? "banned" : "unbanned")}.";
            }
            else
            {
                StatusMessage = $"Error updating user status.";
            }

            return RedirectToPage();
        }
    }
}