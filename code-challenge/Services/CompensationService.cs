using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using challenge.Models;
using Microsoft.Extensions.Logging;
using challenge.Repositories;

namespace challenge.Services
{
    public class CompensationService : ICompensationService
    {
        private readonly ICompensationRepository _compensationRepository;
        private readonly ILogger<CompensationService> _logger;

        public CompensationService(ILogger<CompensationService> logger, ICompensationRepository compensationRepository)
        {
            _compensationRepository = compensationRepository;
            _logger = logger;
        }


        /// <summary>
        ///  Create Compensation Service for Endpoint
        /// </summary>
        /// <param name="compensation"></param>
        /// <returns>Compensation</returns>
        public Compensation Create(Compensation compensation)
        {
            if(compensation != null)
            {
                //check duplacate
                var extComps = _compensationRepository.GetByEmployeeId(compensation.EmployeeId);
                if (extComps != null)
                {
                    Replace(extComps, compensation);
                }
                else
                {
                    _compensationRepository.Add(compensation);
                    _compensationRepository.SaveAsync().Wait();
                }
            }

            return compensation;
        }

        public Compensation GetById(string id)
        {
            if(!String.IsNullOrEmpty(id))
            {
                return _compensationRepository.GetById(id);
            }

            return null;
        }
        public Compensation GetByEmployeeId(string id)
        {
            if (!String.IsNullOrEmpty(id))
            {
                return _compensationRepository.GetByEmployeeId(id);
            }

            return null;
        }
        /// <summary>
        ///  If the Employee Exixts and create method is called then it replaces the data
        /// </summary>
        /// <param name="originalCompensation"></param>
        /// <param name="newCompensation"></param>
        /// <returns>Compensation</returns>
        public Compensation Replace(Compensation originalCompensation, Compensation newCompensation)
        {
            if(originalCompensation != null)
            {
               _compensationRepository.Remove(originalCompensation);
                if (newCompensation != null)
                {
                    // ensure the original has been removed, otherwise EF will complain another entity w/ same id already exists
                    _compensationRepository.SaveAsync().Wait();

                    _compensationRepository.Add(newCompensation);
                    // overwrite the new id with previous employee id
                    newCompensation.CompensationId = originalCompensation.CompensationId;
                }
                _compensationRepository.SaveAsync().Wait();
            }

            return newCompensation;
        }
    }
}
