using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Optimizer_Trade.Processors
{
    class TradeTerminal : ProcessorBase
    {
        int tradeId;
        int simtradeid;
        ArbitrageTradeWF.ArbitrageTradeSimpleBot tradeTerminal = new ArbitrageTradeWF.ArbitrageTradeSimpleBot();
        ProcessorBase SimTrade;
        ILogger logger;
        protected override void FillDefaultParameters(Dictionary<string, string> p)
        {
            p["SimTrade_ID"] = "0";
            p["TradeID"] = "0";
            p["OrderType_IB"] = "MKT";
            p["Volume_IB"] = "0";
            p["LMT_Price_IB"] = "0";
            p["MT_name"] = "PipeName";
            p["IB_Put_name"] = "LocalSymbolPut";
            p["IB_Call_name"] = "LocalSymbolCall";
            p["IsReal"] = "0";
        }
        public override string[] Comments()
        {
            return new string[] {"Позиция СимТрейд в output-ах","Айди терминала", "Тип ордера для InteractiveBrokers",
                                    "Объем сделки с IB", "Цена лимитного ордера IB", "Имя пайпсервера MT", "Контракт опциона Put",
                                        "Контракт опциона Call", "Флаг посылать ли реальные ордера (1 - ДА)" };
        }
        public override bool Initialize(List<IProcessorInput> inputs, string parameters, ILogger logger)
        {
            if (Models.InputItemInteractivBrokerModel.IB == null)
                return false;
            this.logger = logger;
            ParametersParser parser = CreateParser(parameters);
            simtradeid = parser.GetInt("SimTrade_ID");
            if (simtradeid < 0 || simtradeid > Model.Project.Outputs.Count) return false;
            tradeId = parser.GetInt("TradeID");
            tradeTerminal.FirstBroker = new ModelsAT.BrokerMT(tradeId);
            tradeTerminal.FirstBroker.setBrokerParam("Name", parser.GetString("MT_name"));  
            tradeTerminal.FirstBroker.setOrderParam("MKT");
            tradeTerminal.SecondBroker = new ModelsAT.BrokerIBOptionPair(tradeId, Models.InputItemInteractivBrokerModel.IB);
            tradeTerminal.SecondBroker.setBrokerParam("Put", parser.GetString("IB_Put_name"));
            tradeTerminal.SecondBroker.setBrokerParam("Call", parser.GetString("IB_Call_name"));
            tradeTerminal.SecondBroker.setBrokerParam("Volume", parser.GetString("Volume_IB"));
            tradeTerminal.SecondBroker.setBrokerParam("LimitPrice", parser.GetString("LMT_Price_IB"));
            tradeTerminal.SecondBroker.setOrderParam(parser.GetString("OrderType_IB"));
            SimTrade = Model.Project.Proccesors[simtradeid];
            SimTrade.ProcessorAction += EventHandler;
            if (parser.GetInt("IsReal") == 1)
            {
                tradeTerminal.FirstBroker.isReal = true;
                tradeTerminal.SecondBroker.isReal = true;
            }
            Model.OutputsInitializeEnd += InitializeHandler;
            return true;
        }
        public void InitializeHandler()
        {
            SimTrade = Model.Project.Proccesors[simtradeid];
            SimTrade.ProcessorAction += EventHandler;
            Model.OutputsInitializeEnd -= InitializeHandler;
        }
        public override double Process(DateTime time)
        {
            if(tradeTerminal.FirstBroker.Position != null)
                tradeTerminal.Profit = tradeTerminal.FirstBroker.Position.ActualPrice - tradeTerminal.FirstBroker.Position.OpenPrice 
                                        + tradeTerminal.SecondBroker.Position.ActualPrice - tradeTerminal.SecondBroker.Position.OpenPrice;
            return tradeTerminal.Profit;
        }
        public override void Deinitialize()
        {
            if(SimTrade!=null)
                SimTrade.ProcessorAction -= EventHandler;
        }
        public void EventHandler(object[] param)
        {
            if(param[0].ToString()=="OpenLong")
            {
                //TODO
                tradeTerminal.FirstBroker.Ask = Convert.ToDouble(param[1]);
                tradeTerminal.SecondBroker.Ask = Convert.ToDouble(param[2]);
                tradeTerminal.FirstBroker.OpenPosition(ArbitrageTradeWF.OrderType.MKT_Buy);
                tradeTerminal.SecondBroker.OpenPosition(ArbitrageTradeWF.OrderType.MKT_Buy);
            }
            if (param[0].ToString() == "OpenShort")
            {
                //TODO
                tradeTerminal.FirstBroker.Bid = Convert.ToDouble(param[1]);
                tradeTerminal.SecondBroker.Ask = Convert.ToDouble(param[2]);
                tradeTerminal.FirstBroker.OpenPosition(ArbitrageTradeWF.OrderType.MKT_Sell);
                tradeTerminal.SecondBroker.OpenPosition(ArbitrageTradeWF.OrderType.MKT_Sell);
            }
            if (param[0].ToString() == "CloseLong")
            {
                //TODO
                tradeTerminal.FirstBroker.Bid = Convert.ToDouble(param[1]);
                tradeTerminal.SecondBroker.Bid = Convert.ToDouble(param[2]);
                tradeTerminal.FirstBroker.ClosePosition();
                tradeTerminal.SecondBroker.ClosePosition();
            }
            if (param[0].ToString() == "CloseShort")
            {
                //TODO
                tradeTerminal.FirstBroker.Ask = Convert.ToDouble(param[1]);
                tradeTerminal.SecondBroker.Bid = Convert.ToDouble(param[2]);
                tradeTerminal.FirstBroker.ClosePosition();
                tradeTerminal.SecondBroker.ClosePosition();
            }
        }
    }
}
