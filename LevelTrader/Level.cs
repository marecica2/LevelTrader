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
        public string Id { get; set; }
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
        public bool LevelDeactivated { get; set; }
        public bool Traded { get; set; }
        public Direction Direction { get; set; }
        public int LevelActivatedIndex { get; set; }
        public int BeginBarIndex { get; set; }
        public int StopLoss { get; set; }
        public double ProfitTarget { get; set; }

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
