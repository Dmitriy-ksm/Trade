#property version   "1.0"

input string ChannelName="";
input long Magic = 1;
input double Lot = 100;
#include <Files/FilePipe.mqh>
#include <Trade/PositionInfo.mqh>
#include <Trade/Trade.mqh>
//Класс вызывающий событие при получении запроса на открытие или закрытие позиций
class mtEventHandler
{
   public:
   int EventId;
   mtEventHandler()
   {
      EventId = 5000;
   }
   void Event(long chartID)
   {
      long currChart=ChartFirst();
      //Print("Вызываю событие "+EventId);
      if(!EventChartCustom(chartID,EventId,0,0.0,""))
         Print("Ошибка "+GetLastError());
   }
};
//Класс описывающий работу пайпа
class mtsPipeMessageClient
{
private:
   string pipeName;
   CFilePipe pipe;
public:  
   uchar response[];
   bool opensucces;
//Перечисления для работы с пайпами   
enum
   {
    NONE =0,
    BUY_LIMIT = 1,
    SELL_LIMIT = 2,
    BUY_MKT = 3,
    SELL_MKT = 4,
    CLOSE_POSITION = 5,
    CANCEL_ORDER = 6
   };
   enum
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
   mtsPipeMessageClient()
   {
   }
   ~mtsPipeMessageClient()
   {
      ArrayFree(response);
   }
   
   void initialize(string _channelName)
   {
      m_trade.SetExpertMagicNumber(Magic);
      pipeName="\\\\.\\pipe\\"+_channelName;
      opensucces = false;
      ArrayResize(response,0);
   }
   bool openpipe(int _msTimeout, int _sleep)
   {
      int timeout=0;
      int sleep=_sleep;
      while (timeout<_msTimeout)
      {
         if (pipe.Open(pipeName,FILE_READ|FILE_WRITE|FILE_BIN)==INVALID_HANDLE)
         {
            Sleep(sleep);
            timeout+=sleep;
            if (timeout>=_msTimeout) return false;
         }
         else
         {
            break;
         }
      }
      return true;
   }
   void closepipe()
   {
      pipe.Close();
      opensucces = false;
   }
//Функция ожидающая команды на открытие/закрытие позиции
   uchar recive_query(int _msTimeout, int _sleep)
   {
      if(!opensucces)
        opensucces = openpipe(_msTimeout,_sleep);
      if(opensucces)
      {
         ArrayResize(response,0);
         pipe.WriteInteger(0);
         int responseSize=0;  
         if (!pipe.ReadInteger(responseSize)) return CANT_READ_RESPONS_SIZE;
         if (responseSize>1)
         {
            ArrayResize(response,responseSize);
            if (pipe.ReadArray(response)!=responseSize)   return NOT_EQUAL_RESPONS;
            MqlTradeResult result={0};
            int type = (uchar)response[0];
            int posid = 0;
            if(responseSize>1)
            {
               posid = (uchar)response[1];
            }
            switch(type)
            {
               case BUY_MKT: 
                  Print(Symbol() + " recived order "+posid+" to buy");  
                  m_trade.Buy(Lot); 
               return ORDER_CREATE;
               case SELL_MKT: 
                  Print(Symbol() + " recived order "+posid+" to sell"); 
                  m_trade.Sell(Lot); 
               return ORDER_CREATE;
               case CLOSE_POSITION:
                  Print(Symbol() + " close positions "+posid); 
                  CloseAllPositions(posid);
               return SUCCES;
               case CANCEL_ORDER: CancelOrder(); return SUCCES;
            }
         }
         /*if(OrderSend(request_trade,result))
            return ORDER_CREATE;
         else
            return CANT_CREATE_ORDED;*/
      }
      return ERROR;
   }
//Функция для отправки котировок и результатов выполнения ордеров
   bool send(uchar &_request[], int _msTimeout, int _sleep)
   {
      if(!opensucces)
         opensucces = openpipe(_msTimeout,_sleep);
      if(opensucces)
      {
         ArrayResize(response,0);
         int requestSize=ArraySize(_request);
         if (pipe.WriteInteger(requestSize)!=4) return false;
         if (pipe.WriteArray(_request)!=requestSize) return false;
         int responseSize=0;
         if (!pipe.ReadInteger(responseSize))  return false;
         if (responseSize>0)
         {
            ArrayResize(response,responseSize);
            if (pipe.ReadArray(response)!=responseSize) return false;
            return true;
         }  
      }
      return false;
   }
//Отмена ордера
   void CancelOrder()
   {
    CloseAllPositions(0);
   }
//Закрытие позиций
   bool CloseAllPositions(int posid)
   {
   /*for(int i=PositionsTotal()-1;i>=0;i--) // returns the number of current positions
         if(m_position.SelectByIndex(i)) // selects the position by index for further access to its properties
            if(m_position.Symbol()== symbol && m_position.Magic()==Magic)
            {
               if(m_position.Type()==POSITION_TYPE_BUY)
               {
                  string message = Symbol() + " open sell " + posid + " to close LongPosition";
                  Print(message);
                  m_trade.Sell(m_position.Volume());
               }
               if(m_position.Type()==POSITION_TYPE_SELL)
               {

                  string message = Symbol() + " open buy " + posid + " to close ShortPosition";
                  //Print(message);
                  m_trade.Buy(m_position.Volume());
               }
            }*/
      for(int i=PositionsTotal()-1;i>=0;i--) // returns the number of current positions
         if(m_position.SelectByIndex(i)) // selects the position by index for further access to its properties
            if(m_position.Symbol()== symbol && m_position.Magic()==Magic)
            {
               m_trade.PositionClose(m_position.Ticket()); // close a position by the specified symbol
               return true;
            }
      return false;
   }
};
//Тип данных передаваемые по пайпам
union mtsPackage
{
   double values[2];
   uchar bytes[16];
};
union mtsPackage_trade
{
   double value;
   uchar bytes[8];
};
union mtsPackage_result
{
   uchar bytes[1];
};
//Папйпы
mtsPipeMessageClient client,client_trade,client_query;
//Параметры
CPositionInfo  m_position; 
CTrade         m_trade; 
string channelName;
string symbol;
int digits;
int positionId_count;
ulong positionID[100];
//MqlTradeRequest request // Параметры ордера
//Событие получения приказа на открытие/закрытие позиции
mtEventHandler pipeListener;
//Передаваемые значения
mtsPackage package;
mtsPackage_trade package_trade;
mtsPackage_result package_result;
bool isWork;
//Инициализируем параметры
int OnInit()
{
 positionId_count =0;
   m_trade.SetExpertMagicNumber(Magic);
   digits=Digits();
   symbol=Symbol();
   channelName=Symbol();
   if (StringLen(ChannelName)>0) channelName=ChannelName;
   client.initialize("tickopt_"+channelName);
   client_trade.initialize("arbtrade_trade_"+channelName);
   client_query.initialize("arbtrade_query_"+channelName);
//Вызываем событие для чтения пайпа с запросом
   pipeListener.Event(0);
   return INIT_SUCCEEDED;
}
void OnDeinit(const int reason)
{
   Comment("");
}
//Получаем котировки и передаем их в приложение
void OnTick()
{
   package.values[0]=SymbolInfoDouble(symbol,SYMBOL_BID);
   package.values[1]=SymbolInfoDouble(symbol,SYMBOL_ASK);
   client.send(package.bytes,1000,5);
   /*isWork = client.send(package.bytes,1000,5);
   if(!isWork)
   {
      client_query.closepipe();
      client_trade.closepipe();
      client.closepipe();
   }*/
   //Print(result);
   //Comment("Connected to "+channelName);
   //Sleep(10);
}
//Событие ожидающее запросы от приложение
void OnChartEvent(const int id, const long &lparam, const double &dparam, const string &sparam)
{
   //Print("Событие c id " + id+" Ждем на "+pipeListener.EventId);
   if((long)id-1000 == pipeListener.EventId)
   {
         //Print("Наше событие");
	//Ждем нового запроса по пайпу
         client_query.recive_query(1000,5);
           client_query.opensucces = false;
       //  Print("Получили сообщение");
	//После выполнения события снова вызываем событие что бы ожидать следующего зарпоса
         pipeListener.Event(0);
   }
}

