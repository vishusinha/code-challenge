using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using challenge.Models;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using challenge.Data;

namespace challenge.Repositories
{
    public class CompensationRepository : ICompensationRepository
    {
        private readonly CompensationContext _compensationContext;
        private readonly ILogger<ICompensationRepository> _logger;

        public CompensationRepository(ILogger<ICompensationRepository> logger, CompensationContext compensationContext)
        {
            _compensationContext = compensationContext;
            _logger = logger;
        }

        public Compensation Add(Compensation compensation)
        {
            compensation.CompensationId = Guid.NewGuid().ToString();
            _compensationContext.Compensations.Add(compensation);
            return compensation;

        }

        public Compensation GetById(string id)
        {
            _compensationContext.Compensations.Load();
            return _compensationContext.Compensations.SingleOrDefault(e => e.CompensationId == id);
        }
        public Compensation GetByEmployeeId(string employeeId)
        {
            _compensationContext.Compensations.Load();
            return _compensationContext.Compensations.SingleOrDefault(e => e.EmployeeId == employeeId);
        }

        public Task SaveAsync()
        {
            return _compensationContext.SaveChangesAsync();
        }

        public Compensation Remove(Compensation comps)
        {
            return _compensationContext.Remove(comps).Entity;
        }
    }
}
