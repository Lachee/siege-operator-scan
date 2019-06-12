using System;
using System.Collections.Generic;
using System.Text;

namespace SiegeOperatorCompare.Redis
{
    class Operator
    {
        public string Name { get; set; }
        public double Match { get; set; }
        public double MinimumMatch { get; set; }

        public Operator(string name, double minimum)
        {
            Name = name;
            Match = 0;
            MinimumMatch = minimum;
        }
    }
}
