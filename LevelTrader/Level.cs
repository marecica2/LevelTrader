using System;

namespace cAlgo
{
    public class Level
    {
        public string Label { get; set; }
        public string Symbol { get; set; }
        public double EntryPrice { get; set; }
        public double StopLossPrice { get; set; }
        public double ProfitTargetPrice { get; set; }
        public bool Traded { get; set; }
        public DateTime ValidFrom { get; set; }
        public DateTime ValidTo { get; set; }

        override public string ToString()
        {
            return Label + " " + Symbol + " " + EntryPrice + " " + ValidFrom + " " + ValidTo + " traded=" + Traded;
        }
    }
}
