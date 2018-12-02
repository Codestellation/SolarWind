using System;

namespace Codestellation.SolarWind
{
    public class SolarWindException : Exception
    {
        public SolarWindException(string message) : base(message)
        {
        }
    }
}