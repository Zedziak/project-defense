using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Classes;
using ProjectDefense.Data;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ProjectDefense.Pages.Lecturer
{
    public class AllSlotsModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public AllSlotsModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [TempData]
        public string StatusMessage { get; set; }

        public List<Reservation> Reservations { get; set; }

        public class BlockPeriodInput
        {
            [Required]
            [Display(Name = "Block Start Date")]
            [DataType(DataType.Date)]
            public DateTime StartDate { get; set; } = DateTime.Now.Date;

            [Required]
            [Display(Name = "Block End Date")]
            [DataType(DataType.Date)]
            public DateTime EndDate { get; set; } = DateTime.Now.Date;
        }

        [BindProperty]
        public BlockPeriodInput BlockInput { get; set; } = new BlockPeriodInput();

        [BindProperty(SupportsGet = true)]
        public int? RebookId { get; set; }

        [BindProperty]
        public int NewReservationId { get; set; }

        public Reservation ReservationToMove { get; set; }
        public SelectList AvailableSlotsOptions { get; set; }

        public async Task OnGetAsync()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (RebookId.HasValue)
            {
                ReservationToMove = await _context.Reservations
                    .Include(r => r.Student)
                    .Include(r => r.LecturerAvailability.Room)
                    .FirstOrDefaultAsync(r => r.Id == RebookId.Value && r.LecturerAvailability.LecturerId == currentUserId);

                if (ReservationToMove != null)
                {
                    var availableSlots = await _context.Reservations
                        .Include(r => r.LecturerAvailability.Room)
                        .Where(r => r.LecturerAvailability.LecturerId == currentUserId &&
                                    r.StudentId == null &&
                                    !r.IsBlocked &&
                                    r.StartTime > DateTime.Now)
                        .OrderBy(r => r.StartTime)
                        .ToListAsync();

                    AvailableSlotsOptions = new SelectList(availableSlots.Select(s => new {
                        Id = s.Id,
                        DisplayText = $"{s.StartTime:g} - {s.EndTime:t} ({s.LecturerAvailability.Room.Name} - {s.LecturerAvailability.Room.RoomNumber})"
                    }), "Id", "DisplayText");
                }
            }

            Reservations = await _context.Reservations
                .Include(r => r.LecturerAvailability.Room)
                .Include(r => r.Student)
                .Where(r => r.LecturerAvailability.LecturerId == currentUserId)
                .OrderBy(r => r.StartTime)
                .ToListAsync();
        }

        public async Task<IActionResult> OnPostCancelAsync(int reservationId)
        {
            var reservation = await _context.Reservations
                .Include(r => r.LecturerAvailability)
                .FirstOrDefaultAsync(r => r.Id == reservationId);

            if (reservation == null) return NotFound();

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (reservation.LecturerAvailability.LecturerId != currentUserId) return Forbid();

            if (reservation.StartTime < DateTime.Now)
            {
                StatusMessage = "Error: You cannot cancel a reservation that is already in the past.";
                return RedirectToPage();
            }

            reservation.StudentId = null;
            await _context.SaveChangesAsync();
            StatusMessage = "Reservation successfully canceled. The slot is now free.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostBlockPeriodAsync()
        {
            if (!ModelState.IsValid)
            {
                await OnGetAsync();
                return Page();
            }

            if (BlockInput.EndDate < BlockInput.StartDate)
            {
                ModelState.AddModelError(nameof(BlockInput.EndDate), "End date cannot be earlier than start date.");
                await OnGetAsync();
                return Page();
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var endDateFull = BlockInput.EndDate.Date.AddDays(1).AddTicks(-1);

            var reservationsToBlock = await _context.Reservations
                .Include(r => r.LecturerAvailability)
                .Where(r => r.LecturerAvailability.LecturerId == currentUserId &&
                            r.StartTime >= BlockInput.StartDate &&
                            r.StartTime <= endDateFull &&
                            !r.IsBlocked)
                .ToListAsync();

            if (!reservationsToBlock.Any())
            {
                StatusMessage = "No active slots found in the selected period to block.";
                return RedirectToPage();
            }

            foreach (var reservation in reservationsToBlock)
            {
                reservation.IsBlocked = true;
                reservation.StudentId = null;
            }

            await _context.SaveChangesAsync();
            StatusMessage = $"Successfully blocked period: {reservationsToBlock.Count} slots were marked as blocked.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostRebookAsync()
        {
            if (NewReservationId == 0)
            {
                ModelState.AddModelError(nameof(NewReservationId), "Please select a new slot.");
                await OnGetAsync();
                return Page();
            }

            var oldReservation = await _context.Reservations.FindAsync(RebookId);
            var newReservation = await _context.Reservations.FindAsync(NewReservationId);

            if (oldReservation == null || newReservation == null || newReservation.StudentId != null)
            {
                StatusMessage = "Error: The target slot is no longer available.";
                return RedirectToPage();
            }

            newReservation.StudentId = oldReservation.StudentId;
            oldReservation.StudentId = null;

            await _context.SaveChangesAsync();
            StatusMessage = "Student has been successfully re-booked.";
            return RedirectToPage(new { RebookId = (int?)null });
        }
    }
}