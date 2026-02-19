using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Classes;
using ProjectDefense.Data;
using Microsoft.EntityFrameworkCore;

namespace ProjectDefense.Pages.Rooms
{
    public class CreateModel : PageModel
    {
        private readonly ProjectDefense.Data.ApplicationDbContext _context;

        public CreateModel(ProjectDefense.Data.ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult OnGet()
        {
            return Page();
        }

        [BindProperty]
        public Room Room { get; set; }
        

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            bool roomExists = await _context.Rooms.AnyAsync(
                r => r.Name == Room.Name && r.RoomNumber == Room.RoomNumber);

            if (roomExists)
            {
                ModelState.AddModelError(string.Empty, "A room with this name and number already exists.");
                return Page();
            }

            _context.Rooms.Add(Room);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }
    }
}