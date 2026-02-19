using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Classes
{
    public class Room
    {
        public int Id { get; set; }

        [Display(Name = "Room Name")]
        public string Name { get; set; }

        [Display(Name = "Room Number")]
        public string RoomNumber { get; set; }
    }
}
