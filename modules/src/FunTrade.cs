using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Optimizer_Trade.Processors
{
    class FunTrade : ProcessorBase
    {
        enum TradeType
        {
            STANDART = 0,
            IN_OUT = 1,
            ONE_IN_TWO_OUT = 2
        };
        Action ClosePositionStopTrade;
        public void CloseAll()
        {
            ClosePositionStopTrade.Invoke();
        }
        static public int ind = 0;
        TradeType trader_type;
        ProcessorBase ZigZag_1;
        ProcessorBase ZigZag_2;
        ProcessorBase ZigZag_2_2;
        List<ProbaStruct> MinValueOpen_1, MaxValueOpen_1;
        List<ProbaStruct> MinValueClose_1, MaxValueClose_1;
        List<ProbaStruct> MinValueOpen_2, MaxValueOpen_2;
        List<ProbaStruct> MinValueClose_2, MaxValueClose_2;
        List<ProbaStruct> MinValueOpen_2_2, MaxValueOpen_2_2;
        List<ProbaStruct> MinValueClose_2_2, MaxValueClose_2_2;
        ProbaStruct CrashShort, CrashLong;
        ProbaStruct OpenShort, OpenLong, CloseLong, CloseShort;
        ILogger logger;
        List<Position> PositionShort;
        List<Position> PositionLong;
        ProcessorBase Gap;
        int zigzagid_1,zigzagid_2, zigzagid_2_2;
        int gappos;
        bool LoggNeeded;
        double profit_to_close, lose_to_close;
        double all_profit;
        bool openshort, openlong;
        bool closeshort, closelong;
        double ratio,ratio_exp;
        bool expand_position_short, expand_position_long;
        bool can_expand;
        int divergenceOpen, divergenceClose;
        int divergence_min_open, divergence_max_open;
        int divergence_min_close, divergence_max_close;
        ModelsAT.BrokerMT tradeTerminal;
        int convergen_counter;
        LastZigZagValue lastvalue;//-1 - Ищем максимум, 1 - Ищем минимум
        OpenPosition openpos;//0 - нету,1 - шорт, 2 - лонг
        LastPosition last_pos;
        int convergen_min, convergen_max;
        ProbaStruct ConvergenMin, ConvergenMax;
        object locker = new object();
        string name;
        protected override void FillDefaultParameters(Dictionary<string, string> p)
        {
            p["Gap"] = "0";
            p["type"] = "0";
            p["ZigZag_ID1"] = "1";
            p["ZigZag_ID2"] = "2";
            p["ZigZag_ID2_2"] = "3";
            p["DivergenceOpen"] = "3";
            p["DivergenceClose"] = "3";
            p["Name"] = "Proba";
            p["tradeTerminalID"] = "0";
            p["ServerName"] = "server";
            p["IsReal"] = "0";
            p["Logg"] = "1";
            p["Convergence"] = "1";
            p["Ratio"] = 0.5f.ToString();
            p["Ratio_O"] = 0.5f.ToString();
            p["TakeProfit"] = "500";
            p["StopLoss"] = "500";
            p["Expand"] = "1";
        }
        public override string[] Comments()
        {
            return new string[] { "Позиция Gap в output-ах", "Режим работы бота (0-равносильные зигзаги, 1 - ЗигЗаг1 для открытия, ЗигЗаг2 для закрытия, остальное - открытие по первому, закрытие по двум вторым)",
                "Позиция ZigZag_1 в output-ах", "Позиция ZigZag_2 в output-ах", "Позиция ZigZag_2_2 в output-ах", "Кол-во вершин для проверки дивергенции для открытия", 
                "Кол-во вершин для проверки дивергенции для закрытия", "Имя функции для лога", "Айди трейдтерминала", "Имя пайп-канала МетаТрейдера",
                "Отправлять ли ордера к МТ (1-да, остальное - нет)", "Нужно ли логирование?(1 - да, остальное - нет)", "Кол-во конвергенций для выхода из рынка",
                "Величина отношения колена, при котором выходить из рынка", "Величина отношения колена, при котором расширять позицию", "Профит для закрытия",
                "УБыток для закрытия", "Нужно ли наращивать позиции (1-да, остальное - нет"};
        }
        public override bool Initialize(List<IProcessorInput> inputs, string parameters, ILogger logger)
        {
            ClosePositionStopTrade += Stop;
            if (Models.InputItemInteractivBrokerModel.IB == null)
                return false;
            this.logger = logger;
            ParametersParser parser = CreateParser(parameters);
            zigzagid_1 = parser.GetInt("ZigZag_ID1");
            if (zigzagid_1 < 0 || zigzagid_1 > Model.Project.Outputs.Count) return false;
            zigzagid_2 = parser.GetInt("ZigZag_ID2");
            if (zigzagid_2 < 0 || zigzagid_2 > Model.Project.Outputs.Count) return false;
            zigzagid_2_2 = parser.GetInt("ZigZag_ID2_2");
            gappos = parser.GetInt("Gap");
            if (gappos < 0 || gappos > Model.Project.Outputs.Count) return false;
            int t_t = parser.GetInt("type");
            switch(t_t)
            {
                case (0):
                    trader_type = TradeType.STANDART;
                    break;
                case (1):
                    trader_type = TradeType.IN_OUT;
                    break;
                default:
                    trader_type = TradeType.ONE_IN_TWO_OUT;
                    break;
            }
            //Gap = Model.Project.Proccesors[gappos];
            //Gap.ProcessorAction += PositionHandler;
            //ZigZag = Model.Project.Proccesors[zigzagid];
            //ZigZag.ProcessorAction += EventHandler;
            divergenceOpen = parser.GetInt("DivergenceOpen");
            divergenceClose = parser.GetInt("DivergenceClose");
            convergen_counter = parser.GetInt("Convergence");
            if( divergenceOpen < 0 || divergenceClose < 0 || convergen_counter < 0) return false;
            MinValueOpen_1 = new List<ProbaStruct>();
            MaxValueOpen_1 = new List<ProbaStruct>();
            MinValueClose_1 = new List<ProbaStruct>();
            MaxValueClose_1 = new List<ProbaStruct>();
            MinValueOpen_2 = new List<ProbaStruct>();
            MaxValueOpen_2 = new List<ProbaStruct>();
            MinValueClose_2 = new List<ProbaStruct>();
            MaxValueClose_2 = new List<ProbaStruct>();
            MinValueOpen_2_2 = new List<ProbaStruct>();
            MaxValueOpen_2_2 = new List<ProbaStruct>();
            MinValueClose_2_2 = new List<ProbaStruct>();
            MaxValueClose_2_2 = new List<ProbaStruct>();
            PositionShort = new List<Position>();
            PositionLong = new List<Position>();
            name = parser.GetString("Name");
            ratio = parser.GetDouble("Ratio");
            profit_to_close = parser.GetInt("TakeProfit") * Math.Pow(10,-Model.Project.Outputs[gappos].Digits);
            lose_to_close = parser.GetInt("StopLoss") * Math.Pow(10, -Model.Project.Outputs[gappos].Digits);
            ratio_exp = parser.GetDouble("Ratio_O");
            tradeTerminal = new ModelsAT.BrokerMT(parser.GetInt("tradeTerminalID"));
            tradeTerminal.setBrokerParam("Name", parser.GetString("ServerName"));
            tradeTerminal.SetServer();
            if (parser.GetInt("IsReal") == 1)
                tradeTerminal.isReal = true;
            else
                tradeTerminal.isReal = false;
            if (parser.GetInt("Logg") == 1)
                LoggNeeded = true;
            else
                LoggNeeded = false;
            if (parser.GetInt("Expand") == 1)
                can_expand = true;
            else
                can_expand = false;
            divergence_max_open = 0;
            divergence_min_open = 0;
            divergence_max_close = 0;
            divergence_min_close = 0;
            openlong = false;
            openshort = false;
            closelong = false;
            expand_position_short = false;
            expand_position_long = false;
            closeshort = false;
            lastvalue = LastZigZagValue.NONE;
            all_profit = 0;
            openpos = OpenPosition.NONE;
            convergen_min = 0;
            convergen_max = 0;
            ConvergenMin = new ProbaStruct();
            ConvergenMax = new ProbaStruct();
            last_pos = new LastPosition();
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
            ZigZag_1 = Model.Project.Proccesors[zigzagid_1];
            ZigZag_2 = Model.Project.Proccesors[zigzagid_2];
            switch(trader_type)
            {
                case (TradeType.STANDART):
                    ZigZag_1.ProcessorAction += EventHandler_1;
                    ZigZag_2.ProcessorAction += EventHandler_2;
                    break;
                case (TradeType.IN_OUT):
                    ZigZag_1.ProcessorAction += EventHandler_1_type2;
                    ZigZag_2.ProcessorAction += EventHandler_2_type2;
                    break;
                default:
                    ZigZag_1.ProcessorAction += EventHandler_1_type2;
                    ZigZag_2.ProcessorAction += EventHandler_2_type2;
                    ZigZag_2_2 = Model.Project.Proccesors[zigzagid_2_2];
                    ZigZag_2_2.ProcessorAction += EventHandler_2_2_type2;
                    break;
            }
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
            if(ZigZag_1 != null && ZigZag_2 != null)
                switch (trader_type)
                {
                    case (TradeType.STANDART):
                        ZigZag_1.ProcessorAction += EventHandler_1;
                        ZigZag_2.ProcessorAction += EventHandler_2;
                        break;
                    case (TradeType.IN_OUT):
                        ZigZag_1.ProcessorAction += EventHandler_1_type2;
                        ZigZag_2.ProcessorAction += EventHandler_2_type2;
                        break;
                    default:
                        ZigZag_1.ProcessorAction += EventHandler_1_type2;
                        ZigZag_2.ProcessorAction += EventHandler_2_type2;
                        if(ZigZag_2_2 != null)
                            ZigZag_2_2.ProcessorAction += EventHandler_2_2_type2;
                        break;
                }
            if (Gap != null)
                Gap.ProcessorAction -= PositionHandler;
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
                if(profit_to_close>0)
                    if(bid >= PositionLong.Last().value + profit_to_close)
                    {
                        if (LoggNeeded)
                            logger.LogEvent(time, "Close long due to TakeProfit condition");
                        FunTradeWorker.ClosePosition(false, PositionLong, tradeTerminal, true, bid, ref all_profit, locker, logger, time, name, LoggNeeded);
                        openpos = OpenPosition.NONE;
                        return;
                    }
                if(lose_to_close>0)
                    if(bid <= PositionLong.Last().value - lose_to_close)
                    {
                        if (LoggNeeded)
                            logger.LogEvent(time, "Close long due to StopLose condition");
                        FunTradeWorker.ClosePosition(false, PositionLong, tradeTerminal, true, bid, ref all_profit, locker, logger, time, name, LoggNeeded);
                        openpos = OpenPosition.NONE;
                        return;
                    }
            }
            if (openpos == OpenPosition.SHORT)
            {
                if (profit_to_close > 0)    
                    if (PositionShort.Last().value - profit_to_close >= ask)
                    {
                        if (LoggNeeded)
                            logger.LogEvent(time, "Close short due to TakeProfit condition");
                        FunTradeWorker.ClosePosition(false, PositionShort, tradeTerminal, false, ask, ref all_profit, locker, logger, time, name, LoggNeeded);
                        openpos = OpenPosition.NONE;
                        return;
                    }
                if (lose_to_close > 0)
                    if (ask >= PositionShort.Last().value + lose_to_close)
                    {
                        if(LoggNeeded)
                            logger.LogEvent(time, "Close short due to StopLose condition");
                        FunTradeWorker.ClosePosition(false, PositionShort, tradeTerminal, false, ask, ref all_profit, locker, logger, time, name, LoggNeeded);
                        openpos = OpenPosition.NONE;
                        return;
                    }
            }
            //if(convergen_min)
            if (lastvalue == LastZigZagValue.MIN)
            {
                if(openpos == OpenPosition.LONG)
                {
                    if (gap > CrashLong.GapValue)
                    {
                        if (LoggNeeded)
                            logger.LogEvent(time, "Close long due to crash divergence condition");
                        FunTradeWorker.ClosePosition(false, PositionLong, tradeTerminal, true, bid, ref all_profit, locker, logger, time, name, LoggNeeded);
                        openpos = OpenPosition.NONE;
                        return;
                    }
                }
                if (convergen_max == convergen_counter)
                {
                    if (gap > ConvergenMax.GapValue && ask > ConvergenMax.price)
                    {
                        //if (LoggNeeded)
                        //    logger.LogEvent(time, name + " Вторая конвергенция Long");
                        convergen_max = 0;
                        //FunTradeWorker.ClosePosition(true, PositionLong, tradeTerminal, true, bid, ref all_profit, locker, logger, time, name, LoggNeeded);
                        //openpos = OpenPosition.NONE;
                    }
                }
                if (openpos == OpenPosition.LONG)
                {
                    FunTradeWorker.AddPositionVolume_Old(true, ref last_pos, gap, ask, PositionLong, ref expand_position_long, locker, tradeTerminal, logger, time, name, LoggNeeded);
                    //FunTradeWorker.AddPositionVolume_New(true, MaxValueOpen, MinValueOpen, gap, ask, ratio_exp, PositionLong, ref expand_position_long, locker, tradeTerminal, logger, time, name, LoggNeeded);
                }
                if (openlong)
                {
                        if (openpos == OpenPosition.NONE)
                        {
                            if (gap < OpenLong.GapValue && ask < OpenLong.price)
                            {
                                Position pos = new Position();
                                pos.index = ind++;
                                pos.value = ask;
                                last_pos.gap = gap;
                                last_pos.price = ask;
                                PositionLong.Add(pos);
                                openpos = OpenPosition.LONG;
                                lock (locker)
                                {
                                    tradeTerminal.OpenOrder(ArbitrageTradeWF.OrderType.MKT_Buy, pos.index);
                                }
                                if (LoggNeeded)
                                    logger.LogEvent(time, name + " send buy order " + pos.index + " req price : " + pos.value);
                                openlong = false;
                                return;
                            }
                        }

                }
                if (closeshort)
                {
                        if (gap < CloseShort.GapValue && ask < CloseShort.price)
                        {
                            if (LoggNeeded)
                                logger.LogEvent(time, "Close short due to divergence condition");
                            if (FunTradeWorker.ClosePosition(false, PositionShort, tradeTerminal, false, ask, ref all_profit, locker, logger, time, name, LoggNeeded))
                                openpos = OpenPosition.NONE;
                            closeshort = false;
                            return;
                        }
                }
            }
            if (lastvalue == LastZigZagValue.MAX)
            {
                if (openpos == OpenPosition.SHORT)
                {
                    if (gap < CrashShort.GapValue)
                    {
                        if (LoggNeeded)
                            logger.LogEvent(time, "Close short due to crash divergence condition");
                        FunTradeWorker.ClosePosition(false, PositionShort, tradeTerminal, false, ask, ref all_profit, locker, logger, time, name, LoggNeeded);
                        openpos = OpenPosition.NONE;
                        return;
                    }
                }
                if (convergen_min == convergen_counter)
                {
                    if (gap < ConvergenMin.GapValue && bid < ConvergenMin.price)
                    {
                        //if (LoggNeeded)
                        //    logger.LogEvent(time, name + " Вторая конвергенция Short");
                        convergen_min = 0;
                        //if(FunTradeWorker.ClosePosition(true, PositionShort, tradeTerminal, false, ask, ref all_profit, locker, logger, time, name, LoggNeeded))
                        //    openpos = OpenPosition.NONE;
                    }
                }
                if (openpos == OpenPosition.SHORT)
                {
                    FunTradeWorker.AddPositionVolume_Old(false, ref last_pos, gap, bid, PositionShort, ref expand_position_short, locker, tradeTerminal, logger, time, name, LoggNeeded);
                    //FunTradeWorker.AddPositionVolume_New(false, MinValueOpen, MaxValueOpen, gap, bid, ratio_exp, PositionShort, ref expand_position_short, locker, tradeTerminal, logger, time, name, LoggNeeded);
                }
                if (closelong)
                {
                        if (gap > CloseLong.GapValue && bid > CloseLong.price)
                        {
                            if (LoggNeeded)
                                logger.LogEvent(time, "Close long due to divergence condition");
                            FunTradeWorker.ClosePosition(false, PositionLong, tradeTerminal, true, bid, ref all_profit, locker, logger, time, name, LoggNeeded);
                            openpos = OpenPosition.NONE;
                            closelong = false;
                            return;
                        }
                }
                if (openshort)
                {
                        if (openpos == OpenPosition.NONE)
                        {
                            if (gap > OpenShort.GapValue && bid > OpenShort.price)
                            {
                                Position pos = new Position();
                                pos.index = ind++;
                                pos.value = bid;
                                last_pos.gap = gap;
                                last_pos.price = bid;
                                PositionShort.Add(pos);
                                openpos = OpenPosition.SHORT;
                                lock (locker)
                                {
                                    tradeTerminal.OpenOrder(ArbitrageTradeWF.OrderType.MKT_Sell, pos.index);
                                }
                                if (LoggNeeded)
                                    logger.LogEvent(time, name + " send sell order " + pos.index + " req price : " + pos.value);
                                openshort = false;
                                return;
                            }
                        }
                }
            }
        }
        void EventHelper(object[] param, List<ProbaStruct> MinValueOpen, List<ProbaStruct> MinValueClose, List<ProbaStruct> MaxValueOpen, List<ProbaStruct> MaxValueClose)
        {
            if (MinValueOpen.Count == divergenceOpen + 1)
            {
                MinValueOpen.RemoveAt(0);
            }
            if (MinValueClose.Count == divergenceClose + 1)
            {
                MinValueClose.RemoveAt(0);
            }
            if (MaxValueOpen.Count == divergenceOpen + 1)
            {
                MaxValueOpen.RemoveAt(0);
            }
            if (MaxValueClose.Count == divergenceClose + 1)
            {
                MaxValueClose.RemoveAt(0);
            }
            if (param[0].ToString() == "Min")
            {
                lastvalue = LastZigZagValue.MIN;
                double gapValue = (double)param[1];
                double price = (double)param[2];
                DateTime time = (DateTime)param[3];
                ProbaStruct Temp_value = new ProbaStruct() { GapValue = gapValue, price = price, time = time };
                if (MinValueOpen.Count != 0)
                {
                    if (openpos == OpenPosition.SHORT)
                    {
                        if (can_expand)
                        {
                            if (Temp_value.GapValue > MinValueOpen.Last().GapValue && Temp_value.price > MinValueOpen.Last().price)
                                expand_position_short = true;
                            else
                                expand_position_short = false;
                        }
                    }
                    FunTradeWorker.CorvergenceCheck(MinValueOpen, MaxValueOpen, Temp_value, gapValue, price, time,
                                                        ref ConvergenMin, ref convergen_min, Model.Project.Outputs[gappos].Value,
                                                        ratio, ref PositionShort, locker, tradeTerminal, ref openpos, logger, name, LoggNeeded);
                    if (openpos == OpenPosition.NONE)
                    {
                        FunTradeWorker.DivergenceCheck(false, MinValueOpen, Temp_value, ref divergence_min_open, ref openshort, logger, name, time, LoggNeeded, true);
                    }
                }
                if (MinValueClose.Count != 0)
                {
                    if (openpos == OpenPosition.LONG)
                    {
                        FunTradeWorker.DivergenceCheck(false, MinValueClose, Temp_value, ref divergence_min_close, ref closelong, logger, name, time, LoggNeeded, false);
                    }
                }
                MinValueOpen.Add(Temp_value);
                MinValueClose.Add(Temp_value);
                if (divergenceOpen == 0)
                {
                    if (openpos == OpenPosition.NONE)
                    {
                        openshort = true;
                        OpenShort = Temp_value;
                    }
                    if (openpos == OpenPosition.SHORT)
                        expand_position_short = true;
                }
                else
                if (divergence_min_open == divergenceOpen)
                {
                    openshort = true;
                    OpenShort = MinValueOpen.Last();
                    CrashShort = MinValueOpen[0];
                    string message = String.Format("{0} Решение открытия позиции Шорт. Ждем локальную дивергенцию... ", name);
                    //if (LoggNeeded)
                    //    logger.LogEvent(Temp_value.time, message);
                    divergence_min_open = 0;
                }
                if (divergenceClose == 0)
                {
                    if (openpos == OpenPosition.LONG)
                    {
                        CloseLong = Temp_value;
                        closelong = true;
                    }
                }
                else
                if (divergence_min_close == divergenceClose)
                {
                    closelong = true;
                    CloseLong = MinValueClose.Last();
                    string message = String.Format("{0} Решение закрыть позиции Лонг. Ждем локальную дивергенцию... ", name);
                    //if (LoggNeeded)
                    //    logger.LogEvent(Temp_value.time, message);
                    divergence_min_close = 0;
                }
            }
            if (param[0].ToString() == "Max")
            {
                lastvalue = LastZigZagValue.MAX;
                double gapValue = (double)param[1];
                double price = (double)param[2];
                DateTime time = (DateTime)param[3];
                ProbaStruct Temp_value = new ProbaStruct() { GapValue = gapValue, price = price, time = time };
                if (MaxValueOpen.Count != 0)
                {
                    if (openpos == OpenPosition.LONG)
                    {
                        if (can_expand)
                        {
                            if (Temp_value.GapValue < MaxValueOpen.Last().GapValue && Temp_value.price < MaxValueOpen.Last().price)
                                expand_position_long = true;
                            else
                                expand_position_long = false;
                        }
                    }
                    FunTradeWorker.CorvergenceCheck(MaxValueOpen, MinValueOpen, Temp_value, gapValue, price, time, ref ConvergenMax, ref convergen_max,
                                                        Model.Project.Outputs[gappos].Value, ratio, ref PositionLong, locker, tradeTerminal, ref openpos, logger, name, LoggNeeded);
                    if (openpos == OpenPosition.NONE)
                    {
                        FunTradeWorker.DivergenceCheck(true, MaxValueOpen, Temp_value, ref divergence_max_open, ref openlong, logger, name, time, LoggNeeded, true);
                    }
                }
                if (MaxValueClose.Count != 0)
                {
                    if (openpos == OpenPosition.SHORT)
                    {
                        FunTradeWorker.DivergenceCheck(true, MaxValueClose, Temp_value, ref divergence_max_close, ref closeshort, logger, name, time, LoggNeeded, false);
                    }
                }
                MaxValueOpen.Add(Temp_value);
                MaxValueClose.Add(Temp_value);
                if (divergenceOpen == 0)
                {
                    if (openpos == OpenPosition.NONE)
                    {
                        openlong = true;
                        OpenLong = Temp_value;
                    }
                    if (openpos == OpenPosition.LONG)
                        expand_position_short = true;
                }
                else
                if (divergence_max_open == divergenceOpen)
                {
                    openlong = true;
                    OpenLong = MaxValueOpen.Last();
                    CrashLong = MaxValueOpen[0];
                    string message = String.Format("{0} Решение открытия позиции Лонг. Ждем локальную дивергенцию... ", name);
                    //if (LoggNeeded)
                    //    logger.LogEvent(Temp_value.time, message);
                    divergence_max_open = 0;
                }
                if (divergenceClose == 0)
                {
                    if (openpos == OpenPosition.SHORT)
                    {
                        CloseShort = Temp_value;
                        closelong = true;
                    }
                }
                else
                if (divergence_max_close == divergenceClose)
                {
                    closeshort = true;
                    CloseShort = MaxValueClose.Last();
                    string message = String.Format("{0} Решение закрыть позиции Шорт. Ждем локальную дивергенцию... ", name);
                    //if (LoggNeeded)
                    //    logger.LogEvent(Temp_value.time, message);
                    divergence_max_close = 0;
                }
            }
        }
        public void EventHandler_1(object[] param)
        {
            EventHelper(param, MinValueOpen_1, MinValueClose_1, MaxValueOpen_1, MaxValueClose_1);
        }
        public void EventHandler_2(object[] param)
        {
            EventHelper(param, MinValueOpen_2, MinValueClose_2, MaxValueOpen_2, MaxValueClose_2);
        }
        public void EventHandler_1_type2(object[] param)
        {
            if (MinValueOpen_1.Count == divergenceOpen + 1)
            {
                MinValueOpen_1.RemoveAt(0);
            }
            if (MaxValueOpen_1.Count == divergenceOpen + 1)
            {
                MaxValueOpen_1.RemoveAt(0);
            }
            if (param[0].ToString() == "Min")
            {
                lastvalue = LastZigZagValue.MIN;
                double gapValue = (double)param[1];
                double price = (double)param[2];
                DateTime time = (DateTime)param[3];
                ProbaStruct Temp_value = new ProbaStruct() { GapValue = gapValue, price = price, time = time };
                if (MinValueOpen_1.Count != 0)
                {
                    if (openpos == OpenPosition.SHORT)
                    {
                        if (can_expand)
                        {
                            if (Temp_value.GapValue > MinValueOpen_1.Last().GapValue && Temp_value.price > MinValueOpen_1.Last().price)
                                expand_position_short = true;
                            else
                                expand_position_short = false;
                        }
                    }
                    if (openpos == OpenPosition.NONE)
                    {
                        FunTradeWorker.DivergenceCheck(false, MinValueOpen_1, Temp_value, ref divergence_min_open, ref openshort, logger, name, time, LoggNeeded, true);
                    }
                }
                MinValueOpen_1.Add(Temp_value);
                MinValueClose_1.Add(Temp_value);
                if (divergenceOpen == 0)
                {
                    if (openpos == OpenPosition.NONE)
                    {
                        openshort = true;
                        OpenShort = Temp_value;
                    }
                    if (openpos == OpenPosition.SHORT)
                        expand_position_short = true;
                }
                else
                if (divergence_min_open == divergenceOpen)
                {
                    openshort = true;
                    OpenShort = MinValueOpen_1.Last();
                    CrashShort = MinValueOpen_1[0];
                    string message = String.Format("{0} Решение открытия позиции Шорт. Ждем локальную дивергенцию... ", name);
                    //if (LoggNeeded)
                    //    logger.LogEvent(Temp_value.time, message);
                    divergence_min_open = 0;
                }
            }
            if (param[0].ToString() == "Max")
            {
                lastvalue = LastZigZagValue.MAX;
                double gapValue = (double)param[1];
                double price = (double)param[2];
                DateTime time = (DateTime)param[3];
                ProbaStruct Temp_value = new ProbaStruct() { GapValue = gapValue, price = price, time = time };
                if (MaxValueOpen_1.Count != 0)
                {
                    if (openpos == OpenPosition.LONG)
                    {
                        if (can_expand)
                        {
                            if (Temp_value.GapValue < MaxValueOpen_1.Last().GapValue && Temp_value.price < MaxValueOpen_1.Last().price)
                                expand_position_long = true;
                            else
                                expand_position_long = false;
                        }
                    }
                    if (openpos == OpenPosition.NONE)
                    {
                        FunTradeWorker.DivergenceCheck(true, MaxValueOpen_1, Temp_value, ref divergence_max_open, ref openlong, logger, name, time, LoggNeeded, true);
                    }
                }
                MaxValueOpen_1.Add(Temp_value);
                if (divergenceOpen == 0)
                {
                    if (openpos == OpenPosition.NONE)
                    {
                        OpenLong = Temp_value;
                        openlong = true;
                    }
                    if (openpos == OpenPosition.LONG)
                        expand_position_short = true;
                }
                else
                if (divergence_max_open == divergenceOpen)
                {
                    openlong = true;
                    OpenLong = MaxValueOpen_1.Last();
                    CrashLong = MaxValueOpen_1[0];
                    string message = String.Format("{0} Решение открытия позиции Лонг. Ждем локальную дивергенцию... ", name);
                    //if (LoggNeeded)
                    //    logger.LogEvent(Temp_value.time, message);
                    divergence_max_open = 0;
                }
            }
        }
        void EventHelper_2_type2(object[] param, ref List<ProbaStruct> MinValueClose, ref List<ProbaStruct> MaxValueClose)
        {
            if (MinValueClose.Count == divergenceClose + 1)
            {
                MinValueClose.RemoveAt(0);
            }
            if (MaxValueClose.Count == divergenceClose + 1)
            {
                MaxValueClose.RemoveAt(0);
            }
            if (param[0].ToString() == "Min")
            {
                lastvalue = LastZigZagValue.MIN;
                double gapValue = (double)param[1];
                double price = (double)param[2];
                DateTime time = (DateTime)param[3];
                ProbaStruct Temp_value = new ProbaStruct() { GapValue = gapValue, price = price, time = time };
                if (MinValueClose.Count != 0)
                {
                    if (openpos == OpenPosition.LONG)
                    {
                        FunTradeWorker.DivergenceCheck(false, MinValueClose, Temp_value, ref divergence_min_close, ref closelong, logger, name, time, LoggNeeded, false);
                    }
                }
                MinValueClose.Add(Temp_value);
                if (divergenceClose == 0)
                {
                    if (OpenPosition.LONG == openpos)
                    {
                        CloseLong = Temp_value;
                        closelong = true;
                    }
                }
                else
                if (divergence_min_close == divergenceClose)
                {
                    closelong = true;
                    CloseLong = MinValueClose.Last();
                    string message = String.Format("{0} Решение закрыть позиции Лонг. Ждем локальную дивергенцию... ", name);
                    //if (LoggNeeded)
                    //    logger.LogEvent(Temp_value.time, message);
                    divergence_min_close = 0;
                }

            }
            if (param[0].ToString() == "Max")
            {
                lastvalue = LastZigZagValue.MAX;
                double gapValue = (double)param[1];
                double price = (double)param[2];
                DateTime time = (DateTime)param[3];
                ProbaStruct Temp_value = new ProbaStruct() { GapValue = gapValue, price = price, time = time };
                if (MaxValueClose.Count != 0)
                {
                    if (openpos == OpenPosition.SHORT)
                    {
                        FunTradeWorker.DivergenceCheck(true, MaxValueClose, Temp_value, ref divergence_max_close, ref closeshort, logger, name, time, LoggNeeded, false);
                    }
                }
                MaxValueClose.Add(Temp_value);
                if (divergenceClose == 0)
                {
                    if (openpos == OpenPosition.SHORT)
                    {
                        CloseShort = Temp_value;
                        closelong = true;
                    }
                }
                else
                if (divergence_max_close == divergenceClose)
                {
                    closeshort = true;
                    CloseShort = MaxValueClose.Last();
                    string message = String.Format("{0} Решение закрыть позиции Шорт. Ждем локальную дивергенцию... ", name);
                    //if (LoggNeeded)
                    //    logger.LogEvent(Temp_value.time, message);
                    divergence_max_close = 0;
                }
            }
        }
        public void EventHandler_2_type2(object[] param)
        {
            EventHelper_2_type2(param, ref MinValueClose_2, ref MaxValueClose_2);
        }
        public void EventHandler_2_2_type2(object[] param)
        {
            EventHelper_2_type2(param, ref MinValueClose_2_2, ref MaxValueClose_2_2);
        }
    }
}
