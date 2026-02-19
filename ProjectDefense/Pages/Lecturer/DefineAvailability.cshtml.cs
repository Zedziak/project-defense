using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Classes;
using ProjectDefense.Data;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ProjectDefense.Pages.Lecturer
{
    public class DefineAvailabilityModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public DefineAvailabilityModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public class InputModel
        {
            [Display(Name = "Room")]
            [Required]
            public int RoomId { get; set; }

            [Display(Name = "Start Date")]
            [DataType(DataType.Date)]
            public DateTime StartDate { get; set; } = DateTime.Now.Date;

            [Display(Name = "End Date")]
            [DataType(DataType.Date)]
            public DateTime EndDate { get; set; } = DateTime.Now.Date;

            [Display(Name = "Start Time")]
            [DataType(DataType.Time)]
            public TimeOnly StartTime { get; set; } = new TimeOnly(8, 0);

            [Display(Name = "End Time")]
            [DataType(DataType.Time)]
            public TimeOnly EndTime { get; set; } = new TimeOnly(12, 0);

            [Display(Name = "Slot Duration (Minutes)")]
            [Range(1, 120)]
            public int SlotDurationMinutes { get; set; } = 15;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new InputModel();

        public SelectList RoomOptions { get; set; }

        public async Task LoadRoomOptionsAsync()
        {
            var rooms = await _context.Rooms
                .OrderBy(r => r.Name)
                .Select(r => new {
                    Id = r.Id,
                    DisplayText = $"{r.Name} ({r.RoomNumber})"
                })
                .ToListAsync();

            RoomOptions = new SelectList(rooms, "Id", "DisplayText", Input.RoomId);
        }

        public async Task<IActionResult> OnGetAsync()
        {
            await LoadRoomOptionsAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                await LoadRoomOptionsAsync();
                return Page();
            }

            if (Input.EndDate < Input.StartDate)
            {
                ModelState.AddModelError(nameof(Input.EndDate), "End date cannot be earlier than start date.");
                await LoadRoomOptionsAsync();
                return Page();
            }

            if (Input.EndTime <= Input.StartTime)
            {
                ModelState.AddModelError(nameof(Input.EndTime), "End time must be later than start time.");
                await LoadRoomOptionsAsync();
                return Page();
            }

            var newAvailability = new LecturerAvailability
            {
                LecturerId = User.FindFirstValue(ClaimTypes.NameIdentifier),
                RoomId = Input.RoomId,
                StartDate = Input.StartDate,
                EndDate = Input.EndDate,
                StartTime = Input.StartTime,
                EndTime = Input.EndTime,
                SlotDurationMinutes = Input.SlotDurationMinutes
            };

            var conflicting = await _context.LecturerAvailabilities
                .AnyAsync(la =>
                    la.LecturerId == newAvailability.LecturerId &&
                    la.RoomId == newAvailability.RoomId &&
                    la.EndDate >= newAvailability.StartDate &&
                    la.StartDate <= newAvailability.EndDate &&
                    la.EndTime > newAvailability.StartTime &&
                    la.StartTime < newAvailability.EndTime
                );

            if (conflicting)
            {
                ModelState.AddModelError(string.Empty, "Availability conflicts with an existing one.");
                await LoadRoomOptionsAsync();
                return Page();
            }

            _context.LecturerAvailabilities.Add(newAvailability);
            await _context.SaveChangesAsync();

            var slotsToAdd = new List<Reservation>();
            for (var day = newAvailability.StartDate.Date; day <= newAvailability.EndDate.Date; day = day.AddDays(1))
            {
                var slotTime = newAvailability.StartTime;
                while (slotTime.AddMinutes(newAvailability.SlotDurationMinutes) <= newAvailability.EndTime)
                {
                    var slotStart = day.Add(slotTime.ToTimeSpan());
                    var slotEnd = slotStart.AddMinutes(newAvailability.SlotDurationMinutes);

                    slotsToAdd.Add(new Reservation
                    {
                        StartTime = slotStart,
                        EndTime = slotEnd,
                        LecturerAvailabilityId = newAvailability.Id,
                        StudentId = null
                    });
                    slotTime = slotTime.AddMinutes(newAvailability.SlotDurationMinutes);
                }
            }

            await _context.Reservations.AddRangeAsync(slotsToAdd);
            await _context.SaveChangesAsync();

            return RedirectToPage("/Index");
        }
    }
}