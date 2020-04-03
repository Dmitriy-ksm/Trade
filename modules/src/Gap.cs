using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Optimizer_Trade.Processors
{
    
    public class Gap : ProcessorBase
    {
        double[] x;
        int pos;
        double sumx;
        bool ready;
        int Fut;
        double[] spread_col_call;
        double[] spread_col_put;
        int spread_count_put, spread_count_call;
        bool filt_spread;
        int Put;
        int Call;
        int Strike;
        double gap;
        double koef;
        object locker = new object();
        double midle_opt_price_put, midle_opt_price_call;
        DateTime time_prev_spred_put;
        DateTime time_prev_spred_call;
        DateTime time_prev_gap;
        List<IProcessorInput> inputs;
        ILogger logger;
        protected override void FillDefaultParameters(Dictionary<string, string> p)
        {
            p["Future"] = "0";
            p["Put"] = "1";
            p["Call"] = "2";
            p["Strike"] = "1350";
            p["period"] = "25";
            p["SpreadFilt"] = "1";
            p["MidPricePutPeriod"] = "25";
            p["MidPriceCallPeriod"] = "25";
            p["SpreadKoef"] = 1.6f.ToString();
        }
        public override string[] Comments()
        {
            return new string[] {"Позиция Фьюча в input-ах","Позиция Put в input-ах", "Позиция Call в input-ах",
                                    "Страйк опционов", "Период усредненния", "Флаг нужна ли фильтрация по спреду(1-да, иначе-нет)"
                                    , "Период MidPrice для опциона Put", "Период MidPrice для опциона Call", "Максимальное отклонение спреда" };
        }
        public override bool Initialize(List<IProcessorInput> inputs, string parameters, ILogger logger)
        {
            this.logger = logger;
            ParametersParser parser = CreateParser(parameters);
            Strike = parser.GetInt("Strike");
            Fut = parser.GetInt("Future");
            Put = parser.GetInt("Put");
            Call = parser.GetInt("Call");
            if (Call < 0 || Call >= inputs.Count) return false;
            if (Fut < 0 || Fut >= inputs.Count) return false;
            if (Put < 0 || Put >= inputs.Count) return false;
            int period = parser.GetInt("period");
            x = new double[period];
            if (parser.GetInt("SpreadFilt") == 1)
                filt_spread = true;
            else
                filt_spread = false;
            ready = false;
            spread_col_call = new double[parser.GetInt("MidPricePutPeriod")];
            koef = parser.GetDouble("SpreadKoef");
            spread_col_put = new double[parser.GetInt("MidPriceCallPeriod")];
            spread_count_call = 0;
            spread_count_put = 0;
            midle_opt_price_call = 0;
            midle_opt_price_put = 0;
            pos = 0;
            sumx = 0;
            time_prev_gap = new DateTime();
            time_prev_spred_put = new DateTime();
            time_prev_spred_call = new DateTime();
            this.inputs = inputs;
            return true;
        }

        public override double Process(DateTime time)
        {
            bool flag = true;
            double mean;
            double spread, spread_midle, spread_howmuch;
            if (filt_spread)
            {
                if (GapWorker.spread_worker(ref spread_col_call, ref spread_count_call, koef, inputs[Call], out spread, out spread_midle, out spread_howmuch))
                {
                    //logger.LogEvent(time, "Спред опционна Call " + spread + " вырос более чем в " + spread_howmuch + " раза от среднего спреда " + spread_midle);
                    flag = false;
                }
                if (GapWorker.spread_worker(ref spread_col_put, ref spread_count_put, koef, inputs[Put], out spread, out spread_midle, out spread_howmuch))
                {
                    //logger.LogEvent(time, "Спред опционна Put " + spread + " вырос более чем в " + spread_howmuch + " раза от среднего спреда " + spread_midle);
                    flag = false;
                }
            }
            if (flag)
            {
                midle_opt_price_put = (inputs[Put].Ask + inputs[Put].Bid) / 2;
                midle_opt_price_call = (inputs[Call].Ask + inputs[Call].Bid) / 2;
            }
            if (midle_opt_price_put > inputs[Put].Ask || midle_opt_price_put < inputs[Put].Bid)
            {
                midle_opt_price_put = (inputs[Put].Ask + inputs[Put].Bid) / 2;
                Array.Clear(spread_col_put, 0, spread_col_put.Length);
            }
            if (midle_opt_price_call > inputs[Call].Ask || midle_opt_price_call < inputs[Call].Bid)
            {
                midle_opt_price_call = (inputs[Call].Ask + inputs[Call].Bid) / 2;
                Array.Clear(spread_col_call, 0, spread_col_call.Length);
            }
            double pt = inputs[Fut].Point;
            if (pt <= 0) pt = 1.0;
            double gap = (Strike - midle_opt_price_put + midle_opt_price_call - (inputs[Fut].Bid + inputs[Fut].Ask) / 2 ) / pt;
            double midle_gap;
            bool flag2 = GapWorker.new_gap(ref x, ref pos, ref ready, gap, out midle_gap);
            gap -= midle_gap;
            flag = flag & flag2;
            ProcessorAction?.Invoke(new object[] { gap, inputs[Fut].Ask, inputs[Fut].Bid, time });
            return gap;
        }
        public override void Deinitialize()
        {
        }
    }
}
