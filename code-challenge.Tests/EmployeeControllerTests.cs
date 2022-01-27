using challenge.Controllers;
using challenge.Data;
using challenge.Models;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using code_challenge.Tests.Integration.Extensions;

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using code_challenge.Tests.Integration.Helpers;
using System.Text;

namespace code_challenge.Tests.Integration
{
    [TestClass]
    public class EmployeeControllerTests
    {
        private static HttpClient _httpClient;
        private static TestServer _testServer;

        [ClassInitialize]
        public static void InitializeClass(TestContext context)
        {
            _testServer = new TestServer(WebHost.CreateDefaultBuilder()
                .UseStartup<TestServerStartup>()
                .UseEnvironment("Development"));

            _httpClient = _testServer.CreateClient();
        }

        [ClassCleanup]
        public static void CleanUpTest()
        {
            _httpClient.Dispose();
            _testServer.Dispose();
        }

        [TestMethod]
        /// <summary>
        /// To test the Create Compensation REST endpoint 
        /// Compensation CREATE
        ///* HTTP Method: POST
        ///* URL: localhost:8080/api/employee/Compensation/Create
        ///* PAYLOAD: Compensation
        ///* RESPONSE: Compensation
        /// </summary>


        public void CreateCompensation_Returns_Created()
        {
            // Arrange
            var compensation = new Compensation()
            {   CompensationId = "testcompid",
                EmployeeId = "16a596ae-edd3-4847-99fe-c4518e82c86f",
                Salary = 6000,
                EffectiveDate = Convert.ToDateTime("10-05-2021"),
                
            };

            var requestContent = new JsonSerialization().ToJson(compensation);
            Console.WriteLine("response is : " + requestContent);
            // Execute
            var postRequestTask = _httpClient.PostAsync("api/employee/compensation/create",
               new StringContent(requestContent, Encoding.UTF8, "application/json"));
            var response = postRequestTask.Result;
            Console.WriteLine("response create is : " + response);
            // Assert
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);

            var newCompensation = response.DeserializeContent<Compensation>();
            Assert.IsNotNull(newCompensation.EmployeeId);
            Assert.AreEqual(newCompensation.Salary, newCompensation.Salary);
            Assert.AreEqual(newCompensation.EffectiveDate, newCompensation.EffectiveDate);
             
        }
        [TestMethod]
        public void CreateEmployee_Returns_Created()
        {
            // Arrange
            var employee = new Employee()
            {
                Department = "Complaints",
                FirstName = "Debbie",
                LastName = "Downer",
                Position = "Receiver",
            };

            var requestContent = new JsonSerialization().ToJson(employee);

            // Execute
            var postRequestTask = _httpClient.PostAsync("api/employee",
               new StringContent(requestContent, Encoding.UTF8, "application/json"));
            var response = postRequestTask.Result;

            // Assert
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);

