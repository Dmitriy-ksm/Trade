using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Optimizer_Trade.Processors
{
    class OptToMT : ProcessorBase
    {
        object locker1 = new object();
        object locker2 = new object();
        public Yummy.IO.PipeMessageServer Server_Put
        {
            get;
            set;
        }
        public Yummy.IO.PipeMessageServer Server_Call
        {
            get;
            set;
        }
        struct SomeStruct
        {
            public double bid;
            public double ask;
        }
        Queue<SomeStruct> requst_put;
        Queue<SomeStruct> requst_call;
        int Put, Call;
        string name;
        double put_bid, put_ask, call_bid, call_ask;
        List<IProcessorInput> inputs;
        ILogger logger;
        public override void Deinitialize()
        {
            lock(locker1)
            { 
            if (Server_Put != null)
                Server_Put.Stop();
            }
            lock (locker2)
            {
                if (Server_Call != null)
                    Server_Call.Stop();
            }
        }
        public override bool Initialize(List<IProcessorInput> inputs, string parameters, ILogger logger)
        {
            requst_put = new Queue<SomeStruct>();
            requst_call = new Queue<SomeStruct>();
            this.inputs = inputs;
            this.logger = logger;
            ParametersParser parser = CreateParser(parameters);
            Put = parser.GetInt("Put");
            if (Put < 0 || Put >= inputs.Count) return false;
            Call = parser.GetInt("Call");
            if (Call < 0 || Call >= inputs.Count) return false;
            name = parser.GetString("Name");
            SetServer();
            return true;
        }
        protected override void FillDefaultParameters(Dictionary<string, string> p)
        {
            p["Put"] = "0";
            p["Call"] = "1";
            p["name"] = "name";
        }
        public override string[] Comments()
        {
            return new string[] { "Позиция Put в input-ах", "Позиция Call в input-ах", "Имя пайп канала"};
        }
        public override double Process(DateTime time)
        {
            put_ask = inputs[Put].Ask;
            put_bid = inputs[Put].Bid;
            requst_put.Enqueue(new SomeStruct() { ask = put_ask, bid = put_bid });
            if(requst_put.Count>100)
            {
                requst_put.Clear();
            }
            call_ask = inputs[Call].Ask;
            call_bid = inputs[Call].Bid;
            requst_call.Enqueue(new SomeStruct() { ask = call_ask, bid = call_bid });
            if (requst_call.Count > 100)
            {
                requst_call.Clear();
            }
           
            return 0;
        }

        void RequestHandlerPut(Yummy.IO.MessageServer source, object param, byte[] request, List<byte> response)
        {
            lock(locker1)
            {
                if (requst_put.Count != 0)
                {
                    SomeStruct temp = requst_put.Dequeue();
                    byte[] ask = BitConverter.GetBytes(temp.ask);
                    byte[] bid = BitConverter.GetBytes(temp.bid);
                    byte[] opt = bid.Concat(ask).ToArray();
                    response.AddRange(opt);
                }
                else
                {
                    response.Add(1);
                }
            }
            
        }
        void RequestHandlerCall(Yummy.IO.MessageServer source, object param, byte[] request, List<byte> response)
        {
            lock(locker2)
            {
                if (requst_call.Count != 0)
                {
                    SomeStruct temp = requst_call.Dequeue();
                    byte[] ask = BitConverter.GetBytes(temp.ask);
                    byte[] bid = BitConverter.GetBytes(temp.bid);
                    byte[] opt = bid.Concat(ask).ToArray();
                    response.AddRange(opt);
                }
                else
                {
                    response.Add(0);
                }
            }
        }
        public void SetServer()
        {
            this.Server_Put = new Yummy.IO.PipeMessageServer("put_" + name, RequestHandlerPut, this);
            this.Server_Call = new Yummy.IO.PipeMessageServer("call_" + name, RequestHandlerCall, this);
        }
    }
}
