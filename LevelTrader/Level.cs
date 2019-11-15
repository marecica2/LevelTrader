using System;

namespace cAlgo
{
    public enum Direction
    {
        LONG,
        SHORT,
    }

    public class Level
    {
        public string Label { get; set; }
        public string Symbol { get; set; }
        public DateTime ValidFrom { get; set; }
        public DateTime ValidTo { get; set; }
        public double EntryPrice { get; set; }
        public double StopLossPrice { get; set; }
        public double ProfitTargetPrice { get; set; }
        public double ActivatePrice { get; set; }
        public double DeactivatePrice { get; set; }
        public bool LevelActivated { get; set; }
        public bool Traded { get; set; }
        public Direction Direction { get; set; }

        override public string ToString()
        {
            return Label + " " + Symbol + 
                " Price: " + EntryPrice + 
                " Validity:" + ValidFrom + " - " + ValidTo + 
                " Direction: " + Direction +
                " Traded: " + Traded;
        }
    }
}
