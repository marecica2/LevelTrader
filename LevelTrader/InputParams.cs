namespace cAlgo
{
    public enum StrategyType
    {
        ID = 0,
        SWING,
        INVEST
    }

    public class InputParams
    {
        public int TimeZoneOffset { get; set; }
        public string LevelFilePath { get; set; }
        public string LevelFileName { get; set; }
        public int DailyReloadHour { get; set; }
        public int DailyReloadMinute { get; set; }

        public string Instrument { get; set; }
        public double LastPrice { get; set; }

        public double PositionSize { get; set; }
        public int StopLoss { get; set; }
        public double RiskRewardRatio { get; set; }

        public StrategyType Strategy { get; set; }
        public int LevelActivate { get; set; }
        public int LevelDeactivate { get; set; }

        public string BackTestPath { get; set; }
    }
}
