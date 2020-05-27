using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ILCookDomesticStatusCalEventJob.Exceptions
{
    public class BaseCustomException : Exception
    {
        public BaseCustomException(string error) : base(error)
        {
            CustomMessage = error;
        }

        public string CustomMessage { get; set; }
    }
}
