using System;
using System.Collections.Generic;
using System.Text;

namespace ArbitrageTradeWF
{
    //Пары ордеров Купли/Продажи типа LMT/MKT
    //Перечисление для типов возможных оредров
    public enum OrderType
    {
        LMT_Buy = 0,
        MKT_Buy = 1,
        LMT_Sell = 2,
        MKT_Sell = 3
    }
    /// <summary>
    /// Класс представляющий позиции в приложении
    /// </summary>
    public class ArbitrageTradeSimpleBotPosition
    {
        //Стандартные параметры ордера
        //Тип ордера
        public OrderType OrderType { get; }
        //Время открытия позиции
        public DateTime OpenTime { get; set; }
        //Цена открытия
        public double OpenPrice { get; }
        //Время когда позиция была заполнена
        public DateTime VerificationTime { get; set; }
        //Цена по которой позиция была выполненна
        public double ActualPrice { get; set; }
        //Параметры ордера которые использовались при открытии позиции
        public object[] OrderParam { get; }
        //Инициализируем позицию
        public ArbitrageTradeSimpleBotPosition(OrderType ot, double op, DateTime od, params object[] par)
        {
            OrderType = ot;
            OpenPrice = op;
            OpenTime = od;
            OrderParam = par;
        }
        //Время от открытия до выполнения(или закрытия) позиции
        public TimeSpan ExecutionTime()
        {
            return VerificationTime.Subtract(OpenTime);
        }
        //Расчет слиппейджа
        public double Slippage()
        {
            if (OrderType < OrderType.LMT_Sell)
                return OpenPrice - ActualPrice;
            else
                return ActualPrice - OpenPrice;
        }
        //Функция вывода объекта ArbitrageTradeSimpleBotPosition
        public override string ToString()
        {
            StringBuilder ret_val = new StringBuilder("Position " + OrderType + " / Slippage " + Slippage() + " /  Execute Time " + ExecutionTime()+" / Params ");
            foreach(var obj in OrderParam)
                ret_val.Append(obj.ToString()+" | ");
            return ret_val.ToString();
        }  
    }
}
