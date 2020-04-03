using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Optimizer_Trade.Processors
{
    class SmallZigZag : ZigZag
    {
        enum DealType
        {
            NONE = 0,
            SHORT = 1,
            LONG = 2
        };
        static string DealTypeString(DealType type)
        {
            if (type == DealType.SHORT)
                return "Short";
            else
                return "Long";
        }
        ILogger logger;
        List<IProcessorInput> inputs;
        int min_node, gap_position, Fut;
        string file_name_profit, file_name_lose;
        double fut_bid, fut_ask;
        double max_gap, min_gap;
        double next_peak;
        bool ready;
        int type;
        DateTime[] dealtime;
        int dealcounter;
        double[] dealprice;
        DealType dealtype;
        public static string logPath;
        double global_profit, global_lose;
        DateTime peak_time;
        void Deal(string logfile, DateTime time_open, DateTime time_close, double deal_price, double[] deal_prices, double global_profit, DealType type)
        {
            using (StreamWriter sw = new StreamWriter(logfile, true))
            {
                string message = String.Format(" Сделка {0} {1} / {2} : Профит {3} (открыто по цене {4} / закрыто по цене {5}. Общий профит: {6})",
                                                DealTypeString(dealtype), time_open.ToString("dd.MM hh:mm:ss:fff"), time_close.ToString("dd.MM hh:mm:ss:fff"),
                                                deal_price, deal_prices[0], deal_prices[1], global_profit);
                sw.WriteLine(message);
            }
        }
        public override void Deinitialize()
        {
        }
        public override bool Initialize(List<IProcessorInput> inputs, string parameters, ILogger logger)
        {
            dealprice = new double[2];
            dealtime = new DateTime[2];
            dealcounter = 0;
            this.inputs = inputs;
            this.logger = logger;
            ParametersParser parser = CreateParser(parameters);
            min_node = parser.GetInt("MinValue");
            gap_position = parser.GetInt("GapPosition");
            if (gap_position < 0 || gap_position > Model.Project.Outputs.Count) return false;
            Fut = parser.GetInt("Future");
            if (Fut < 0 || Fut >= inputs.Count) return false;
            Value = 0;
            type = 0;
            next_peak = 0;
            max_gap = 0;
            min_gap = 0;
            ready = false;
            peak_time = new DateTime();
            string name = parser.GetString("Name");
            file_name_profit = Path.Combine(logPath, name + "_profit_log.txt");
            file_name_lose = Path.Combine(logPath, name + "_lose_log.txt");
            return true; 
        }
        public override double Process(DateTime time)
        {
            double temp_value = Model.Project.Outputs[gap_position].Value;
            if (type == 0)
            {
                if (temp_value > min_node)
                {
                    next_peak = temp_value;
                    peak_time = time;
                    fut_ask = inputs[Fut].Ask;
                    type = 1;
                }
                if (temp_value < -min_node)
                {

                    next_peak = temp_value;
                        peak_time = time;
                        fut_bid = inputs[Fut].Bid;
                        type = 2;
                    
                }
            }
            if (type == 1)
            {
                if (temp_value > next_peak)
                {
                    next_peak = temp_value;
                    peak_time = time;
                    fut_ask = inputs[Fut].Ask;
                }
                if (next_peak - temp_value >= min_node) 
                {
                    Value = next_peak;
                    ValueTime = peak_time;
                    IsNewValue = true;
                    ProcessorAction?.Invoke(new object[] { "Max", Value, fut_ask, ValueTime, inputs[Fut].Ask });
                    //next_value_to_change = min_node;
                    dealprice[dealcounter] = fut_ask;
                    dealtime[dealcounter] = ValueTime;
                    dealcounter++;
                    if (dealtype == DealType.NONE)
                        dealtype = DealType.LONG;
                    next_peak = temp_value;
                    peak_time = time;
                    type = 2;
                    fut_bid = inputs[Fut].Bid;
                }
            }
            if (type == 2)
            {
                if (temp_value < next_peak)
                {
                    next_peak = temp_value;
                        peak_time = time;
                        fut_bid = inputs[Fut].Bid;
                }
                if (temp_value - next_peak >= min_node)
                {
                    Value = next_peak;
                    ValueTime = peak_time;
                    IsNewValue = true;
                    ProcessorAction?.Invoke(new object[] { "Min", Value, fut_bid, ValueTime, inputs[Fut].Bid });
                    //next_value_to_change = -min_node;
                    //next_value_to_change = min_node;
                    dealprice[dealcounter] = fut_bid;
                    dealtime[dealcounter] = ValueTime;
                    dealcounter++;
                    if (dealtype == DealType.NONE)
                        dealtype = DealType.SHORT;
                    next_peak = temp_value;
                    peak_time = time;
                    type = 1;
                    fut_ask = inputs[Fut].Ask;
                }
            }
            if (dealcounter == 2)
            {
                double profit = 0;
                if (dealtype == DealType.SHORT)
                    profit = dealprice[0] - dealprice[1];
                else
                    profit = dealprice[1] - dealprice[0];
                if (profit > 0)
                {
                    global_profit += profit;
                    Deal(file_name_profit, dealtime[0], dealtime[1], profit, dealprice, global_profit, dealtype);
                }
                else
                {
                    global_lose += profit;
                    Deal(file_name_lose, dealtime[0], dealtime[1], profit, dealprice, global_lose, dealtype);
                }
                dealtime[0] = dealtime[1];
                dealprice[0] = dealprice[1];
                dealcounter = 1;
                if (dealtype == DealType.LONG)
                    dealtype = DealType.SHORT;
                else
                    dealtype = DealType.LONG;
            }
            return Value;
        }
        protected override void FillDefaultParameters(Dictionary<string, string> p)
        {
            p["GapPosition"] = "0";
            p["MinValue"] = "500";
            p["Future"] = "0";
            p["Name"] = "SmallZipZapDeals";
        }
        public override string[] Comments()
        {
            return new string[] { "Позиция гэпа в output-ах", "Минимальное значение колена", "Позиция Фьюча в input-ах", "Название файла для лога" };
        }
    }
}