/*void  OnTradeTransaction(const MqlTradeTransaction&    trans, const MqlTradeRequest& request, const MqlTradeResult& result)
   {
         
         
         ENUM_ORDER_STATE lastOrderState=trans.order_state;
         //Print("State: " + lastOrderState);
         if(trans.type==TRADE_TRANSACTION_ORDER_ADD)
         {
            
            //Print("Position opened with price: " + trans.price);
         }
         if(lastOrderState==ORDER_STATE_PLACED)
         {
            Print("Position opened with price: " + trans.price);
         }
   }*/
//При выполнении ордера отправляем параметры позиции в приложение
/*void OnTrade()
{
         int total=PositionsTotal(); // количество открытых позиций   
         //--- перебор всех открытых позиций
            for(int i=total-1; i>=0; i--)
            {
               //--- параметры ордера
               ulong  position_ticket=PositionGetTicket(i);                                    // тикет позиции
               string position_symbol=PositionGetString(POSITION_SYMBOL);                      // символ 
               ulong  magic=PositionGetInteger(POSITION_MAGIC);  // MagicNumber позиции
               double Price=PositionGetDouble(POSITION_PRICE_OPEN);
               ENUM_POSITION_TYPE Type=(ENUM_POSITION_TYPE)PositionGetInteger(POSITION_TYPE);  // тип позиции 
               bool flag= true;
               if(symbol==position_symbol||Magic==magic)
               {
                  for(int i=0;i<positionId_count;i++)
                  {
                     if(positionID[i]==position_ticket)
                     flag = false;
                  }
                  positionID[positionId_count] = position_ticket;
                  positionId_count++;
                  if(positionId_count==100)
                  {
                  positionId_count=1;
                  positionID[0]=positionID[99];
                  }
                  //package_trade.value = Price;
                  //client_trade.send(package_trade.bytes,1000,5);
                  //if(flag)
                  //Print("Position opened with price: " + Price);
               }
            }
}*/

