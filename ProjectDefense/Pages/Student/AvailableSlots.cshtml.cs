using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Classes;
using ProjectDefense.Data;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ProjectDefense.Pages.Student
{
    public class AvailableSlotsModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public AvailableSlotsModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty(SupportsGet = true)]
        public int? ChangeId { get; set; }

        public List<Reservation> AvailableReservations { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var studentId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!ChangeId.HasValue)
            {
                var hasBooking = await _context.Reservations
                    .AnyAsync(r => r.StudentId == studentId &&
                                   r.StartTime > DateTime.Now &&
                                   !r.IsBlocked);

                if (hasBooking)
                {
                    StatusMessage = "You already have an active reservation. You must cancel it or change it.";
                    return RedirectToPage("./MyReservation");
                }
            }

            AvailableReservations = await _context.Reservations
                .Include(r => r.LecturerAvailability.Room)
                .Include(r => r.LecturerAvailability.Lecturer)
                .Where(r => r.StudentId == null &&
                            r.StartTime > DateTime.Now &&
                            !r.IsBlocked)
                .OrderBy(r => r.StartTime)
                .ToListAsync();

            if (ChangeId.HasValue)
            {
                StatusMessage = "Your old slot is still active. Please select a new slot to change to.";
            }

            return Page();
        }

        public async Task<IActionResult> OnPostBookAsync(int reservationId)
        {
            var studentId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var newReservation = await _context.Reservations.FindAsync(reservationId);

            if (newReservation == null ||
                newReservation.StudentId != null ||
                newReservation.IsBlocked ||
                newReservation.StartTime <= DateTime.Now)
            {
                StatusMessage = "Error: This slot is no longer available.";
                return RedirectToPage();
            }

            if (ChangeId.HasValue)
            {
                var oldReservation = await _context.Reservations
                    .FirstOrDefaultAsync(r => r.Id == ChangeId.Value && r.StudentId == studentId);

                if (oldReservation != null)
                {
                    oldReservation.StudentId = null;
                }
            }
            else
            {
                var hasBooking = await _context.Reservations
                    .AnyAsync(r => r.StudentId == studentId && r.StartTime > DateTime.Now && !r.IsBlocked);
                if (hasBooking)
                {
                    StatusMessage = "Error: You already have an active reservation.";
                    return RedirectToPage();
                }
            }

            newReservation.StudentId = studentId;
            await _context.SaveChangesAsync();

            StatusMessage = "Your reservation has been successfully updated.";
            return RedirectToPage("./MyReservation");
        }
    }
}