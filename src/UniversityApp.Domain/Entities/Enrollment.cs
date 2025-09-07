using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UniversityApp.Domain.Entities
{
    public class Enrollment
    {
        public int Id { get; set; }

        public int StudentId { get; set; }
        public Student? Student { get; set; }

        public int CourseId { get; set; }
        public Course? Course { get; set; }

        public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;
    }
}
