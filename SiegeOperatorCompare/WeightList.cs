using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SiegeOperatorCompare
{
    class WeightList
    {
        public double Minimum { get; set; } = 0.15;
        private Dictionary<string, double> weights;

        public WeightList()
        {
            weights = new Dictionary<string, double>();
        }

        public double GetWeight(string op)
        {
            if (weights.TryGetValue(op, out var w))
                return w;

            return Minimum;
        }

        public void Load(string file)
        {
            if (!File.Exists(file))
                throw new FileNotFoundException();

            string[] parts = File.ReadAllLines(file);
            foreach(string p in parts)
            {
                string[] subp = p.Split('=');
                if (string.IsNullOrEmpty(subp[0]) || subp.Length == 1)
                    continue;

                if (double.TryParse(subp[1], out var weight))
                    weights[subp[0]] = weight;
            }
        }
    }
}
