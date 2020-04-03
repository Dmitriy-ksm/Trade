using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Optimizer_Trade.Processors
{
    enum OpenPosition
    {
        NONE = 0,
        LONG = 2,
        SHORT = 1
    };
    enum LastZigZagValue
    {
        NONE = 0,
        MIN = -1,
        MAX = 1
    };
    struct ProbaStruct
    {
        public double GapValue, price;
        public DateTime time;
    }
    struct Position
    {
        public int index;
        public double value;
    }
    struct LastPosition
    {
        public double price;
        public double gap;
    }
    static class GapWorker
    {
        public static bool spread_worker(ref double[] spread_col, ref int spread_count, double koef, IProcessorInput Opt, out double spread, out double midle_spread, out double howmuch)
        {
            spread = 0;
            midle_spread = 0;
            howmuch = 0;
            spread = ((Opt.Ask - Opt.Bid) / Opt.Point);
            double spread_sum = 0;
            int counter = 0;
            foreach (var spreads in spread_col)
            {
                if (spreads != 0)
                {
                    spread_sum += spreads;
                    counter++;
                }
            }
            midle_spread = spread_sum / counter;
            howmuch = spread / midle_spread;
            if (howmuch > koef)
                return true;
            spread_col[spread_count] = spread;
            spread_count++;
            if (spread_count == spread_col.Length)
                spread_count = 0;
            return false;
        }

        public static double gap_mean(ref double sumx, ref double[] x, ref int pos, ref bool ready, double gap)
        {
            sumx += gap - x[pos];
            x[pos] = gap;
            pos++;
            if (pos == x.Length)
            {
                pos = 0;
                ready = true;
            }
            double mean = ready ? sumx / x.Length : sumx / pos;
            return mean;
        }

        public static bool new_gap(ref double[] x, ref int pos, ref bool ready, double gap, out double new_gap)
        {
            new_gap = 0;
            x[pos] = gap;
            pos++;
            if (pos == x.Length)
            {
                pos = 0;
                ready = true;
            }
            if (ready)
            {
                double sum = 0;
                foreach (double var in x)
                    sum += var;
                new_gap = sum / x.Length;
                return true;
            }
            else
            {
                double sum = 0;
                int counter = 0;
                foreach (var var in x)
                {
                    if (var != 0)
                    {
                        sum += var;
                        counter++;
                    }
                }
                new_gap = sum / counter;
                return false;
            }
        }
    }
    static class FunTradeWorker
    {
        static void AddPositionVolumeAssist(ref LastPosition last_pos, double gap, double price, List<Position> Position, 
                                                        object locker, ModelsAT.BrokerMT tradeTerminal, ArbitrageTradeWF.OrderType order_type,
                                                        ILogger logger, DateTime time, string name, bool LoggNeeded)
        {
            last_pos.gap = gap;
            last_pos.price = price;
            int ind = Position.Last().index;
            lock (locker)
            {
                tradeTerminal.OpenOrder(order_type, ind);
            }
            Position.Add(new Processors.Position() { index = ind, value = price});
            if (LoggNeeded)
                logger.LogEvent(time, name + " Увеличиваем объем позиции " + ind + " req price : " + price);
        }
        public static void AddPositionVolume_Old(bool LongShort, ref LastPosition last_pos, double gap, double price, List<Position> Position,
                                                        ref bool expand_position, object locker, ModelsAT.BrokerMT tradeTerminal, ILogger logger, 
                                                        DateTime time, string name, bool LoggNeeded)
        {
            if(expand_position)
            {
                if (!LongShort)
                {
                    if (gap > last_pos.gap && price > last_pos.price)
                    {
                        AddPositionVolumeAssist(ref last_pos, gap, price, Position, locker, tradeTerminal, ArbitrageTradeWF.OrderType.MKT_Sell, logger, time, name, LoggNeeded);
                        expand_position = false;
                    }
                }
                else
                {
                    if (gap < last_pos.gap && price < last_pos.price)
                    {
                        AddPositionVolumeAssist(ref last_pos, gap, price, Position, locker, tradeTerminal, ArbitrageTradeWF.OrderType.MKT_Buy, logger, time, name, LoggNeeded);
                        expand_position = false;
                    }
                }
            }
        }

        public static void AddPositionVolume_New(bool LongShort, List<ProbaStruct> MainZigZagCollection, List<ProbaStruct> OfZigZagCollection, double gap, double price, 
                                                        double ratio_exp, List<Position> Position, ref bool expand_position, object locker, ModelsAT.BrokerMT tradeTerminal,
                                                        ILogger logger, DateTime time, string name, bool LoggNeeded)
        {
            if(expand_position)
            {
                double last_knee = Math.Abs(MainZigZagCollection.Last().GapValue - OfZigZagCollection.Last().GapValue);
                double temp_knee = Math.Abs(OfZigZagCollection.Last().GapValue - gap);
                double r_exp = temp_knee / last_knee;
                if (r_exp >= ratio_exp)
                {
                    if (LoggNeeded)
                        logger.LogEvent(time, name + " Размер ratio_O " + r_exp);
                    int ind = Position.Last().index;
                    lock (locker)
                    {
                        if (!LongShort)
                            tradeTerminal.OpenOrder(ArbitrageTradeWF.OrderType.MKT_Sell, ind);
                        else
                            tradeTerminal.OpenOrder(ArbitrageTradeWF.OrderType.MKT_Buy, ind);
                    }
                    expand_position = false;
                    if (LoggNeeded)
                        logger.LogEvent(time, name + " Увеличиваем объем позиции " + ind + " req price : " + price);
                }
            }
        }
        public static bool ClosePosition(bool fast_exit, List<Position> Position, ModelsAT.BrokerMT tradeTerminal, bool LongShort, double price, ref double all_profit, 
                                                 object locker, ILogger logger, DateTime time, string name, bool LoggNeeded)
        {
            if (Position.Count > 0)
            {
                lock (locker)
                {
                    if (LoggNeeded && fast_exit)
                        logger.LogEvent(time, name + " Аварийный выход с рынка");
                    tradeTerminal.ClosePosition(Position[0].index);
                    foreach (var item in Position)
                    {
                        string order;
                        double profit = 0;
                        if (!LongShort)
                        { 
                            profit = item.value - price;
                            order = "buy";
                        }
                        else
                        { 
                            profit = price - item.value;
                            order = "sell";
                        }
                        all_profit += profit;
                        
                        if (LoggNeeded)
                            logger.LogEvent(time, name + " send " + order + " order " + item.index + " req price : " + price + " Profit: " + profit);
                    }
                }
                Position.Clear();
                return true;
            }
            return false;
        }
        static void DivergenceAssist(ProbaStruct NewZigZagValue, ref int divergence_counter, ILogger logger, string name, bool LoggNeeded, string message)
        {
            divergence_counter++;
            //if (LoggNeeded)
            //    logger.LogEvent(NewZigZagValue.time, message);
        }
        public static void DivergenceCheck(bool MaxMin,List<ProbaStruct> ZigZagCollection, ProbaStruct NewZigZagValue, ref int divergence_counter, 
                                                ref bool pos_flag, ILogger logger, string name, DateTime time, bool LoggNeeded, bool OpenClose)
        {
            if (!MaxMin)
            {
                if (NewZigZagValue.GapValue > ZigZagCollection[0].GapValue && NewZigZagValue.price > ZigZagCollection.Last().price)
                {
                    string type = "закрытия Long";
                    if(OpenClose)
                    {
                        type = "открытия Short";
                    }
                    string message = String.Format("{0} Дивергенция {2} {1} ", name, divergence_counter + 1, type);
                    DivergenceAssist(NewZigZagValue, ref divergence_counter, logger, name, LoggNeeded, message);
                }
                else
                {
                    divergence_counter = 0;
                    pos_flag = false;
                }
            }
            else
            {
                if (NewZigZagValue.GapValue < ZigZagCollection[0].GapValue && NewZigZagValue.price < ZigZagCollection.Last().price)
                {
                    string type = "закрытия Short";
                    if (OpenClose)
                    {
                        type = "открытия Long";
                    }
                    string message = String.Format("{0} Дивергенция {2} {1} ", name, divergence_counter + 1, type);
                    DivergenceAssist(NewZigZagValue, ref divergence_counter, logger, name, LoggNeeded, message);
                }
                else
                {
                    divergence_counter = 0;
                    pos_flag = false;
                }
            }
        }
        public static void CorvergenceCheck(List<ProbaStruct> ZigZagCollectionMain, List<ProbaStruct> ZigZagCollectionOff, 
                                                ProbaStruct NewZigZagValue, double converGap, double converPrice,
                                                DateTime time, ref ProbaStruct CorvergenceHandle, ref int convergence_counter, 
                                                double gap, double RatioConver, ref List<Position> Position, object locker, 
                                                ModelsAT.BrokerMT tradeTerminal, ref OpenPosition openpos, ILogger logger, string name, bool LoggNeeded)
        {
            if (ZigZagCollectionMain.Last().GapValue < NewZigZagValue.GapValue && ZigZagCollectionMain.Last().price < NewZigZagValue.price)
            {
                convergence_counter++;
                CorvergenceHandle.GapValue = converGap;
                CorvergenceHandle.price = converPrice;
                CorvergenceHandle.time = time;
                if (ZigZagCollectionOff.Count != 0)
                {
                    double R = Math.Abs(NewZigZagValue.GapValue - gap);
                    double R1 = Math.Abs(NewZigZagValue.GapValue - ZigZagCollectionOff.Last().GapValue);
                    double Ratio = R / R1;
                   /* if (LoggNeeded)
                        logger.LogEvent(time, name + " Ratio: " + Ratio);
                    if (RatioConver <= Ratio)
                    {
                        if (Position.Count > 0)
                        {
                            lock (locker)
                            {
                                logger.LogEvent(time, name + " Аварийный выход с рынка");
                                tradeTerminal.ClosePosition(Position[0].index);
                                openpos = OpenPosition.NONE;
                            }
                            Position.Clear();
                        }
                    }*/
                }
                //if (LoggNeeded)
                //    logger.LogEvent(time, name + " Медвежья конвергенция ");
            }
            else
                convergence_counter = 0;
        }
    }
}
