using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using challenge.Services;
using challenge.Models;
using Newtonsoft.Json.Linq;

namespace challenge.Controllers
{
    [Route("api/employee")]
    public class EmployeeController : Controller
    {
        private readonly ILogger _logger;
        private readonly IEmployeeService _employeeService;
        private readonly ICompensationService _compensationService;

        public EmployeeController(ILogger<EmployeeController> logger, IEmployeeService employeeService,ICompensationService compensationService)
        {
            _logger = logger;
            _employeeService = employeeService;
            _compensationService = compensationService;
        }

        [HttpPost]
        public IActionResult CreateEmployee([FromBody] Employee employee)
        {
            _logger.LogDebug($"Received employee create request for '{employee.FirstName} {employee.LastName}'");

            _employeeService.Create(employee);

            return CreatedAtRoute("getEmployeeById", new { id = employee.EmployeeId }, employee);
        }

        [HttpGet("{id}", Name = "getEmployeeById")]
        public IActionResult GetEmployeeById(String id)
        {
            _logger.LogDebug($"Received employee get request for '{id}'");

            var employee = _employeeService.GetById(id);

            if (employee == null)
                return NotFound();

            return Ok(employee);
        }

        /// <summary>
        /// Gets Employee Reporting Stucture with NumberofReports  
        /// </summary>
        /// <param name="id" Or EmployeeID></param>
        /// <returns>NewEmployee with NumberofReports property updated</returns>
        /// 

        [HttpGet("structure/{id}", Name = "getEmployeeStructureById")]
        public IActionResult GetEmployeeStructureById(String id)
        {
            _logger.LogDebug($"Received employee structure get request for '{id}'");

            var employee = _employeeService.GetById(id);
            var epmStrucutre = new ReportingStructure();
            if(employee!=null)
            {
                epmStrucutre.Employee = employee;

                dynamic rec = new JObject();
                rec.id = employee.EmployeeId;
                rec.status = "false";
                int count = 0;
                //var allrec = new JArray() as dynamic;
                //allrec.Add(rec);
                var it = new List<JObject>();
                it.Add(rec);
                int fl = 0;
            
                var it1 = new List<JObject>();
            repeat:
                if (fl==1)
                {
                    fl = 0;
                    it = it1;
                }
              

                foreach(var item in it.ToList())
                {
                    var emp = _employeeService.GetById(item["id"].ToString());
                    count +=(emp.DirectReports == null ? 0 : emp.DirectReports.Count());
                    if(emp.DirectReports!=null)
                    {
                        foreach(var eump in emp.DirectReports)
                        {
                            dynamic rrec = new JObject();
                            rrec.id = eump.EmployeeId;
                            rrec.status = "false";
                            it1.Add(rrec);
                                fl = 1;
                        }
                    }

                    var cu = it1.Where(y => y["id"].ToString() == item["id"].ToString()).FirstOrDefault();
                    if(cu!=null)
                    {
                        it1.Remove(cu);
                    }
                    it1 = it1.ToList();

                }
                if(fl==1)
                {
                    goto repeat;
                }
                epmStrucutre.numberOfReports = count;
                return Ok(epmStrucutre);
            }

            if (employee == null)
                return NotFound();

            return Ok(employee);
        }
        [HttpPut("{id}")]
        public IActionResult ReplaceEmployee(String id, [FromBody]Employee newEmployee)
        {
            _logger.LogDebug($"Recieved employee update request for '{id}'");

            var existingEmployee = _employeeService.GetById(id);
            if (existingEmployee == null)
                return NotFound();

            _employeeService.Replace(existingEmployee, newEmployee);

            return Ok(newEmployee);
        }



        #region Compensation end points
        [HttpGet("compensation/get/{id}", Name = "getEmployeeCompensationById")]
        public IActionResult GetCompensationByEmployeeId(String id)
        {
            _logger.LogDebug($"Received employee Compensation get request for '{id}'");

            var employee = _employeeService.GetById(id);
            if(employee==null)
                return NotFound("Employee Record Not Found");

            var compensastionModel = new EmployeeCompensation();
            var comps =_compensationService.GetByEmployeeId(id);
            if (comps == null)
                return NotFound("Compensation Record Not Found");

            
                compensastionModel.Employee = employee;
                compensastionModel.Compensation = comps;
                return Ok(compensastionModel);              

         }

        [HttpPost("compensation/create", Name = "createCompensation")]
        public IActionResult CreateCompensation([FromBody] Compensation compensation)
        {
            _logger.LogDebug($"Received employee compensation create request for '{compensation.EmployeeId}'");

            var existingRecord = _employeeService.GetById(compensation.EmployeeId);
            if (existingRecord == null)
                return NotFound("Employee Id not found");

            _compensationService.Create(compensation);

           // return Ok();
            return CreatedAtRoute("getEmployeeCompensationById", new { id = compensation.EmployeeId }, compensation);
        }

        #endregion
    }
}
