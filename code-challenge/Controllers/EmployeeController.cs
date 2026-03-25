using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using challenge.Services;
using challenge.Models;

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
            if (employee == null)
                return NotFound();

            var reportingStructure = new ReportingStructure
            {
                Employee = employee,
                numberOfReports = _employeeService.GetNumberOfReports(id)
            };

            return Ok(reportingStructure);
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
