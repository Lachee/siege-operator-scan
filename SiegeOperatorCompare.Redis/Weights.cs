using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SiegeOperatorCompare.Redis
{
    class Weights
    {
        public double Minimum { get; set; } = 0.5;
        private Dictionary<string, double> weights;

        public Weights()
        {
            weights = new Dictionary<string, double>();
        }

        public bool Contains(string op) => weights.ContainsKey(op);
        public void Set(string op, double value) => weights[op] = value;
        public void Add(string op, double value) => weights.Add(op, value);
        public void Add(string op) => weights.Add(op, Minimum);

        public double GetWeight(string op)
        {
            if (weights.TryGetValue(op, out var w))
                return w;

            return Minimum;
        }

        public void Save(string file)
        {
            //Prepare the builder
            StringBuilder content = new StringBuilder();

            //Build the weights
            foreach(var kp in weights)
                content.Append(kp.Key).Append('=').Append(kp.Value).Append('\n');

            //Save the file
            File.WriteAllText(file, content.ToString(), Encoding.UTF8);
        }

        public bool Load(string file)
        {
            if (!File.Exists(file))
                return false;

            string[] parts = File.ReadAllLines(file);
            foreach(string p in parts)
            {
                string[] subp = p.Split('=');
                if (string.IsNullOrEmpty(subp[0]) || subp.Length == 1)
                    continue;

                if (double.TryParse(subp[1], out var weight))
                    weights[subp[0]] = weight;
            }

            return true;
        }
    }
}
