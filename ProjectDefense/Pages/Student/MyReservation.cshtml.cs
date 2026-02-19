using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Classes;
using ProjectDefense.Data;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ProjectDefense.Pages.Student
{
    public class MyReservationModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public MyReservationModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public Reservation CurrentReservation { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        public async Task OnGetAsync()
        {
            var studentId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            CurrentReservation = await _context.Reservations
                .Include(r => r.LecturerAvailability.Room)
                .Include(r => r.LecturerAvailability.Lecturer)
                .FirstOrDefaultAsync(r => r.StudentId == studentId &&
                                          r.StartTime > DateTime.Now &&
                                          !r.IsBlocked);
        }

        public async Task<IActionResult> OnPostCancelAsync(int reservationId)
        {
            var studentId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var reservation = await _context.Reservations.FindAsync(reservationId);

            if (reservation == null)
            {
                return NotFound();
            }

            if (reservation.StudentId != studentId)
            {
                return Forbid();
            }

            if (reservation.StartTime <= DateTime.Now)
            {
                StatusMessage = "Error: You cannot cancel a reservation that has already started or finished.";
                return RedirectToPage();
            }

            if (reservation.IsBlocked)
            {
                StatusMessage = "Error: This reservation cannot be cancelled as it has been blocked by the lecturer.";
                return RedirectToPage();
            }

            reservation.StudentId = null;
            await _context.SaveChangesAsync();

            StatusMessage = "Your reservation has been successfully canceled.";

            return RedirectToPage("./AvailableSlots");
        }
    }
}