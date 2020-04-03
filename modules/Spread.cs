using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Optimizer_Trade.Processors
{
    public class Spread : ProcessorBase
    {
        double[] x;
        bool ready;
        int pos;
        double sumx;
        int in1;
        int in2;
        List<IProcessorInput> inputs;
        ILogger logger;
        public Spread()
        {
        }
        protected override void FillDefaultParameters(Dictionary<string, string> p)
        {
            p["in1"] = "0";
            p["in2"] = "1";
            p["period"] = "50";
        }
        public override string[] Comments()
        {
            return new string[] {"","", "" };
        }
        public override bool Initialize(List<IProcessorInput> inputs, string parameters, ILogger logger)
        {
            this.logger = logger;
            ParametersParser parser = CreateParser(parameters);
            int period = parser.GetInt("period");
            if (period <= 10) return false;
            in1 = parser.GetInt("in1");
            in2 = parser.GetInt("in2");
            if (in1 < 0 || in1 >= inputs.Count) return false;
            if (in2 < 0 || in2 >= inputs.Count) return false;
            this.inputs = inputs;
            x = new double[period];
            ready = false;
            pos = 0;
            return true;
        }
        public override double Process(DateTime time)
        {
            if (inputs.Count >= 2)
            {
                double delta = inputs[in1].Bid / inputs[in2].Bid;
                sumx += delta - x[pos];
                x[pos] = delta;
                pos++;
                if (pos == x.Length)
                {
                    pos = 0;
                    ready = true;
                }
                if (ready)
                {
                    double meanX = sumx / x.Length;
                    if (meanX!=0)
                    {
                        return 100.0 * ((delta / meanX)-1.0);
                    }
                }
            }
            return double.NaN;
        }
        public override void Deinitialize()
        {
        }
    }
}
