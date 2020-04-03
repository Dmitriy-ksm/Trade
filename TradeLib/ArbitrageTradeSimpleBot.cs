using System;
using System.Threading;

namespace ArbitrageTradeWF
{
    /// <summary>
    /// Класс что представляет самой самого бото для торговли, основной класс через который работает приложение
    /// </summary>
    public class ArbitrageTradeSimpleBot
    {
        //Событие открытия одной позиции
        public Action ForceMajor;
        //События создания ордера и закрытия позиции
        public Action<ArbitrageTradeSimpleBotPosition, ArbitrageTradeSimpleBotPosition> PositionOpen;
        public Action<ArbitrageTradeSimpleBotPosition, ArbitrageTradeSimpleBotPosition> PositionClose;
        //Флаг проверяющий есть ли открытая пара позиций
        public bool IsPositionOpen { get; set; }
        //Брокеры(Поставщики Ликвидности) для торговли
        public IBrokers FirstBroker { get; set; }
        public IBrokers SecondBroker { get; set; }
        public Action<int,double> OnStatDataChange;
        public double Profit {            get;set;        }
        //Конструктор, инициализируем переменные торговли, подписываемся на события
        public ArbitrageTradeSimpleBot()
        {

        }
        ~ArbitrageTradeSimpleBot()
        {
        }
        /// <summary>
        /// Остановка торгов, закрытие позиций, отмена ордеров
        /// </summary>
        public void CLOSE()
        {
            FirstBroker.Stop();
            SecondBroker.Stop();
            if (IsPositionOpen)
            {
                    FirstBroker.ClosePosition();
                    SecondBroker.ClosePosition();
                    FirstBroker.CancelOrder();
                    SecondBroker.CancelOrder();
                    //FirstBroker.CloseOrderSell();
                    //SecondBroker.CloseOrderBuy();
                    PositionClose?.Invoke(FirstBroker.Position, SecondBroker.Position);
                    IsPositionOpen = false;
                    return;
            }
        }
        public void OpenPosition(OrderType type_fb,OrderType type_sb)
        {
            FirstBroker.OpenPosition(type_fb);
            SecondBroker.OpenPosition(type_sb);
            PositionOpen?.Invoke(FirstBroker.Position, SecondBroker.Position);
        }
        public void ClosePosition()
        {
            FirstBroker.ClosePosition();
            SecondBroker.ClosePosition();
            PositionClose?.Invoke(FirstBroker.Position, SecondBroker.Position);
        }
    }
}
