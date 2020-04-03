using System;
using System.Collections.Generic;
using System.Text;

namespace ArbitrageTradeWF
{
    /// <summary>
    /// Интерфейс для описания брокеров(поставщиков ликвидности)
    /// </summary>
    public delegate void EventHandler();
    public interface IBrokers : IDisposable
    {
        //Событие получения котировок
        event EventHandler OnBidAskChange;
        //Флаг симуляции
        bool isReal  {   get; set;  }
        //Флаг остановки
        bool isStop  {   get; set;  }
        //Проверка готов ли брокер торговать
        bool isReadyToTrade { get; set; }
        //Бид и Аск полученые от брокера
        double Bid { get; set; }
        double Ask { get; set; }
        //Идентификатор брокера
        int ID { get; }
        // Позиция открытая по брокеру
        ArbitrageTradeSimpleBotPosition Position { get; set; }
        //Флаг - открылась ли позиция
        bool isPositionOpen { get; set; }
        //Флаг - была ли выполненна позиция в полном объеме
        bool PositionFilled { get; set; }
        //Параметры для выполнения стандартных функций 
        //В них хранятся данные для получения котировок, подключения,отправки ордеров, или любые другие 
        object[] BrokerParam { get; set; }
        object[] OrderParam { get; set; }
        //Функции для заполнения колекций выше
        void setBrokerParam(params object[] param);
        void setOrderParam(params object[] param);
        void setConnectParam(params object[] param);
        //List<string> ParamName();
        //Отправить ордеры на выполнение
        void StartTrade();
        //Создание позиций
        void OpenPosition(OrderType type);
        //Закрытие позиций 
        void ClosePosition();
        //Закрытие ордеров
        void CancelOrder();
        //Остановка торгов
        void Stop();
    }
}
