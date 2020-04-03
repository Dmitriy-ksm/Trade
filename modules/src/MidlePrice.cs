using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Optimizer_Trade.Processors
{
    class MidlePrice : ProcessorBase
    {
        int Opt;
        List<IProcessorInput> inputs;
        double midle_opt_price;
        double[] spread_col;
        double koef;
        bool filt_spread;
        int spread_count;
        DateTime time_prev_spred;
        ILogger logger;
        protected override void FillDefaultParameters(Dictionary<string, string> p)
        {
            p["Opt"] = "0";
            p["period"] = "25";
            p["SpreadKoef"] = 1.6f.ToString();
            p["SpreadFilt"] = "1";
        }
        public override string[] Comments()
        {
            return new string[] {"Позиция input-а в input-ах", "Период усреднения", "Максимальное отклонение спреда", "Флаг нужна ли фильтрация по спреду(1-да, иначе-нет)" };
        }
        public override bool Initialize(List<IProcessorInput> inputs, string parameters, ILogger logger)
        {
            this.logger = logger;
            ParametersParser parser = CreateParser(parameters);
            //Strike = parser.GetInt("Strike");
            Opt = parser.GetInt("Opt");
            if (Opt < 0 || Opt >= inputs.Count) return false;
            int period = parser.GetInt("period");
            spread_col = new double[period];
            spread_count = 0;
            if (parser.GetInt("SpreadFilt") == 1)
                filt_spread = true;
            else
                filt_spread = false;
            midle_opt_price = 0;
            koef = parser.GetDouble("SpreadKoef");
            time_prev_spred = new DateTime();
            this.inputs = inputs;
            return true;
        }
        public override double Process(DateTime time)
        {
            bool flag = true;
            //double mean;
            double spread, spread_midle, spread_howmuch;
            if(filt_spread)
                if (GapWorker.spread_worker(/*time, ref time_prev_spred, */ref spread_col, ref spread_count, koef, inputs[Opt], out spread, out spread_midle, out spread_howmuch))
                        flag = false;
            if (flag)
                midle_opt_price = (inputs[Opt].Ask + inputs[Opt].Bid) / 2;
            if(midle_opt_price > inputs[Opt].Ask || midle_opt_price < inputs[Opt].Bid)
            {
                midle_opt_price = (inputs[Opt].Ask + inputs[Opt].Bid) / 2;
                Array.Clear(spread_col, 0, spread_col.Length);
            }
            //ProcessorAction?.Invoke(new object[] { flag, midle_opt_price });
            return midle_opt_price;
        }
        public override void Deinitialize()
        {
        }
    }
}
