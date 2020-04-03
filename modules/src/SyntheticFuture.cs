using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Optimizer_Trade.Processors
{
    static class SyntheticFutureInput
    {
        private static Dictionary<string, double[]> syntheticFuture_input = new Dictionary<string, double[]>();
        public static double getSyntheticFuture(string name, int type)
        {
            switch (type)
            {
                case 0:
                    return syntheticFuture_input[name][0];
                default:
                    return syntheticFuture_input[name][1];
            }
        }
        public static void AddKey(string name)
        {
            if (!syntheticFuture_input.ContainsKey(name))
                syntheticFuture_input.Add(name, new double[2]);
        }
        public static event Action<string> OnAdding;
        public static void AddValue(string name,double value1, double value2)
        {
            syntheticFuture_input[name][0] = value1;
            syntheticFuture_input[name][1] = value2;
            OnAdding?.Invoke(name);
        }
    }

    class SyntheticFuture : ProcessorBase
    {
        public int Call
        {
            get { return call; }
        }
        public int Put
        {
            get { return put; }
        }
        List<IProcessorInput> inputs;
        ILogger logger;
        int call, put, type;
        string inp_name;//, inp_name_2;
        double strike;
        protected override void FillDefaultParameters(Dictionary<string, string> p)
        {
            p["Call"] = "0";
            p["Put"] = "1";
            p["Strike"] = 1.0f.ToString();
            p["Type"] = "0";
            p["input_name"] = "SyntheticFuture";
            //p["input_name_2"] = "new_SyntheticFuture";
        }
        public override string[] Comments()
        {
            return new string[] {"Позиция опциона Call в input-ах","Позиция опциона Put в input-ах", "Страйк по опционам",
                                    "Флаг что определяет вывод функции на график(0-аск, иначе-бид)", "Имя колекции для сохранения Бид/Аск синтетика"};
        }
        public override bool Initialize(List<IProcessorInput> inputs, string parameters, ILogger logger)
        {
            this.logger = logger;
            ParametersParser parser = CreateParser(parameters);
            call = parser.GetInt("Call");
            put = parser.GetInt("Put");
            if (call < 0 || call >= inputs.Count) return false;
            if (put < 0 || put >= inputs.Count) return false;
            strike = parser.GetDouble("Strike");
            type = parser.GetInt("Type");
            inp_name = parser.GetString("input_name");
            //inp_name_2 = parser.GetString("input_name_2");
           /* if(inp_name_2 == inp_name)
            {
                inp_name_2 += "_n";
            }*/
            this.inputs = inputs;
            SyntheticFutureInput.AddKey(inp_name);
            //SyntheticFutureInput.AddKey(inp_name_2);
            return true;
        }
        public override double Process(DateTime time)
        {
            //if(inputs[call].)
            double syntheticFuture = 0.0;
            double syntheticFuture_ask = strike + inputs[call].Ask - inputs[put].Ask;
            double syntheticFuture_bid = strike + inputs[call].Bid - inputs[put].Bid;
            //double syntheticFuture_ask_reverse = strike + inputs[call].Ask - inputs[put].Bid;
            //double syntheticFuture_bid_reverse = strike + inputs[call].Bid - inputs[put].Ask;
            SyntheticFutureInput.AddValue(inp_name, syntheticFuture_ask, syntheticFuture_bid);
            //SyntheticFutureInput.AddValue(inp_name_2, syntheticFuture_ask_reverse, syntheticFuture_bid_reverse);
            switch (type)
            { 
                case 0:
                    syntheticFuture = syntheticFuture_ask;
                    break;
                default:
                    syntheticFuture = syntheticFuture_bid;
                    break;
            }
            return syntheticFuture;
        }
        public override void Deinitialize()
        {
        }
    }
}
