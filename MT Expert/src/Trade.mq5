//+------------------------------------------------------------------+
//|                                                        Trade.mq5 |
//|                                                                  |
//|                                                                  |
//+------------------------------------------------------------------+
#property copyright ""
#property link      ""
#property version   "1.00"
//--- input parameters
input string   pipeName;
input int      Strike = 0;
input int      period=100;
input bool     spreadFilt=true;
input int      midPricePutPeriod=25;
input int      midPriceCallPeriod=25;
input float    spreadKoef=1.1;
input int      ZigZag1MinValue = 250;
input int      ZigZag2MinValue = 250;
input int      ZigZag2_2MinValue = 250;
input short    TradeType=0;
input short    divergenceOpen=2;
input short    divergenceClose=1;
input int      takeProfit=0;
input int      stopLoss=0;
input float    Point=0.01;
input bool     expand=true;
input long     Magic = 1;
input double   Lot = 1;
#include <Files/FilePipe.mqh>
#include <Trade/PositionInfo.mqh>
#include <Trade/Trade.mqh>
struct ProbaStruct
{
   double GapValue;
   double price;
} structura;
enum {
 NONE =0,
 SHORT = 1,
 LONG = 2
};
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
      if(!EventChartCustom(chartID,EventId,0,0.0,""))
         Print("Ошибка "+GetLastError());
   }
};
class mtsPipeMessageClient
{
private:
   string pipeName;
   CFilePipe pipe;
public:  
   uchar response[];
   double bid,ask;
   bool opensucces;
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
   bid =0;
   ask = 0;
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
   int recive(int _msTimeout, int _sleep)
   {
      if(!opensucces)
         opensucces = openpipe(_msTimeout,_sleep);
      if(opensucces)
      {
         ArrayResize(response,0);
         pipe.WriteInteger(0);
         int responseSize=0;  
         if (!pipe.ReadInteger(responseSize)) return CANT_READ_RESPONS_SIZE;
         if (responseSize==16)
         {
            ArrayResize(response,responseSize);
            if (pipe.ReadArray(response)!=responseSize)   return NOT_EQUAL_RESPONS;
            mtsPackage resp;
            ArrayCopy(resp.bytes,response,0,0,16);
            bid = resp.values[0];
            ask = resp.values[1];
            Print(bid+" "+ask);
            return SUCCES;
         }
      }
      return ERROR;
   }
};
//Тип данных передаваемые по пайпам
union mtsPackage
{
   double values[2];
   uchar bytes[16];
};
//Параметры
CPositionInfo  m_position; 
CTrade         m_trade; 
//Пайпы
mtsPipeMessageClient client_put, client_call;
//Событие получения put call котировок
mtEventHandler pipeListener, tradeworker;
string channelName;
string symbol;
int digits;
double bid,ask;
int positionId_count;
ulong positionID[100];
//ГЭП
        double spread_col_call[];
        double spread_col_put[];
        int spread_count_put, spread_count_call;
double midle_opt_price_put, midle_opt_price_call;
        double x[];
        int pos;
        double sumx;
        bool ready;
//ЗИГЗАГ_1
double prev_peak_1, next_peak_1, next_value_to_change_1;
double ZigZagValue_1;        
//ЗИГЗАГ_2
double prev_peak_2, next_peak_2, next_value_to_change_2;
double ZigZagValue_2;
//ЗИГЗАГ_2_2
double prev_peak_2_2, next_peak_2_2, next_value_to_change_2_2;
double ZigZagValue_2_2;
//Трейд
ProbaStruct MinValueOpen_1[], MinValueOpen_2[], MinValueClose_1[], MinValueClose_2[], MaxValueOpen_1[], MaxValueOpen_2[], MaxValueClose_1[], MaxValueClose_2[];
ProbaStruct MaxValueClose_2_2[], MinValueClose_2_2[];
int minOpenCount1,minCloseCount1,maxOpenCount1,maxCloseCount1,minOpenCount2,minCloseCount2,maxOpenCount2,maxCloseCount2;
int minCloseCount2_2, maxCloseCount2_2;
bool expand_short,expand_long;
short posopen;
double LongLastPrice, ShortLastPrice,LongLastGap, ShortLastGap;
bool openshort, openlong, closeshort, closelong;
ProbaStruct OpenShort,OpenLong,CrashLong,CloseShort,CrashShort,CloseLong;
short last_value = 0;