            var newEmployee = response.DeserializeContent<Employee>();
            Assert.IsNotNull(newEmployee.EmployeeId);
            Assert.AreEqual(employee.FirstName, newEmployee.FirstName);
            Assert.AreEqual(employee.LastName, newEmployee.LastName);
            Assert.AreEqual(employee.Department, newEmployee.Department);
            Assert.AreEqual(employee.Position, newEmployee.Position);
        }
        
        
      
        [TestMethod]
        /// <summary>
        /// GET Reporting Structure to get NumberOfReports
        ///  * HTTP Method: GET 
        ///  * Param : EmployeeID
        ///  * URL:  http://localhost:8080/api/employee/Structure/16a596ae-edd3-4847-99fe-c4518e82c86f
        ///  * RESPONSE: ReportingStructure
        /// </summary>

        public void GetEmployeeStructureById_Returns_Ok()
        {
            // Arrange
            var employeeId = "16a596ae-edd3-4847-99fe-c4518e82c86f";
            var expectedFirstName = "John";
            var expectedLastName = "Lennon";
            var expectednumberOfReports = 4;

            // Execute
            var getRequestTask = _httpClient.GetAsync($"api/employee/Structure/{employeeId}");
            var response = getRequestTask.Result;

            // Assert
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            var reportingStructure = response.DeserializeContent<ReportingStructure>();
            Assert.AreEqual(expectedFirstName, reportingStructure.Employee.FirstName);
            Assert.AreEqual(expectedLastName, reportingStructure.Employee.LastName);
            Assert.AreEqual(expectednumberOfReports, reportingStructure.numberOfReports);
        }
       
        
        [TestMethod]
        /// <summary>
        /// To Get Compensation details Salary and Effectivedate 
        /// Compensation GET
        ///* Param : EmployeeID
        ///* HTTP Method: GET 
        ///* URL: http://localhost:8080/api/employee/Compensation/get/16a596ae-edd3-4847-99fe-c4518e82c86f
        ///*RESPONSE: Compensation
        /// </summary>

        public void GetCompensationById_Returns_Ok()
        {
            // Arrange
            var employeeId = "16a596ae-edd3-4847-99fe-c4518e82c86f";
            var expectedFirstName = "John";
            var expectedLastName = "Lennon";
            var expectedsalary = 6000;


            // Execute
            var getRequestTask = _httpClient.GetAsync($"api/employee/compensation/get/{employeeId}");
            var response = getRequestTask.Result;

            // Assert
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            var employeeCompensation = response.DeserializeContent<EmployeeCompensation>();
            Assert.AreEqual(expectedFirstName, employeeCompensation.Employee.FirstName);
            Assert.AreEqual(expectedLastName, employeeCompensation.Employee.LastName);
            Assert.AreEqual(expectedsalary, employeeCompensation.Compensation.Salary);
        }

        [TestMethod]
        public void GetEmployeeById_Returns_Ok()
        {
            // Arrange
            var employeeId = "16a596ae-edd3-4847-99fe-c4518e82c86f";
            var expectedFirstName = "John";
            var expectedLastName = "Lennon";

            // Execute
            var getRequestTask = _httpClient.GetAsync($"api/employee/{employeeId}");
            var response = getRequestTask.Result;

            // Assert
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            var employee = response.DeserializeContent<Employee>();
            Assert.AreEqual(expectedFirstName, employee.FirstName);
            Assert.AreEqual(expectedLastName, employee.LastName);
        }

        [TestMethod]
        public void UpdateEmployee_Returns_Ok()
        {
            // Arrange
            var employee = new Employee()
            {
                EmployeeId = "03aa1462-ffa9-4978-901b-7c001562cf6f",
                Department = "Engineering",
                FirstName = "Pete",
                LastName = "Best",
                Position = "Developer VI",
            };
            var requestContent = new JsonSerialization().ToJson(employee);

            // Execute
            var putRequestTask = _httpClient.PutAsync($"api/employee/{employee.EmployeeId}",
               new StringContent(requestContent, Encoding.UTF8, "application/json"));
            var putResponse = putRequestTask.Result;
            
            // Assert
            Assert.AreEqual(HttpStatusCode.OK, putResponse.StatusCode);
            var newEmployee = putResponse.DeserializeContent<Employee>();

            Assert.AreEqual(employee.FirstName, newEmployee.FirstName);
            Assert.AreEqual(employee.LastName, newEmployee.LastName);
        }

        [TestMethod]
        public void UpdateEmployee_Returns_NotFound()
        {
            // Arrange
            var employee = new Employee()
            {
                EmployeeId = "Invalid_Id",
                Department = "Music",
                FirstName = "Sunny",
                LastName = "Bono",
                Position = "Singer/Song Writer",
            };
            var requestContent = new JsonSerialization().ToJson(employee);

            // Execute
            var postRequestTask = _httpClient.PutAsync($"api/employee/{employee.EmployeeId}",
               new StringContent(requestContent, Encoding.UTF8, "application/json"));
            var response = postRequestTask.Result;

            // Assert
            Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}
