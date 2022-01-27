using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace challenge.Models
{
    public class Compensation
    {
        public String CompensationId { get; set; }
        public String EmployeeId { get; set; }
        public Double Salary { get; set; }
        public DateTime EffectiveDate { get; set; }
       
    }
    public class EmployeeCompensation
    {
        public Employee Employee { get; set; }
        public Compensation Compensation { get; set; }

    }
}