//+------------------------------------------------------------------+
//| Expert initialization function                                   |
//+------------------------------------------------------------------+
int OnInit()
  {
//---
   ArrayResize(spread_col_call,midPriceCallPeriod);
   ArrayResize(spread_col_put,midPricePutPeriod);     
   ArrayResize(x,period); 
   spread_count_call = 0;
   spread_count_put = 0;
   midle_opt_price_call = 0;
   midle_opt_price_put = 0;
     prev_peak_1 = 0;
     next_value_to_change_1 = 0; 
      prev_peak_2 = 0;
     next_value_to_change_2 = 0; 
      ArrayResize(MinValueOpen_1,divergenceOpen+1);
      ArrayResize(MinValueOpen_2,divergenceOpen+1);
      ArrayResize(MaxValueOpen_1,divergenceOpen+1);
      ArrayResize(MaxValueOpen_2,divergenceOpen+1);
      ArrayResize(MinValueClose_1,divergenceClose+1);
      ArrayResize(MinValueClose_2,divergenceClose+1);
      ArrayResize(MaxValueClose_1,divergenceClose+1);
      ArrayResize(MaxValueClose_2,divergenceClose+1);
       ArrayResize(MinValueClose_2_2,divergenceClose+1);
      ArrayResize(MaxValueClose_2_2,divergenceClose+1);
   minOpenCount1 = 0;
   minCloseCount1 = 0;
   maxOpenCount1 = 0;
   maxCloseCount1 = 0;
   minOpenCount2 = 0;
   minCloseCount2 = 0;
   maxOpenCount2 = 0;
   maxCloseCount2 = 0;  
      minCloseCount2_2 = 0;
      maxCloseCount2_2 = 0;  
   posopen = NONE;
   openshort = false;
   openlong = false;
   closeshort = false; 
   closelong = false;
    positionId_count =0;
   m_trade.SetExpertMagicNumber(Magic);
   digits=Digits();
   symbol=Symbol();
   channelName=Symbol();
   if (StringLen(pipeName)>0) channelName=pipeName;
   client_put.initialize("put_"+channelName);
   client_call.initialize("call_"+channelName);
   pipeListener.Event(0);
   bid =0;
   ask =0;
   tradeworker.EventId = 5001;
//---
   return(INIT_SUCCEEDED);
  }
//+------------------------------------------------------------------+
//| Expert deinitialization function                                 |
//+------------------------------------------------------------------+
void OnDeinit(const int reason)
  {
//---
  }
//+------------------------------------------------------------------+
//| Expert tick function                                             |
//+------------------------------------------------------------------+
void OnTick()
  {
//---
      bid = SymbolInfoDouble(symbol,SYMBOL_BID);
      ask = SymbolInfoDouble(symbol,SYMBOL_ASK);
      tradeworker.Event(0);
  }
