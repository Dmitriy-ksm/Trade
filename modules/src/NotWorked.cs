using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Optimizer_Trade.Processors
{
    class NotWorked : ProcessorBase
    {
        List<IProcessorInput> inputs;
        ILogger logger;
        protected override void FillDefaultParameters(Dictionary<string, string> p)
        {
        }
        public override string[] Comments()
        {
            return new string[] {};
        }
        public override bool Initialize(List<IProcessorInput> inputs, string parameters, ILogger logger)
        {
            return true;
        }

        public override double Process(DateTime time)
        {
            return 0;
        }
        public override void Deinitialize()
        {
        }
    }
}
