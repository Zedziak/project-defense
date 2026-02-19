using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Classes;
using ProjectDefense.Data;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Text;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using System.IO;

namespace ProjectDefense.Pages.Lecturer
{
    public class ExportModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public ExportModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty(SupportsGet = true)]
        public int? RoomId { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime StartDate { get; set; } = DateTime.Now.Date;

        [BindProperty(SupportsGet = true)]
        public DateTime EndDate { get; set; } = DateTime.Now.Date.AddDays(7);

        [BindProperty]
        public string ExportFormat { get; set; }

        public SelectList RoomOptions { get; set; }
        private List<Reservation> FilteredReservations { get; set; }

        public async Task OnGetAsync()
        {
            await LoadRoomOptionsAsync();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            await LoadRoomOptionsAsync();
            if (!ModelState.IsValid || RoomId == null)
            {
                if (RoomId == null)
                {
                    ModelState.AddModelError("RoomId", "Please select a room.");
                }
                return Page();
            }

            if (EndDate < StartDate)
            {
                ModelState.AddModelError(nameof(EndDate), "End date cannot be earlier than start date.");
                return Page();
            }

            FilteredReservations = await _context.Reservations
                .Include(r => r.LecturerAvailability.Room)
                .Include(r => r.Student)
                .Where(r => r.LecturerAvailability.RoomId == RoomId &&
                            r.StartTime.Date >= StartDate &&
                            r.StartTime.Date <= EndDate)
                .OrderBy(r => r.StartTime)
                .ToListAsync();

            switch (ExportFormat)
            {
                case "txt":
                    return GenerateTxt();
                case "xlsx":
                    return GenerateXlsx();
                case "pdf":
                    return GeneratePdf();
                default:
                    return Page();
            }
        }

        private async Task LoadRoomOptionsAsync()
        {
            var rooms = await _context.Rooms
                .OrderBy(r => r.Name)
                .Select(r => new {
                    Id = r.Id,
                    DisplayText = $"{r.Name} ({r.RoomNumber})"
                })
                .ToListAsync();

            RoomOptions = new SelectList(rooms, "Id", "DisplayText", RoomId);
        }

        private FileResult GenerateTxt()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Reservation Report ({StartDate:d} - {EndDate:d})");
            sb.AppendLine("==================================================");

            foreach (var r in FilteredReservations)
            {
                string studentName = r.Student?.UserName ?? "--- FREE ---";
                string roomDisplay = $"{r.LecturerAvailability.Room.Name} ({r.LecturerAvailability.Room.RoomNumber})";
                sb.AppendLine($"{r.StartTime:g} - {r.EndTime:t} | {studentName} | Room: {roomDisplay}");
            }

            return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/plain", $"Report.txt");
        }

        private FileResult GenerateXlsx()
        {
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Reservations");

                worksheet.Cell(1, 1).Value = "Date";
                worksheet.Cell(1, 2).Value = "Start Time";
                worksheet.Cell(1, 3).Value = "End Time";
                worksheet.Cell(1, 4).Value = "Student";
                worksheet.Cell(1, 5).Value = "Room";

                worksheet.Row(1).Style.Font.Bold = true;

                int currentRow = 2;
                foreach (var r in FilteredReservations)
                {
                    worksheet.Cell(currentRow, 1).Value = r.StartTime.Date;
                    worksheet.Cell(currentRow, 2).Value = r.StartTime.TimeOfDay;
                    worksheet.Cell(currentRow, 3).Value = r.EndTime.TimeOfDay;
                    worksheet.Cell(currentRow, 4).Value = r.Student?.UserName ?? "--- FREE ---";
                    worksheet.Cell(currentRow, 5).Value = $"{r.LecturerAvailability.Room.Name} ({r.LecturerAvailability.Room.RoomNumber})";
                    currentRow++;
                }

                worksheet.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    var content = stream.ToArray();
                    return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Report.xlsx");
                }
            }
        }

        private FileResult GeneratePdf()
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(12));

                    page.Header()
                        .Text($"Reservation Report ({StartDate:d} - {EndDate:d})")
                        .SemiBold().FontSize(20).FontColor(Colors.Blue.Medium);

                    page.Content()
                        .PaddingTop(1, Unit.Centimetre)
                        .Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(3);
                                columns.RelativeColumn(4);
                                columns.RelativeColumn(3);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Text("Date / Time");
                                header.Cell().Text("Student");
                                header.Cell().Text("Room");
                            });

                            foreach (var r in FilteredReservations)
                            {
                                table.Cell().Text($"{r.StartTime:g} - {r.EndTime:t}");
                                table.Cell().Text(r.Student?.UserName ?? "--- FREE ---");
                                table.Cell().Text($"{r.LecturerAvailability.Room.Name} ({r.LecturerAvailability.Room.RoomNumber})");
                            }
                        });

                    page.Footer()
                        .AlignCenter()
                        .Text(x =>
                        {
                            x.Span("Page ");
                            x.CurrentPageNumber();
                        });
                });
            }).GeneratePdf();

            return File(document, "application/pdf", "Report.pdf");
        }
    }
}