//+------------------------------------------------------------------+
void OnChartEvent(const int id, const long &lparam, const double &dparam, const string &sparam)
{
   if((long)id-1000 == pipeListener.EventId)
   {
           client_put.recive(1000,5);
           client_put.opensucces = false;
            client_call.recive(1000,5);
           client_call.opensucces = false;
         pipeListener.Event(0);
   }
   if((long)id-1000 == tradeworker.EventId)
   {
      if (client_call.bid <= 0) return;
      if (client_call.bid > client_call.ask) return;
      if (client_put.bid <= 0) return;
      if (client_put.bid > client_put.ask) return;
      //GAP
      bool flag = true;
      double mean;
      double spread, spread_midle, spread_howmuch;
      if (spreadFilt)
            {
                if (spread_worker(spread_col_call, midPriceCallPeriod, spread_count_call, spreadKoef, client_call.ask, client_call.bid , spread, spread_midle, spread_howmuch))
                {
                    flag = false;
                }
                if (spread_worker(spread_col_put, midPricePutPeriod, spread_count_put, spreadKoef, client_put.ask, client_put.bid, spread, spread_midle, spread_howmuch))
                {
                    flag = false;
                }
            }
        if (flag)
            {
                midle_opt_price_put = (client_put.ask + client_put.bid) / 2;
                midle_opt_price_call = (client_call.ask + client_call.bid) / 2;
            }
            if (midle_opt_price_put > client_put.ask || midle_opt_price_put <client_put.bid)
            {
                midle_opt_price_put = (client_put.ask + client_put.bid) / 2;
                ArrayFill(spread_col_put,0,midPricePutPeriod,0);
            }
            if (midle_opt_price_call > client_call.ask || midle_opt_price_call < client_call.bid)
            {
                midle_opt_price_call = (client_call.ask + client_call.bid) / 2;
                ArrayFill(spread_col_call,0,midPriceCallPeriod,0);
            }
      double gap = (Strike - midle_opt_price_put + midle_opt_price_call - (bid + ask) / 2 ) / Point;
      gap -= new_gap(x, pos, ready, gap);
      //Trade
      if(posopen == LONG)
      {
         if(takeProfit > 0)
         {
            if(bid >= LongLastPrice + takeProfit)
            {
               CloseAllPositions();
               posopen = NONE;
            }
         }
         if(stopLoss >0)
         {
            if(bid<= LongLastPrice - stopLoss)
            {
               CloseAllPositions();
               posopen = NONE;
            }
         }
      }
      if(posopen == SHORT)
      {
         if(takeProfit > 0)
         {
            if(ask <= LongLastPrice - takeProfit)
            {
               CloseAllPositions();
               posopen = NONE;
            }
         }
         if(stopLoss >0)
         {
            if(ask >= LongLastPrice + stopLoss)
            {
               CloseAllPositions();
               posopen = NONE;
            }
         }
      }
      if(last_value<0)
      {
         if(posopen == LONG)
         {
            if(gap>CrashLong.GapValue)
            {
               CloseAllPositions();
               posopen = NONE;
               return;
            }
            if(expand_long)
            {
                if (gap < LongLastGap && ask < LongLastPrice)
                {
                  LongLastGap = gap;
                  LongLastPrice = ask;
                  m_trade.Buy(Lot);
                  expand_long = false;
                }
            }
         }
         if(posopen == NONE)
         {
            if(openlong)
            {
                if (gap < OpenLong.GapValue && ask < OpenLong.price)
                {
                  LongLastGap = gap;
                  LongLastPrice = ask;
                  m_trade.Buy(Lot);
                  openlong = false;
                  posopen = LONG;
                  return;
                }
            }
         }
         if(posopen == SHORT)
         {
            if(closeshort)
            {
               if(gap< CloseShort.GapValue && ask< CloseShort.price)
               {
                  CloseAllPositions();
                  closeshort = false;
                  posopen = NONE;
                  return;
               }
            }
         }
      }
      if(last_value > 0)
      {
         if( posopen == SHORT)
         {
            if(gap<CrashShort.GapValue)
            {
             posopen = NONE;
             CloseAllPositions();
             return;
            }
            if(expand_short)
            {
                if (gap > ShortLastGap && bid < ShortLastPrice)
                {
                  ShortLastGap = gap;
                  ShortLastPrice = bid;
                  m_trade.Sell(Lot);
                  expand_short = false;
                }
            }
         }
         if( posopen == LONG)
         {
            if(closelong)
            {
               if (gap > CloseLong.GapValue && bid > CloseLong.price)
               {
                  CloseAllPositions();
                  posopen = NONE;
                  closelong = false;
                  return;
               }
            }
         }
         if(posopen==NONE)
         {
            if(openshort)
            {
               if (gap > OpenShort.GapValue && bid > OpenShort.price)
               {
                  ShortLastGap = gap;
                  ShortLastPrice = bid;
                  posopen = SHORT;
                  m_trade.Sell(Lot);
                  openshort = false;
                  return;
               }
            }
         }
      }
      //ЗигЗаг_1
       if (next_value_to_change_1 == 0)
            {
                if (gap > ZigZag1MinValue)
                {
                    next_peak_1 = gap;
                    next_value_to_change_1 = -ZigZag1MinValue;
                }
                if(gap < -ZigZag1MinValue)
                {
                    next_peak_1 = gap;
                    next_value_to_change_1 = ZigZag1MinValue;
                }
            }
       if (next_value_to_change_1 < 0)
            {
                if (gap > next_peak_1)
                {
                    next_peak_1 = gap;
                }
                if (gap < -ZigZag1MinValue)
                {
                    ZigZagValue_1 = next_peak_1;
                    if(TradeType == 0)
                        ZigZag1Type1("Max", ZigZagValue_1, ask);
                    else
                        ZigZag1Type2("Max", ZigZagValue_1, ask);
                    next_value_to_change_1 = ZigZag1MinValue;
                    next_peak_1 = gap;
                }
            }
       if (next_value_to_change_1 > 0)
            {
                if (gap < next_peak_1)
                {
                    next_peak_1 = gap;
                }
                if (gap > ZigZag1MinValue)
                {
                    ZigZagValue_1 = next_peak_1;
                    if(TradeType == 0)
                        ZigZag1Type1("Min", ZigZagValue_1, bid);
                    else
                        ZigZag1Type2("Min", ZigZagValue_1, bid);
                    next_value_to_change_1 = -ZigZag1MinValue;
                    next_peak_1 = gap;
                }
            }
      //ЗигЗаг_2
       if (next_value_to_change_2 == 0)
            {
                if (gap > ZigZag2MinValue)
                {
                    next_peak_2 = gap;
                    next_value_to_change_2 = -ZigZag2MinValue;
                }
                if(gap < -ZigZag2MinValue)
                {
                    next_peak_2 = gap;
                    next_value_to_change_2 = ZigZag2MinValue;
                }
            }
       if (next_value_to_change_2 < 0)
            {
                if (gap > next_peak_2)
                {
                    next_peak_2 = gap;
                }
                if (gap < -ZigZag2MinValue)
                {
                    ZigZagValue_2 = next_peak_2;
                    if(TradeType == 0)
                        ZigZag2Type1("Max", ZigZagValue_2, ask);
                    else
                        ZigZag2Type2("Max", ZigZagValue_2, ask);
                    next_value_to_change_2 = ZigZag2MinValue;
                    next_peak_2 = gap;
                }
            }
       if (next_value_to_change_2 > 0)
            {
                if (gap < next_peak_2)
                {
                    next_peak_2 = gap;
                }
                if (gap > ZigZag2MinValue)
                {
                    ZigZagValue_2 = next_peak_2;
                    if(TradeType ==0)
                        ZigZag2Type1("Min", ZigZagValue_2, bid);
                    else
                        ZigZag2Type2("Min", ZigZagValue_2, bid);
                    next_value_to_change_2 = -ZigZag2MinValue;
                    next_peak_2 = gap;
                }
            }
       //ЗигЗаг_2_2
       if (next_value_to_change_2_2 == 0)
            {
                if (gap > ZigZag2_2MinValue)
                {
                    next_peak_2_2 = gap;
                    next_value_to_change_2_2 = -ZigZag2_2MinValue;
                }
                if(gap < -ZigZag2_2MinValue)
                {
                    next_peak_2_2 = gap;
                    next_value_to_change_2_2 = ZigZag2_2MinValue;
                }
            }
       if (next_value_to_change_2_2 < 0)
            {
                if (gap > next_peak_2_2)
                {
                    next_peak_2_2 = gap;
                }
                if (gap < -ZigZag2_2MinValue)
                {
                    ZigZagValue_2_2 = next_peak_2_2;
                    if(TradeType == 2)
                        ZigZag2_2Type2("Max", ZigZagValue_2_2, ask);
                    next_value_to_change_2_2 = ZigZag2_2MinValue;
                    next_peak_2_2 = gap;
                }
            }
       if (next_value_to_change_2_2 > 0)
            {
                if (gap < next_peak_2_2)
                {
                    next_peak_2_2 = gap;
                }
                if (gap > ZigZag2_2MinValue)
                {
                    ZigZagValue_2_2 = next_peak_2_2;
                    if(TradeType ==2)
                        ZigZag2_2Type2("Min", ZigZagValue_2_2, bid);
                    next_value_to_change_2_2 = -ZigZag2_2MinValue;
                    next_peak_2_2 = gap;
                }
            }
   }
}
   void CloseAllPositions()
   {
      for(int i=PositionsTotal()-1;i>=0;i--) // returns the number of current positions
         if(m_position.SelectByIndex(i)) // selects the position by index for further access to its properties
            if(m_position.Symbol()== symbol && m_position.Magic()==Magic)
               m_trade.PositionClose(m_position.Ticket()); // close a position by the specified symbol
   }
