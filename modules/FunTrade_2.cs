using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Optimizer_Trade.Processors
{
    class FunTrade_2 : ProcessorBase
    {
        Action ClosePositionStopTrade;
        public void CloseAll()
        {
            ClosePositionStopTrade.Invoke();
        }
        static public int ind = 0;
        ProcessorBase ZigZag, ZigZagSmall;
        List<ProbaStruct> MinValueBig, MaxValueBig;
        List<ProbaStruct> MinValueSmall, MaxValueSmall;
        ILogger logger;
        Position PositionShort;
        Position PositionLong;
        ProcessorBase Gap;
        int zigzagid,zigzagidsmall;
        int gappos;
        bool LoggNeeded;
        double ratio;
        double profit_to_close, lose_to_close;
        double all_profit;
        int Size;
        bool convergenceshort, convergencelong;
        bool divergenceshort, divergencelong;
        bool openshortcloselong, openlongcloseshort;
        bool longlock, shortlock;
        ProbaStruct ConvergenMin, ConvergenMax;
        ModelsAT.BrokerMT tradeTerminal;
        LastZigZagValue lastvalue;//-1 - Ищем максимум, 1 - Ищем минимум
        OpenPosition openpos;//0 - нету,1 - шорт, 2 - лонг
        double lvl_up_last, lvl_dwn_last;
        object locker = new object();
        string name;
        protected override void FillDefaultParameters(Dictionary<string, string> p)
        {
            p["Gap"] = "0";
            p["ZigZag_ID"] = "1";
            p["ZigZagSmall_ID"] = "2";
            p["Name"] = "Proba";
            p["tradeTerminalID"] = "0";
            p["ServerName"] = "server";
            p["IsReal"] = "0";
            p["Logg"] = "1";
            p["Ratio"] = 0.5f.ToString();
            p["size"] = "3";
            p["TakeProfit"] = "500";
            p["StopLoss"] = "500";
        }
        public override string[] Comments()
        {
            return new string[] { "Позиция Gap в output-ах", "Позиция ZigZag в output-ах", "Позиция ZigZagSmall в output-ах","Имя функции для лога", "Айди трейдтерминала", "Имя пайп-канала МетаТрейдера",
                "Отправлять ли ордера к МТ (1-да, остальное - нет)", "Нужно ли логирование?(1 - да, остальное - нет)", "Величина отношения колена, при котором выходить из рынка",
                 "Количество вершин для проверки", "Профит для закрытия", "УБыток для закрытия" };
        }
        public override bool Initialize(List<IProcessorInput> inputs, string parameters, ILogger logger)
        {
            ClosePositionStopTrade += Stop;
            if (Models.InputItemInteractivBrokerModel.IB == null)
                return false;
            this.logger = logger;
            ParametersParser parser = CreateParser(parameters);
            zigzagid = parser.GetInt("ZigZag_ID");
            zigzagidsmall = parser.GetInt("ZigZagSmall_ID");
            if (zigzagid < 0 || zigzagid > Model.Project.Outputs.Count) return false;
            if (zigzagidsmall < 0 || zigzagidsmall > Model.Project.Outputs.Count) return false;
            gappos = parser.GetInt("Gap");
            if (gappos < 0 || gappos > Model.Project.Outputs.Count) return false;
            //Gap = Model.Project.Proccesors[gappos];
            //Gap.ProcessorAction += PositionHandler;
            //ZigZag = Model.Project.Proccesors[zigzagid];
            //ZigZag.ProcessorAction += EventHandler;
            MinValueSmall = new List<ProbaStruct>();
            MaxValueSmall = new List<ProbaStruct>();
            MinValueBig = new List<ProbaStruct>();
            MaxValueBig = new List<ProbaStruct>();
            PositionShort = new Position();
            PositionLong = new Position();
            name = parser.GetString("Name");
            ratio = parser.GetDouble("Ratio");
            tradeTerminal = new ModelsAT.BrokerMT(parser.GetInt("tradeTerminalID"));
            tradeTerminal.setBrokerParam("Name", parser.GetString("ServerName"));
            tradeTerminal.SetServer();
            Size = parser.GetInt("Size");
            if (parser.GetInt("IsReal") == 1)
                tradeTerminal.isReal = true;
            if (parser.GetInt("Logg") == 1)
                LoggNeeded = true;
            else
                LoggNeeded = false;
            //if (divergence > size || divergence == 0) return false;
            openlongcloseshort = false;
            openshortcloselong = false;
            longlock = false;
            shortlock = false;
            profit_to_close = parser.GetInt("TakeProfit") * Math.Pow(10, -Model.Project.Outputs[gappos].Digits);
            lose_to_close = parser.GetInt("StopLoss") * Math.Pow(10, -Model.Project.Outputs[gappos].Digits);
            convergenceshort = false;
            convergencelong = false;
            divergenceshort = false;
            divergencelong = false;
            lastvalue = LastZigZagValue.NONE;
            all_profit = 0;
            openpos = OpenPosition.NONE;
            ConvergenMin = new ProbaStruct();
            ConvergenMax = new ProbaStruct();
            Model.OutputsInitializeEnd += InitializeHandler;
            Model.LoadSimulation += LoadSec;
            return true;
        }

        public void Stop()
        {
            lock (locker)
            {
                tradeTerminal.ClosePosition();
                tradeTerminal.isReal = false;
            }
        }
        public void LoadSec()
        {
            tradeTerminal.isReal = false;
            Model.LoadSimulation -= LoadSec;
        }
        public void InitializeHandler()
        {
            Gap = Model.Project.Proccesors[gappos];
            Gap.ProcessorAction += PositionHandler;
            ZigZag = Model.Project.Proccesors[zigzagid];
            ZigZagSmall = Model.Project.Proccesors[zigzagidsmall];
            ZigZag.ProcessorAction += EventHandlerBig;
            ZigZagSmall.ProcessorAction += EventHandlerSmall;
            Model.OutputsInitializeEnd -= InitializeHandler;
        }
        public override double Process(DateTime time)
        {
            if (ind > 200)
                ind = 0;
            return all_profit;
        }
        public override void Deinitialize()
        {
            if (ZigZag != null)
                ZigZag.ProcessorAction -= EventHandlerBig;
            if (Gap != null)
                Gap.ProcessorAction -= PositionHandler;
            if(ZigZagSmall!=null)
                ZigZagSmall.ProcessorAction -= EventHandlerSmall;
            tradeTerminal.Stop();
        }
        public void PositionHandler(object[] param)
        {
            double gap = (double)param[0];
            double ask = (double)param[1];
            double bid = (double)param[2];
            DateTime time = (DateTime)param[3];
            if (openpos == OpenPosition.LONG)
            {
                if (profit_to_close > 0)
                    if (bid >= PositionLong.value + profit_to_close)
                    {
                        if (LoggNeeded)
                            logger.LogEvent(time, "Close long due to TakeProfit condition");
                        openpos = OpenPosition.NONE;
                        lock (locker)
                        {
                            tradeTerminal.ClosePosition(PositionLong.index);
                        }
                        double profit = bid - PositionLong.value;
                        all_profit += profit;
                        if (LoggNeeded)
                            logger.LogEvent(time, name + " send sell order " + PositionLong.index + " req price : " + bid + " Profit: " + profit);
                        return;
                    }
                if (lose_to_close > 0)
                    if (bid <= PositionLong.value - lose_to_close)
                    {
                        if (LoggNeeded)
                            logger.LogEvent(time, "Close long due to StopLose condition");
                        openpos = OpenPosition.NONE;
                        lock (locker)
                        {
                            tradeTerminal.ClosePosition(PositionLong.index);
                        }
                        double profit = bid - PositionLong.value;
                        all_profit += profit;
                        if (LoggNeeded)
                            logger.LogEvent(time, name + " send sell order " + PositionLong.index + " req price : " + bid + " Profit: " + profit);
                        return;
                    }
            }
            if (openpos == OpenPosition.SHORT)
            {
                if(profit_to_close > 0)
                    if (PositionShort.value - profit_to_close >= ask)
                    {
                        if (LoggNeeded)
                            logger.LogEvent(time, "Close short due to TakeProfit condition");
                        openpos = OpenPosition.NONE;
                        lock (locker)
                        {
                            tradeTerminal.ClosePosition(PositionShort.index);
                        }
                        double profit = PositionShort.value - ask;
                        all_profit += profit;
                        if (LoggNeeded)
                            logger.LogEvent(time, name + " send buy order " + PositionShort.index + " req price : " + ask + " Profit: " + profit);
                        return;
                    }
                if (lose_to_close > 0)
                    if (ask >= PositionShort.value + lose_to_close)
                    {
                        if (LoggNeeded)
                            logger.LogEvent(time, "Close short due to StopLose condition");
                        openpos = OpenPosition.NONE;
                        lock (locker)
                        {
                            tradeTerminal.ClosePosition(PositionShort.index);
                        }
                        double profit = PositionShort.value - ask;
                        all_profit += profit;
                        if (LoggNeeded)
                            logger.LogEvent(time, name + " send buy order " + PositionShort.index + " req price : " + ask + " Profit: " + profit);
                        return;
                    }
            }
            if (lastvalue == LastZigZagValue.MIN)
            {
                
                if (openpos == OpenPosition.LONG)
                {
                   
                }
                if (convergencelong)
                {
                    double last_knee = Math.Abs(MaxValueSmall.Last().GapValue - MinValueSmall.Last().GapValue);
                    double temp_knee = Math.Abs(MinValueSmall.Last().GapValue - gap);
                    double r_exp = temp_knee / last_knee;
                    if (r_exp >= ratio)
                    {
                        if (!shortlock)
                        {
                            Position pos = new Position();
                            pos.index = ind++;
                            pos.value = ask;
                            PositionLong = pos;
                            openpos = OpenPosition.LONG;
                            lock (locker)
                            {
                                tradeTerminal.OpenOrder(ArbitrageTradeWF.OrderType.MKT_Buy, ind);
                            }
                            if (LoggNeeded)
                                logger.LogEvent(time, name + " Открываем long по дивергенции " + ind + " req price : " + ask);
                            return;
                        }   
                    }
                }
                if (divergencelong)
                {
                    openpos = OpenPosition.NONE;
                    tradeTerminal.ClosePosition(PositionShort.index);
                    double profit = PositionShort.value - ask;
                    all_profit += profit;
                    if (LoggNeeded)
                        logger.LogEvent(time, name + " send buy order " + PositionShort.index + " req price : " + ask + " Profit: " + profit);
                    return;
                }
                if (openlongcloseshort)
                {
                    lock (locker)
                    {
                        if(gap>lvl_up_last)
                        {
                            openlongcloseshort = false;
                            if (openpos == OpenPosition.SHORT)
                            {
                                openpos = OpenPosition.NONE;
                                tradeTerminal.ClosePosition(PositionShort.index);
                                double profit = PositionShort.value - ask;
                                all_profit += profit;
                                if (LoggNeeded)
                                    logger.LogEvent(time, name + " send buy order " + PositionShort.index + " req price : " + ask + " Profit: " + profit);
                                return;
                            }
                            if (openpos == OpenPosition.NONE)
                            {
                                Position pos = new Position();
                                pos.index = ind++;
                                pos.value = ask;
                                PositionLong = pos;
                                openpos = OpenPosition.LONG;
                                tradeTerminal.OpenOrder(ArbitrageTradeWF.OrderType.MKT_Buy, pos.index);
                                if (LoggNeeded)
                                    logger.LogEvent(time, name + " send buy order " + pos.index + " req price : " + pos.value);

                                return;
                            }
                        }
                    }
                }
            }
            if (lastvalue == LastZigZagValue.MAX)
            {

                if (openpos == OpenPosition.SHORT)
                {

                }
                if (convergenceshort)
                {
                    double last_knee = Math.Abs(MinValueSmall.Last().GapValue - MaxValueSmall.Last().GapValue);
                    double temp_knee = Math.Abs(MaxValueSmall.Last().GapValue - gap);
                    double r_exp = temp_knee / last_knee;
                    if (r_exp >= ratio)
                    {
                        if (!shortlock)
                        {
                            Position pos = new Position();
                            pos.index = ind++;
                            pos.value = bid;
                            PositionShort = pos;
                            openpos = OpenPosition.SHORT;
                            lock (locker)
                            {
                                tradeTerminal.OpenOrder(ArbitrageTradeWF.OrderType.MKT_Sell, ind);
                            }
                            if (LoggNeeded)
                                logger.LogEvent(time, name + " Открываем short по дивергенции " + ind + " req price : " + bid);
                            return;
                        }
                    }
                }
                if (divergenceshort)
                {
                    openpos = OpenPosition.NONE;
                    lock(locker)
                    {
                        tradeTerminal.ClosePosition(PositionLong.index);
                    }
                    double profit = bid - PositionLong.value;
                    all_profit += profit;
                    if (LoggNeeded)
                        logger.LogEvent(time, name + " send sell order " + PositionLong.index + " req price : " + bid + " Profit: " + profit);
                    return;
                }
                if (openshortcloselong)
                {
                    lock (locker)
                    {
                        if (gap < lvl_dwn_last)
                        {
                            openlongcloseshort = false;
                            if (openpos == OpenPosition.LONG)
                            {
                                openpos = OpenPosition.NONE;
                                tradeTerminal.ClosePosition(PositionLong.index);
                                double profit = bid - PositionLong.value;
                                all_profit += profit;
                                if (LoggNeeded)
                                    logger.LogEvent(time, name + " send sell order " + PositionLong.index + " req price : " + bid + " Profit: " + profit);
                                return;
                            }
                            if (openpos == OpenPosition.NONE)
                            {
                                Position pos = new Position();
                                pos.index = ind++;
                                pos.value = bid;
                                PositionShort = pos;
                                openpos = OpenPosition.SHORT;
                                tradeTerminal.OpenOrder(ArbitrageTradeWF.OrderType.MKT_Sell, pos.index);
                                if (LoggNeeded)
                                    logger.LogEvent(time, name + " send sell order " + pos.index + " req price : " + pos.value);
                                return;
                            }
                        }
                    }
                }
            }
        }
        public void EventHandlerSmall(object[] param)
        {
            if (MinValueSmall.Count == Size)
            {
                MinValueSmall.RemoveAt(0);
            }
            if (MaxValueSmall.Count == Size)
            {
                MaxValueSmall.RemoveAt(0);
            }
            if (param[0].ToString() == "Min")
            {
                lastvalue = LastZigZagValue.MIN;
                double gapValue = (double)param[1];
                double price = (double)param[2];
                DateTime time = (DateTime)param[3];
                ProbaStruct Temp_value = new ProbaStruct() { GapValue = gapValue, price = price, time = time };
                MinValueSmall.Add(Temp_value);
                if (MinValueSmall.Count == Size)
                {
                    int convergence = 0;
                    int divergence = 0; 
                    lock (locker)
                    {
                        for (int i = 1; i < Size; i++)
                        {
                            if (MinValueSmall[i].GapValue < MinValueSmall[i - 1].GapValue && MinValueSmall[i].price > MinValueSmall[i - 1].price)
                                convergence++;
                            if (MinValueSmall[i].GapValue > MinValueSmall[i - 1].GapValue && MinValueSmall[i].price > MinValueSmall[i - 1].price)
                                divergence++;
                        }
                        if (MinValueSmall.Last().GapValue < lvl_dwn_last * 0.8)
                            longlock = true;
                        else
                            longlock = false;
                        if (convergence == Size)
                            convergenceshort = true;
                        else
                            convergenceshort = false;
                        if (divergence == Size)
                            divergenceshort = true;
                        else
                            divergenceshort = false;
                    }
                }

            }
            if (param[0].ToString() == "Max")
            {
                lastvalue = LastZigZagValue.MAX;
                double gapValue = (double)param[1];
                double price = (double)param[2];
                DateTime time = (DateTime)param[3];
                ProbaStruct Temp_value = new ProbaStruct() { GapValue = gapValue, price = price, time = time };
                MaxValueSmall.Add(Temp_value);

                if (MaxValueSmall.Count == Size)
                {
                    int convergence = 0;
                    int divergence = 0;
                    lock (locker)
                    {
                        if (!longlock)
                            openlongcloseshort = true;
                        for (int i = 1; i < Size; i++)
                        {
                            if (MaxValueSmall[i].GapValue > MaxValueSmall[i - 1].GapValue && MaxValueSmall[i].price < MaxValueSmall[i - 1].price)
                                convergence++;
                            if (MaxValueSmall[i].GapValue < MaxValueSmall[i - 1].GapValue && MaxValueSmall[i].price < MaxValueSmall[i - 1].price)
                                divergence++;
                        }
                        if (MaxValueSmall.Last().GapValue > lvl_up_last * 0.8)
                            shortlock = true;
                        else
                            shortlock = false;
                        if (convergence == Size)
                            convergencelong = true;
                        else
                            convergencelong = false;
                        if (divergence == Size)
                            divergencelong = true;
                        else
                            divergencelong = false;
                    }
                }
            }
        }
        public void EventHandlerBig(object[] param)
        {
            if (MinValueBig.Count == Size)
            {
                MinValueBig.RemoveAt(0);
            }
            if (MaxValueBig.Count == Size)
            {
                MaxValueBig.RemoveAt(0);
            }
            if (param[0].ToString() == "Min")
            {
                lastvalue = LastZigZagValue.MIN;
                double gapValue = (double)param[1];
                double price = (double)param[2];
                DateTime time = (DateTime)param[3];
                ProbaStruct Temp_value = new ProbaStruct() { GapValue = gapValue, price = price, time = time };
                MinValueBig.Add(Temp_value);

                if (MinValueBig.Count == Size)
                {
                    double level = 0;
                    lock (locker)
                    {
                        if(!shortlock)
                            openshortcloselong = true;
                        for (int i = 1; i < Size; i++)
                        {
                            if (MinValueBig[i].GapValue < MinValueBig[i - 1].GapValue && MinValueBig[i].price > MinValueBig[i - 1].price)
                                openshortcloselong = false;
                            level += MinValueBig[i].GapValue;
                        }
                        level /= Size;
                        lvl_dwn_last = level;
                    }
                }
                
            }
            if (param[0].ToString() == "Max")
            {
                lastvalue = LastZigZagValue.MAX;
                double gapValue = (double)param[1];
                double price = (double)param[2];
                DateTime time = (DateTime)param[3];
                ProbaStruct Temp_value = new ProbaStruct() { GapValue = gapValue, price = price, time = time };
                MaxValueBig.Add(Temp_value);

                if (MaxValueBig.Count == Size)
                {
                    double level = 0;
                    lock (locker)
                    {
                        if (!longlock)
                            openlongcloseshort = true;
                        for (int i = 1; i < Size; i++)
                        {
                            if (MaxValueBig[i].GapValue > MaxValueBig[i - 1].GapValue && MaxValueBig[i].price < MaxValueBig[i - 1].price)
                                openlongcloseshort = false;
                            level += MaxValueBig[i].GapValue;
                        }
                        level /= Size;
                        lvl_up_last = level;
                    }
                }
            }
        }
    }
}
