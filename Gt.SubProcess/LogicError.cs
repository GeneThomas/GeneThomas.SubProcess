using System;

namespace Gt.SubProcess
{
    internal class LogicError: Exception
    {
        /// <summary>
        /// An exception that indicates a fault in the program
        /// use InvalidOperationException if fault is due
        /// to state of an object being wrong
        /// <param name="message"></param>
        /// </summary>
        public LogicError(string message) :
               base("Internal error: " + message)
        {
        }
    }
}

