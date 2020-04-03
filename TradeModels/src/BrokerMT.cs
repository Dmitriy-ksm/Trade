using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ArbitrageTradeWF;
using System.Threading;

namespace Optimizer_Trade.ModelsAT
{
    class MTParam
    {
        public string PipeName;
        public double LimitPrice;
        public static List<string> paramNames()
        {
            List<string> ret_val = new List<string>();
            ret_val.Add("LimitPrice");
            return ret_val;
        }
    }
    class BrokerMT : BaseAT
    {

        object locker1 = new object();
        object locker2 = new object();
        #region MetaTradeScript
        //Перечисления для общения с экспертом 
        public enum MT_REQUEST
        {
            NONE = 0,
            BUY_LIMIT = 1,
            SELL_LIMIT = 2,
            BUY_MKT = 3,
            SELL_MKT = 4,
            CLOSE_POSITION = 5,
            CANCEL_ORDER = 6
        };
        public enum MT_RESPONS
        {
            SUCCES = 0,
            ORDER_CREATE = 1,
            PIPE_OPEN_ERROR = 100,
            NOT_EQUAL_RESPONS = 102,
            NOT_EQUAL_REQUEST = 101,
            CANT_READ_RESPONS_SIZE = 103,
            CANT_CREATE_ORDED = 104,
            ERROR = -1
        };
        //Флаг который следит нужно ли нам давать запрос к MT
        bool send_req;
        //Запрос к MT
        MT_REQUEST request_type;
        struct SomeStruct
        {
            public MT_REQUEST req;
            public int posid;
        }
        Queue<SomeStruct> requst = new Queue<SomeStruct>();
        public MT_REQUEST Request
        {
            get { return request_type; }
        }
        //Серверы для передачи по пайпам
        public Yummy.IO.PipeMessageServer Server_trade
        {
            get;
            set;
        }
        public Yummy.IO.PipeMessageServer Server_query
        {
            get;
            set;
        }
        /* public Yummy.IO.PipeMessageServer Server_result
         {
             get;
             set;
         }*/
        //Блок для одновременной записи BidAsk из двух разных котировок
        object sync = new object();
        //Функции общения с пайпами
        //Функция получения данных об заполении позиции
        void RequestHandler_trade(Yummy.IO.MessageServer source, object param, byte[] request, List<byte> response)
        {
            lock(locker2)
            {

                ModelsAT.BaseAT input = param as ModelsAT.BaseAT;
                Position.ActualPrice = BitConverter.ToDouble(request, 0);
                Position.VerificationTime = DateTime.Now;
                PositionFilled = true;
                //string mes = "Order MT filled. Time:" + DateTime.Now.ToString("HH:mm:ss.ffffff");
                //logHandler?.Message(ID, mes);
                response.Add(0);
            }
        }
        //Функция для отправки запроса об открытии или закрытии позиции
        void RequestHandler_query(Yummy.IO.MessageServer source, object param, byte[] request, List<byte> response)
        {
           /* if (!send_req)
            { 
                response.Add(0);
                return;
            }*/
            //Ждем флага отправки запроса
            /*while (!send_req)
            {
                //Проверяем нужно ли закрыть брокера
                if (isStop)
                    break;
            }*/
            //Отправляем запрос
            //send_req = false;

            lock(locker1)
            {
                if (requst.Count != 0)
                {
                    SomeStruct temp = requst.Dequeue();
                    MT_REQUEST req = request_type;
                    if (req == MT_REQUEST.BUY_MKT || req == MT_REQUEST.SELL_MKT)
                        PositionFilled = false;
                    request_type = MT_REQUEST.NONE;
                    response.Add(Convert.ToByte((sbyte)temp.req));
                    response.Add(Convert.ToByte((sbyte)temp.posid));
                }
                else
                {
                    response.Add(0);
                }
            }
           
            
        }
        #endregion
        //Переопределяем родительских функций под особенности MetaTrade
        #region IBroker
        //Инициализация пайпов
        public void SetServer()
        {
            //this.Server_trade = new Yummy.IO.PipeMessageServer("arbtrade_trade_" + ((MTParam)BrokerParam[0]).PipeName, RequestHandler_trade, this);
            this.Server_query = new Yummy.IO.PipeMessageServer("arbtrade_query_" + ((MTParam)BrokerParam[0]).PipeName, RequestHandler_query, this);
            //this.Server_result = new Yummy.IO.PipeMessageServer("arbtrade_result_" + DataParam[0].ToString(), RequestHandler_result, this);
        }
        //Заполнение параметров брокеров
        public override void setBrokerParam(params object[] param)
        {
            double val;
            if (param[0].ToString() == "LimitPrice")
            {
                if (Double.TryParse(param[1].ToString(), out val))
                    ((MTParam)BrokerParam[0]).LimitPrice = val;
                else
                    ((MTParam)BrokerParam[0]).LimitPrice = 0;
            }
            if (param[0].ToString() == "Name")
            {
                ((MTParam)BrokerParam[0]).PipeName = param[1].ToString();
            }
        }
        //Отмена ордера
        public override void CancelOrder()
        {
            if(isReal)
            { 
                request_type = MT_REQUEST.CANCEL_ORDER;
                send_req = true;
            }
        }
        //Закрытие позиции
        public override void ClosePosition()
        {
            //isReadyToTrade = false;
            if(isReal)
            { 
                request_type = MT_REQUEST.CLOSE_POSITION;
                send_req = true;
            }
            /*if(Position.OrderType < OrderType.LMT_Sell)
                OpenOrder(OrderType.MKT_Sell, true);
            else
                OpenOrder(OrderType.MKT_Buy, true);
            /*string mes = "Position Closed." + Position.ToString() + " Time:" + DateTime.Now.ToString("HH:mm:ss.ffffff");
            logHandler?.Message(ID, mes);*/
        }
        public void ClosePosition(int posID)
        {
            requst.Enqueue(new SomeStruct() { req = MT_REQUEST.CLOSE_POSITION, posid = posID });
        }
        //Открытие позиции
        public override void OpenPosition(OrderType type)
        {
            if (isStop)
                return;
            if (type < OrderType.LMT_Sell)
                OpenOrder(OrderType.MKT_Buy,false);
            else
                OpenOrder(OrderType.MKT_Sell, false);
        }
        //Создание и подготовка к отправке ордера
        void OpenOrder(OrderType type,bool isOpen)
        {
            OrderType orderType = type;
            double price = 0;
            /*if (OrderParam[3].ToString() == "LMT")
            {
                OrderParam[0] = MT_REQUEST.BUY_LIMIT;
                price = (double)OrderParam[2];
            }*/
            //if (OrderParam[2].ToString() == "MKT")
            //{
                if (orderType == OrderType.MKT_Buy)
                    OrderParam[0] = MT_REQUEST.BUY_MKT;
                else
                    OrderParam[0] = MT_REQUEST.SELL_MKT;
                //orderType = OrderType.MKT_Buy;
                price = Bid;
            //}
            if(isReal)
            { 
                request_type = (MT_REQUEST)OrderParam[0];
                send_req = true;
            }
            if(!isOpen)
                Position = new ArbitrageTradeSimpleBotPosition(orderType, price, DateTime.Now, OrderParam);
            else
                Position.ActualPrice = price;
            //isReadyToTrade = true;
            isPositionOpen = true;
        }
        public void OpenOrder(OrderType type, int posID)
        {
            OrderType orderType = type;
            double price = 0;
            /*if (OrderParam[3].ToString() == "LMT")
            {
                OrderParam[0] = MT_REQUEST.BUY_LIMIT;
                price = (double)OrderParam[2];
            }*/
            //if (OrderParam[2].ToString() == "MKT")
            //{
            if (orderType == OrderType.MKT_Buy)
                OrderParam[0] = MT_REQUEST.BUY_MKT;
            else
                OrderParam[0] = MT_REQUEST.SELL_MKT;
            //orderType = OrderType.MKT_Buy;
            price = Bid;
            
            //}
            if (isReal)
            {
                requst.Enqueue(new SomeStruct() { req = (MT_REQUEST)OrderParam[0], posid = posID });
                //request_type = (MT_REQUEST)OrderParam[0];
                //send_req = true;
                //send_query();
            }
        }
        //Установка параметров ордера(не используется для MT),заглушка на будущее
        public override void setOrderParam(object[] param)
        {
            OrderParam[1] = 0;//LMT Price
            //OrderParam[1] = (double)param[1];//Volume
            OrderParam[2] = param[0].ToString();//Type
            if (param[0].ToString() == "LMT")
            {
                OrderParam[1] = (double)param[2];//LMT Price
            }
        }
        public override void StartTrade()
        {
            isStop = false;
        }
        //Остановка работы с пайпами
        public override void Stop()
        {
            isStop = true;
            lock(locker2)
            {
                if (Server_trade != null)
                    Server_trade.Stop();
            }
            lock(locker1)
            {
                if (Server_query != null)
                    Server_query.Stop();
            }
        }
        #endregion
        //Инициализация брокера
        public BrokerMT(int id)
        {
            isStop = false;
            send_req = false;
            this.ID = id;
            request_type = MT_REQUEST.NONE;
            BrokerParam = new object[1];
            BrokerParam[0] = new MTParam();
            OrderParam = new object[4];
            isReadyToTrade = true;
            isReal = false;
        }

        public static List<string> ParamName()
        {
            return MTParam.paramNames();
        }
        //При уничтожении брокера нужно остановить пайпы
        public override void Dispose()
        {
            Stop();
        }
    }
}
