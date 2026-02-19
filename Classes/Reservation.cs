using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Classes
{
    public class Reservation
    {
        public int Id { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

        public int LecturerAvailabilityId { get; set; }
        [ForeignKey("LecturerAvailabilityId")]
        public virtual LecturerAvailability LecturerAvailability { get; set; }

        public string? StudentId { get; set; }
        [ForeignKey("StudentId")]
        public virtual User? Student { get; set; }

        public bool IsBlocked { get; set; } = false;
    }
}
