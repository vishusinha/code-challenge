using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace challenge.Models
{
    public class ReportingStructure
    {
        public Employee Employee { get; set; }
        public int numberOfReports { get; set; }

    }
    public class EmployeeCompensation
    {
        public Employee Employee { get; set; }
        public Compensation Compensation { get; set; }

    }
}