void ZigZag1Type1(string ZigZagT, double ZigZagValue, double price)
{
   ZigZagType1(ZigZagT,ZigZagValue,price,MinValueOpen_1,MaxValueOpen_1,MinValueClose_1,MaxValueClose_1,minOpenCount1,minCloseCount1,maxOpenCount1,maxCloseCount1);
}
void ZigZag2Type1(string ZigZagT, double ZigZagValue, double price)
{
   ZigZagType1(ZigZagT,ZigZagValue,price,MinValueOpen_2,MaxValueOpen_2,MinValueClose_2,MaxValueClose_2,minOpenCount2,minCloseCount2,maxOpenCount2,maxCloseCount2);
}
void ZigZag1Type2(string ZigZagT, double ZigZagValue, double price)
{
   ProbaStruct Temp_value;
   Temp_value.GapValue = ZigZagValue;
   Temp_value.price = price;
   if(divergenceOpen > 0)
   {
      ArrayCopy(MinValueOpen_1,MinValueOpen_1,0,1,divergenceOpen);
      ArrayCopy(MaxValueOpen_1,MaxValueOpen_1,0,1,divergenceOpen);
   }
    if(ZigZagT == "Min")
   {
      last_value = -1;
      MinValueOpen_1[minOpenCount1] = Temp_value;
      if(posopen == SHORT)
      {
         if(expand)
         {
            if(divergenceOpen > 0)
            {
               if(Temp_value.GapValue > MinValueOpen_1[minOpenCount1-1].GapValue && Temp_value.price > MinValueOpen_1[minOpenCount1-1].price)
                  expand_short = true;
               else
                  expand_short = false;
            }
            else
               expand_short = true;
         }
      }
      minOpenCount1++;
      if(posopen == NONE)
      {
         int divergenceOpenCounter =0;
         if(divergenceOpen>0)
         for(int i=1;i<=divergenceOpen;i++)
         {
            if(MinValueOpen_1[i].GapValue > MinValueOpen_1[0].GapValue && MinValueOpen_1[i].price > MinValueOpen_1[i-1].price)
            {
               divergenceOpenCounter++;
            }
         }
         if(divergenceOpenCounter == divergenceOpen || divergenceOpen == 0)
         {
            OpenShort = Temp_value;
            CrashShort = MinValueOpen_1[0];
            openshort = true;
         }
         else
         {
            openshort = false;
         }
      }
      if(minOpenCount1 == divergenceOpen+1)
         minOpenCount1 =0;
      if(divergenceOpen>0)
         minOpenCount1 = 1;
   }
   if(ZigZagT == "Max")
   {
      last_value = 1;
      MaxValueOpen_1[maxOpenCount1] = Temp_value;
      if(posopen == LONG)
      {
         if(expand)
         {
            if(divergenceOpen > 0)
            {
               if(Temp_value.GapValue < MaxValueOpen_1[maxOpenCount1-1].GapValue && Temp_value.price > MaxValueOpen_1[maxOpenCount1-1].price)
                  expand_long = true;
               else
                  expand_long = false;
            }
            else
               expand_long = true;
         }
      }
      maxOpenCount1++;
      if(posopen == NONE)
      {
         int divergenceOpenCounter =0;
         if(divergenceOpen>0)
         for(int i=1;i<=divergenceOpen;i++)
         {
            if(MaxValueOpen_1[i].GapValue < MaxValueOpen_1[0].GapValue && MaxValueOpen_1[i].price < MaxValueOpen_1[i-1].price)
            {
               divergenceOpenCounter++;
            }
         }
         if(divergenceOpenCounter == divergenceOpen || divergenceOpen == 0)
         {
            OpenLong = Temp_value;
            CrashLong = MaxValueOpen_1[0];
            openlong = true;
         }
         else
         {
            openlong = false;
         }
      }
      if(maxOpenCount1 == divergenceOpen+1)
         maxOpenCount1 =0;
      if(divergenceOpen>0)
         maxOpenCount1 = 1;
   }
}
void ZigZag2Type2(string ZigZagT, double ZigZagValue, double price)
{
   ZigZag2HelperType2(ZigZagT,ZigZagValue,price,MinValueClose_2,MaxValueClose_2,minCloseCount2,maxCloseCount2);
}
void ZigZag2_2Type2(string ZigZagT, double ZigZagValue, double price)
{
   ZigZag2HelperType2(ZigZagT,ZigZagValue,price,MinValueClose_2_2,MaxValueClose_2_2,minCloseCount2_2,maxCloseCount2_2);
}
void ZigZag2HelperType2(string ZigZagT,double ZigZagValue, double price,
                        ProbaStruct &MinValueClose[], ProbaStruct &MaxValueClose[],
                        int &minCloseCount, int &maxCloseCount)
{
   ProbaStruct Temp_value;
   Temp_value.GapValue = ZigZagValue;
   Temp_value.price = price;
   if(divergenceClose > 0)
   {
      ArrayCopy(MinValueClose,MinValueClose,0,1,divergenceOpen);
      ArrayCopy(MaxValueClose,MaxValueClose,0,1,divergenceOpen);   
   }
   if(ZigZagT == "Min")
   {
      last_value = -1;
      MinValueClose[minCloseCount] = Temp_value;
      minCloseCount++;
      if(posopen == LONG)
      {
         int divergenceCloseCounter = 0;
         if(divergenceClose>0)
         for(int i=1;i<=divergenceClose;i++)
         {
            if(MinValueClose[i].GapValue > MinValueClose[0].GapValue && MinValueClose[i].price > MinValueClose[i-1].price)
            {
               divergenceCloseCounter++;
            }
         }
         if(divergenceCloseCounter == divergenceClose || divergenceClose == 0)
         {
            closelong = true;
            CloseLong = Temp_value;
         }
         else
         {
            closelong = false;
         }
      }
      if(minCloseCount == divergenceClose+1)
         minCloseCount =0;
      if(divergenceClose>0)
         minCloseCount = 1;
   }
   if(ZigZagT == "Max")
   {
      last_value = 1;
      MaxValueClose[maxCloseCount] = Temp_value;
      maxCloseCount++;
      if(posopen == SHORT)
      {
         int divergenceCloseCounter = 0;
         if(divergenceClose>0)
         for(int i=1;i<=divergenceClose;i++)
         {
            if(MaxValueClose[i].GapValue < MaxValueClose[0].GapValue && MaxValueClose[i].price < MaxValueClose[i-1].price)
            {
               divergenceCloseCounter++;
            }
         }
         if(divergenceCloseCounter == divergenceClose || divergenceClose == 0)
         {
            closeshort = true;
            CloseShort = Temp_value;
         }
         else
         {
            closeshort = false;
         }
      }
      if(maxCloseCount == divergenceClose+1)
         maxCloseCount =0;
      if(divergenceClose>0)
         maxCloseCount = 1;
   }
}
void ZigZagType1(string ZigZagT, double ZigZagValue, double price, 
                  ProbaStruct &MinValueOpen[], ProbaStruct &MaxValueOpen[], 
                  ProbaStruct &MinValueClose[], ProbaStruct &MaxValueClose[],
                  int &minOpenCount, int &minCloseCount, int &maxOpenCount, int & maxCloseCount) 
{
   ProbaStruct Temp_value;
   Temp_value.GapValue = ZigZagValue;
   Temp_value.price = price;
   if(divergenceOpen > 0)
   {
      ArrayCopy(MinValueOpen,MinValueOpen,0,1,divergenceOpen);
      ArrayCopy(MaxValueOpen,MaxValueOpen,0,1,divergenceOpen);
   }
   if(divergenceClose > 0)
   {
      ArrayCopy(MinValueClose,MinValueClose,0,1,divergenceOpen);
      ArrayCopy(MaxValueClose,MaxValueClose,0,1,divergenceOpen);   
   }
   if(ZigZagT == "Min")
   {
      last_value = -1;
      MinValueOpen[minOpenCount] = Temp_value;
      MinValueClose[minCloseCount] = Temp_value;
      if(posopen == SHORT)
      {
         if(expand)
         {
            if(divergenceOpen > 0)
            {
               if(Temp_value.GapValue > MinValueOpen[minOpenCount-1].GapValue && Temp_value.price > MinValueOpen[minOpenCount-1].price)
                  expand_short = true;
               else
                  expand_short = false;
            }
            else
               expand_short = true;
         }
      }
      minOpenCount++;
      minCloseCount++;
      if(posopen == NONE)
      {
         int divergenceOpenCounter =0;
         if(divergenceOpen>0)
         for(int i=1;i<=divergenceOpen;i++)
         {
            if(MinValueOpen[i].GapValue > MinValueOpen[0].GapValue && MinValueOpen[i].price > MinValueOpen[i-1].price)
            {
               divergenceOpenCounter++;
            }
         }
         if(divergenceOpenCounter == divergenceOpen || divergenceOpen == 0)
         {
            OpenShort = Temp_value;
            CrashShort = MinValueOpen[0];
            openshort = true;
         }
         else
         {
            openshort = false;
         }
      }
      if(posopen == LONG)
      {
         int divergenceCloseCounter = 0;
         if(divergenceClose>0)
         for(int i=1;i<=divergenceClose;i++)
         {
            if(MinValueClose[i].GapValue > MinValueClose[0].GapValue && MinValueClose[i].price > MinValueClose[i-1].price)
            {
               divergenceCloseCounter++;
            }
         }
         if(divergenceCloseCounter == divergenceClose || divergenceClose == 0)
         {
            closelong = true;
            CloseLong = Temp_value;
         }
         else
         {
            closelong = false;
         }
      }
      if(minOpenCount == divergenceOpen+1)
         minOpenCount =0;
      if(minCloseCount == divergenceClose+1)
         minCloseCount =0;
      if(divergenceOpen>0)
         minOpenCount = 1;
      if(divergenceClose>0)
         minCloseCount = 1;
   }
   if(ZigZagT == "Max")
   {
      last_value = 1;
      MaxValueOpen[maxOpenCount] = Temp_value;
      MaxValueClose[maxCloseCount] = Temp_value;
      if(posopen == LONG)
      {
         if(expand)
         {
            if(divergenceOpen > 0)
            {
               if(Temp_value.GapValue < MaxValueOpen[maxOpenCount-1].GapValue && Temp_value.price > MaxValueOpen[maxOpenCount-1].price)
                  expand_long = true;
               else
                  expand_long = false;
            }
            else
               expand_long = true;
         }
      }
      maxOpenCount++;
      maxCloseCount++;
      if(posopen == NONE)
      {
         int divergenceOpenCounter =0;
         if(divergenceOpen>0)
         for(int i=1;i<=divergenceOpen;i++)
         {
            if(MaxValueOpen[i].GapValue < MaxValueOpen[0].GapValue && MaxValueOpen[i].price < MaxValueOpen[i-1].price)
            {
               divergenceOpenCounter++;
            }
         }
         if(divergenceOpenCounter == divergenceOpen || divergenceOpen == 0)
         {
            OpenLong = Temp_value;
            CrashLong = MaxValueOpen[0];
            openlong = true;
         }
         else
         {
            openlong = false;
         }
      }
      if(posopen == SHORT)
      {
         int divergenceCloseCounter = 0;
         if(divergenceClose>0)
         for(int i=1;i<=divergenceClose;i++)
         {
            if(MaxValueClose[i].GapValue < MaxValueClose[0].GapValue && MaxValueClose[i].price < MaxValueClose[i-1].price)
            {
               divergenceCloseCounter++;
            }
         }
         if(divergenceCloseCounter == divergenceClose || divergenceClose == 0)
         {
            closeshort = true;
            CloseShort = Temp_value;
         }
         else
         {
            closeshort = false;
         }
      }
      if(maxOpenCount == divergenceOpen+1)
         maxOpenCount =0;
      if(maxCloseCount == divergenceClose+1)
         maxCloseCount =0;
      if(divergenceOpen>0)
         maxOpenCount = 1;
      if(divergenceClose>0)
         maxCloseCount = 1;
   }
}
bool spread_worker(double &spread_col[], int size, int &spread_count, double koef, double ask, double bid, double &spread, double& midle_spread, double &howmuch)
        {
            spread = 0;
            midle_spread = 0;
            howmuch = 0;
            spread = ((ask - bid) / Point);
            double spread_sum = 0;
            int counter = 0;
            for(int i=0;i<size;i++)
            {
                if(spread_col[i] != 0)
                {
                    spread_sum += spread_col[i];
                    counter++;
                }
            }
            midle_spread = spread_sum / counter;
            howmuch = spread / midle_spread;
            if (howmuch > koef)
                return true;
            spread_col[spread_count] = spread;
            spread_count++;
            if (spread_count == size)
                spread_count = 0;
            return false;
        }
double new_gap(double &x[], int &pos, bool &ready, double gap)
        {
            double new_gap = 0;
            x[pos] = gap;
            pos++;
            if (pos == period)
            {
                pos = 0;
                ready = true;
            }
            if (ready)
            {
                double sum = 0;
                for(int i=0;i<period;i++)
                {
                 sum+=x[i];
                }
                new_gap = sum / period;
            }
            else
            {
                double sum = 0;
                int counter = 0;
                for(int i=0;i<period;i++)
                {
                  if(x[i]!=0)
                  {
                     sum+=x[i];
                     counter++;
                  }
                 
                }
                new_gap = sum / counter;
            }
            return new_gap;
        }
