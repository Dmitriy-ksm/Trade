using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ArbitrageTradeWF;

namespace Optimizer_Trade.ModelsAT
{
    /// <summary>
    /// Базовый класс для представление брокеров
    /// </summary>
    abstract class BaseAT : IBrokers
    {
        //Флаг симуляции
        public bool isReal
        {
            get; set;
        }

        public virtual int ID
        {
            get;
            set;
        }

        public bool isStop
        {
            get; set;
        }
        
        public bool isReadyToTrade { get; set; }

        public ArbitrageTradeSimpleBotPosition Position { get; set; }

        public bool isPositionOpen { get; set; }
        public bool PositionFilled { get; set; }

        protected double bid, ask;
        public event ArbitrageTradeWF.EventHandler OnBidAskChange;
        public double Bid
        {
            get { return bid; }
            set { bid = value; }
        }
        //Фукнция для вызова события OnBidAskChange из наследственных классов
        protected void RaiseBidAskChangeEvent()
        {
            OnBidAskChange?.Invoke();
        }
        public double Ask
        {
            get { return ask; }
            set { ask = value; }
        }

        public object[] ConnectParam { get; set; }
        public object[] BrokerParam { get; set; }
        public object[] OrderParam { get; set; }
        public object[] CancelOrderParam { get; set; }

        /*public virtual List<string> ParamName()
        {
            return new List<string>();
        }*/
        //---------------------------------------------------------------
        //---------------------------------------------------------------
        //---------------------------------------------------------------
        //---------------------------------------------------------------
        //---------------------------------------------------------------
        //---------------------------------------------------------------
        //Установка параметров
        public virtual void setBrokerParam(params object[] param)
        {
            BrokerParam = param;
        }
        public virtual void setConnectParam(params object[] param)
        {
            ConnectParam = param;
        }
        public abstract void setOrderParam(object[] param);
        //---------------------------------------------------------------
        //---------------------------------------------------------------
        //---------------------------------------------------------------
        //Открытие позиций
        public abstract void OpenPosition(OrderType type);
        public virtual void StartTrade() { }
        //---------------------------------------------------------------
        //---------------------------------------------------------------
        //---------------------------------------------------------------
        //Закрытие позиций
        public abstract void ClosePosition();
        //---------------------------------------------------------------
        //---------------------------------------------------------------
        //---------------------------------------------------------------
        //Отмена ордера
        public abstract void CancelOrder();
        //---------------------------------------------------------------
        //---------------------------------------------------------------
        //---------------------------------------------------------------
        //Остановка торгов
        public abstract void Stop();

        public abstract void Dispose();
    }
}
