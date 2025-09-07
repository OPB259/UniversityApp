using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UniversityApp.Domain.Entities
{
    public class Student
    {
        public int Id { get; set; }
        public string Name { get; set; } = default!;
        public string Email { get; set; } = default!;

        public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    }
}
